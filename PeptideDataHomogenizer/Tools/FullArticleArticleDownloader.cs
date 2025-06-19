using Entities;
using Entities.RegexData;
using Microsoft.AspNetCore.Mvc;
using PeptideDataHomogenizer.Data;
using PeptideDataHomogenizer.Data.State;
using PeptideDataHomogenizer.Tools.HtmlTools;
using PeptideDataHomogenizer.Tools.NotCurrentlyInUse;
using PeptideDataHomogenizer.Tools.PubMedSearch;
using PeptideDataHomogenizer.Tools.RegexExtractors;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PeptideDataHomogenizer.Tools
{
    public interface IFullArticleDownloader
    {
        Task<List<Article>?> GetArticlesFromPubMedQuerySequential(string query, int page, int pageSize);
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

        public FullArticleDownloader([FromServices] IPageFetcher pageFetcher, [FromServices] IEUtilitiesService eUtilitiesService, [FromServices] PythonRegexProteinDataExtractor pythonRegexProteinDataExtractor, [FromServices] DatabaseDataHandler databaseDataHandler,[FromServices] IPubMedAPIConsumer pubMedAPIConsumer, [FromServices] PDBRecordsExtractor pDBRecordsExtractor,[FromServices] PDBContextDataExtractor pdbContextDataExtractor)
        {
            _pageFetcher = pageFetcher;
            _eUtilitiesService = eUtilitiesService;
            _pythonRegexProteinDataExtractor = pythonRegexProteinDataExtractor;
            _databaseDataHandler = databaseDataHandler;
            _pubMedAPIConsumer = pubMedAPIConsumer;
            _pdbRecordsExtractor = pDBRecordsExtractor;
            _pdbContextDataExtractor = pdbContextDataExtractor;
        }

        private void LogResults(ConcurrentBag<string> errors, int totalArticles)
        {
            if (errors.Any())
            {
                foreach (var error in errors)
                {
                    Console.WriteLine(error);
                }
            }
            else
            {
                Console.WriteLine($"Successfully processed {totalArticles} articles.");
            }
            ControlledTimer.PrintAllLogs();
        }
        public async Task<List<Article>?> GetArticlesFromPubMedQuerySequential(string query, int page, int pageSize)
        {
            Console.WriteLine($"[START] GetArticlesFromPubMedQuerySequential: query='{query}', page={page}, pageSize={pageSize}");

            // GET THE IDS OF THE ARTICLES RESULTING FROM THE QUERY
            var articles = await _pubMedAPIConsumer.SearchArticlesAsync(
                db: "pubmed",
                query: query,
                page: page,
                pageSize: pageSize,
                apiKey: "02d0770019f922e19b8e321275fa18b33908"
            ).ConfigureAwait(false);
            Console.WriteLine($"[INFO] PubMedAPIConsumer.SearchArticlesAsync returned {articles.ArticlesPubMedIds.Count} IDs");

            ControlledTimer.StartOperationTimer($"RETRIEVING FULL TEXT OF {pageSize} ARTICLES (SEQUENTIAL)");

            // Check and extract already downloaded articles.
            var articlesInDatabase = await _databaseDataHandler.GetArticlesByPubMedIdsAsync(articles.ArticlesPubMedIds).ConfigureAwait(false);
            Console.WriteLine($"[INFO] Retrieved {articlesInDatabase.Count} articles from database by PubMed IDs");

            // Filter out articles ids that are already in the database
            var idsNotInDatabase = articles.ArticlesPubMedIds
                .Where(id => !articlesInDatabase.Any(a => a.PubMedId == id))
                .ToList();
            Console.WriteLine($"[INFO] {idsNotInDatabase.Count} article IDs not found in database");

            // For new ids, get Articles Details (DOI and other info, but no full text)
            var articlesWithoutFullTextFromPubMed = await _pubMedAPIConsumer.GetArticlesFromPubMedApi(idsNotInDatabase).ConfigureAwait(false);
            Console.WriteLine($"[INFO] Retrieved {articlesWithoutFullTextFromPubMed.Count} articles from PubMed API (without full text)");

            // Convert ArticleDetail to Article
            var convertedArticlesWithoutFullTextFromPubMed = new List<Article>();
            foreach (var articleToBeConverted in articlesWithoutFullTextFromPubMed)
            {
                convertedArticlesWithoutFullTextFromPubMed.Add(new Article
                {
                    Abstract = articleToBeConverted.Abstract,
                    Title = articleToBeConverted.Title,
                    Authors = articleToBeConverted.AuthorsToString(),
                    Journal = articleToBeConverted.Journal.Title,
                    PubMedId = articleToBeConverted.PMID,
                    Doi = articleToBeConverted.DOI,
                    PublicationDate = articleToBeConverted.PubDate.Value
                });
            }
            Console.WriteLine($"[INFO] Converted {convertedArticlesWithoutFullTextFromPubMed.Count} ArticleDetail objects to Article objects");

            // Add converted articles to database
            await _databaseDataHandler.AddArticlesWithoutChapters(convertedArticlesWithoutFullTextFromPubMed);
            Console.WriteLine($"[INFO] Added {convertedArticlesWithoutFullTextFromPubMed.Count} articles to database (without chapters)");


            // Get full text for articles from PubMed
            var fullTextTasks = convertedArticlesWithoutFullTextFromPubMed
                .Select(async m =>
                {
                    try
                    {
                        Console.WriteLine($"[TASK] Fetching full text for DOI {m.Doi} (Title: {m.Title})");
                        var chapters = await _pageFetcher.GetFullPageContentAsync(
                            "https://doi.org/" + m.Doi,
                            new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token, // Timeout after 2 mins
                            m.Title
                        );
                        Console.WriteLine($"[TASK] Got {chapters.Count} chapters for DOI {m.Doi}");

                        // Check for discredited publisher marker
                        if (chapters.Count == 1 &&
                            chapters[0].Title == "Discredited Publisher" &&
                            chapters[0].Content == "This publisher is discredited and not supported.")
                        {
                            Console.WriteLine($"[TASK] Discredited publisher detected for DOI {m.Doi}");
                            await _databaseDataHandler.DiscreditArticle(m.Doi, "Discredited Publisher");
                        }

                        chapters.ForEach(c => c.ArticleDoi = m.Doi);
                        m.Chapters = chapters;
                        return m;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to fetch full text for DOI {m.Doi}: {ex.Message}");
                        return m; // Return the original article even if fetching fails
                    }
                })
                .ToList();

            articlesInDatabase = articlesInDatabase.Where(a => !a.Completed && !a.Discredited).ToList(); // Filter out articles that are completed or discredited
            Console.WriteLine($"[INFO] {articlesInDatabase.Count} articles in DB need full text (not completed/discredited)");

            foreach (var arti in articlesInDatabase)
            {
                if (arti.Chapters == null || arti.Chapters.Count() == 0)
                {
                    Console.WriteLine($"[DEBUG] Article {arti.PubMedId} ({arti.Doi}) has no chapters, will fetch full text.");
                }
            }
            // Add tasks for articles in DB with no chapters
            fullTextTasks.AddRange(
                articlesInDatabase
                    .Where(a => a.Chapters == null || a.Chapters.Count == 0)
                    .Select(async m =>
                    {
                        try
                        {
                            Console.WriteLine($"[TASK] Fetching full text for DB DOI {m.Doi} (Title: {m.Title})");
                            var chapters = await _pageFetcher.GetFullPageContentAsync(
                                "https://doi.org/" + m.Doi,
                                new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token,
                                m.Title
                            );

                            // Check for discredited publisher marker
                            if (chapters.Count == 1 &&
                                chapters[0].Title == "Discredited Publisher" &&
                                chapters[0].Content == "This publisher is discredited and not supported.")
                            {
                                Console.WriteLine($"[TASK] Discredited publisher detected for DB DOI {m.Doi}");
                                await _databaseDataHandler.DiscreditArticle(m.Doi, "Discredited Publisher");
                            }

                            chapters.ForEach(c => c.ArticleDoi = m.Doi);
                            m.Chapters = chapters;
                            return m;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Failed to fetch full text for DB DOI {m.Doi}: {ex}");
                            return m;
                        }
                    })
            );

            Console.WriteLine($"[INFO] Awaiting {fullTextTasks.Count} full text fetch tasks...");
            await Task.WhenAll(fullTextTasks); // Now safe (concurrency controlled by PageFetcher's _pageSemaphore)
            Console.WriteLine($"[INFO] All full text fetch tasks completed");

            // Extract newly extracted chapters
            var newChapters = fullTextTasks
                .Select(t => t.Result.Chapters)
                .Where(c => c != null && c.Count > 0)
                .SelectMany(c => c)
                .ToList();
            Console.WriteLine($"[INFO] {newChapters.Count} new chapters extracted from fetched articles");

            // Add all chapters to the database
            await _databaseDataHandler.AddChaptersAsync(newChapters);
            Console.WriteLine($"[INFO] Added {newChapters.Count} chapters to database");

            ControlledTimer.StopOperationTimer($"RETRIEVING FULL TEXT OF {pageSize} ARTICLES (SEQUENTIAL)");

            LogResults(new ConcurrentBag<string>(), articlesWithoutFullTextFromPubMed.Count);

            // Combine the two lists into one
            var combinedArticles = articlesInDatabase.Concat(convertedArticlesWithoutFullTextFromPubMed).ToList();

            List<string> KnownSoftwareNames = new List<string>();
            List<string> ImplicitWaterNames = new List<string>();
            List<string> ExplicitWaterNames = new List<string>();
            List<string> KnownForceFields = new List<string>();
            List<string> KnownMethods = new List<string>();
            List<string> KnownIons = new List<string>();

            var simulationSoftwares = await _databaseDataHandler.GetAllAsync<SimulationSoftware>();
            KnownSoftwareNames = simulationSoftwares.Select(x => x.SoftwareName).ToList();

            var waterModels = await _databaseDataHandler.GetAllAsync<WaterModel>().ConfigureAwait(false);
            ImplicitWaterNames = waterModels.Where(wm => wm.WaterModelType == "implicit").Select(wm => wm.WaterModelName).ToList();
            ExplicitWaterNames = waterModels.Where(wm => wm.WaterModelType == "explicit").Select(wm => wm.WaterModelName).ToList();

            var forceFields = await _databaseDataHandler.GetAllAsync<ForceFieldSoftware>().ConfigureAwait(false);
            KnownForceFields = forceFields.Select(ff => ff.SoftwareName).ToList();

            var methods = await _databaseDataHandler.GetAllAsync<SimulationMethod>().ConfigureAwait(false);
            KnownMethods = methods.Select(m => m.MethodName).ToList();

            var ions = await _databaseDataHandler.GetAllAsync<Ion>().ConfigureAwait(false);
            KnownIons = ions.Select(i => i.IonName).ToList();

            foreach (var a in combinedArticles)
            {
                var proteinData = await _databaseDataHandler.GetProteinDataByArticleAsync(a.Doi).ConfigureAwait(false);
                if (proteinData.Any())
                {
                    a.ProteinData = proteinData.ToList();
                    Console.WriteLine($"[LOOP] DATA ALREADY EXIST FOR {a.Doi} ({a.ProteinData.Count} protein records)");
                }
                else
                {
                    var chapterContents = a.Chapters.Select(c => c.Content).ToList();
                    Console.WriteLine($"[LOOP] Extracting protein data for DOI={a.Doi} from {chapterContents.Count} chapters");
                    a.ProteinData = await _pdbRecordsExtractor.ExtractMdData(
                        string.Join(" ", chapterContents),
                        KnownSoftwareNames, ImplicitWaterNames, ExplicitWaterNames, KnownForceFields, KnownMethods, KnownIons
                    );
                    Console.WriteLine($"[LOOP] Extracted {a.ProteinData.Count} protein data records for DOI={a.Doi}");

                    var pdbIds = a.ProteinData.Select(pd => pd.ProteinId)
                        .Where(id => !string.IsNullOrEmpty(id) && !id.Contains("AlphaFold", StringComparison.InvariantCultureIgnoreCase) && !id.Contains("RosettaFold", StringComparison.InvariantCultureIgnoreCase))
                        .Distinct()
                        .ToList();
                    Console.WriteLine($"[LOOP] Found {pdbIds.Count} unique PDB IDs for DOI={a.Doi}");

                    var extractTasks = pdbIds
                        .ToDictionary(
                            pdbId => pdbId,
                            pdbId => _pageFetcher.ExtractClassificationOrganismAndMethodAsync(pdbId)
                        );

                    Console.WriteLine($"[LOOP] Awaiting {extractTasks.Count} ExtractClassificationOrganismAndMethodAsync tasks for DOI={a.Doi}");
                    await Task.WhenAll(extractTasks.Values).ConfigureAwait(false);

                    var pdbIdToInfo = new Dictionary<string, (string classification, string organism, string method)>();

                    foreach (var kvp in extractTasks)
                    {
                        var pdbId = kvp.Key;
                        var results = await kvp.Value;
                        var valid = results.FirstOrDefault(r =>
                            !string.Equals(r.Item1, "Not found", StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(r.Item2, "Not found", StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(r.Item3, "Not found", StringComparison.OrdinalIgnoreCase)
                        );
                        if (
                            string.Equals(valid.Item1, "Not found", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(valid.Item2, "Not found", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(valid.Item3, "Not found", StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            pdbIdToInfo[pdbId] = ("Not found", "Not found", "Not found");
                        }
                        else
                        {
                            pdbIdToInfo[pdbId] = valid;
                        }
                    }
                    Console.WriteLine($"[LOOP] Classification/organism/method info extracted for {pdbIdToInfo.Count} PDB IDs for DOI={a.Doi}");

                    // Set classification, organism, method if at least one is valid
                    foreach (var pd in a.ProteinData)
                    {
                        if (pdbIdToInfo.TryGetValue(pd.ProteinId, out var info))
                        {
                            if (!string.Equals(info.classification, "Not found", StringComparison.OrdinalIgnoreCase))
                                pd.Classification = info.classification;
                            if (!string.Equals(info.organism, "Not found", StringComparison.OrdinalIgnoreCase))
                                pd.Organism = info.organism;
                            if (!string.Equals(info.method, "Not found", StringComparison.OrdinalIgnoreCase))
                                pd.Method = info.method;
                        }
                    }

                    // Remove ProteinData with all empty or whitespace for classification, method, and organism
                    int beforeFilter = a.ProteinData.Count;
                    a.ProteinData = a.ProteinData
                        .Where(pd =>
                            !string.IsNullOrWhiteSpace(pd.Classification) ||
                            !string.IsNullOrWhiteSpace(pd.Method) ||
                            !string.IsNullOrWhiteSpace(pd.Organism)
                        )
                        .ToList();
                    int afterFilter = a.ProteinData.Count;
                    Console.WriteLine($"[LOOP] Filtered protein data for DOI={a.Doi}: {beforeFilter} -> {afterFilter} records");
                }
            }

            _pageFetcher.Dispose();
            Console.WriteLine("[END] GetArticlesFromPubMedQuerySequential finished");

            // Return articles with full text
            return combinedArticles;
        }
    }
}
