using Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PeptideDataHomogenizer.Data;

namespace PeptideDataHomogenizer.Services
{
    public class ArticlePerProjectService
    {
        private readonly ApplicationDbContext _context;
        private ProteinDataPerProjectService _proteinDataPerProjectService;
        private ProteinDataService _proteinDataService;

        public ArticlePerProjectService(ApplicationDbContext context, [FromServices] ProteinDataPerProjectService proteinDataPerProjectService, [FromServices] ProteinDataService proteinDataService)
        {
            _context = context;
            _proteinDataPerProjectService = proteinDataPerProjectService;
            _proteinDataService = proteinDataService;
        }
        /*
         * public class ArticlePerProject
    {
        [Key]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [ForeignKey("ProjectId")]
        public Project Project { get; set; }
        [Column("ProjectId")]
        public int ProjectId { get; set; }

        [Required]
        [ForeignKey("ArticleId")]
        public Article Article { get; set; }
        [Column("ArticleId")]
        public string ArticleId { get; set; }

        [Column("IsDiscredited")]
        public bool IsDiscredited { get; set; } = false;

        [Column("DiscreditedReason")]
        [MaxLength(1000)]
        public string DiscreditedReason { get; set; } = string.Empty;

        [Column("IsApproved")]
        public bool IsApproved { get; set; } = false;

        [Column("datetime_approval")]
        public DateTime? DatetimeApproval { get; set; }

        [Column("approved_by")]
        [MaxLength(255)]
        public string ApprovedById { get; set; } = string.Empty;
    }

         
         */


        //Get if article is discredited or approved in a project
        public async Task<ArticlePerProject?> GetArticlePerProjectAsync(int projectId, string articleId)
        {
            return await _context.ArticlePerProjects
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ProjectId == projectId && a.ArticleId == articleId);
        }

        //getarticlesbyprojectandlistofarticledois
        public async Task<List<ArticlePerProject>> GetArticlesByProjectAndListOfArticleDoisAsync(int projectId, List<string> articleDois)
        {
            return await _context.ArticlePerProjects
                .AsNoTracking()
                .Where(a => a.ProjectId == projectId && articleDois.Contains(a.ArticleId))
                .Include(a=>a.Article)
                .ThenInclude(a=>a.ProteinData)
                .Include(a => a.Project)
                .ToListAsync();
        }

        //getdiscreditedarticlesbyproject Dictionary<Article, ArticlePerProject>
        public async Task<Dictionary<Article, ArticlePerProject>> GetDiscreditedArticlesByProjectAsync(int projectId)
        {
            return await _context.ArticlePerProjects
                .AsNoTracking()
                .Where(a => a.ProjectId == projectId && a.IsDiscredited)
                .Include(a => a.Article)
                .ToDictionaryAsync(a => a.Article, a => a);
        }

        //recredit article in project
        public async Task RecreditArticleInProjectAsync(int projectId, string articleId)
        {
            var articlePerProject = await _context.ArticlePerProjects
                .FirstOrDefaultAsync(a => a.ProjectId == projectId && a.ArticleId == articleId);
            if (articlePerProject != null)
            {
                articlePerProject.IsDiscredited = false;
                articlePerProject.DiscreditedReason = string.Empty;
                _context.ArticlePerProjects.Update(articlePerProject);
                await _context.SaveChangesAsync();
            }
        }


        //filterlistofarticlesbynotapprovedandnotdiscredited
        public async Task<List<string>> FilterListOfArticlesByNotApprovedAndNotDiscreditedAsync(int projectId, List<string> articleDois)
        {
            var ApprovedOrDiscreditedProjects = await _context.ArticlePerProjects
                .AsNoTracking()
                .Where(a => a.ProjectId == projectId &&
                            articleDois.Contains(a.ArticleId) &&
                            a.IsApproved ||
                            a.IsDiscredited)
                .Select(a => a.ArticleId)
                .ToListAsync();

            return articleDois.Except(ApprovedOrDiscreditedProjects).ToList();
        }

        //replacearticleperprojectbyidarticledoiandprojectid
        public async Task CompleteArticlePerProjectByIdArticleDoiAndProjectIdAsync(
    string articleDoi,
    int projectId,
    List<ProteinData> proteinData,string approvedById)
        {

            if (string.IsNullOrEmpty(articleDoi))
                throw new ArgumentException("Article DOI cannot be empty", nameof(articleDoi));

            // Use transaction to ensure atomic operations
            using var transaction = await _context.Database.BeginTransactionAsync();


            try
            {
                // Find or create the ArticlePerProject relationship
                var existingRelationship = await _context.ArticlePerProjects
                    .FirstOrDefaultAsync(a => a.ArticleId == articleDoi && a.ProjectId == projectId);

                if (existingRelationship != null)
                {
                    // Update existing relationship
                    existingRelationship.IsApproved = true;
                    existingRelationship.DatetimeApproval = DateTime.UtcNow;
                    existingRelationship.ApprovedById = approvedById;
                    existingRelationship.IsDiscredited = false;
                    existingRelationship.DiscreditedReason = "";
                    _context.Update(existingRelationship);
                }
                else
                {
                    // Create new relationship
                    ArticlePerProject newArticlePerProject = new();
                    newArticlePerProject.Id = 0; // Ensure EF treats as new entity
                    newArticlePerProject.ArticleId = articleDoi;
                    newArticlePerProject.ProjectId = projectId;
                    newArticlePerProject.IsApproved = true;
                    newArticlePerProject.DatetimeApproval = DateTime.UtcNow;
                    newArticlePerProject.Article = null; // Prevent navigation property issues
                    _context.Add(newArticlePerProject);
                }

                // Save the relationship first to ensure we have a valid ID if needed
                await _context.SaveChangesAsync();

                // Handle protein data replacement
                if (proteinData != null)
                {
                    await _proteinDataService.SaveOrUpdateProteinDataListAsync(
                        proteinData,
                        articleDoi,
                        projectId);
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
