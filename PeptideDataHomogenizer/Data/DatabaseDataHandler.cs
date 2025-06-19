using Entities;
using Microsoft.EntityFrameworkCore;

namespace PeptideDataHomogenizer.Data
{
    public class DatabaseDataHandler
    {
        private readonly DbContext _context;

        public DatabaseDataHandler(DbContext context)
        {
            _context = context;
            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        // Basic CRUD operations
        public async Task AddAsync<T>(T entity) where T : class
        {

            _context.ChangeTracker.Clear();
            await _context.Set<T>().AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync<T>(T entity) where T : class
        {

            _context.ChangeTracker.Clear();
            _context.Set<T>().Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync<T>(T entity) where T : class
        {
            _context.ChangeTracker.Clear();
            _context.Set<T>().Remove(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<T> GetByIdAsync<T>(params object[] keyValues) where T : class
        {
            return await _context.Set<T>().FindAsync(keyValues);
        }

        public async Task<List<T>> GetAllAsync<T>() where T : class
        {
            _context.ChangeTracker.Clear();
            return await _context.Set<T>().AsNoTracking().ToListAsync();
        }

        // Specialized operations
        public async Task<IEnumerable<ProteinData>> GetProteinDataByArticleAsync(string articleDoi)
        {
            return await _context.Set<ProteinData>()
                .Where(p => p.ArticleDoi == articleDoi)
                .ToListAsync();
        }

        public async Task<List<ProteinData>> GetApprovedProteinDataAsync(bool approved = true)
        {
            return await _context.Set<ProteinData>()
                .Include(m=>m.Article)
                .Where(p => p.Approved == approved && p.Article.Completed)
                .ToListAsync();
        }

        public async Task<List<Article>> GetArticlesByPubMedIdsAsync(List<string> pubmedIds)
        {
            return await _context.Set<Article>()
                .Include(a => a.Chapters)
                .Include(a => a.ProteinData)
                .AsSingleQuery()
                .Where(a => pubmedIds.Contains(a.PubMedId))
                .ToListAsync();
        }

        public async Task DiscreditArticle(string doi, string discreditedReason)
        {
            var article = await _context.Set<Article>().FirstOrDefaultAsync(a => a.Doi == doi);
            if (article != null)
            {
                article.Discredited = true;
                article.DiscreditedReason = discreditedReason;
                _context.Set<Article>().Update(article);

                var chapters = await _context.Set<Chapter>().Where(c => c.ArticleDoi == doi).ToListAsync();
                _context.Set<Chapter>().RemoveRange(chapters);

                var proteinData = await _context.Set<ProteinData>().Where(p => p.ArticleDoi == doi).ToListAsync();
                _context.Set<ProteinData>().RemoveRange(proteinData);

                await _context.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"Article with DOI {doi} not found.");
            }
        }


        //addarticleswithoutchapters
        public async Task AddArticlesWithoutChapters(List<Article> articles)
        {
            foreach (var article in articles)
            {
                _context.Set<Article>().Add(article);
            }
            await _context.SaveChangesAsync();
        }

        //addlistofchaptersoptimized
        public async Task AddChaptersAsync(List<Chapter> chapters)
        {
            foreach(var chapter in chapters)
            {
                _context.Set<Chapter>().Add(chapter);
            }

            await _context.SaveChangesAsync();
        }

        //completearticleasync (set completed to true)
        public async Task CompleteArticleAsync(string articleDoi)
        {
            var article = await _context.Set<Article>().FirstOrDefaultAsync(a => a.Doi == articleDoi);
            if (article != null)
            {
                article.Completed = true;
                _context.Set<Article>().Update(article);
                await _context.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"Article with DOI {articleDoi} not found.");
            }
        }

        //delete chapter by title and article DOI
        public async Task DeleteChapterAsync(string title, string articleDoi)
        {
            var chapter = await _context.Set<Chapter>()
                .FirstOrDefaultAsync(c => c.Title == title && c.ArticleDoi == articleDoi);
            if (chapter != null)
            {
                _context.Set<Chapter>().Remove(chapter);
                await _context.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"Chapter with title '{title}' and Article DOI '{articleDoi}' not found.");
            }
        }

        //REPLACEchaptersasync (delete all existing chapters and add new ones)
        public async Task ReplaceChaptersAsync(List<Chapter> chapters,string doi)
        {
            _context.ChangeTracker.Clear();
            // Delete all existing chapters
            var existingChapters = await _context.Set<Chapter>().Where(c=>c.ArticleDoi == doi).ToListAsync();
            _context.Set<Chapter>().RemoveRange(existingChapters);
            // Add new chapters
            foreach (var chapter in chapters)
            {
                _context.Set<Chapter>().Add(chapter);
            }
            await _context.SaveChangesAsync();
        }

        // saveorupdateproteindatalistasync
        public async Task SaveOrUpdateProteinDataListAsync(List<ProteinData> proteinDataList)
        {
            _context.ChangeTracker.Clear();

            // Replace every null value with default value
            proteinDataList.ForEach(p =>
            {
                p.Classification ??= string.Empty;
                p.SoftwareName ??= string.Empty;
                p.SoftwareVersion ??= string.Empty;
                p.WaterModel ??= string.Empty;
                p.ForceField ??= string.Empty;
                p.SimulationMethod ??= string.Empty;
                p.Ions ??= string.Empty;
            });

            // If approved is true and dateapproval is null, set dateapproved to current date
            proteinDataList.ForEach(p =>
            {
                if (p.Approved && p.DatetimeApproval == null)
                {
                    p.DatetimeApproval = DateTime.UtcNow;
                }
            });

            // Ensure all referenced Article DOIs exist before inserting ProteinData
            var articleDois = proteinDataList.Select(p => p.ArticleDoi).Distinct().ToList();
            var existingDois = await _context.Set<Article>()
                .Where(a => articleDois.Contains(a.Doi))
                .Select(a => a.Doi)
                .ToListAsync();

            var missingDois = articleDois.Except(existingDois).ToList();
            if (missingDois.Any())
            {
                throw new InvalidOperationException(
                    $"Cannot add ProteinData: the following Article DOIs do not exist: {string.Join(", ", missingDois)}");
            }

            // Remove all existing ProteinData for each ArticleDoi
            foreach (var doi in articleDois)
            {
                var existingProteinData = await _context.Set<ProteinData>()
                    .Where(p => p.ArticleDoi == doi)
                    .ToListAsync();
                if (existingProteinData.Any())
                {
                    _context.Set<ProteinData>().RemoveRange(existingProteinData);
                }
            }

            // Add all new ProteinData
            await _context.Set<ProteinData>().AddRangeAsync(proteinDataList);
            await _context.SaveChangesAsync();
        }

        //GetApprovedProteinsCountAsync
        public async Task<int> GetApprovedProteinsCountAsync()
        {
            return await _context.Set<ProteinData>()
                .CountAsync(p => p.Approved && p.Article.Completed);
        }

        //GetCountSumOfDiscreditedArticlesAndDiscreditedJournalsAndDiscreditedPublishersAsync
        public async Task<int> GetDiscreditedCountsAsync()
        {
            var discreditedArticles = await _context.Set<Article>()
                .Where(a => a.Discredited)
                .CountAsync();
            var discreditedJournals = await _context.Set<DiscreditedJournal>().CountAsync();
            var discreditedPublishers = await _context.Set<DiscreditedPublisher>().CountAsync();
            return discreditedArticles + discreditedJournals + discreditedPublishers;
        }

        public async Task<List<Article>> GetDiscreditedArticlesAsync(bool discredited = true)
        {
            return await _context.Set<Article>()
                .Where(a => a.Discredited == discredited)
                .ToListAsync();
        }

        //ricredit article by doi
        public async Task RecreditArticleAsync(string doi)
        {
            // Detach all tracked entities to avoid tracking conflicts
            _context.ChangeTracker.Clear();

            var article = await _context.Set<Article>().FirstOrDefaultAsync(a => a.Doi == doi);
            _context.ChangeTracker.Clear();
            if (article != null)
            {
                article.Discredited = false;
                article.DiscreditedReason = string.Empty;
                _context.Set<Article>().Update(article);
                await _context.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"Article with DOI {doi} not found.");
            }
        }

        // Utility methods
        public async Task<bool> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<int> ExecuteSqlCommandAsync(string sql, params object[] parameters)
        {
            return await _context.Database.ExecuteSqlRawAsync(sql, parameters);
        }
    }
}
