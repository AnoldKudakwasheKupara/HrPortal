using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrPortal.Services
{
    public class ReminderRun
    {
        public Dictionary<string, int> Sent { get; } = new();

        public int Total => Sent.Values.Sum();

        public void Add(string kind, int count)
        {
            if (count <= 0)
                return;

            Sent[kind] = Sent.TryGetValue(kind, out var existing)
                ? existing + count
                : count;
        }
    }

    public class ReminderService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ReminderOptions _options;
        private readonly ILogger<ReminderService> _logger;

        public ReminderService(
            ApplicationDbContext context,
            NotificationService notifications,
            UserManager<ApplicationUser> userManager,
            IOptionsSnapshot<ReminderOptions> options,
            ILogger<ReminderService> logger)
        {
            _context = context;
            _notifications = notifications;
            _userManager = userManager;
            _options = options.Value;
            _logger = logger;
        }

        public const string LicenceExpiry = "Licence expiry";
        public const string DocumentExpiry = "Document expiry";
        public const string ContractEnding = "Contract ending";
        public const string LeaveWaiting = "Leave awaiting a decision";
        public const string RequisitionWaiting = "Requisition awaiting a decision";
        public const string MaintenanceDue = "Maintenance due";
        public const string TrainingDue = "Training due";
        public const string CertificationExpiry = "Certification expiry";
        public const string PolicyReview = "Policy review due";
        public const string PolicyUnread = "Policy not acknowledged";

        public async Task<ReminderRun> RunAsync()
        {
            var run = new ReminderRun();

            var today = DateTime.Today;
            var horizon = today.AddDays(_options.ExpiryWindowDays);

            run.Add(
                LicenceExpiry,
                await RemindExpiringLicencesAsync(today, horizon));

            run.Add(
                DocumentExpiry,
                await RemindExpiringDocumentsAsync(today, horizon));

            run.Add(
                ContractEnding,
                await RemindEndingContractsAsync(today, horizon));

            run.Add(
                LeaveWaiting,
                await RemindStaleLeaveAsync(today));

            run.Add(
                RequisitionWaiting,
                await RemindStaleRequisitionsAsync(today));

            run.Add(
                MaintenanceDue,
                await RemindDueMaintenanceAsync(today, horizon));

            run.Add(
                TrainingDue,
                await RemindDueTrainingAsync(today, horizon));

            run.Add(
                CertificationExpiry,
                await RemindExpiringCertificationsAsync(today, horizon));

            run.Add(
                PolicyReview,
                await RemindPolicyReviewsAsync(today, horizon));

            run.Add(
                PolicyUnread,
                await RemindUnacknowledgedPoliciesAsync(today));

            await _context.SaveChangesAsync();

            if (run.Total > 0)
            {
                _logger.LogInformation(
                    "Reminder run sent {Count} reminder(s).",
                    run.Total);
            }

            return run;
        }

        private async Task<int> RemindExpiringLicencesAsync(
            DateTime today,
            DateTime horizon)
        {
            var licences = await _context.Licenses
                .AsNoTracking()
                .Where(l =>
                    l.Status == LicenseStatus.Active &&
                    l.ExpiryDate <= horizon)
                .OrderBy(l => l.ExpiryDate)
                .ToListAsync();

            if (licences.Count == 0)
                return 0;

            var recipients = await EmployeeIdsInRoleAsync(Roles.Administrator);

            var sent = 0;

            foreach (var licence in licences)
            {
                var due = Due(today, licence.ExpiryDate);

                foreach (var employeeId in recipients)
                {
                    if (!await ShouldSendAsync(
                            LicenceExpiry,
                            licence.Id.ToString(),
                            employeeId,
                            today))
                    {
                        continue;
                    }

                    _notifications.Notify(
                        employeeId,
                        $"Licence {due}",
                        $"{licence.Name} {due} " +
                        $"({licence.ExpiryDate:d MMM yyyy}).",
                        $"/License/Details/{licence.Id}");

                    sent++;
                }
            }

            return sent;
        }

        private async Task<int> RemindExpiringDocumentsAsync(
            DateTime today,
            DateTime horizon)
        {
            var documents = await _context.EmployeeDocuments
                .AsNoTracking()
                .Include(d => d.Employee)
                .Where(d =>
                    d.ExpiryDate != null &&
                    d.ExpiryDate <= horizon &&
                    d.Employee.IsActive)
                .OrderBy(d => d.ExpiryDate)
                .ToListAsync();

            if (documents.Count == 0)
                return 0;

            var recipients = await EmployeeIdsInRoleAsync(Roles.HrOfficer);

            var sent = 0;

            foreach (var document in documents)
            {
                var due = Due(today, document.ExpiryDate!.Value);

                var who =
                    $"{document.Employee.FirstName} " +
                    $"{document.Employee.LastName}";

                foreach (var employeeId in recipients)
                {
                    if (!await ShouldSendAsync(
                            DocumentExpiry,
                            document.Id.ToString(),
                            employeeId,
                            today))
                    {
                        continue;
                    }

                    _notifications.Notify(
                        employeeId,
                        $"Document {due}",
                        $"{document.OriginalFileName} on {who}'s file " +
                        $"{due} ({document.ExpiryDate:d MMM yyyy}).",
                        $"/EmployeeDocument?employeeId={document.EmployeeId}");

                    sent++;
                }
            }

            return sent;
        }

        private async Task<int> RemindEndingContractsAsync(
            DateTime today,
            DateTime horizon)
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Where(e =>
                    e.IsActive &&
                    e.ContractEndDate != null &&
                    e.ContractEndDate <= horizon)
                .OrderBy(e => e.ContractEndDate)
                .ToListAsync();

            if (employees.Count == 0)
                return 0;

            var recipients = await EmployeeIdsInRoleAsync(Roles.HrOfficer);

            var sent = 0;

            foreach (var employee in employees)
            {
                var due = Due(today, employee.ContractEndDate!.Value);

                foreach (var employeeId in recipients)
                {
                    if (!await ShouldSendAsync(
                            ContractEnding,
                            employee.Id.ToString(),
                            employeeId,
                            today))
                    {
                        continue;
                    }

                    _notifications.Notify(
                        employeeId,
                        $"Contract {due}",
                        $"{employee.FirstName} {employee.LastName}'s " +
                        $"{Humanise(employee.EmploymentType)} contract {due} " +
                        $"({employee.ContractEndDate:d MMM yyyy}).",
                        $"/Employee/Details/{employee.Id}");

                    sent++;
                }
            }

            return sent;
        }

        private async Task<int> RemindPolicyReviewsAsync(
            DateTime today,
            DateTime horizon)
        {
            var policies = await _context.Policies
                .AsNoTracking()
                .Where(p =>
                    p.Status == PolicyStatus.Active &&
                    p.ReviewDate != null &&
                    p.ReviewDate <= horizon)
                .OrderBy(p => p.ReviewDate)
                .ToListAsync();

            if (policies.Count == 0)
                return 0;

            var recipients = await EmployeeIdsInRoleAsync(Roles.HrOfficer);

            var sent = 0;

            foreach (var policy in policies)
            {
                var days = (policy.ReviewDate!.Value.Date - today).Days;

                var when = days switch
                {
                    < 0 => $"was due for review {-days} day{Plural(-days)} ago",
                    0 => "is due for review today",
                    _ => $"is due for review in {days} day{Plural(days)}"
                };

                foreach (var employeeId in recipients)
                {
                    if (!await ShouldSendAsync(
                            PolicyReview,
                            policy.Id.ToString(),
                            employeeId,
                            today))
                    {
                        continue;
                    }

                    _notifications.Notify(
                        employeeId,
                        $"Policy {(days < 0 ? "overdue for review" : "review due")}",
                        $"{policy.Title} version {policy.Version} {when} " +
                        $"({policy.ReviewDate:d MMM yyyy}).",
                        $"/Policy/Details/{policy.Id}");

                    sent++;
                }
            }

            return sent;
        }

        private async Task<int> RemindUnacknowledgedPoliciesAsync(DateTime today)
        {
            var policies = await _context.Policies
                .AsNoTracking()
                .Where(p =>
                    p.Status == PolicyStatus.Active &&
                    p.RequiresAcknowledgement &&
                    p.EffectiveDate <= today)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Version,
                    p.Audience,
                    Acknowledged = p.Acknowledgements
                        .Select(a => a.EmployeeId)
                        .ToList()
                })
                .ToListAsync();

            if (policies.Count == 0)
                return 0;

            var staff = await _context.Employees
                .AsNoTracking()
                .Where(e => e.IsActive && e.UserId != null)
                .Select(e => new { e.Id, e.UserId })
                .ToListAsync();

            var sent = 0;

            foreach (var policy in policies)
            {
                var audienceIds = await AudienceEmployeeIdsAsync(policy.Audience);

                foreach (var employee in staff)
                {
                    if (policy.Acknowledged.Contains(employee.Id))
                        continue;

                    if (!audienceIds.Contains(employee.Id))
                        continue;

                    if (!await ShouldSendAsync(
                            PolicyUnread,
                            policy.Id.ToString(),
                            employee.Id,
                            today))
                    {
                        continue;
                    }

                    _notifications.Notify(
                        employee.Id,
                        "Policy to read",
                        $"{policy.Title} version {policy.Version} is in " +
                        "force and you have not yet confirmed you have " +
                        "read it.",
                        $"/Policy/Details/{policy.Id}");

                    sent++;
                }
            }

            return sent;
        }

        private async Task<HashSet<int>> AudienceEmployeeIdsAsync(
            DocumentAudience audience)
        {
            if (audience == DocumentAudience.Everyone)
            {
                var all = await _context.Employees
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

        private async Task<int> RemindExpiringCertificationsAsync(
            DateTime today,
            DateTime horizon)
        {
            var certifications = await _context.Certifications
                .AsNoTracking()
                .Include(c => c.Employee)
                .Where(c =>
                    c.ExpiryDate != null &&
                    c.ExpiryDate <= horizon &&
                    c.Employee.IsActive)
                .OrderBy(c => c.ExpiryDate)
                .ToListAsync();

            if (certifications.Count == 0)
                return 0;

            var hr = await EmployeeIdsInRoleAsync(Roles.HrOfficer);

            var sent = 0;

            foreach (var certification in certifications)
            {
                var due = Due(today, certification.ExpiryDate!.Value);

                var who =
                    $"{certification.Employee.FirstName} " +
                    $"{certification.Employee.LastName}";

                var recipients = hr
                    .Append(certification.EmployeeId)
                    .Distinct();

                foreach (var employeeId in recipients)
                {
                    if (!await ShouldSendAsync(
                            CertificationExpiry,
                            certification.Id.ToString(),
                            employeeId,
                            today))
                    {
                        continue;
                    }

                    var mine = employeeId == certification.EmployeeId;

                    _notifications.Notify(
                        employeeId,
                        $"Certification {due}",
                        mine
                            ? $"Your {certification.Name} {due} " +
                              $"({certification.ExpiryDate:d MMM yyyy})."
                            : $"{who}'s {certification.Name} {due} " +
                              $"({certification.ExpiryDate:d MMM yyyy}).",
                        mine
                            ? "/MyProfile"
                            : $"/Certification?employeeId={certification.EmployeeId}");

                    sent++;
                }
            }

            return sent;
        }

        private async Task<int> RemindDueMaintenanceAsync(
            DateTime today,
            DateTime horizon)
        {
            var jobs = await _context.AssetMaintenances
                .AsNoTracking()
                .Include(m => m.Asset)
                .Where(m =>
                    m.Status == MaintenanceStatus.Scheduled &&
                    m.ScheduledFor <= horizon)
                .OrderBy(m => m.ScheduledFor)
                .ToListAsync();

            if (jobs.Count == 0)
                return 0;

            var recipients = await EmployeeIdsInRoleAsync(Roles.Administrator);

            var sent = 0;

            foreach (var job in jobs)
            {
                var days = (job.ScheduledFor.Date - today).Days;

                var when = days switch
                {
                    < 0 => $"was due {-days} day{Plural(-days)} ago",
                    0 => "is due today",
                    _ => $"is due in {days} day{Plural(days)}"
                };

                foreach (var employeeId in recipients)
                {
                    if (!await ShouldSendAsync(
                            MaintenanceDue,
                            job.Id.ToString(),
                            employeeId,
                            today))
                    {
                        continue;
                    }

                    _notifications.Notify(
                        employeeId,
                        $"Maintenance {(days < 0 ? "overdue" : "due")}",
                        $"{Humanise(job.Type)} on {job.Asset.DeviceModel} " +
                        $"{when} ({job.ScheduledFor:d MMM yyyy}).",
                        $"/AssetMaintenance");

                    sent++;
                }
            }

            return sent;
        }

        private async Task<int> RemindDueTrainingAsync(
            DateTime today,
            DateTime horizon)
        {
            var assignments = await _context.TrainingAssignments
                .AsNoTracking()
                .Include(a => a.TrainingCourse)
                .Where(a =>
                    !a.IsCompleted &&
                    a.DueDate != null &&
                    a.DueDate <= horizon &&
                    a.Employee.IsActive)
                .OrderBy(a => a.DueDate)
                .ToListAsync();

            var sent = 0;

            foreach (var assignment in assignments)
            {
                var days = (assignment.DueDate!.Value.Date - today).Days;

                var when = days switch
                {
                    < 0 => $"was due {-days} day{Plural(-days)} ago",
                    0 => "is due today",
                    _ => $"is due in {days} day{Plural(days)}"
                };

                if (!await ShouldSendAsync(
                        TrainingDue,
                        assignment.Id.ToString(),
                        assignment.EmployeeId,
                        today))
                {
                    continue;
                }

                _notifications.Notify(
                    assignment.EmployeeId,
                    $"Training {(days < 0 ? "overdue" : "due")}",
                    $"{assignment.TrainingCourse.Title} {when} " +
                    $"({assignment.DueDate:d MMM yyyy}).",
                    "/MyTraining");

                sent++;
            }

            return sent;
        }

        private async Task<int> RemindStaleLeaveAsync(DateTime today)
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.StaleApprovalDays);

            var pending = await _context.LeaveRequests
                .AsNoTracking()
                .Where(r =>
                    r.Status == LeaveRequestStatus.Submitted ||
                    r.Status == LeaveRequestStatus.HRReview ||
                    r.Status == LeaveRequestStatus.HRApproved ||
                    r.Status == LeaveRequestStatus.MDReview)
                .Select(r => new
                {
                    r.Id,
                    r.RequestNumber,
                    r.Status,
                    r.SubmittedAt,
                    Employee = r.Employee.FirstName + " " + r.Employee.LastName,
                    LastAction = r.History
                        .Max(h => (DateTime?)h.ActionDate)
                })
                .ToListAsync();

            var sent = 0;

            foreach (var request in pending)
            {
                var waitingSince = request.LastAction ?? request.SubmittedAt;

                if (waitingSince > cutoff)
                    continue;

                var withMd =
                    LeaveWorkflowService.IsAwaitingMd(request.Status);

                var approver = await _notifications.GetApproverEmployeeId(
                    withMd ? ApprovalRole.MD : ApprovalRole.HROfficer);

                if (approver == null)
                    continue;

                if (!await ShouldSendAsync(
                        LeaveWaiting,
                        request.Id.ToString(),
                        approver.Value,
                        today))
                {
                    continue;
                }

                var days = (int)(DateTime.UtcNow - waitingSince).TotalDays;

                _notifications.Notify(
                    approver.Value,
                    $"Leave waiting {days} day{Plural(days)}",
                    $"{request.RequestNumber} from {request.Employee} has " +
                    $"been waiting {days} day{Plural(days)} for your " +
                    "decision.",
                    withMd
                        ? "/LeaveApproval/FinalApproval"
                        : "/LeaveApproval");

                sent++;
            }

            return sent;
        }

        private async Task<int> RemindStaleRequisitionsAsync(DateTime today)
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.StaleApprovalDays);

            var pending = await _context.Requisitions
                .AsNoTracking()
                .Where(r =>
                    r.Status == RequisitionStatus.Submitted ||
                    r.Status == RequisitionStatus.ReviewApproved)
                .Select(r => new
                {
                    r.Id,
                    r.RequisitionNumber,
                    r.Status,
                    r.SubmittedAt,
                    r.EstimatedTotal,
                    Employee = r.Employee.FirstName + " " + r.Employee.LastName,
                    LastAction = r.History
                        .Max(h => (DateTime?)h.ActionDate)
                })
                .ToListAsync();

            var sent = 0;

            foreach (var requisition in pending)
            {
                var waitingSince =
                    requisition.LastAction
                    ?? requisition.SubmittedAt
                    ?? DateTime.UtcNow;

                if (waitingSince > cutoff)
                    continue;

                var withMd = requisition.Status ==
                    RequisitionStatus.ReviewApproved;

                var approver = await _notifications.GetApproverEmployeeId(
                    withMd
                        ? ApprovalRole.MD
                        : ApprovalRole.RequisitionReviewer);

                if (approver == null)
                    continue;

                if (!await ShouldSendAsync(
                        RequisitionWaiting,
                        requisition.Id.ToString(),
                        approver.Value,
                        today))
                {
                    continue;
                }

                var days = (int)(DateTime.UtcNow - waitingSince).TotalDays;

                _notifications.Notify(
                    approver.Value,
                    $"Requisition waiting {days} day{Plural(days)}",
                    $"{requisition.RequisitionNumber} from " +
                    $"{requisition.Employee} " +
                    $"({requisition.EstimatedTotal:N2}) has been waiting " +
                    $"{days} day{Plural(days)} for your decision.",
                    withMd
                        ? "/RequisitionApproval/FinalApproval"
                        : "/RequisitionApproval");

                sent++;
            }

            return sent;
        }

        private static string Due(DateTime today, DateTime date)
        {
            var days = (date.Date - today).Days;

            return days switch
            {
                < 0 => $"expired {-days} day{Plural(-days)} ago",
                0 => "expires today",
                _ => $"expires in {days} day{Plural(days)}"
            };
        }

        private static string Plural(int count) => count == 1 ? "" : "s";

        private static string Humanise(Enum value)
        {
            var name = value.ToString();

            var spaced = string.Concat(
                name.Select((c, i) =>
                    i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));

            return spaced.ToLowerInvariant();
        }

        private async Task<bool> ShouldSendAsync(
            string kind,
            string referenceKey,
            int employeeId,
            DateTime today)
        {
            var existing = await _context.ReminderLogs
                .FirstOrDefaultAsync(r =>
                    r.Kind == kind &&
                    r.ReferenceKey == referenceKey &&
                    r.EmployeeId == employeeId);

            if (existing == null)
            {
                _context.ReminderLogs.Add(new ReminderLog
                {
                    Kind = kind,
                    ReferenceKey = referenceKey,
                    EmployeeId = employeeId,
                    LastSentOn = today
                });

                return true;
            }

            if (existing.LastSentOn.Date.AddDays(
                    _options.RepeatEveryDays) > today)
            {
                return false;
            }

            existing.LastSentOn = today;

            return true;
        }

        private async Task<List<int>> EmployeeIdsInRoleAsync(string role)
        {
            var users = await _userManager.GetUsersInRoleAsync(role);

            if (users.Count == 0)
                return new List<int>();

            var userIds = users.Select(u => u.Id).ToList();

            return await _context.Employees
                .AsNoTracking()
                .Where(e =>
                    e.IsActive &&
                    e.UserId != null &&
                    userIds.Contains(e.UserId.Value))
                .Select(e => e.Id)
                .ToListAsync();
        }
    }
}
