using Entities;
using Entities.RegexData;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PeptideDataHomogenizer.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ProteinData> ProteinData { get; set; }
    public DbSet<Article> Articles { get; set; }
    public DbSet<Chapter> Chapters { get; set; }
    public DbSet<DiscreditedJournal> DiscreditedJournals { get; set; }
    public DbSet<DiscreditedPublisher> DiscreditedPublishers { get; set; }
    public DbSet<ApplicationUser> ApplicationUsers { get; set; }

    public DbSet<ForceFieldSoftware> ForceFieldsSoftware { get; set; }
    public DbSet<SimulationMethod> SimulationMethods { get; set; }
    public DbSet<SimulationSoftware> SimulationSoftware { get; set; }
    public DbSet<WaterModel> WaterModels { get; set; }
    public DbSet<Ion> Ions { get; set; }

}
