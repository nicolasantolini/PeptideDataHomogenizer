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
         public record ProteinData
    {
        public ProteinData (ProteinData proteinData)
        {
            Id = proteinData.Id;
            ProteinId = proteinData.ProteinId;
            Classification = proteinData.Classification;
            Organism = proteinData.Organism;
            Method = proteinData.Method;
            SoftwareName = proteinData.SoftwareName;
            SoftwareVersion = proteinData.SoftwareVersion;
            WaterModel = proteinData.WaterModel;
            ForceField = proteinData.ForceField;
            SimulationMethod = proteinData.SimulationMethod;
            Temperature = proteinData.Temperature;
            Ions = proteinData.Ions;
            IonConcentration = proteinData.IonConcentration;
            SimulationLength = proteinData.SimulationLength;
            ArticleDoi = proteinData.ArticleDoi;
        }

        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("protein_id")]
        public string ProteinId { get; set; }

        [Column("classification")]
        [MaxLength(255)]
        public string Classification { get; set; } = string.Empty;

        [Column("organism")]
        [MaxLength(255)]
        public string Organism { get; set; } = string.Empty;

        [Column("method")]
        [MaxLength(255)]
        public string Method { get; set; } = string.Empty;

        [NotMapped]
        [JsonIgnore]
        [Column("residue")]
        [MaxLength(255)]
        public string? Residue { get; set; } = string.Empty;

        [Column("software_name")]
        [MaxLength(255)]
        public string SoftwareName { get; set; } = string.Empty;

        [Column("software_version")]
        [MaxLength(255)]
        public string SoftwareVersion { get; set; } = string.Empty;

        [Column("water_model")]
        [MaxLength(255)]
        public string WaterModel { get; set; } = string.Empty;

        [Column("force_field")]
        [MaxLength(255)]
        public string ForceField { get; set; } = string.Empty;
        [Column("simulation_method")]
        [MaxLength(255)]
        public string SimulationMethod { get; set; } = string.Empty;


        [Column("temperature")]
        public double Temperature { get; set; } = 0.0;

        [Column("ions")]
        [MaxLength(255)]
        public string Ions { get; set; } = string.Empty;

        [Column("ion_concentration")]
        public double IonConcentration { get; set; } = 0.0;

        [Column("simulation_length")]
        public double SimulationLength { get; set; } = 0.0;


        public class ProteinPerProject
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [ForeignKey("ProjectId")]
        public Project Project { get; set; }
        [Column("ProjectId")]
        public int ProjectId { get; set; }

        [Required]
        [ForeignKey("ArticleDoi")]
        public Article Article { get; set; }
        [Column("ArticleDoi")]
        public string ArticleDoi { get; set; } = string.Empty;

        [Required]
        [ForeignKey("ProteinId")]
        public ProteinData Protein { get; set; }
        [Column("ProteinId")]
        public string ProteinId { get; set; }

        [Column("IsApproved")]
        public bool IsApproved { get; set; } = false;
    }
         */


        //get protein data per project and article doi, return a list of protein data for that article
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

        //getapprovedproteindatabyprojectidandrticledoi
        public async Task<List<ProteinData>> GetApprovedProteinDataByProjectIdAndArticleDoiAsync(int projectId, string articleDoi)
        {
            return await _context.Set<ProteinData>()
                .Where(pd => pd.ArticleDoi == articleDoi && projectId == pd.ProjectId)
                .ToListAsync();
        }

        //delete proteindata by project id and article doi
        public async Task DeleteProteinDataByProjectIdAndArticleDoiAsync(int projectId, string articleDoi)
        {
            Console.WriteLine($"[DEBUG] Entering DeleteProteinDataByProjectIdAndArticleDoiAsync with projectId={projectId}, articleDoi={articleDoi}");
            var proteinDataList = await _context.Set<ProteinData>()
                .Where(pd => pd.ArticleDoi == articleDoi && pd.ProjectId == projectId)
                .ToListAsync();
            Console.WriteLine($"[DEBUG] Found {proteinDataList.Count} ProteinData records to delete for ArticleDoi: {articleDoi}, ProjectId: {projectId}");
            if (proteinDataList.Any())
            {
                proteinDataList.ForEach(pdb => pdb.Article = null);
                _context.Set<ProteinData>().RemoveRange(proteinDataList);
                await _context.SaveChangesAsync();
                Console.WriteLine("[DEBUG] Successfully deleted ProteinData records.");
            }
            else
            {
                Console.WriteLine("[DEBUG] No ProteinData records found to delete.");
            }
        }

        //getapprovedproteindatabyprojectid
        public async Task<List<ProteinData>> GetApprovedProteinDataByProjectIdAsync(int projectId)
        {
            Console.WriteLine($"[DEBUG] Entering GetApprovedProteinDataByProjectIdAsync with projectId={projectId}");

            var articles = await _context.ArticlePerProjects
                .Where(ap => ap.IsApproved && ap.ProjectId == projectId)
                .Include(ap => ap.Article)
                .ToListAsync();

            Console.WriteLine($"[DEBUG] Retrieved {articles.Count} ArticlePerProject records for projectId={projectId}");

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
                    Console.WriteLine($"[DEBUG] ArticlePerProject[{articleIndex}] ArticleId={articlePerProject.ArticleId} has {articlePerProject.Article.ProteinData?.Count ?? 0} ProteinData records");
                    int proteinIndex = 0;
                    foreach (var pd in articlePerProject.Article.ProteinData)
                    {
                        proteinIndex++;
                        pd.Article = articlePerProject.Article;
                        proteinDataList.Add(pd);
                        Console.WriteLine($"[DEBUG] Added ProteinData Id={pd.Id}, ProteinId={pd.ProteinId}, ArticleDoi={pd.ArticleDoi} (from ArticlePerProject[{articleIndex}])");
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG] ArticlePerProject[{articleIndex}] Article is null");
                }
            }

            Console.WriteLine($"[DEBUG] Total ProteinData records before deduplication: {proteinDataList.Count}");

            // Remove duplicates by Id
            var dedupedList = proteinDataList
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .ToList();

            Console.WriteLine($"[DEBUG] Total ProteinData records after deduplication: {dedupedList.Count}");

            // Print out duplicate Ids if any
            var duplicateIds = proteinDataList.GroupBy(p => p.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateIds.Any())
            {
                Console.WriteLine($"[DEBUG] Duplicate ProteinData Ids found: {string.Join(", ", duplicateIds)}");
            }
            else
            {
                Console.WriteLine("[DEBUG] No duplicate ProteinData Ids found.");
            }

            return dedupedList;
        }

        //getapprovedproteinscountbyprojectidanduserid (check articleperproject table for approved articles and count the proteins in those articles)
        public async Task<int> GetApprovedProteinsCountByProjectIdAndUserIdAsync(int projectId, string userId)
        {
            var articles = await _context.ArticlePerProjects
                .Where(ap => ap.IsApproved && ap.ProjectId == projectId && ap.ApprovedById == userId)
                .Include(ap => ap.Article)
                .ThenInclude(a => a.ProteinData)
                .ToListAsync();
            return articles.Sum(ap => ap.Article?.ProteinData.Count() ?? 0);
        }


        //for every project that has an article doi and is approved, get all protein data for that article and put it in a list, and return a dictionary(project,List<ProteinData>) of lists of protein data per project
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

        //getproteindatabyprojectidandarticledoi
        public async Task<List<ProteinData>> GetProteinDataByProjectIdAndArticleDoiAsync(int projectId, string articleDoi)
        {
            return await _context.Set<ProteinData>()
                .Where(pd => pd.ArticleDoi == articleDoi && pd.ProjectId == projectId)
                .ToListAsync();
        }


    }
}
