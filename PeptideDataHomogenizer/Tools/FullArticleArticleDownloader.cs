using Entities;
using Entities.RegexData;
using Microsoft.AspNetCore.Mvc;
using PeptideDataHomogenizer.Data;
using PeptideDataHomogenizer.Data.State;
using PeptideDataHomogenizer.Services;
using PeptideDataHomogenizer.Tools.HtmlTools;
using PeptideDataHomogenizer.Tools.NotCurrentlyInUse;
using PeptideDataHomogenizer.Tools.PubMedSearch;
using PeptideDataHomogenizer.Tools.RegexExtractors;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using static PeptideDataHomogenizer.Components.Pages.Home;

namespace PeptideDataHomogenizer.Tools
{
    public interface IFullArticleDownloader
    {
        Task<List<Article>?> GetArticlesFromPubMedQuerySequential(string query, int page, int pageSize,int projectId,AdditionalQueryParams additionalQueryParams);
    }

    public class FullArticleDownloader : IFullArticleDownloader
    {
        private readonly IPageFetcher _pageFetcher;
        private readonly IEUtilitiesService _eUtilitiesService;
        private readonly PythonRegexProteinDataExtractor _pythonRegexProteinDataExtractor;
        private readonly DatabaseDataHandler _databaseDataHandler;
        private readonly IPubMedAPIConsumer _pubMedAPIConsumer;
        private readonly PDBRecordsExtractor _pdbRecordsExtractor;
        private readonly PDBContextDataExtractor _pdbContextDataExtractor;

        private readonly ArticleService  _articleService;
        private readonly ArticleContentService _articleContentService;
        private readonly ArticleModerationService _articleModerationService;
        private readonly ProteinDataService _proteinDataService;
        private readonly ArticlePerProjectService _articlePerProjectService;
        private readonly ProteinDataPerProjectService _proteinDataPerProjectService;
        private readonly JournalsService _journalsService;


        public FullArticleDownloader([FromServices] IPageFetcher pageFetcher, [FromServices] IEUtilitiesService eUtilitiesService, [FromServices] PythonRegexProteinDataExtractor pythonRegexProteinDataExtractor, [FromServices] DatabaseDataHandler databaseDataHandler,[FromServices] IPubMedAPIConsumer pubMedAPIConsumer, [FromServices] PDBRecordsExtractor pDBRecordsExtractor,[FromServices] PDBContextDataExtractor pdbContextDataExtractor, [FromServices] ArticleService articleService, [FromServices] ArticleContentService articleContentService,
            [FromServices] ArticleModerationService articleModerationService,
            [FromServices] ProteinDataService proteinDataService,
            [FromServices]ArticlePerProjectService articlePerProjectService,
            [FromServices]ProteinDataPerProjectService proteinDataPerProjectService,
            [FromServices] JournalsService journalsService)
        {
            _pageFetcher = pageFetcher;
            _eUtilitiesService = eUtilitiesService;
            _pythonRegexProteinDataExtractor = pythonRegexProteinDataExtractor;
            _databaseDataHandler = databaseDataHandler;
            _pubMedAPIConsumer = pubMedAPIConsumer;
            _pdbRecordsExtractor = pDBRecordsExtractor;
            _pdbContextDataExtractor = pdbContextDataExtractor;
            _articleService = articleService ?? throw new ArgumentNullException(nameof(articleService));
            _articleContentService = articleContentService ?? throw new ArgumentNullException(nameof(articleContentService));
            _articleModerationService = articleModerationService ?? throw new ArgumentNullException(nameof(articleModerationService));
            _proteinDataService = proteinDataService ?? throw new ArgumentNullException(nameof(proteinDataService));
            _articlePerProjectService = articlePerProjectService ?? throw new ArgumentNullException(nameof(articlePerProjectService));
            _proteinDataPerProjectService = proteinDataPerProjectService ?? throw new ArgumentNullException(nameof(proteinDataPerProjectService));
            _journalsService = journalsService ?? throw new ArgumentNullException(nameof(journalsService));
        }

