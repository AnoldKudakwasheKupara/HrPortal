using HrPortal.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    public class TimesheetAccessService
    {
        private readonly ApplicationDbContext _context;
        private readonly CurrentUserService _currentUser;

        private TimesheetAccess? _cached;

        public TimesheetAccessService(
            ApplicationDbContext context,
            CurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<TimesheetAccess> GetAsync()
        {
            if (_cached != null)
                return _cached;

            var userId = _currentUser.GetUserId();

            if (userId == null)
                return _cached = TimesheetAccess.None;

            var employee = await _context.Employees
                .AsNoTracking()
                .Where(e => e.UserId == userId.Value)
                .Select(e => new
                {
                    e.Id,
                    e.IsActive,
                    e.Department.TracksTimesheets,
                    DepartmentName = e.Department.Name
                })
                .FirstOrDefaultAsync();

            if (employee == null)
                return _cached = TimesheetAccess.None;

            return _cached = new TimesheetAccess(
                employee.Id,
                employee.IsActive && employee.TracksTimesheets,
                employee.DepartmentName);
        }
    }

    public record TimesheetAccess(
        int? EmployeeId,
        bool Enabled,
        string? DepartmentName)
    {
        public static readonly TimesheetAccess None =
            new(null, false, null);
    }
}
