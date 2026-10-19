using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    public class TimesheetRegisterService
    {
        public const int PageSize = 50;

        private readonly ApplicationDbContext _context;

        public TimesheetRegisterService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<TimesheetRegisterViewModel> BuildAsync(
            int? employeeId,
            WorkCategory? category,
            DateTime? from,
            DateTime? to,
            int page)
        {
            var (start, end) = Range(from, to);

            var filtered = Filtered(employeeId, category, start, end);

            var totals = await filtered
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Entries = g.Count(),
                    Minutes = g.Sum(t => t.DurationMinutes)
                })
                .FirstOrDefaultAsync();

            var totalEntries = totals?.Entries ?? 0;

            var totalPages =
                (int)Math.Ceiling(totalEntries / (double)PageSize);

            if (page < 1)
                page = 1;

            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            var people = await filtered
                .Select(t => t.EmployeeId)
                .Distinct()
                .CountAsync();

            var byPerson = await filtered
                .GroupBy(t => new
                {
                    t.EmployeeId,
                    t.Employee.FirstName,
                    t.Employee.LastName,
                    Department = t.Employee.Department.Name
                })
                .Select(g => new TimesheetPersonSummary
                {
                    EmployeeId = g.Key.EmployeeId,
                    Name = g.Key.FirstName + " " + g.Key.LastName,
                    Department = g.Key.Department,
                    Entries = g.Count(),
                    Minutes = g.Sum(t => t.DurationMinutes)
                })
                .ToListAsync();

            var daysByPerson = await filtered
                .Select(t => new { t.EmployeeId, t.WorkDate })
                .Distinct()
                .GroupBy(x => x.EmployeeId)
                .Select(g => new
                {
                    EmployeeId = g.Key,
                    Days = g.Count()
                })
                .ToListAsync();

            var dayLookup = daysByPerson
                .ToDictionary(d => d.EmployeeId, d => d.Days);

            foreach (var person in byPerson)
            {
                person.DaysLogged =
                    dayLookup.TryGetValue(person.EmployeeId, out var days)
                        ? days
                        : 0;
            }

            var byCategory = await filtered
                .GroupBy(t => t.Category)
                .Select(g => new TimesheetCategorySummary
                {
                    Category = g.Key,
                    Entries = g.Count(),
                    Minutes = g.Sum(t => t.DurationMinutes)
                })
                .ToListAsync();

            var entries = await filtered
                .OrderByDescending(t => t.WorkDate)
                .ThenBy(t => t.Employee.FirstName)
                .ThenBy(t => t.StartTime)
                .ThenBy(t => t.Id)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(t => new TimesheetRowViewModel
                {
                    Id = t.Id,
                    WorkDate = t.WorkDate,
                    EmployeeName =
                        t.Employee.FirstName + " " + t.Employee.LastName,
                    DepartmentName = t.Employee.Department.Name,
                    Category = t.Category,
                    Title = t.Title,
                    Details = t.Details,
                    Reference = t.Reference,
                    AssetModel = t.Asset == null ? null : t.Asset.DeviceModel,
                    AssetSerialNumber =
                        t.Asset == null ? null : t.Asset.SerialNumber,
                    StartTime = t.StartTime,
                    EndTime = t.EndTime,
                    DurationMinutes = t.DurationMinutes
                })
                .ToListAsync();

            var model = new TimesheetRegisterViewModel
            {
                From = start,
                To = end,
                EmployeeId = employeeId,
                Category = category,
                Entries = entries,
                TotalEntries = totalEntries,
                Page = page,
                TotalPages = totalPages,
                TotalMinutes = totals?.Minutes ?? 0,
                People = people,
                WorkingDays = (int)WorkingCalendar.CountWorkingDays(start, end),

                ByPerson = byPerson
                    .OrderByDescending(p => p.Minutes)
                    .ThenBy(p => p.Name)
                    .ToList(),

                ByCategory = byCategory
                    .OrderByDescending(c => c.Minutes)
                    .ToList()
            };

            return model;
        }

        public IQueryable<TimesheetEntry> Filtered(
            int? employeeId,
            WorkCategory? category,
            DateTime start,
            DateTime end)
        {
            var query = _context.TimesheetEntries
                .AsNoTracking()
                .Where(t => t.WorkDate >= start && t.WorkDate <= end);

            if (employeeId.HasValue)
                query = query.Where(t => t.EmployeeId == employeeId.Value);

            if (category.HasValue)
                query = query.Where(t => t.Category == category.Value);

            return query;
        }

        public static (DateTime Start, DateTime End) Range(
            DateTime? from,
            DateTime? to)
        {
            var today = DateTime.Today;

            var start = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
            var end = (to ?? today).Date;

            return start <= end ? (start, end) : (end, start);
        }
        public async Task<(byte[] Content, string FileName)> ExportAsync(
            int? employeeId,
            WorkCategory? category,
            DateTime? from,
            DateTime? to)
        {
            var (start, end) = Range(from, to);

            var entries = await Filtered(employeeId, category, start, end)
                .Include(t => t.Employee).ThenInclude(e => e.Department)
                .Include(t => t.Asset)
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

            return (
                CsvExportService.Build(headers, rows),
                CsvExportService.FileName(
                    $"timesheets-{start:yyyyMMdd}-{end:yyyyMMdd}"));
        }

        public async Task<List<SelectListItem>> LoggingEmployeesAsync()
        {
            return await _context.Employees
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
        }
    }
}
