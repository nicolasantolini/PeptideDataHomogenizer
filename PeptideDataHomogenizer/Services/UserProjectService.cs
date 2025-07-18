namespace PeptideDataHomogenizer.Services
{
    using Entities;
    using Microsoft.AspNetCore.Components.Forms;
    using Microsoft.EntityFrameworkCore;
    using PeptideDataHomogenizer.Data;

    public class UserProjectService
    {
        private readonly ApplicationDbContext _context;

        public UserProjectService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ApplicationUser>> GetUsersByProjectIdAsync(int projectId)
        {
            var userProjects = await _context.Set<UsersPerProject>()
                .Where(u => u.ProjectId == projectId)
                .Select(u => u.UserId)
                .ToListAsync();
            return await _context.Set<ApplicationUser>().Where(p => userProjects.Contains(p.Id.ToString()) && p.HasRegistered)
                .ToListAsync();
        }

        public async Task<string> GetRoleByProjectIdAndUserIdAsync(int projectId, string userId)
        {
            var userProject = await _context.Set<UsersPerProject>()
                .FirstOrDefaultAsync(up => up.ProjectId == projectId && up.UserId == userId);
            return userProject?.Role ?? string.Empty;
        }

        public async Task AddUserToProjectAsync(string userId, int projectId, string role)
        {
            var userProject = new UsersPerProject
            {
                UserId = userId,
                ProjectId = projectId,
                Role = role
            };
            await _context.Set<UsersPerProject>().AddAsync(userProject);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveUserFromProjectAsync(string userId, int projectId)
        {
            var userProject = await _context.Set<UsersPerProject>()
                .FirstOrDefaultAsync(up => up.UserId == userId && up.ProjectId == projectId);
            if (userProject != null)
            {
                _context.Set<UsersPerProject>().Remove(userProject);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateRoleByUserIdAndProjectIdAsync(string userId, int projectId, string newRole)
        {
            _context.ChangeTracker.Clear();
            var userProject = await _context.Set<UsersPerProject>()
                .FirstOrDefaultAsync(u => u.UserId == userId && u.ProjectId == projectId);
            if (userProject != null)
            {
                userProject.Role = newRole;
                _context.Set<UsersPerProject>().Update(userProject);
                await _context.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"User with ID {userId} not found in Project with ID {projectId}.");
            }
        }
    }
}
