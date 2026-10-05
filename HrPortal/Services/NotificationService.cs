using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

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
