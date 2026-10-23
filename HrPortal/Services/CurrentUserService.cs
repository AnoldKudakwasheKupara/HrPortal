using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HrPortal.Services
{
    public class CurrentUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private Employee? _employee;
        private bool _employeeLoaded;

        private int? _employeeId;
        private bool _employeeIdLoaded;

        private string? _displayName;
        private bool _displayNameLoaded;

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
            if (_employeeLoaded)
                return _employee;

            var userId = GetUserId();

            if (userId == null)
            {
                _employeeLoaded = true;
                return _employee = null;
            }

            _employee = await _context.Employees
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.UserId == userId.Value);

            _employeeLoaded = true;

            if (_employee != null)
            {
                _employeeId = _employee.Id;
                _employeeIdLoaded = true;
            }

            return _employee;
        }

        public async Task<int?> GetEmployeeIdAsync()
        {
            if (_employeeIdLoaded)
                return _employeeId;

            var userId = GetUserId();

            if (userId == null)
            {
                _employeeIdLoaded = true;
                return _employeeId = null;
            }

            _employeeId = await _context.Employees
                .AsNoTracking()
                .Where(e => e.UserId == userId.Value)
                .Select(e => (int?)e.Id)
                .FirstOrDefaultAsync();

            _employeeIdLoaded = true;

            return _employeeId;
        }

        public async Task<string?> GetDisplayNameAsync()
        {
            if (_displayNameLoaded)
                return _displayName;

            _displayNameLoaded = true;

            var principal = _httpContextAccessor.HttpContext?.User;

            var claimed = principal
                ?.FindFirst(ApplicationUserClaimsPrincipalFactory
                    .FullNameClaim)
                ?.Value;

            if (!string.IsNullOrWhiteSpace(claimed))
                return _displayName = claimed;

            var userId = GetUserId();

            if (userId == null)
                return _displayName = principal?.Identity?.Name;

            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId.Value)
                .Select(u => new
                {
                    u.FullName,
                    u.Email
                })
                .FirstOrDefaultAsync();

            return _displayName =
                user?.FullName
                ?? user?.Email
                ?? principal?.Identity?.Name;
        }
    }
}
