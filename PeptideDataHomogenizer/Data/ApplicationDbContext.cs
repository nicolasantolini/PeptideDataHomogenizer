using Entities;
using Entities.RegexData;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace PeptideDataHomogenizer.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ExtractedTable>()
            .Property(e => e.Rows)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<Dictionary<string, string>>>(v, (JsonSerializerOptions)null) ?? new List<Dictionary<string, string>>()
            );
    }

    public DbSet<ProteinData> ProteinData { get; set; }
    public DbSet<Article> Articles { get; set; }
    public DbSet<Chapter> Chapters { get; set; }
    public DbSet<ExtractedTable> Tables { get; set; }
    public DbSet<ImageHolder> Images { get; set; }
    public DbSet<DiscreditedJournal> DiscreditedJournals { get; set; }
    public DbSet<DiscreditedPublisher> DiscreditedPublishers { get; set; }
    public DbSet<ApplicationUser> ApplicationUsers { get; set; }

    public DbSet<ForceFieldSoftware> ForceFieldsSoftware { get; set; }
    public DbSet<SimulationMethod> SimulationMethods { get; set; }
    public DbSet<SimulationSoftware> SimulationSoftware { get; set; }
    public DbSet<WaterModel> WaterModels { get; set; }
    public DbSet<Ion> Ions { get; set; }
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<UsersPerOrganization> UsersPerOrganizations { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<UsersPerProject> UsersPerProjects { get; set; }

    public DbSet<ArticlePerProject> ArticlePerProjects { get; set; }

}
