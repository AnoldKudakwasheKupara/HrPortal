using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>
    /// Raises the notifications listed in the requirements and
    /// targets them at the right people.
    ///
    /// Who holds an approval role is read from the approval
    /// configuration, so the responsible person can change
    /// without touching this code.
    /// </summary>
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }


        // ============================================================
        // SEND
        // ============================================================

        /// <summary>
        /// Queues a notification for one employee. The caller
        /// saves, so a notification is only stored if the action
        /// that caused it is stored too.
        /// </summary>
        public void Notify(
            int employeeId,
            string title,
            string message,
            string? link = null)
        {
            _context.Notifications.Add(
                new Notification
                {
                    EmployeeId = employeeId,
                    Title = title,
                    Message = message,
                    Link = link,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
        }

        /// <summary>
        /// Queues a notification for whoever currently holds an
        /// approval role, if anyone does.
        /// </summary>
        public async Task NotifyApproverAsync(
            ApprovalRole role,
            string title,
            string message,
            string? link = null)
        {
            var approverEmployeeId = await GetApproverEmployeeId(role);

            if (approverEmployeeId == null)
                return;

            Notify(
                approverEmployeeId.Value,
                title,
                message,
                link);
        }


        // ============================================================
        // LOOKUPS
        // ============================================================

        /// <summary>
        /// The employee currently configured for an approval role,
        /// or null when nobody is configured.
        /// </summary>
        public async Task<int?> GetApproverEmployeeId(ApprovalRole role)
        {
            return await _context.ApprovalConfigurations
                .AsNoTracking()
                .Where(a => a.Role == role && a.IsActive)
                .Select(a => (int?)a.EmployeeId)
                .FirstOrDefaultAsync();
        }
    }
}
