using Entities;
using Microsoft.EntityFrameworkCore;
using PeptideDataHomogenizer.Data;

namespace PeptideDataHomogenizer.Services
{
    public class ArticleModerationService
    {
        private readonly ApplicationDbContext _context;

        public ArticleModerationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task DiscreditArticlesByJournalAndProjectIdAsync(string journal, int projectId)
        {
            var articles = await _context.ArticlePerProjects
                .Where(a => a.ProjectId == projectId &&
                            a.Article.Journal.ToLower() == journal.ToLower() &&
                            !a.IsDiscredited)
                .ToListAsync();
            foreach (var article in articles)
            {
                article.IsDiscredited = true;
                article.DiscreditedReason = "The journal that published this article was discredited.";
                article.IsApproved = false;
                article.ApprovedById = " ";
                article.DatetimeApproval = null;
                _context.ArticlePerProjects.Update(article);
            }
            await _context.SaveChangesAsync();
        }


        public async Task DiscreditArticleInProjectAsync(int projectId, string articleId, string discreditedReason)
        {
            var articlePerProject = await _context.ArticlePerProjects
                .FirstOrDefaultAsync(a => a.ProjectId == projectId && a.ArticleId == articleId);

            if (articlePerProject != null)
            {
                articlePerProject.IsDiscredited = true;
                articlePerProject.DiscreditedReason = discreditedReason;
                articlePerProject.IsApproved = false;
                articlePerProject.ApprovedById = " ";
                articlePerProject.DatetimeApproval = null;
                _context.ArticlePerProjects.Update(articlePerProject);

            }
            else
            {
                articlePerProject = new ArticlePerProject
                {
                    ProjectId = projectId,
                    ArticleId = articleId,
                    IsDiscredited = true,
                    DiscreditedReason = discreditedReason,
                    IsApproved = false,
                    ApprovedById = " ",
                    DatetimeApproval = null
                };
                await _context.ArticlePerProjects.AddAsync(articlePerProject);
            }

            //if any other project has this article, do not delete the proteins, otherwise delete them
            var otherProjectsWithArticle = await _context.ArticlePerProjects
                .Where(ap => ap.ArticleId == articleId && ap.ProjectId != projectId && !ap.IsDiscredited && !ap.IsApproved)
                .AnyAsync();

            //remove proteindata associated with this article
            var proteins = await _context.Set<ProteinData>()
                .Where(p => p.ArticleDoi == articleId && p.ProjectId == projectId)
                .ToListAsync();
            proteins.ForEach(p => p.Article = null);
            _context.Set<ProteinData>().RemoveRange(proteins);

            if (!otherProjectsWithArticle)
            {
                var chapters = await _context.Set<Chapter>().Where(c => c.ArticleDoi == articleId).ToListAsync();
                _context.Set<Chapter>().RemoveRange(chapters);

                var images = await _context.Set<ImageHolder>().Where(i => i.ArticleDoi == articleId).ToListAsync();
                _context.Set<ImageHolder>().RemoveRange(images);

                var tables = await _context.Set<ExtractedTable>().Where(t => t.ArticleDoi == articleId).ToListAsync();
                _context.Set<ExtractedTable>().RemoveRange(tables);
            }


            await _context.SaveChangesAsync();
        }

        public async Task<int> GetDiscreditedArticlesCountByProjectIdAndUserIdAsync(int projectId,string userId)
        {
            return await _context.ArticlePerProjects
                .CountAsync(a => a.ProjectId == projectId && a.IsDiscredited && a.ApprovedById==userId);
        }
    }
}
