using Entities;
using Microsoft.EntityFrameworkCore;
using PeptideDataHomogenizer.Data;

namespace PeptideDataHomogenizer.Services
{
    public class ProteinDataService
    {
        private readonly ApplicationDbContext _context;

        public ProteinDataService(ApplicationDbContext context)
        {
            _context = context;
        }



        // Pseudocode plan:
        // 1. Before adding ProteinData entities, ensure their Article navigation property is not set or is set to null.
        // 2. Only set the ArticleDoi foreign key property, not the Article navigation property, to avoid EF Core trying to track a new Article with null Doi.
        // 3. Update the SaveOrUpdateProteinDataListAsync method accordingly.

        public async Task SaveOrUpdateProteinDataListAsync(List<ProteinData> proteinDataList, string articleDoi, int projectId)
        {
            Console.WriteLine($"[DEBUG] Starting SaveOrUpdateProteinDataListAsync for ArticleDoi: {articleDoi}, ProjectId: {projectId}");
            _context.ChangeTracker.Clear();

            Console.WriteLine($"[DEBUG] Normalizing {proteinDataList.Count} proteinDataList entries.");
            proteinDataList.ForEach(p =>
            {
                p.Classification ??= string.Empty;
                p.SoftwareName ??= string.Empty;
                p.SoftwareVersion ??= string.Empty;
                p.WaterModel ??= string.Empty;
                p.WaterModelType ??= string.Empty;
                p.ForceField ??= string.Empty;
                p.SimulationMethod ??= string.Empty;
                p.Ions ??= string.Empty;
                p.Organism ??= string.Empty;
                p.Method ??= string.Empty;
                p.Residue ??= string.Empty;
                p.Binder ??= string.Empty;
                // Ensure navigation property is not set to avoid EF tracking issues
                p.Article = null;
            });

            var proteinIds = proteinDataList.Select(p => p.ProteinId).ToList();
            Console.WriteLine($"[DEBUG] Protein IDs to save/update: {string.Join(", ", proteinIds)}");

            // Remove all existing proteins for the article and project
            var existingProteins = await _context.Set<ProteinData>()
                .AsNoTracking()
                .Where(p => p.ArticleDoi == articleDoi && p.ProjectId == projectId)
                .ToListAsync();
            Console.WriteLine($"[DEBUG] Found {existingProteins.Count} existing proteins to remove for ArticleDoi: {articleDoi}, ProjectId: {projectId}");

            // Detach navigation properties to avoid EF tracking issues
            existingProteins.ForEach(p => p.Article = null);

            _context.Set<ProteinData>().RemoveRange(existingProteins);

            await _context.SaveChangesAsync();

            _context.ChangeTracker.Clear();
            // Add new proteins
            proteinDataList.ForEach(m => {
                m.ArticleDoi = articleDoi;
                m.ProjectId = projectId;
                m.Id = 0; // Reset Id to ensure new entries are added
                m.Article = null;
                } // Ensure navigation property is not set to avoid EF tracking issues
            );

            await _context.Set<ProteinData>().AddRangeAsync(proteinDataList);

            await _context.SaveChangesAsync();
            Console.WriteLine($"[DEBUG] SaveOrUpdateProteinDataListAsync completed for ArticleDoi: {articleDoi}, ProjectId: {projectId}");
        }

        //Getalldistinctsoftwarenames 
        public async Task<List<string>> GetAllDistinctSoftwareNamesAsync()
        {
            return await _context.Set<ProteinData>()
                .Select(p => p.SoftwareName)
                .Distinct()
                .ToListAsync();
        }

        //Getalldistinct water models
        public async Task<List<string>> GetAllDistinctWaterModelsAsync()
        {
            return await _context.Set<ProteinData>()
                .Select(p => p.WaterModel)
                .Distinct()
                .ToListAsync();
        }

        //Getalldistinctforcefields
        public async Task<List<string>> GetAllDistinctForceFieldsAsync()
        {
            return await _context.Set<ProteinData>()
                .Select(p => p.ForceField)
                .Distinct()
                .ToListAsync();
        }
        //Getalldistinctsimulationmethods
        public async Task<List<string>> GetAllDistinctSimulationMethodsAsync()
        {
            return await _context.Set<ProteinData>()
                .Select(p => p.SimulationMethod)
                .Distinct()
                .ToListAsync();
        }
        //Getalldistinctions
        public async Task<List<string>> GetAllDistinctIonsAsync()
        {
            return await _context.Set<ProteinData>()
                .Select(p => p.Ions)
                .Distinct()
                .ToListAsync();
        }


    }
}
