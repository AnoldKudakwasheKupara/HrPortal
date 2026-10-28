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

        private readonly List<PendingEmail> _pending = new();

        public NotificationService(
            ApplicationDbContext context,
            IServiceScopeFactory scopeFactory,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _scopeFactory = scopeFactory;
            _logger = logger;

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

        /// <summary>
        /// Tells everybody who could act on this: whoever holds the
        /// matching sign-in role, and the person named in the
        /// approval settings if somebody is.
        ///
        /// It used to tell only the latter, so a company that had
        /// granted the HR Officer role but never filled in the
        /// approval setting got no notification at all -- a request
        /// would arrive in the queue and nobody would be told it was
        /// there.
        /// </summary>
        public async Task NotifyApproverAsync(
            ApprovalRole role,
            string title,
            string message,
            string? link = null)
        {
            foreach (var employeeId in await ApproverEmployeeIdsAsync(role))
                Notify(employeeId, title, message, link);
        }

        /// <summary>
        /// Everybody who can act in this role, by sign-in role or by
        /// the approval setting, without repeating anybody who is
        /// both.
        /// </summary>
        public async Task<List<int>> ApproverEmployeeIdsAsync(
            ApprovalRole role)
        {
            var signInRole = role switch
            {
                ApprovalRole.HROfficer => Roles.HrOfficer,
                ApprovalRole.MD => Roles.Md,
                _ => null
            };

            var holders = signInRole == null
                ? new List<int>()
                : await _context.Employees
                    .AsNoTracking()
                    .Where(e =>
                        e.IsActive &&
                        e.UserId != null &&
                        _context.UserRoles.Any(ur =>
                            ur.UserId == e.UserId.Value &&
                            _context.Roles.Any(r =>
                                r.Id == ur.RoleId &&
                                r.Name == signInRole)))
                    .Select(e => e.Id)
                    .ToListAsync();

            var configured = await GetApproverEmployeeId(role);

            if (configured != null && !holders.Contains(configured.Value))
                holders.Add(configured.Value);

            return holders;
        }

        /// <summary>
        /// The one person named in the approval settings, where
        /// somebody is. Still used to tell whose own request should
        /// skip a stage rather than be reviewed by themselves.
        /// </summary>
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
                        item.Title,
                        item.Message,
                        item.Link));
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
            string? Link);
    }
}
