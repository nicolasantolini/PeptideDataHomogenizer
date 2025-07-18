using Entities;
using Microsoft.EntityFrameworkCore;
using PeptideDataHomogenizer.Data;

namespace PeptideDataHomogenizer.Services
{
    public class ArticleService
    {
        private readonly ApplicationDbContext _context;

        public ArticleService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Article>> GetArticlesByPubMedIdsAsync(List<string> pubmedIds)
        {
            return await _context.Set<Article>()
                .Include(a => a.Chapters)
                .Include(a => a.Tables)
                .Include(a => a.Images)
                .Include(a => a.ProteinData)
                .AsSingleQuery()
                .Where(a => pubmedIds.Contains(a.PubMedId))
                .ToListAsync();
        }

        public async Task AddArticlesWithoutChapters(List<Article> articles)
        {
            _context.ChangeTracker.Clear(); // Clear the change tracker to avoid tracking issues
            _context.Set<Article>().AddRange(articles);
            await _context.SaveChangesAsync();
        }
    }
}
