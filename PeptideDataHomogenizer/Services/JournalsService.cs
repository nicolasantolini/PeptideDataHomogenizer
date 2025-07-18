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

        /*
         * public class DiscreditedJournal
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("title")]
        [MaxLength(255)]
        public string Title { get; set; }

        [ForeignKey("project")]
        [Column("project_id")]
        public int ProjectId { get; set; }
        public Project Project { get; set; }

        [Column("discredited_reason")]
        [MaxLength(1000)]
        public string DiscreditedReason { get; set; } = string.Empty;

        [Column("discredited_by")]
        [MaxLength(255)]
        public string DiscreditedById { get; set; } = string.Empty;

    }
         */


        public async Task<List<DiscreditedJournal>> GetDiscreditedJournalsAsync(int projectId)
        {
                return await _context.DiscreditedJournals
                .Where(j => j.ProjectId == projectId)
                .ToListAsync();
        }

        //update or insert a discredited journal match on title and project id
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