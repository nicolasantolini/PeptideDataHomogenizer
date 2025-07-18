using Entities;
using Microsoft.EntityFrameworkCore;
using PeptideDataHomogenizer.Data;

namespace PeptideDataHomogenizer.Services
{
    public class ArticleContentService
    {
        private readonly ApplicationDbContext _context;

        public ArticleContentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddChaptersAsync(List<Chapter> chapters)
        {
            foreach (var chapter in chapters)
            {
                _context.Set<Chapter>().Add(chapter);
            }
            await _context.SaveChangesAsync();
        }

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

        public async Task ReplaceChaptersAsync(List<Chapter> chapters, string doi)
        {
            _context.ChangeTracker.Clear();
            var existingChapters = await _context.Set<Chapter>().Where(c => c.ArticleDoi == doi).ToListAsync();
            _context.Set<Chapter>().RemoveRange(existingChapters);
            foreach (var chapter in chapters)
            {
                _context.Set<Chapter>().Add(chapter);
            }
            await _context.SaveChangesAsync();
        }

        public async Task ReplaceTablesAsync(List<ExtractedTable> tables, string doi)
        {
            _context.ChangeTracker.Clear();
            var existingTables = await _context.Set<ExtractedTable>().Where(t => t.ArticleDoi == doi).ToListAsync();
            _context.Set<ExtractedTable>().RemoveRange(existingTables);
            foreach (var table in tables)
            {
                _context.Set<ExtractedTable>().Add(table);
            }
            await _context.SaveChangesAsync();
        }

        public async Task ReplaceImagesAsync(List<ImageHolder> images, string doi)
        {
            _context.ChangeTracker.Clear();
            var existingImages = await _context.Set<ImageHolder>().Where(i => i.ArticleDoi == doi).ToListAsync();
            _context.Set<ImageHolder>().RemoveRange(existingImages);
            foreach (var image in images)
            {
                _context.Set<ImageHolder>().Add(image);
            }
            await _context.SaveChangesAsync();
        }
    }
}
