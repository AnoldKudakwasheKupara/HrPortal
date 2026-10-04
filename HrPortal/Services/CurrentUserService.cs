using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HrPortal.Services
{
    /// <summary>
    /// Resolves the signed-in user to their employee record.
    ///
    /// Administrative accounts such as the seeded administrator
    /// have no employee record, so every caller has to cope with
    /// null rather than assume one exists.
    /// </summary>
    public class CurrentUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }


        public int? GetUserId()
        {
            var value = _httpContextAccessor
                .HttpContext?
                .User
                .FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(value, out var id) ? id : null;
        }

        public async Task<Employee?> GetEmployeeAsync()
        {
            var userId = GetUserId();

            if (userId == null)
                return null;

            return await _context.Employees
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.UserId == userId.Value);
        }

        public async Task<int?> GetEmployeeIdAsync()
        {
            var userId = GetUserId();

            if (userId == null)
                return null;

            return await _context.Employees
                .AsNoTracking()
                .Where(e => e.UserId == userId.Value)
                .Select(e => (int?)e.Id)
                .FirstOrDefaultAsync();
        }
    }
}
