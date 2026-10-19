using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.AdminOnly)]
    public class TimesheetController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TimesheetController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            int? employeeId,
            WorkCategory? category,
            DateTime? from,
            DateTime? to)
        {
            var (start, end) = Range(from, to);

            var entries = await Filtered(employeeId, category, start, end)
                .OrderByDescending(t => t.WorkDate)
                .ThenBy(t => t.Employee.FirstName)
                .ThenBy(t => t.StartTime)
                .ThenBy(t => t.Id)
                .ToListAsync();

            var model = new TimesheetRegisterViewModel
            {
                From = start,
                To = end,
                EmployeeId = employeeId,
                Category = category,
                Entries = entries,
                TotalMinutes = entries.Sum(t => t.DurationMinutes),
                People = entries.Select(t => t.EmployeeId).Distinct().Count(),
                WorkingDays = CountWorkingDays(start, end),

                ByPerson = entries
                    .GroupBy(t => t.EmployeeId)
                    .Select(g => new TimesheetPersonSummary
                    {
                        EmployeeId = g.Key,
                        Name = g.First().Employee.FirstName + " " +
                               g.First().Employee.LastName,
                        Department = g.First().Employee.Department.Name,
                        Entries = g.Count(),
                        Minutes = g.Sum(t => t.DurationMinutes),
                        DaysLogged = g.Select(t => t.WorkDate.Date)
                            .Distinct()
                            .Count()
                    })
                    .OrderByDescending(p => p.Minutes)
                    .ThenBy(p => p.Name)
                    .ToList(),

                ByCategory = entries
                    .GroupBy(t => t.Category)
                    .Select(g => new TimesheetCategorySummary
                    {
                        Category = g.Key,
                        Entries = g.Count(),
                        Minutes = g.Sum(t => t.DurationMinutes)
                    })
                    .OrderByDescending(c => c.Minutes)
                    .ToList()
            };

            await LoadEmployees();

            return View(model);
        }

        public async Task<IActionResult> Export(
            int? employeeId,
            WorkCategory? category,
            DateTime? from,
            DateTime? to)
        {
            var (start, end) = Range(from, to);

            var entries = await Filtered(employeeId, category, start, end)
                .OrderBy(t => t.WorkDate)
                .ThenBy(t => t.Employee.FirstName)
                .ThenBy(t => t.StartTime)
                .ThenBy(t => t.Id)
                .ToListAsync();

            var headers = new[]
            {
                "Date", "Employee No", "Employee", "Department",
                "Type of work", "Task", "Details", "Reference", "Asset",
                "Started", "Finished", "Minutes", "Hours"
            };

            var rows = entries.Select(t => new string?[]
            {
                t.WorkDate.ToString("yyyy-MM-dd"),
                t.Employee.EmployeeNumber,
                t.Employee.FirstName + " " + t.Employee.LastName,
                t.Employee.Department.Name,
                t.Category.ToString(),
                t.Title,
                t.Details,
                t.Reference,
                t.Asset == null
                    ? null
                    : t.Asset.DeviceModel +
                      (string.IsNullOrWhiteSpace(t.Asset.SerialNumber)
                          ? ""
                          : " (" + t.Asset.SerialNumber + ")"),
                t.StartTime?.ToString("HH:mm"),
                t.EndTime?.ToString("HH:mm"),
                t.DurationMinutes.ToString(),
                t.Hours.ToString("0.00")
            });

            var file = CsvExportService.Build(headers, rows);

            return File(
                file,
                "text/csv",
                CsvExportService.FileName(
                    $"timesheets-{start:yyyyMMdd}-{end:yyyyMMdd}"));
        }

        private IQueryable<TimesheetEntry> Filtered(
            int? employeeId,
            WorkCategory? category,
            DateTime start,
            DateTime end)
        {
            var query = _context.TimesheetEntries
                .AsNoTracking()
                .Include(t => t.Employee).ThenInclude(e => e.Department)
                .Include(t => t.Asset)
                .Where(t => t.WorkDate >= start && t.WorkDate <= end);

            if (employeeId.HasValue)
                query = query.Where(t => t.EmployeeId == employeeId.Value);

            if (category.HasValue)
                query = query.Where(t => t.Category == category.Value);

            return query;
        }

        private static (DateTime Start, DateTime End) Range(
            DateTime? from,
            DateTime? to)
        {
            var today = DateTime.Today;

            var start = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
            var end = (to ?? today).Date;

            return start <= end ? (start, end) : (end, start);
        }

        private static int CountWorkingDays(DateTime start, DateTime end)
        {
            var count = 0;

            for (var day = start; day <= end; day = day.AddDays(1))
            {
                if (day.DayOfWeek != DayOfWeek.Saturday &&
                    day.DayOfWeek != DayOfWeek.Sunday)
                {
                    count++;
                }
            }

            return count;
        }

        private async Task LoadEmployees()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Where(e =>
                    e.Department.TracksTimesheets ||
                    _context.TimesheetEntries.Any(t => t.EmployeeId == e.Id))
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .Select(e => new SelectListItem(
                    e.FirstName + " " + e.LastName,
                    e.Id.ToString()))
                .ToListAsync();

            ViewBag.Employees = employees;
        }
    }
}
