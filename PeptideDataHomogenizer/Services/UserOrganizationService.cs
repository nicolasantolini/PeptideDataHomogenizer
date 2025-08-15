using Entities;
using Microsoft.EntityFrameworkCore;
using PeptideDataHomogenizer.Data;

namespace PeptideDataHomogenizer.Services
{
    public class UserOrganizationService
    {
        private readonly ApplicationDbContext _context;

        public UserOrganizationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Organization>> GetOrganizationsByUserIdAsync(string userId)
        {
            var userOrganizations = await _context.Set<UsersPerOrganization>()
                .Where(u => u.UserId == userId)
                .Select(u => u.OrganizationId)
                .ToListAsync();

            return await _context.Set<Organization>().Where(o => userOrganizations.Contains(o.Id))
                .ToListAsync();
        }

        public async Task<bool> IsUserInOrganizationAsync(string userId, int organizationId)
        {
            return await _context.Set<UsersPerOrganization>()
                .AnyAsync(u => u.UserId == userId && u.OrganizationId == organizationId);
        }
        public async Task<List<ApplicationUser>> GetUsersByOrganizationIdAsync(int organizationId)
        {
            var userOrganizations = await _context.Set<UsersPerOrganization>()
                .Where(u => u.OrganizationId == organizationId)
                .Select(u => u.UserId)
                .ToListAsync();
            return await _context.Set<ApplicationUser>().Where(o => userOrganizations.Contains(o.Id.ToString()))
                .ToListAsync();
        }

        public async Task<string> GetRoleByOrganizationIdAndUserIdAsync(int organizationId, string userId)
        {
            var userOrganization = await _context.Set<UsersPerOrganization>()
                .FirstOrDefaultAsync(u => u.OrganizationId == organizationId && u.UserId == userId);
            return userOrganization?.Role ?? string.Empty;
        }

        public async Task AddUserToOrganizationAsync(string userId, int organizationId, string role)
        {
            var userOrganization = new UsersPerOrganization
            {
                UserId = userId,
                OrganizationId = organizationId,
                Role = role
            };
            await _context.Set<UsersPerOrganization>().AddAsync(userOrganization);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRoleByUserIdAndOrganizationIdAsync(string userId, int organizationId, string newRole)
        {
            _context.ChangeTracker.Clear();
            var userOrganization = await _context.Set<UsersPerOrganization>()
                .FirstOrDefaultAsync(u => u.UserId == userId && u.OrganizationId == organizationId);
            if (userOrganization != null)
            {
                userOrganization.Role = newRole;
                _context.Set<UsersPerOrganization>().Update(userOrganization);
                await _context.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"User with ID {userId} not found in Organization with ID {organizationId}.");
            }
        }

        public async Task<List<UsersPerOrganization>> GetUsersPerOrganizationByListOfUserIdsAsync(List<string> userIds)
        {
            if (userIds == null || !userIds.Any())
            {
                return new List<UsersPerOrganization>();
            }
            _context.ChangeTracker.Clear();
            return await _context.Set<UsersPerOrganization>()
                .Where(u => userIds.Contains(u.UserId))
                .ToListAsync();
        }

        public async Task RemoveUserFromAllOrganizationsAsync(string userId)
        {
            _context.ChangeTracker.Clear();
            var userOrganizations = await _context.Set<UsersPerOrganization>()
                .Where(u => u.UserId == userId)
                .ToListAsync();
            if (userOrganizations.Any())
            {
                _context.Set<UsersPerOrganization>().RemoveRange(userOrganizations);
                await _context.SaveChangesAsync();
            }
        }

        public async Task RemoveUserFromOrganizationAsync(string userId, int organizationId)
        {
            var userOrganization = await _context.Set<UsersPerOrganization>()
                .FirstOrDefaultAsync(u => u.UserId == userId && u.OrganizationId == organizationId);
            if (userOrganization != null)
            {
                _context.Set<UsersPerOrganization>().Remove(userOrganization);
                await _context.SaveChangesAsync();
            }
        }
    }
}
