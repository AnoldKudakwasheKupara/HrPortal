using HrPortal.Entities;
using HrPortal.Services;
using System.Security.Claims;

namespace HrPortal.Security
{
    /// <summary>
    /// The list-side counterpart to
    /// <see cref="EmployeeOwnedHandler"/>.
    ///
    /// A resource handler decides one loaded record at a time,
    /// which is no use for a list: you cannot authorize a
    /// thousand rows individually, and you certainly cannot load
    /// them all to find out. So the same rule is expressed once
    /// more here as a query filter, and the two are kept in this
    /// folder side by side so a change to one is an obvious
    /// prompt to check the other.
    ///
    /// Call <see cref="VisibleToAsync{T}"/> on any query of a
    /// record type that implements <see cref="IEmployeeOwned"/>.
    /// It narrows the query to the caller's own records unless
    /// their role lets them see everyone's.
    /// </summary>
    public class RecordScope
    {
        private readonly CurrentUserService _currentUser;

        public RecordScope(CurrentUserService currentUser)
        {
            _currentUser = currentUser;
        }


        /// <summary>
        /// Narrows a query to what the caller may see.
        ///
        /// A caller with no employee record and no elevated role
        /// sees nothing, which is the safe answer: it is better
        /// for an unlinked account to show an empty list than
        /// everyone's records.
        /// </summary>
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


        /// <summary>
        /// Whether this role sees every employee's records of
        /// this type. Mirrors the view rules in
        /// <see cref="EmployeeOwnedHandler"/>.
        /// </summary>
        public static bool SeesEveryone<T>(ClaimsPrincipal user)
            where T : IEmployeeOwned
        {
            if (user.IsInRole(Roles.Administrator))
                return true;

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

            // Notifications stay personal.
            return false;
        }
    }
}
