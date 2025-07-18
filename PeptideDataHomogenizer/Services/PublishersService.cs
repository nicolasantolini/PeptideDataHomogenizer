using Entities;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using PeptideDataHomogenizer.Data;

namespace PeptideDataHomogenizer.Services
{
    public class PublishersService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public PublishersService(ApplicationDbContext context,IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _environment = webHostEnvironment;
        }

        /*
         * public class DiscreditedPublisher
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("url")]
        [MaxLength(255)]
        public string url { get; set; }

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


        public async Task<List<DiscreditedPublisher>> GetDiscreditedPublishersAsync(int projectId)
        {
                return await _context.DiscreditedPublishers
                .Where(p => p.ProjectId == projectId)
                .ToListAsync();
        }

        //update or insert a discredited publisher match on url and project id
        public async Task<DiscreditedPublisher> UpsertDiscreditedPublisherAsync(DiscreditedPublisher discreditedPublisher)
        {
            var existingPublisher = await _context.DiscreditedPublishers
                .FirstOrDefaultAsync(p => p.Url == discreditedPublisher.Url && p.ProjectId == discreditedPublisher.ProjectId);
            if (existingPublisher != null)
            {
                existingPublisher.DiscreditedReason = discreditedPublisher.DiscreditedReason;
                existingPublisher.DiscreditedById = discreditedPublisher.DiscreditedById;
                _context.DiscreditedPublishers.Update(existingPublisher);
                await _context.SaveChangesAsync();
                return existingPublisher;
            }
            else
            {
                _context.DiscreditedPublishers.Add(discreditedPublisher);
                await _context.SaveChangesAsync();
                return discreditedPublisher;
            }
        }

    }
}