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

        public async Task SaveOrUpdateProteinDataListAsync(List<ProteinData> proteinDataList, string articleDoi, int projectId)
        {
            _context.ChangeTracker.Clear();

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

                p.Article = null;
            });

            var proteinIds = proteinDataList.Select(p => p.ProteinId).ToList();

            // Remove all existing proteins for the article and project
            var existingProteins = await _context.Set<ProteinData>()
                .AsNoTracking()
                .Where(p => p.ArticleDoi == articleDoi && p.ProjectId == projectId)
                .ToListAsync();

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
        }

        public async Task<List<string>> GetAllDistinctSoftwareNamesAsync()
        {
            return await _context.Set<ProteinData>()
                .Select(p => p.SoftwareName)
                .Distinct()
                .ToListAsync();
        }

        public async Task<List<string>> GetAllDistinctWaterModelsAsync()
        {
            return await _context.Set<ProteinData>()
                .Select(p => p.WaterModel)
                .Distinct()
                .ToListAsync();
        }

        public async Task<List<string>> GetAllDistinctForceFieldsAsync()
        {
            return await _context.Set<ProteinData>()
                .Select(p => p.ForceField)
                .Distinct()
                .ToListAsync();
        }

        public async Task<List<string>> GetAllDistinctSimulationMethodsAsync()
        {
            return await _context.Set<ProteinData>()
                .Select(p => p.SimulationMethod)
                .Distinct()
                .ToListAsync();
        }

        public async Task<List<string>> GetAllDistinctIonsAsync()
        {
            return await _context.Set<ProteinData>()
                .Select(p => p.Ions)
                .Distinct()
                .ToListAsync();
        }


    }
}
