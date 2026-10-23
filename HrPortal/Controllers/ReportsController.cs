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

        private readonly ReportsOverviewService _overview;

        public ReportsController(
            ApplicationDbContext context,
            ReportsOverviewService overview,
            LeaveReportService leaveReports,
            TrainingReportService trainingReports,
            PeopleReportService peopleReports,
            AssetReportService assetReports,
            OperationsReportService operationsReports)
        {
            _context = context;
            _overview = overview;
            _leaveReports = leaveReports;
            _trainingReports = trainingReports;
            _peopleReports = peopleReports;
            _assetReports = assetReports;
            _operationsReports = operationsReports;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _overview.BuildAsync(User));
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
