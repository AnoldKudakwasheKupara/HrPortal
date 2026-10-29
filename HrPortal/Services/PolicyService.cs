using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HrPortal.Services
{
    public class PolicyService
    {
        public const string ReviewDateField = "ReviewDate";

        private readonly ApplicationDbContext _context;
        private readonly CurrentUserService _currentUser;
        private readonly AuditService _audit;
        private readonly NotificationService _notifications;

        public PolicyService(
            ApplicationDbContext context,
            CurrentUserService currentUser,
            AuditService audit,
            NotificationService notifications)
        {
            _context = context;
            _currentUser = currentUser;
            _audit = audit;
            _notifications = notifications;
        }

        public static bool CanManage(ClaimsPrincipal user) =>
            user.IsInRole(Roles.Administrator) ||
            user.IsInRole(Roles.HrOfficer);

        public static string NextVersion(string version)
        {
            var parts = version.Split('.');

            if (int.TryParse(parts[0], out var major))
            {
                return parts.Length > 1
                    ? $"{major + 1}.0"
                    : (major + 1).ToString();
            }

            return version;
        }

        public static string? ValidateDates(
            DateTime effectiveDate,
            DateTime? reviewDate)
        {
            if (reviewDate.HasValue &&
                reviewDate.Value.Date <= effectiveDate.Date)
            {
                return "The review date must be after the date it takes " +
                       "effect.";
            }

            return null;
        }

        public async Task<Policy?> ReadableAsync(
            int? id,
            ClaimsPrincipal user)
        {
            if (id == null)
                return null;

            var policy = await _context.Policies
                .AsNoTracking()
                .Include(p => p.Supersedes)
                .Include(p => p.Acknowledgements)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (policy == null)
                return null;

            if (!AudienceRules.CanSee(user, policy.Audience))
                return null;

            if (policy.Status != PolicyStatus.Active && !CanManage(user))
                return null;

            return policy;
        }

        public async Task<ServiceResult> PublishAsync(int id)
        {
            var policy = await _context.Policies.FindAsync(id);

            if (policy == null)
                return ServiceResult.Missing();

            if (policy.Status == PolicyStatus.Active)
                return ServiceResult.Refused("That policy is already in force.");

            policy.Status = PolicyStatus.Active;
            policy.PublishedAt = DateTime.UtcNow;

            var replaced = string.Empty;

            if (policy.SupersedesPolicyId.HasValue)
            {
                var previous = await _context.Policies
                    .FindAsync(policy.SupersedesPolicyId.Value);

                if (previous != null &&
                    previous.Status == PolicyStatus.Active)
                {
                    previous.Status = PolicyStatus.Superseded;

                    replaced =
                        $" It replaces version {previous.Version}, which " +
                        "is now marked as replaced.";
                }
            }

            await TellReadersAsync(
                policy,
                replacing: policy.SupersedesPolicyId.HasValue);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(Policy),
                policy.Id.ToString(),
                $"{policy.Title} version {policy.Version} published." +
                replaced);

            return ServiceResult.Ok(
                $"{policy.Title} version {policy.Version} is now in " +
                "force." + replaced);
        }

        public async Task<ServiceResult> WithdrawAsync(int id)
        {
            var policy = await _context.Policies.FindAsync(id);

            if (policy == null)
                return ServiceResult.Missing();

            policy.Status = PolicyStatus.Withdrawn;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(Policy),
                policy.Id.ToString(),
                $"{policy.Title} version {policy.Version} withdrawn.");

            return ServiceResult.Ok("Policy withdrawn.");
        }

        public async Task<ServiceResult> AcknowledgeAsync(
            int id,
            ClaimsPrincipal user)
        {
            var policy = await ReadableAsync(id, user);

            if (policy == null)
                return ServiceResult.Missing();

            if (policy.Status != PolicyStatus.Active)
            {
                return ServiceResult.Refused(
                    "That policy is not in force, so there is nothing " +
                    "to acknowledge.");
            }

            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
            {
                return ServiceResult.Refused(
                    "Your sign-in is not linked to an employee record, " +
                    "so an acknowledgement cannot be recorded against it.");
            }

            var already = await _context.PolicyAcknowledgements
                .AnyAsync(a =>
                    a.PolicyId == id &&
                    a.EmployeeId == employeeId.Value);

            if (already)
                return ServiceResult.Ok("You had already acknowledged this.");

            _context.PolicyAcknowledgements.Add(new PolicyAcknowledgement
            {
                PolicyId = id,
                EmployeeId = employeeId.Value,
                AcknowledgedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(PolicyAcknowledgement),
                id.ToString(),
                $"{policy.Title} version {policy.Version} acknowledged.");

            return ServiceResult.Ok(
                $"Thank you. Your reading of {policy.Title} " +
                $"version {policy.Version} has been recorded.");
        }
        /// <summary>
        /// Tells everybody the policy is meant for that it is now in
        /// force: active staff with a sign-in, narrowed to the roles
        /// its audience allows. Somebody with no sign-in could not
        /// open it, so there is nothing to tell them.
        /// </summary>
        private async Task TellReadersAsync(Policy policy, bool replacing)
        {
            var roles = AudienceRules.RolesFor(policy.Audience);

            var readers = _context.Employees
                .AsNoTracking()
                .Where(e => e.IsActive && e.UserId != null);

            if (roles != null)
            {
                readers = readers.Where(e =>
                    _context.UserRoles.Any(ur =>
                        ur.UserId == e.UserId!.Value &&
                        _context.Roles.Any(r =>
                            r.Id == ur.RoleId &&
                            roles.Contains(r.Name!))));
            }

            var employeeIds = await readers
                .Select(e => e.Id)
                .ToListAsync();

            var title = replacing
                ? $"{policy.Title} has been updated"
                : $"New policy: {policy.Title}";

            var message =
                (replacing
                    ? $"Version {policy.Version} of {policy.Title} is now " +
                      "in force and replaces the earlier version."
                    : $"{policy.Title} (version {policy.Version}) is now " +
                      "in force.") +
                $" It takes effect on {policy.EffectiveDate:d MMM yyyy}." +
                (policy.RequiresAcknowledgement
                    ? " Please read it and confirm in the portal that " +
                      "you have."
                    : string.Empty);

            foreach (var employeeId in employeeIds)
            {
                _notifications.Notify(
                    employeeId,
                    title,
                    message,
                    $"/Policy/Details/{policy.Id}",
                    replacing
                        ? EmailKind.PolicyUpdated
                        : EmailKind.PolicyPublished);
            }
        }
    }
}
