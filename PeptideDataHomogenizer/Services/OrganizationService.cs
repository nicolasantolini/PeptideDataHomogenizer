namespace PeptideDataHomogenizer.Services
{
    using Entities;
    using Microsoft.AspNetCore.Components.Forms;
    using Microsoft.EntityFrameworkCore;
    using PeptideDataHomogenizer.Data;

    public class OrganizationService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;

        public OrganizationService(ApplicationDbContext dbContext, IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _environment = environment;
        }

        public async Task<List<Organization>> GetAllOrganizationsAsync()
        {
            return await _dbContext.Organizations.ToListAsync();
        }

        public async Task<Organization> GetOrganizationByIdAsync(int id)
        {
            return await _dbContext.Organizations.FindAsync(id);
        }

        public async Task AddOrganizationAsync(Organization organization, IBrowserFile logoFile = null)
        {
            //if (logoFile != null && logoFile.Size > 0)
            //{
            //    organization.LogoPath = await SaveLogoAsync(logoFile);
            //}
            _dbContext.Organizations.Add(organization);
            await _dbContext.SaveChangesAsync();
        }

        public async Task UpdateOrganizationAsync(Organization organization, IBrowserFile logoFile = null)
        {
            _dbContext.Organizations.Update(organization);
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteOrganizationAsync(int id)
        {
            var organization = await GetOrganizationByIdAsync(id);
            if (organization != null)
            {
                _dbContext.Organizations.Remove(organization);
                await _dbContext.SaveChangesAsync();
            }
        }

        private async Task<string> SaveLogoAsync(IBrowserFile logoFile)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "organization_logos");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString();
            //append the file extension from the original file
            var fileExtension = Path.GetExtension(logoFile.Name);
            if (string.IsNullOrEmpty(fileExtension))
            {
                fileExtension = ".png"; // Default to PNG if no extension is provided
            }
            uniqueFileName += fileExtension;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                using (var logoStream = logoFile.OpenReadStream())
                {
                    await logoStream.CopyToAsync(fileStream);
                }
            }

            return $"/organization_logos/{uniqueFileName}";
        }

        private void DeleteLogo(string logoPath)
        {
            if (!string.IsNullOrEmpty(logoPath))
            {
                var fullPath = Path.Combine(_environment.WebRootPath, logoPath.TrimStart('/'));
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
        }

        public async Task<List<Organization>> SearchOrganizationsAsync(string searchTerm)
        {
            return await _dbContext.Organizations
                .Where(o => o.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .OrderBy(o => o.Name)
                .ToListAsync();
        }

        public async Task<List<Organization>> GetOrganizationsByUserIdAsync(string userId)
        {
            var userOrganizations = await _dbContext.UsersPerOrganizations
                .Where(u => u.UserId == userId)
                .Select(u => u.OrganizationId)
                .ToListAsync();
            return await _dbContext.Organizations
                .Where(o => userOrganizations.Contains(o.Id))
                .ToListAsync();
        }
    }
}
