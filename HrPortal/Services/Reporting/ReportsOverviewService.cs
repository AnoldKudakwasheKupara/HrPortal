using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.ViewModels;
using HrPortal.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HrPortal.Services.Reporting
{
    public class ReportsOverviewService
    {
        private readonly ApplicationDbContext _context;

        public ReportsOverviewService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReportsIndexViewModel> BuildAsync(
            ClaimsPrincipal user)
        {
            var isAdmin = user.IsInRole(Roles.Administrator);
            var isHr = user.IsInRole(Roles.HrOfficer);
            var isMd = user.IsInRole(Roles.Md);

            var today = DateTime.Today;
            var yearStart = new DateTime(today.Year, 1, 1);

            var model = new ReportsIndexViewModel
            {
                ShowLeave = isHr || isMd,
                ShowBalances = isHr,
                ShowTraining = isHr,
                ShowPeople = isHr || isAdmin,
                ShowDocuments = isHr,
                ShowSystem = isAdmin
            };

            if (model.ShowLeave)
            {
                model.LeaveRequests = await _context.LeaveRequests
                    .CountAsync();

                model.LeaveAwaitingDecision = await _context.LeaveRequests
                    .CountAsync(r =>
                        r.Status == LeaveRequestStatus.Submitted ||
                        r.Status == LeaveRequestStatus.HRReview ||
                        r.Status == LeaveRequestStatus.HRApproved ||
                        r.Status == LeaveRequestStatus.MDReview);

                var approvedThisYear = await _context.LeaveRequests
                    .Where(r =>
                        r.Status == LeaveRequestStatus.Approved &&
                        r.StartDate >= yearStart)
                    .ToListAsync();

                model.ApprovedDaysThisYear =
                    approvedThisYear.Sum(r => r.NumberOfDays);

                model.EmployeesOnLeaveThisYear = approvedThisYear
                    .Select(r => r.EmployeeId)
                    .Distinct()
                    .Count();
            }

            if (model.ShowBalances)
            {
                var balances = await _context.LeaveBalances
                    .Where(b => b.Year == today.Year)
                    .ToListAsync();

                model.RemainingDays = balances.Sum(b => b.RemainingDays);
                model.BalancesFullyUsed =
                    balances.Count(b => b.RemainingDays <= 0);
            }

            if (model.ShowTraining)
            {
                model.TrainingAssignments = await _context.TrainingAssignments
                    .CountAsync();

                model.TrainingOverdue = await _context.TrainingAssignments
                    .CountAsync(a =>
                        !a.IsCompleted &&
                        a.DueDate != null &&
                        a.DueDate < today);

                model.Courses = await _context.TrainingCourses
                    .CountAsync(c => c.IsActive);

                var ratings = await _context.TrainingFeedbacks
                    .Select(f => f.Rating)
                    .ToListAsync();

                model.AverageCourseRating = ratings.Count == 0
                    ? null
                    : Math.Round(ratings.Average(), 2);
            }

            if (model.ShowPeople)
            {
                model.ActiveEmployees = await _context.Employees
                    .CountAsync(e => e.IsActive);

                model.EmployeesWithoutLogin = await _context.Employees
                    .CountAsync(e => e.IsActive && e.UserId == null);
            }

            if (model.ShowDocuments)
            {
                model.Documents = await _context.EmployeeDocuments
                    .CountAsync();

                var covered = await _context.EmployeeDocuments
                    .Select(d => d.EmployeeId)
                    .Distinct()
                    .CountAsync();

                var active = await _context.Employees
                    .CountAsync(e => e.IsActive);

                model.EmployeesWithoutDocuments =
                    Math.Max(0, active - covered);
            }

            if (model.ShowSystem)
            {
                model.Vendors = await _context.Vendors
                    .CountAsync(v => v.IsActive);

                model.VendorsWithoutContact = await _context.Vendors
                    .CountAsync(v =>
                        v.IsActive &&
                        (v.Email == null || v.Email == "") &&
                        (v.PhoneNumber == null || v.PhoneNumber == ""));

                model.Assets = await _context.Assets.CountAsync();

                model.AssetsNeedingAttention = await _context.Assets
                    .CountAsync(a =>
                        a.Condition == AssetCondition.Old ||
                        a.Condition == AssetCondition.Bad);

                model.ItActivity30Days = await _context.ItActivities
                    .CountAsync(a => a.LoggedOn >= today.AddDays(-30));

                model.ItActivityOutstanding = await _context.ItActivities
                    .CountAsync(a => a.Status != ItActivityStatus.Resolved);

                model.MaintenanceOpen = await _context.AssetMaintenances
                    .CountAsync(m =>
                        m.Status == MaintenanceStatus.Scheduled);

                model.MaintenanceOverdue = await _context.AssetMaintenances
                    .CountAsync(m =>
                        m.Status == MaintenanceStatus.Scheduled &&
                        m.ScheduledFor < today);

                model.Licences = await _context.Licenses
                    .CountAsync(l => l.Status == LicenseStatus.Active);

                var licenceCutoff = today.AddDays(
                    ExpiryWindows.LicenceDays);

                model.LicencesDueSoon = await _context.Licenses
                    .CountAsync(l =>
                        l.Status == LicenseStatus.Active &&
                        l.ExpiryDate <= licenceCutoff);

                var since = today.AddDays(-30);

                model.AuditEntries30Days = await _context.AuditLogs
                    .CountAsync(a => a.Timestamp >= since);
            }

            return model;
        }
    }
}
