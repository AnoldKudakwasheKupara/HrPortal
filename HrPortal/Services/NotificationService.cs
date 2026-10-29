using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationService> _logger;
        private readonly ApproverDirectory _approvers;

        private readonly List<PendingEmail> _pending = new();

        public NotificationService(
            ApplicationDbContext context,
            IServiceScopeFactory scopeFactory,
            ILogger<NotificationService> logger,
            ApproverDirectory approvers)
        {
            _context = context;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _approvers = approvers;

            _context.SavedChanges += OnSavedChanges;
        }

        /// <summary>
        /// Puts a notification in the person's bell, and emails it
        /// too when it is given a kind. Without one it stays in the
        /// bell: most of what the portal says is progress or a
        /// repeated nudge, and an inbox that gets all of it is one
        /// that stops being read.
        /// </summary>
        public void Notify(
            int employeeId,
            string title,
            string message,
            string? link = null,
            EmailKind? email = null)
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

            if (email is EmailKind kind)
                _pending.Add(new PendingEmail(employeeId, title, message, link, kind));
        }

        /// <summary>
        /// Tells everybody who holds the role, not one person named
        /// on a settings page. A company with two HR officers has
        /// two people who may act, and a request either of them can
        /// decide should reach both.
        /// </summary>
        public async Task NotifyApproverAsync(
            ApprovalRole role,
            string title,
            string message,
            string? link = null,
            EmailKind? email = null)
        {
            var approvers = await _approvers.EmployeeIdsAsync(role);

            foreach (var employeeId in approvers)
                Notify(employeeId, title, message, link, email);
        }

        private void OnSavedChanges(object? sender, SavedChangesEventArgs e)
        {
            if (_pending.Count == 0)
                return;

            var batch = _pending.ToList();
            _pending.Clear();

            _ = DeliverAsync(batch);
        }

        private async Task DeliverAsync(List<PendingEmail> batch)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var channels = scope.ServiceProvider
                    .GetServices<INotificationChannel>()
                    .Where(c => c.IsEnabled)
                    .ToList();

                if (channels.Count == 0)
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
                        emp.FirstName,
                        Name = emp.FirstName + " " + emp.LastName
                    })
                    .ToDictionaryAsync(emp => emp.Id);

                var outbound = new List<OutboundNotification>();

                foreach (var item in batch)
                {
                    if (!recipients.TryGetValue(item.EmployeeId, out var to))
                        continue;

                    outbound.Add(new OutboundNotification(
                        item.EmployeeId,
                        to.Email,
                        to.Name,
                        to.FirstName,
                        item.Title,
                        item.Message,
                        item.Link,
                        item.Kind));
                }

                if (outbound.Count == 0)
                    return;

                foreach (var channel in channels)
                {
                    try
                    {
                        var report = await channel.DeliverAsync(outbound);

                        if (!report.AllDelivered)
                        {
                            _logger.LogWarning(
                                "The {Channel} channel delivered " +
                                "{Delivered} of {Total} notification(s); " +
                                "{Failed} failed.",
                                channel.Name,
                                report.Delivered,
                                outbound.Count,
                                report.Failed);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "The {Channel} channel could not deliver " +
                            "{Count} notification(s).",
                            channel.Name,
                            outbound.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Could not deliver {Count} notification(s).",
                    batch.Count);
            }
        }

        private sealed record PendingEmail(
            int EmployeeId,
            string Title,
            string Message,
            string? Link,
            EmailKind Kind);
    }
}
