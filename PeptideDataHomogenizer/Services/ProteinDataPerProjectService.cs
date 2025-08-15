using Entities;
using Microsoft.EntityFrameworkCore;
using PeptideDataHomogenizer.Data;

namespace PeptideDataHomogenizer.Services
{
    public class ProteinDataPerProjectService
    {
        private readonly ApplicationDbContext _context;

        public ProteinDataPerProjectService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<List<ProteinData>> GetProteinDataAsync(string doi, int projectId)
        {
            var articlePerProject = await _context.ArticlePerProjects
                .Where(ap => ap.IsApproved && ap.ArticleId == doi && ap.ProjectId == projectId)
                .Include(ap => ap.Article)
                .ThenInclude(a => a.ProteinData)
                .FirstOrDefaultAsync();
            if (articlePerProject == null || articlePerProject.Article == null)
            {
                return new List<ProteinData>();
            }
            return articlePerProject.Article.ProteinData
                .Select(pd => new ProteinData(pd))
                .ToList();
        }

        public async Task<List<ProteinData>> GetApprovedProteinDataByProjectIdAndArticleDoiAsync(int projectId, string articleDoi)
        {
            return await _context.Set<ProteinData>()
                .Where(pd => pd.ArticleDoi == articleDoi && projectId == pd.ProjectId)
                .ToListAsync();
        }

        public async Task DeleteProteinDataByProjectIdAndArticleDoiAsync(int projectId, string articleDoi)
        {
            var proteinDataList = await _context.Set<ProteinData>()
                .Where(pd => pd.ArticleDoi == articleDoi && pd.ProjectId == projectId)
                .ToListAsync();
            if (proteinDataList.Any())
            {
                proteinDataList.ForEach(pdb => pdb.Article = null);
                _context.Set<ProteinData>().RemoveRange(proteinDataList);
                await _context.SaveChangesAsync();
            }
            else
            {
                Console.WriteLine("No ProteinData records found to delete.");
            }
        }

        public async Task<List<ProteinData>> GetApprovedProteinDataByProjectIdAsync(int projectId)
        {

            var articles = await _context.ArticlePerProjects
                .Where(ap => ap.IsApproved && ap.ProjectId == projectId)
                .Include(ap => ap.Article)
                .ToListAsync();


            foreach(var articlePerProject in articles)
            {
                articlePerProject.Article.ProteinData = await _context.Set<ProteinData>()
                    .Where(pd => pd.ArticleDoi == articlePerProject.ArticleId && pd.ProjectId == projectId)
                    .ToListAsync();
            }

            var proteinDataList = new List<ProteinData>();
            int articleIndex = 0;
            foreach (var articlePerProject in articles)
            {
                articleIndex++;
                if (articlePerProject.Article != null)
                {
                    int proteinIndex = 0;
                    foreach (var pd in articlePerProject.Article.ProteinData)
                    {
                        proteinIndex++;
                        pd.Article = articlePerProject.Article;
                        proteinDataList.Add(pd);
                    }
                }
                else
                {
                    Console.WriteLine($"ArticlePerProject[{articleIndex}] Article is null");
                }
            }

            Console.WriteLine($"Total ProteinData records before deduplication: {proteinDataList.Count}");

            // Remove duplicates by Id
            var dedupedList = proteinDataList
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .ToList();

            Console.WriteLine($"Total ProteinData records after deduplication: {dedupedList.Count}");

            // Print out duplicate Ids if any
            var duplicateIds = proteinDataList.GroupBy(p => p.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateIds.Any())
            {
                Console.WriteLine($"Duplicate ProteinData Ids found: {string.Join(", ", duplicateIds)}");
            }
            else
            {
                Console.WriteLine("No duplicate ProteinData Ids found.");
            }

            return dedupedList;
        }

        public async Task<int> GetApprovedProteinsCountByProjectIdAndUserIdAsync(int projectId, string userId)
        {
            var articles = await _context.ArticlePerProjects
                .Where(ap => ap.IsApproved && ap.ProjectId == projectId && ap.ApprovedById == userId)
                .Include(ap => ap.Article)
                .ThenInclude(a => a.ProteinData)
                .ToListAsync();
            return articles.Sum(ap => ap.Article?.ProteinData.Count() ?? 0);
        }

        public async Task<Dictionary<Project, List<ProteinData>>> GetProteinDataPerProjectAsync(string doi)
        {
            var articles = await _context.ArticlePerProjects
                .Where(ap => ap.IsApproved && ap.ArticleId==doi)
                .Include(ap => ap.Project)
                .ThenInclude(ap=>ap.Organization)
                .Include(ap => ap.Article)
                .ThenInclude(a => a.ProteinData)
                .ToListAsync();
            var proteinDataPerProject = new Dictionary<Project, List<ProteinData>>();
            foreach (var articlePerProject in articles)
            {
                if (!proteinDataPerProject.ContainsKey(articlePerProject.Project))
                {
                    proteinDataPerProject[articlePerProject.Project] = new List<ProteinData>();
                }
                var proteinDataList = articlePerProject.Article.ProteinData
                    .Select(pd => new ProteinData(pd))
                    .ToList();
                proteinDataPerProject[articlePerProject.Project].AddRange(proteinDataList);
            }
            return proteinDataPerProject;
        }

        public async Task<List<ProteinData>> GetProteinDataByProjectIdAndArticleDoiAsync(int projectId, string articleDoi)
        {
            return await _context.Set<ProteinData>()
                .Where(pd => pd.ArticleDoi == articleDoi && pd.ProjectId == projectId)
                .ToListAsync();
        }


    }
}
