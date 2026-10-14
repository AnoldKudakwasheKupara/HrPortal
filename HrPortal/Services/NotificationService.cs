using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>
    /// Writes the in-app notifications, and sends the matching emails.
    ///
    /// An email is only sent once the notification it copies has been
    /// committed, so nobody is told about an approval that was rolled
    /// back. Sending happens on a background task in its own scope, so
    /// a slow or unreachable mail server does not hold up the request
    /// that caused it.
    /// </summary>
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationService> _logger;

        private readonly List<PendingEmail> _pending = new();

        public NotificationService(
            ApplicationDbContext context,
            IServiceScopeFactory scopeFactory,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _scopeFactory = scopeFactory;
            _logger = logger;

            // Flush when the work that produced the notification is
            // safely saved, not before.
            _context.SavedChanges += OnSavedChanges;
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

            _pending.Add(new PendingEmail(employeeId, title, message, link));
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

        private void OnSavedChanges(object? sender, SavedChangesEventArgs e)
        {
            if (_pending.Count == 0)
                return;

            var batch = _pending.ToList();
            _pending.Clear();

            // Deliberately not awaited. The notification is already
            // committed and the email is a copy of it, so the caller
            // should not wait on a mail server, nor fail if it is down.
            _ = DeliverAsync(batch);
        }

        private async Task DeliverAsync(List<PendingEmail> batch)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var email = scope.ServiceProvider
                    .GetRequiredService<EmailService>();

                if (!email.IsEnabled)
                    return;

                var context = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                var employeeIds = batch
                    .Select(p => p.EmployeeId)
                    .Distinct()
                    .ToList();

                var recipients = await context.Employees
                    .AsNoTracking()
                    .Where(emp => employeeIds.Contains(emp.Id))
                    .Select(emp => new
                    {
                        emp.Id,
                        emp.Email,
                        Name = emp.FirstName + " " + emp.LastName
                    })
                    .ToDictionaryAsync(emp => emp.Id);

                foreach (var item in batch)
                {
                    if (!recipients.TryGetValue(item.EmployeeId, out var to))
                        continue;

                    await email.SendAsync(
                        to.Email,
                        to.Name,
                        item.Title,
                        item.Message,
                        item.Link);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Could not deliver {Count} notification email(s).",
                    batch.Count);
            }
        }

        private sealed record PendingEmail(
            int EmployeeId,
            string Title,
            string Message,
            string? Link);
    }
}
