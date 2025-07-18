using Microsoft.EntityFrameworkCore;
using PeptideDataHomogenizer.Data;

namespace PeptideDataHomogenizer.Services
{
    public class UserService
    {
        private readonly ApplicationDbContext _context;

        public UserService(ApplicationDbContext context)
        {
            _context = context;
        }

        //getuserbyregistrationtokenasync
        public async Task<ApplicationUser> GetUserByRegistrationTokenAsync(long registrationToken)
        {
            return await _context.Set<ApplicationUser>()
                .FirstOrDefaultAsync(u => u.RegistrationToken.HasValue && u.RegistrationToken == registrationToken);
        }


    }
}
