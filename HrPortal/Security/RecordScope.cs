using HrPortal.Entities;
using HrPortal.Services;
using System.Security.Claims;

namespace HrPortal.Security
{
    public class RecordScope
    {
        private readonly CurrentUserService _currentUser;

        public RecordScope(CurrentUserService currentUser)
        {
            _currentUser = currentUser;
        }

        public async Task<IQueryable<T>> VisibleToAsync<T>(
            IQueryable<T> query,
            ClaimsPrincipal user)
            where T : class, IEmployeeOwned
        {
            if (SeesEveryone<T>(user))
                return query;

            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return query.Where(_ => false);

            return query.Where(r => r.EmployeeId == employeeId.Value);
        }

        public static bool SeesEveryone<T>(ClaimsPrincipal user)
            where T : IEmployeeOwned
        {
            var isHr = user.IsInRole(Roles.HrOfficer);
            var isMd = user.IsInRole(Roles.Md);

            var type = typeof(T);

            if (type == typeof(LeaveRequest))
                return isHr || isMd;

            if (type == typeof(LeaveBalance) ||
                type == typeof(EmployeeDocument) ||
                type == typeof(TrainingAssignment))
            {
                return isHr;
            }

            return false;
        }
    }
}
