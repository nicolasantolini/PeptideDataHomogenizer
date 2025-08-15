using Entities;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using PeptideDataHomogenizer.Data;

namespace PeptideDataHomogenizer.Services
{
    public class JournalsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public JournalsService(ApplicationDbContext context,IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _environment = webHostEnvironment;
        }


        public async Task<List<DiscreditedJournal>> GetDiscreditedJournalsAsync(int projectId)
        {
                return await _context.DiscreditedJournals
                .Where(j => j.ProjectId == projectId)
                .ToListAsync();
        }

        public async Task<DiscreditedJournal> UpsertDiscreditedJournalAsync(DiscreditedJournal discreditedJournal)
        {
            var existingJournal = await _context.DiscreditedJournals
                .FirstOrDefaultAsync(j => j.Title == discreditedJournal.Title && j.ProjectId == discreditedJournal.ProjectId);
            if (existingJournal != null)
            {
                existingJournal.DiscreditedReason = discreditedJournal.DiscreditedReason;
                existingJournal.DiscreditedById = discreditedJournal.DiscreditedById;
                _context.DiscreditedJournals.Update(existingJournal);
            }
            else
            {
                _context.DiscreditedJournals.Add(discreditedJournal);
            }
            await _context.SaveChangesAsync();
            return discreditedJournal;
        }

    }
}