using Entities;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using PeptideDataHomogenizer.Data;

namespace PeptideDataHomogenizer.Services
{
    public class ProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProjectService(ApplicationDbContext context,IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _environment = webHostEnvironment;
        }

        //GetProjectsByOrganizationIdAsync
        public async Task<List<Project>> GetProjectsByOrganizationIdAsync(int organizationId)
        {
            return await _context.Set<Project>()
                .Where(p => p.OrganizationId == organizationId)
                .ToListAsync();
        }

        public async Task<List<Project>> GetProjectsByOrganizationIdAndUserIdAsync(int organizationId, string userId)
        {
            return await _context.Set<Project>()
                .Where(p => p.OrganizationId == organizationId && _context.UsersPerProjects.Any(u => u.UserId == userId && p.Id == u.ProjectId))
                .ToListAsync();
        }

        //getprojectbyorganizationid,projectname, logopath, description
        public async Task<Project> GetProjectMatchAsync(int organizationId, string projectName,string description)
        {
            return await _context.Set<Project>()
                .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.Name == projectName && p.Description == description);
        }

        /*
         * Implement a method to uopdate a project like this:
         * public async Task UpdateOrganizationAsync(Organization organization, IBrowserFile logoFile = null)
        {
            if (logoFile != null && logoFile.Size > 0)
            {
                if (!string.IsNullOrEmpty(organization.LogoPath))
                {
                    DeleteLogo(organization.LogoPath);
                }
                organization.LogoPath = await SaveLogoAsync(logoFile);
            }
            _dbContext.Organizations.Update(organization);
            await _dbContext.SaveChangesAsync();
        }
         */

        public async Task UpdateProjectAsync(Project project)
        {
            _context.Set<Project>().Update(project);
            await _context.SaveChangesAsync();
        }

        private async Task<string> SaveLogoAsync(IBrowserFile logoFile)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "project_logos");
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

    }
}