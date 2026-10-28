using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>
    /// How much is sitting with the signed-in person as somebody's
    /// supervisor.
    ///
    /// The sidebar asks this on every page, so the answer is worked
    /// out once per request and held -- the same arrangement the
    /// notification badge uses.
    /// </summary>
    public class SupervisorQueueService
    {
        private readonly ApplicationDbContext _context;
        private readonly CurrentUserService _currentUser;

        private int? _count;

        public SupervisorQueueService(
            ApplicationDbContext context,
            CurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<int> PendingCountAsync()
        {
            if (_count != null)
                return _count.Value;

            var me = await _currentUser.GetEmployeeIdAsync();

            if (me == null)
            {
                _count = 0;
                return 0;
            }

            _count = await _context.LeaveRequests
                .AsNoTracking()
                .CountAsync(r =>
                    r.Status == LeaveRequestStatus.SupervisorReview &&
                    r.Employee.SupervisorId == me.Value);

            return _count.Value;
        }

        /// <summary>
        /// Whether to offer the queue at all. Somebody who supervises
        /// nobody should not see an empty page in their sidebar.
        /// </summary>
        public async Task<bool> SupervisesAnyoneAsync()
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            if (me == null)
                return false;

            return await _context.Employees
                .AsNoTracking()
                .AnyAsync(e =>
                    e.SupervisorId == me.Value && e.IsActive);
        }
    }
}