        public async Task<List<Article>?> GetArticlesFromPubMedQuerySequential(string query, int page, int pageSize,int projectId,AdditionalQueryParams additionalQueryParams)
        {
            Console.WriteLine($"[START] GetArticlesFromPubMedQuerySequential: query='{query}', page={page}, pageSize={pageSize}");

            // Step 1: Fetch article IDs from PubMed
            var (articlesFromPubMed, newArticles) = await FetchAndProcessPubMedArticles(query, page, pageSize,projectId,additionalQueryParams);

            // Step 2: Process full text for all articles
            var combinedArticles = await ProcessFullTextArticles(articlesFromPubMed, newArticles,projectId);

            // Step 3: Enrich articles with protein data
            await EnrichArticlesWithProteinData(combinedArticles,projectId);

            _pageFetcher.Dispose();
            Console.WriteLine("[END] GetArticlesFromPubMedQuerySequential finished");

            return combinedArticles;
        }

        private async Task<(List<Article> existingArticles, List<Article> newArticles)> FetchAndProcessPubMedArticles(string query, int page, int pageSize,int projectId,AdditionalQueryParams additionalQueryParams)
        {
            // Get article IDs from PubMed API
            var searchResults = await _pubMedAPIConsumer.SearchArticlesAsync(
                db: "pubmed",
                query: query,
                page: page,
                pageSize: pageSize,
                additionalQueryParams: additionalQueryParams,
                apiKey: "39a091d19a4e6a1223fd588526d497418f08"
            ).ConfigureAwait(false);
            Console.WriteLine($"[INFO] PubMedAPIConsumer.SearchArticlesAsync returned {searchResults.ArticlesPubMedIds.Count} IDs");

            // Check database for existing articles
            var existingArticles = await _articleService.GetArticlesByPubMedIdsAsync(searchResults.ArticlesPubMedIds).ConfigureAwait(false);
            Console.WriteLine($"[INFO] Retrieved {existingArticles.Count} articles from database by PubMed IDs");

            // Filter out IDs already in database
            var newArticleIds = searchResults.ArticlesPubMedIds
                .Where(id => !existingArticles.Any(a => a.PubMedId == id))
                .ToList();
            Console.WriteLine($"[INFO] {newArticleIds.Count} article IDs not found in database");

            // Fetch details for new articles from PubMed
            var newArticleDetails = await _pubMedAPIConsumer.GetArticlesFromPubMedApi(newArticleIds).ConfigureAwait(false);
            Console.WriteLine($"[INFO] Retrieved {newArticleDetails.Count} articles from PubMed API (without full text)");

            var discreditedJournals = await _journalsService.GetDiscreditedJournalsAsync(projectId).ConfigureAwait(false);
            //print all discredited journals
            Console.WriteLine($"[INFO] Found {discreditedJournals.Count} discredited journals for project {projectId}");
            foreach (var journal in discreditedJournals)
            {
                Console.WriteLine($"[INFO] Discredited Journal: {journal.Title}");
            }
            
            // discredit articles from discredited journals
            foreach (var article in newArticleDetails)
            {
                if (discreditedJournals.Any(journal => article.Journal.Title.Contains(journal.Title,StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($"[INFO] Discrediting article {article.DOI} from discredited journal {article.Journal.Title}");
                    await _articleModerationService.DiscreditArticleInProjectAsync(projectId, article.DOI, "Discredited Journal");
                }
            }

            foreach(var article in existingArticles)
            {
                if (discreditedJournals.Any(journal => article.Journal.Contains(journal.Title,StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($"[INFO] Discrediting existing article {article.Doi} from discredited journal {article.Journal}");
                    await _articleModerationService.DiscreditArticleInProjectAsync(projectId, article.Doi, "Discredited Journal");
                }
            }

            // Convert to Article objects and add to database
            var newArticles = await ConvertAndStoreNewArticles(newArticleDetails);

            return (existingArticles, newArticles);
        }

        private async Task<List<Article>> ConvertAndStoreNewArticles(List<ArticleDetail> articleDetails)
        {
            //if article doi is empty, skip it
            articleDetails = articleDetails
                .Where(ad => !string.IsNullOrWhiteSpace(ad.DOI))
                .ToList();
            var newArticles = articleDetails.Select(detail => new Article
            {
                Abstract = detail.Abstract,
                Title = detail.Title,
                Authors = detail.AuthorsToString(),
                Journal = detail.Journal.Title,
                PubMedId = detail.PMID,
                Doi = detail.DOI,
                PublicationDate = detail.PubDate.Value
            }).ToList();

            Console.WriteLine($"[INFO] Converted {newArticles.Count} ArticleDetail objects to Article objects");


            await _articleService.AddArticlesWithoutChapters(newArticles);
            Console.WriteLine($"[INFO] Added {newArticles.Count} articles to database (without chapters)");
            
            return newArticles;
        }

        private async Task<List<Article>> ProcessFullTextArticles(List<Article> existingArticles, List<Article> newArticles, int projectId)
        {
            Console.WriteLine($"[DEBUG] existingArticles.Count before FilterListOfArticlesByNotApprovedAndNotDiscreditedAsync: {existingArticles.Count}");
            var existingArticleNeedingTexDois = await _articlePerProjectService.FilterListOfArticlesByNotApprovedAndNotDiscreditedAsync(projectId, existingArticles.Select(a => a.Doi).ToList());
            Console.WriteLine($"[DEBUG] existingArticleNeedingTexDois.Count after FilterListOfArticlesByNotApprovedAndNotDiscreditedAsync: {existingArticleNeedingTexDois.Count}");
            // Filter existing articles that need full text, only take those doi is in existingArticleNeedingTextDois
            var existingArticlesNeedingText = existingArticles
                .Where(a => existingArticleNeedingTexDois.Contains(a.Doi) && (a.Chapters == null || a.Chapters.Count == 0))
                .ToList();
            Console.WriteLine($"[INFO] {existingArticlesNeedingText.Count} articles in DB need full text (not completed/discredited)");

            // Create tasks for fetching full text for new articles
            var newArticleTasks = newArticles.Select(a => FetchFullTextForArticle(a, projectId)).ToList();

            // Create tasks for existing articles that need full text
            var existingArticleTasks = existingArticlesNeedingText
                .Where(a => a.Chapters == null || a.Chapters.Count == 0)
                .Select(a => FetchFullTextForArticle(a, projectId))
                .ToList();

            var allTasks = newArticleTasks.Concat(existingArticleTasks).ToList();
            Console.WriteLine($"[INFO] Awaiting {allTasks.Count} full text fetch tasks...");

            await Task.WhenAll(allTasks);
            Console.WriteLine($"[INFO] All full text fetch tasks completed");

            // Process and store all new chapters
            var newChapters = allTasks
                .Select(t => t.Result.Chapters)
                .Where(c => c != null && c.Count > 0)
                .SelectMany(c => c)
                .ToList();


            Console.WriteLine($"[INFO] {newChapters.Count} new chapters extracted from fetched articles");

            await _articleContentService.AddChaptersAsync(newChapters);
            Console.WriteLine($"[INFO] Added {newChapters.Count} chapters to database");

            var newTables = allTasks
                .Select(t => t.Result.Tables)
                .Where(t => t != null && t.Count > 0)
                .SelectMany(t => t)
                .ToList();
            newTables.ForEach(nt => nt.Id = 0); // Reset IDs to ensure new entries
            Console.WriteLine($"[INFO] {newTables.Count} new tables extracted from fetched articles");
            await _databaseDataHandler.AddRangeAsync(newTables);
            Console.WriteLine($"[INFO] Added {newTables.Count} tables to database");

            var newImages = allTasks
                .Select(t => t.Result.Images)
                .Where(i => i != null && i.Count > 0)
                .SelectMany(i => i)
                .ToList();
            newImages.ForEach(ni => ni.Id = 0);
            Console.WriteLine($"[INFO] {newImages.Count} new images extracted from fetched articles");
            await _databaseDataHandler.AddRangeAsync(newImages);
            Console.WriteLine($"[INFO] Added {newImages.Count} images to database");

            return existingArticles.Concat(newArticles).ToList();
        }

        private async Task<Article> FetchFullTextForArticle(Article article,int projectId)
        {
            try
            {
                Console.WriteLine($"[TASK] Fetching full text for DOI {article.Doi} (Title: {article.Title})");

                var extractedData = await _pageFetcher.GetFullPageContentAsync(
                    "https://doi.org/" + article.Doi,
                    projectId,
                    new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token,
                    article.Title
                    
                );

                var chapters = extractedData.Item1;

                Console.WriteLine($"[TASK] Got {chapters.Count} chapters for DOI {article.Doi}");

                if (IsDiscreditedPublisher(chapters))
                {
                    Console.WriteLine($"[TASK] Discredited publisher detected for DOI {article.Doi}");
                    await _articleModerationService.DiscreditArticleInProjectAsync(projectId,article.Doi, "Discredited Publisher");
                }

                chapters.ForEach(c => c.ArticleDoi = article.Doi);
                article.Chapters = chapters;
                article.Tables = extractedData.Item2;
                article.Tables.ForEach(t => t.ArticleDoi = article.Doi);
                article.Images = extractedData.Item3;
                article.Images.ForEach(i => i.ArticleDoi = article.Doi);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to fetch full text for DOI {article.Doi}: {ex.Message}");
            }

            return article;
        }

        private bool IsDiscreditedPublisher(List<Chapter> chapters)
        {
            return chapters.Count == 1 &&
                   chapters[0].Title == "Discredited Publisher" &&
                   chapters[0].Content == "This publisher is discredited and not supported.";
        }

        private async Task EnrichArticlesWithProteinData(List<Article> articles,int projectId)
        {
            var knownData = await GetKnownDatabaseData();

            foreach (var article in articles)
            {
                await ProcessArticleProteinData(article, knownData,projectId);
            }
        }

        private async Task<KnowledgeBase> GetKnownDatabaseData()
        {
            var simulationSoftwares = await _databaseDataHandler.GetAllAsync<SimulationSoftware>();
            var waterModels = await _databaseDataHandler.GetAllAsync<WaterModel>().ConfigureAwait(false);
            var forceFields = await _databaseDataHandler.GetAllAsync<ForceFieldSoftware>().ConfigureAwait(false);
            var methods = await _databaseDataHandler.GetAllAsync<SimulationMethod>().ConfigureAwait(false);
            var ions = await _databaseDataHandler.GetAllAsync<Ion>().ConfigureAwait(false);

            var simulationSoftwareFromSavedData = await _proteinDataService.GetAllDistinctSoftwareNamesAsync().ConfigureAwait(false);
            var waterModelsFromSavedData = await _proteinDataService.GetAllDistinctWaterModelsAsync().ConfigureAwait(false);
            var forceFieldsFromSavedData = await _proteinDataService.GetAllDistinctForceFieldsAsync().ConfigureAwait(false);
            var methodsFromSavedData = await _proteinDataService.GetAllDistinctSimulationMethodsAsync().ConfigureAwait(false);
            var ionsFromSavedData = await _proteinDataService.GetAllDistinctIonsAsync().ConfigureAwait(false);

            //merge all lists removing duplicates
            simulationSoftwareFromSavedData.AddRange(simulationSoftwares.Select(x => x.SoftwareName));
            waterModelsFromSavedData.AddRange(waterModels.Select(x => x.WaterModelName));
            forceFieldsFromSavedData.AddRange(forceFields.Select(x => x.SoftwareName));
            methodsFromSavedData.AddRange(methods.Select(x => x.MethodName));
            ionsFromSavedData.AddRange(ions.Select(x => x.IonName));

            //remove duplicates
            simulationSoftwareFromSavedData = simulationSoftwareFromSavedData.Distinct().ToList();
            waterModelsFromSavedData = waterModelsFromSavedData.Distinct().ToList();
            forceFieldsFromSavedData = forceFieldsFromSavedData.Distinct().ToList();
            methodsFromSavedData = methodsFromSavedData.Distinct().ToList();
            ionsFromSavedData = ionsFromSavedData.Distinct().ToList();
            //create knowledge base




            return new KnowledgeBase
            {
                SoftwareNames = simulationSoftwares.Select(x => x.SoftwareName).ToList(),
                ImplicitWaterNames = waterModels.Where(wm => wm.WaterModelType == "implicit").Select(wm => wm.WaterModelName).ToList(),
                ExplicitWaterNames = waterModels.Where(wm => wm.WaterModelType == "explicit").Select(wm => wm.WaterModelName).ToList(),
                ForceFields = forceFields.Select(ff => ff.SoftwareName).ToList(),
                Methods = methods.Select(m => m.MethodName).ToList(),
                Ions = ions.Select(i => i.IonName).ToList()
            };
        }

        private async Task ProcessArticleProteinData(Article article, KnowledgeBase knownData,int projectId)
        {
            var existingProteinData = await _proteinDataPerProjectService.GetApprovedProteinDataByProjectIdAndArticleDoiAsync(projectId,article.Doi).ConfigureAwait(false);

            if (existingProteinData.Any())
            {
                article.ProteinData = existingProteinData.ToList();
                Console.WriteLine($"[LOOP] DATA ALREADY EXIST FOR {article.Doi} ({article.ProteinData.Count} protein records)");
                return;
            }

            Console.WriteLine($"[LOOP] Extracting protein data for DOI={article.Doi} from {article.Chapters.Count} chapters");

            var chapterContents = article.Chapters.Select(c => c.Content).ToList();
            article.ProteinData = await _pdbRecordsExtractor.ExtractMdData(
                string.Join(" ", chapterContents),
                knownData.SoftwareNames,
                knownData.ImplicitWaterNames,
                knownData.ExplicitWaterNames,
                knownData.ForceFields,
                knownData.Methods,
                knownData.Ions
            );

            Console.WriteLine($"[LOOP] Extracted {article.ProteinData.Count} protein data records for DOI={article.Doi}");

            await EnrichProteinDataWithPdbInfo(article);
            FilterEmptyProteinData(article);
        }

        private async Task EnrichProteinDataWithPdbInfo(Article article)
        {
            var pdbIds = article.ProteinData
                .Select(pd => pd.ProteinId)
                .Where(id => !string.IsNullOrEmpty(id) &&
                       !id.Contains("AlphaFold", StringComparison.InvariantCultureIgnoreCase) &&
                       !id.Contains("RosettaFold", StringComparison.InvariantCultureIgnoreCase))
                .Distinct()
                .ToList();

            Console.WriteLine($"[LOOP] Found {pdbIds.Count} unique PDB IDs for DOI={article.Doi}");

            var pdbInfoTasks = pdbIds.ToDictionary(
                id => id,
                id => _pageFetcher.ExtractClassificationOrganismAndMethodAsync(id)
            );

            Console.WriteLine($"[LOOP] Awaiting {pdbInfoTasks.Count} ExtractClassificationOrganismAndMethodAsync tasks for DOI={article.Doi}");
            await Task.WhenAll(pdbInfoTasks.Values).ConfigureAwait(false);

            foreach (var protein in article.ProteinData)
            {
                if (pdbInfoTasks.TryGetValue(protein.ProteinId, out var task))
                {
                    var results = await task;
                    var validResult = results.FirstOrDefault(r =>
                        !IsNotFound(r.Item1) ||
                        !IsNotFound(r.Item2) ||
                        !IsNotFound(r.Item3));

                    if (validResult != default)
                    {
                        if (!IsNotFound(validResult.Item1))
                            protein.Classification = validResult.Item1;
                        if (!IsNotFound(validResult.Item2))
                            protein.Organism = validResult.Item2;
                        if (!IsNotFound(validResult.Item3))
                            protein.Method = validResult.Item3;
                    }
                }
            }
        }

        private void FilterEmptyProteinData(Article article)
        {
            int beforeFilter = article.ProteinData.Count;
            article.ProteinData = article.ProteinData
                .Where(pd =>
                    !string.IsNullOrWhiteSpace(pd.Classification) ||
                    !string.IsNullOrWhiteSpace(pd.Method) ||
                    !string.IsNullOrWhiteSpace(pd.Organism))
                .ToList();
            Console.WriteLine($"[LOOP] Filtered protein data for DOI={article.Doi}: {beforeFilter} -> {article.ProteinData.Count} records");
        }

        private bool IsNotFound(string value) =>
            string.Equals(value, "Not found", StringComparison.OrdinalIgnoreCase);

        private class KnowledgeBase
        {
            public List<string> SoftwareNames { get; set; }
            public List<string> ImplicitWaterNames { get; set; }
            public List<string> ExplicitWaterNames { get; set; }
            public List<string> ForceFields { get; set; }
            public List<string> Methods { get; set; }
            public List<string> Ions { get; set; }
        }
    }
}
