using HrPortal.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>
    /// The unread count shown on the bell in the top bar. The shell
    /// renders on every page, so the answer is worked out once per
    /// request and kept.
    /// </summary>
    public class NotificationBadgeService
    {
        private readonly ApplicationDbContext _context;
        private readonly CurrentUserService _currentUser;

        private int? _count;

        public NotificationBadgeService(
            ApplicationDbContext context,
            CurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<int> UnreadCountAsync()
        {
            if (_count != null)
                return _count.Value;

            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
            {
                _count = 0;
                return 0;
            }

            _count = await _context.Notifications
                .AsNoTracking()
                .CountAsync(n =>
                    n.EmployeeId == employeeId.Value &&
                    !n.IsRead);

            return _count.Value;
        }
    }
}
