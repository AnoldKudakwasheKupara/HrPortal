using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reminders
{
    public class ReminderRunContext
    {
        private readonly NotificationService _notifications;
        private readonly UserManager<ApplicationUser> _userManager;

        private Dictionary<(string Kind, string ReferenceKey, int EmployeeId),
            ReminderLog> _sentLog = new();

        private readonly Dictionary<string, List<int>> _roleEmployeeIds =
            new();

        private readonly Dictionary<DocumentAudience, HashSet<int>>
            _audienceEmployeeIds = new();

        public ReminderRunContext(
            ApplicationDbContext db,
            NotificationService notifications,
            UserManager<ApplicationUser> userManager,
            ReminderOptions options,
            DateTime today,
            DateTime horizon)
        {
            Db = db;
            Options = options;
            Today = today;
            Horizon = horizon;

            _notifications = notifications;
            _userManager = userManager;
        }

        public ApplicationDbContext Db { get; }

        public ReminderOptions Options { get; }

        public DateTime Today { get; }

        public DateTime Horizon { get; }

        public async Task LoadSentLogAsync()
        {
            var logs = await Db.ReminderLogs.ToListAsync();

            _sentLog = logs.ToDictionary(
                r => (r.Kind, r.ReferenceKey, r.EmployeeId));
        }

        public bool ShouldSend(
            string kind,
            string referenceKey,
            int employeeId,
            DateTime today)
        {
            var key = (kind, referenceKey, employeeId);

            if (!_sentLog.TryGetValue(key, out var existing))
            {
                var added = new ReminderLog
                {
                    Kind = kind,
                    ReferenceKey = referenceKey,
                    EmployeeId = employeeId,
                    LastSentOn = today
                };

                Db.ReminderLogs.Add(added);

                _sentLog[key] = added;

                return true;
            }

            if (existing.LastSentOn.Date.AddDays(
                    Options.RepeatEveryDays) > today)
            {
                return false;
            }

            existing.LastSentOn = today;

            return true;
        }

        public void Notify(
            int employeeId,
            string title,
            string message,
            string? link = null)
        {
            _notifications.Notify(employeeId, title, message, link);
        }

        public Task NotifyApproverAsync(
            ApprovalRole role,
            string title,
            string message,
            string? link = null)
        {
            return _notifications.NotifyApproverAsync(
                role,
                title,
                message,
                link);
        }

        public Task<int?> GetApproverEmployeeIdAsync(ApprovalRole role)
        {
            return _notifications.GetApproverEmployeeId(role);
        }

        public async Task<List<int>> EmployeeIdsInRoleAsync(string role)
        {
            if (_roleEmployeeIds.TryGetValue(role, out var cached))
                return cached;

            return _roleEmployeeIds[role] =
                await LoadEmployeeIdsInRoleAsync(role);
        }

        private async Task<List<int>> LoadEmployeeIdsInRoleAsync(string role)
        {
            var users = await _userManager.GetUsersInRoleAsync(role);

            if (users.Count == 0)
                return new List<int>();

            var userIds = users.Select(u => u.Id).ToList();

            return await Db.Employees
                .AsNoTracking()
                .Where(e =>
                    e.IsActive &&
                    e.UserId != null &&
                    userIds.Contains(e.UserId.Value))
                .Select(e => e.Id)
                .ToListAsync();
        }

        public async Task<HashSet<int>> AudienceEmployeeIdsAsync(
            DocumentAudience audience)
        {
            if (_audienceEmployeeIds.TryGetValue(audience, out var cached))
                return cached;

            return _audienceEmployeeIds[audience] =
                await LoadAudienceEmployeeIdsAsync(audience);
        }

        private async Task<HashSet<int>> LoadAudienceEmployeeIdsAsync(
            DocumentAudience audience)
        {
            if (audience == DocumentAudience.Everyone)
            {
                var all = await Db.Employees
                    .AsNoTracking()
                    .Where(e => e.IsActive)
                    .Select(e => e.Id)
                    .ToListAsync();

                return all.ToHashSet();
            }

            var roles = audience == DocumentAudience.Managers
                ? new[] { Roles.Administrator, Roles.HrOfficer, Roles.Md }
                : new[] { Roles.Administrator, Roles.HrOfficer };

            var ids = new HashSet<int>();

            foreach (var role in roles)
            {
                foreach (var id in await EmployeeIdsInRoleAsync(role))
                    ids.Add(id);
            }

            return ids;
        }
    }
}
