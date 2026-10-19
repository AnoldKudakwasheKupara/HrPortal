using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.Services.Reporting;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.AnyManager)]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly LeaveReportService _leaveReports;
        private readonly TrainingReportService _trainingReports;
        private readonly PeopleReportService _peopleReports;
        private readonly AssetReportService _assetReports;
        private readonly OperationsReportService _operationsReports;

        public ReportsController(
            ApplicationDbContext context,
            LeaveReportService leaveReports,
            TrainingReportService trainingReports,
            PeopleReportService peopleReports,
            AssetReportService assetReports,
            OperationsReportService operationsReports)
        {
            _context = context;
            _leaveReports = leaveReports;
            _trainingReports = trainingReports;
            _peopleReports = peopleReports;
            _assetReports = assetReports;
            _operationsReports = operationsReports;
        }

        public async Task<IActionResult> Index()
        {
            var isAdmin = User.IsInRole(Roles.Administrator);
            var isHr = User.IsInRole(Roles.HrOfficer);
            var isMd = User.IsInRole(Roles.Md);

            var today = DateTime.Today;
            var yearStart = new DateTime(today.Year, 1, 1);

            var model = new ReportsIndexViewModel
            {
                ShowLeave = isHr || isMd,
                ShowBalances = isHr,
                ShowTraining = isHr,
                ShowPeople = isHr || isAdmin,
                ShowDocuments = isHr,
                ShowSystem = isAdmin,
                ShowProcurement = isAdmin || isMd
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

            if (model.ShowProcurement)
            {
                model.RequisitionsAwaiting = await _context.Requisitions
                    .CountAsync(r =>
                        r.Status == RequisitionStatus.Submitted ||
                        r.Status == RequisitionStatus.ReviewApproved);

                model.RequisitionsApprovedValue = await _context.Requisitions
                    .Where(r =>
                        r.Status == RequisitionStatus.Approved &&
                        r.CompletedAt != null &&
                        r.CompletedAt >= yearStart)
                    .SumAsync(r => (decimal?)r.EstimatedTotal) ?? 0m;
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
                    LicenseController.ExpiringWindowDays);

                model.LicencesDueSoon = await _context.Licenses
                    .CountAsync(l =>
                        l.Status == LicenseStatus.Active &&
                        l.ExpiryDate <= licenceCutoff);

                var since = today.AddDays(-30);

                model.AuditEntries30Days = await _context.AuditLogs
                    .CountAsync(a => a.Timestamp >= since);
            }

            return View(model);
        }

        [Authorize(Roles = Roles.HrOrMd)]
        public async Task<IActionResult> LeaveRequests(
            ReportFilter filter,
            string? export)
        {
            var payload = await _leaveReports.LeaveRequestsAsync(
                filter,
                export == "csv");

            if (payload.IsCsv)
                return Csv(payload);

            await LoadLookups();

            return View(payload.Model);
        }

        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> LeaveBalances(
            ReportFilter filter,
            string? export)
        {
            var payload = await _leaveReports.LeaveBalancesAsync(
                filter,
                export == "csv");

            if (payload.IsCsv)
                return Csv(payload);

            await LoadLookups();

            return View(payload.Model);
        }

        [Authorize(Roles = Roles.HrOrMd)]
        public async Task<IActionResult> Absence(
            ReportFilter filter,
            string? export)
        {
            var payload = await _leaveReports.AbsenceAsync(
                filter,
                export == "csv");

            if (payload.IsCsv)
                return Csv(payload);

            await LoadLookups();

            return View(payload.Model);
        }

        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> Training(
            ReportFilter filter,
            string? export)
        {
            var payload = await _trainingReports.TrainingAsync(
                filter,
                export == "csv");

            if (payload.IsCsv)
                return Csv(payload);

            await LoadLookups();

            return View(payload.Model);
        }

        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> TrainingCourses(
            ReportFilter filter,
            string? export)
        {
            var payload = await _trainingReports.TrainingCoursesAsync(
                filter,
                export == "csv");

            if (payload.IsCsv)
                return Csv(payload);

            await LoadLookups();

            return View(payload.Model);
        }

        [Authorize(Roles = Roles.AdminOrHr)]
        public async Task<IActionResult> Employees(
            ReportFilter filter,
            string? export)
        {
            var payload = await _peopleReports.EmployeesAsync(
                filter,
                export == "csv");

            if (payload.IsCsv)
                return Csv(payload);

            await LoadLookups();

            return View(payload.Model);
        }

        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> Documents(
            ReportFilter filter,
            string? export)
        {
            var payload = await _peopleReports.DocumentsAsync(
                filter,
                export == "csv");

            if (payload.IsCsv)
                return Csv(payload);

            await LoadLookups();

            return View(payload.Model);
        }

        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> Assets(
            ReportFilter filter,
            string? export)
        {
            var payload = await _assetReports.AssetsAsync(
                filter,
                export == "csv");

            if (payload.IsCsv)
                return Csv(payload);

            await LoadLookups();

            return View(payload.Model);
        }

        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> Vendors(
            ReportFilter filter,
            string? export)
        {
            var payload = await _assetReports.VendorsAsync(
                filter,
                export == "csv");

            if (payload.IsCsv)
                return Csv(payload);

            await LoadLookups();

            return View(payload.Model);
        }

        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> ItActivity(
            ReportFilter filter,
            string? export)
        {
            var payload = await _operationsReports.ItActivityAsync(
                filter,
                export == "csv");

            if (payload.IsCsv)
                return Csv(payload);

            await LoadLookups();

            return View(payload.Model);
        }

        [Authorize(Roles = Roles.AdminOrMd)]
        public async Task<IActionResult> Requisitions(
            ReportFilter filter,
            string? export)
        {
            var payload = await _operationsReports.RequisitionsAsync(
                filter,
                export == "csv");

            if (payload.IsCsv)
                return Csv(payload);

            await LoadLookups();

            return View(payload.Model);
        }

        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> Maintenance(
            ReportFilter filter,
            string? export)
        {
            var payload = await _assetReports.MaintenanceAsync(
                filter,
                export == "csv");

            if (payload.IsCsv)
                return Csv(payload);

            await LoadLookups();

            return View(payload.Model);
        }

        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> Licences(
            ReportFilter filter,
            string? export)
        {
            var payload = await _assetReports.LicencesAsync(
                filter,
                export == "csv");

            if (payload.IsCsv)
                return Csv(payload);

            await LoadLookups();

            return View(payload.Model);
        }

        private static string Slice(ReportFilter filter, DateTime soon) =>
            filter.Show switch
            {
                "expired" => "Live licences already past their expiry date",
                "expiring" =>
                    "Live licences expiring on or before " +
                    soon.ToString("d MMM yyyy"),
                "autorenew" => "Licences set to renew automatically",
                _ => filter.IncludeInactive
                    ? "All licences, cancelled ones included"
                    : "Live licences"
            };

        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> AuditTrail(
            ReportFilter filter,
            string? export)
        {
            var payload = await _peopleReports.AuditTrailAsync(
                filter,
                export == "csv");

            if (payload.IsCsv)
                return Csv(payload);

            ViewBag.Actions = await _peopleReports.AuditActionsAsync();

            await LoadLookups();

            return View(payload.Model);
        }

        private FileContentResult Csv(ReportPayload payload) =>
            Csv(payload.CsvName, payload.CsvHeaders, payload.CsvRows);

        private FileContentResult Csv(
            string name,
            IReadOnlyList<string> headers,
            IEnumerable<IReadOnlyList<string?>> rows)
        {
            return File(
                CsvExportService.Build(headers, rows),
                "text/csv",
                CsvExportService.FileName(name));
        }

        private static ReportTrend MonthlyTrend(
            string title,
            IEnumerable<(DateTime When, decimal Value)> values,
            string unit = "",
            string format = "N0")
        {
            var trend = new ReportTrend(title, unit, format);

            var start = new DateTime(
                DateTime.Today.Year,
                DateTime.Today.Month,
                1).AddMonths(-11);

            var buckets = values
                .Where(v => v.When.Date >= start)
                .GroupBy(v => new { v.When.Year, v.When.Month })
                .ToDictionary(
                    g => (g.Key.Year, g.Key.Month),
                    g => g.Sum(v => v.Value));

            for (var i = 0; i < 12; i++)
            {
                var month = start.AddMonths(i);

                buckets.TryGetValue(
                    (month.Year, month.Month),
                    out var value);

                trend.Add(month.ToString("MMM"), value);
            }

            return trend;
        }

        private static string Period(ReportFilter filter)
        {
            if (filter.From.HasValue && filter.To.HasValue)
            {
                return $"{filter.From.Value:d MMM yyyy} to " +
                       $"{filter.To.Value:d MMM yyyy}";
            }

            if (filter.From.HasValue)
                return $"From {filter.From.Value:d MMM yyyy}";

            if (filter.To.HasValue)
                return $"Up to {filter.To.Value:d MMM yyyy}";

            return "All dates";
        }

        private static string D(DateTime? value) =>
            value?.ToString("yyyy-MM-dd") ?? string.Empty;

        private static string N(decimal value) =>
            value.ToString("0.##");

        private async Task LoadLookups()
        {
            ViewBag.Departments = await _context.Departments
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .Select(d => new SelectListItem(d.Name, d.Id.ToString()))
                .ToListAsync();

            ViewBag.Employees = await _context.Employees
                .OrderBy(e => e.FirstName)
                .Select(e => new SelectListItem(
                    e.FirstName + " " + e.LastName,
                    e.Id.ToString()))
                .ToListAsync();

            ViewBag.LeaveTypes = await _context.LeaveTypes
                .OrderBy(l => l.Name)
                .Select(l => new SelectListItem(l.Name, l.Id.ToString()))
                .ToListAsync();

            ViewBag.Courses = await _context.TrainingCourses
                .OrderBy(c => c.Title)
                .Select(c => new SelectListItem(c.Title, c.Id.ToString()))
                .ToListAsync();

            ViewBag.Categories = await _context.DocumentCategories
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
                .ToListAsync();

            ViewBag.Vendors = await _context.Vendors
                .OrderBy(v => v.Name)
                .Select(v => new SelectListItem(v.Name, v.Id.ToString()))
                .ToListAsync();

            ViewBag.VendorCategories = await _context.Vendors
                .Where(v => v.Category != null && v.Category != "")
                .Select(v => v.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.Companies = await _context.Assets
                .Where(a => a.Company != null && a.Company != "")
                .Select(a => a.Company!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }
    }
}
