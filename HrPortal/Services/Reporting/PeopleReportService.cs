using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reporting
{
    public class PeopleReportService
    {
        private readonly ApplicationDbContext _context;

        public PeopleReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReportPayload> EmployeesAsync(
            ReportFilter filter,
            bool export)
        {
            var today = DateTime.Today;

            var query = _context.Employees
                .Include(e => e.Department)
                .AsNoTracking()
                .AsQueryable();

            if (!filter.IncludeInactive)
                query = query.Where(e => e.IsActive);

            if (filter.DepartmentId.HasValue)
                query = query.Where(e => e.DepartmentId == filter.DepartmentId.Value);

            if (filter.From.HasValue)
                query = query.Where(e => e.DateJoined >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(e => e.DateJoined <= filter.To.Value);

            var employees = await query
                .OrderBy(e => e.Department!.Name)
                .ThenBy(e => e.FirstName)
                .ToListAsync();

            var userIds = employees
                .Where(e => e.UserId != null)
                .Select(e => e.UserId!.Value)
                .ToList();

            var roleRows = await (
                    from userRole in _context.UserRoles
                    join role in _context.Roles
                        on userRole.RoleId equals role.Id
                    where userIds.Contains(userRole.UserId)
                    select new
                    {
                        userRole.UserId,
                        role.Name
                    })
                .AsNoTracking()
                .ToListAsync();

            var rolesByUser = roleRows
                .GroupBy(r => r.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .Select(r => r.Name ?? string.Empty)
                        .OrderBy(n => n)
                        .ToList());

            var rows = new List<EmployeeReportRow>();

            foreach (var e in employees)
            {
                var roles =
                    e.UserId != null &&
                    rolesByUser.TryGetValue(e.UserId.Value, out var found)
                        ? found
                        : new List<string>();

                rows.Add(new EmployeeReportRow
                {
                    EmployeeNumber = e.EmployeeNumber,
                    Name = e.FirstName + " " + e.LastName,
                    JobTitle = e.JobTitle,
                    EmploymentType = e.EmploymentType,
                    ContractEndDate = e.ContractEndDate,
                    EndDate = e.EndDate,
                    Department = e.Department?.Name ?? "--",
                    Email = e.Email,
                    DateJoined = e.DateJoined,
                    YearsOfService =
                        (int)((today - e.DateJoined).TotalDays / 365.25),
                    IsActive = e.IsActive,
                    HasLogin = e.UserId != null,
                    Roles = string.Join(", ", roles.OrderBy(r => r))
                });
            }

            if (export)
            {
                return ReportPayload.ForCsv(
                    "employees",
                    new[]
                    {
                        "Employee number", "Name", "Job title", "Department",
                        "Email", "Employment type", "Contract ends", "Left on",
                        "Date joined", "Years of service", "Active",
                        "Has login", "Roles"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.EmployeeNumber, r.Name, r.JobTitle, r.Department,
                        r.Email, r.EmploymentType.ToString(), ReportMath.D(r.ContractEndDate),
                        ReportMath.D(r.EndDate),
                        ReportMath.D(r.DateJoined), r.YearsOfService.ToString(),
                        r.IsActive ? "Yes" : "No",
                        r.HasLogin ? "Yes" : "No", r.Roles
                    }));
            }

            var model = new ReportViewModel<EmployeeReportRow>
            {
                Title = "Employees",
                Description =
                    "Headcount with length of service and account status.",
                Action = "Employees",
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Employees", rows.Count.ToString()),
                    new ReportStat(
                        "Active", rows.Count(r => r.IsActive).ToString()),
                    new ReportStat(
                        "Departments",
                        rows.Select(r => r.Department).Distinct()
                            .Count().ToString()),
                    new ReportStat(
                        "Without a login",
                        rows.Count(r => !r.HasLogin).ToString()),
                    new ReportStat(
                        "Average service",
                        rows.Count == 0
                            ? "--"
                            : rows.Average(r => r.YearsOfService)
                                .ToString("0.#") + " yrs")
                },
                Coverage = filter.IncludeInactive
                    ? "Active and inactive employees"
                    : "Active employees"
            };

            var headcount =
                new ReportBreakdown("Headcount by department", "people");

            foreach (var g in rows
                .GroupBy(r => r.Department)
                .OrderByDescending(g => g.Count()))
            {
                headcount.Add(g.Key, g.Count());
            }

            var service = new ReportBreakdown("Length of service", "people")
                .Add("Under 1 year",
                    rows.Count(r => r.YearsOfService < 1))
                .Add("1 to 3 years",
                    rows.Count(r => r.YearsOfService is >= 1 and < 3))
                .Add("3 to 5 years",
                    rows.Count(r => r.YearsOfService is >= 3 and < 5))
                .Add("5 years or more",
                    rows.Count(r => r.YearsOfService >= 5));

            var byRole = new ReportBreakdown("By role", "people");

            foreach (var g in rows
                .SelectMany(r => string.IsNullOrWhiteSpace(r.Roles)
                    ? new[] { "No role" }
                    : r.Roles.Split(", "))
                .GroupBy(role => role)
                .OrderByDescending(g => g.Count()))
            {
                byRole.Add(g.Key, g.Count());
            }

            model.Breakdowns.AddRange(
                new[] { headcount, service, byRole }
                    .Where(b => b.Total > 0));

            return ReportPayload.ForView(model);
        }

        public async Task<ReportPayload> DocumentsAsync(
            ReportFilter filter,
            bool export)
        {
            var query = _context.EmployeeDocuments
                .Include(d => d.Employee).ThenInclude(e => e.Department)
                .Include(d => d.DocumentCategory)
                .Include(d => d.UploadedByEmployee)
                .AsNoTracking()
                .AsQueryable();

            if (filter.From.HasValue)
                query = query.Where(d => d.UploadedAt >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(d => d.UploadedAt <= filter.To.Value);

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(d =>
                    d.Employee.DepartmentId == filter.DepartmentId.Value);
            }

            if (filter.EmployeeId.HasValue)
                query = query.Where(d => d.EmployeeId == filter.EmployeeId.Value);

            if (filter.DocumentCategoryId.HasValue)
            {
                query = query.Where(d =>
                    d.DocumentCategoryId == filter.DocumentCategoryId.Value);
            }

            var documents = await query
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            var rows = documents.Select(d => new DocumentReportRow
            {
                Employee = d.Employee.FirstName + " " + d.Employee.LastName,
                Department = d.Employee.Department?.Name ?? "--",
                Category = d.DocumentCategory.Name,
                FileName = d.OriginalFileName,
                UploadedAt = d.UploadedAt,
                ExpiryDate = d.ExpiryDate,
                UploadedBy = !string.IsNullOrWhiteSpace(d.UploadedByName)
                    ? d.UploadedByName!
                    : d.UploadedByEmployee != null
                        ? d.UploadedByEmployee.FirstName + " " +
                          d.UploadedByEmployee.LastName
                        : "Not recorded"
            }).ToList();

            if (export)
            {
                return ReportPayload.ForCsv(
                    "employee-documents",
                    new[]
                    {
                        "Employee", "Department", "Category", "File",
                        "Expires", "Uploaded", "Uploaded by"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Employee, r.Department, r.Category, r.FileName,
                        ReportMath.D(r.ExpiryDate), ReportMath.D(r.UploadedAt), r.UploadedBy
                    }));
            }

            var withDocs = rows.Select(r => r.Employee).Distinct().Count();

            var activeEmployees = await _context.Employees
                .CountAsync(e => e.IsActive);

            var model = new ReportViewModel<DocumentReportRow>
            {
                Title = "Employee documents",
                Description =
                    "What is on file, for whom, and who filed it.",
                Action = "Documents",
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Documents", rows.Count.ToString()),
                    new ReportStat(
                        "Employees covered", withDocs.ToString(),
                        $"of {activeEmployees} active"),
                    new ReportStat(
                        "Categories used",
                        rows.Select(r => r.Category).Distinct()
                            .Count().ToString()),
                    new ReportStat(
                        "Nothing on file",
                        Math.Max(0, activeEmployees - withDocs).ToString(),
                        "active employees")
                },
                Coverage = ReportMath.Period(filter)
            };

            var byCategory = new ReportBreakdown("By category", "documents");

            foreach (var g in rows
                .GroupBy(r => r.Category)
                .OrderByDescending(g => g.Count()))
            {
                byCategory.Add(g.Key, g.Count());
            }

            var today = DateTime.Today;

            var expiring = new ReportBreakdown("Expiry", "documents")
                .Add(
                    "Expired",
                    rows.Count(r =>
                        r.ExpiryDate != null &&
                        r.ExpiryDate.Value.Date < today),
                    "critical")
                .Add(
                    "Within 30 days",
                    rows.Count(r =>
                        r.ExpiryDate != null &&
                        r.ExpiryDate.Value.Date >= today &&
                        r.ExpiryDate.Value.Date <= today.AddDays(30)),
                    "warning")
                .Add(
                    "Later",
                    rows.Count(r =>
                        r.ExpiryDate != null &&
                        r.ExpiryDate.Value.Date > today.AddDays(30)),
                    "good")
                .Add(
                    "No expiry recorded",
                    rows.Count(r => r.ExpiryDate == null));

            var byDept = new ReportBreakdown("By department", "documents");

            foreach (var g in rows
                .GroupBy(r => r.Department)
                .OrderByDescending(g => g.Count()))
            {
                byDept.Add(g.Key, g.Count());
            }

            model.Breakdowns.AddRange(
                new[] { byCategory, byDept, expiring }
                    .Where(b => b.Items.Count > 0));

            return ReportPayload.ForView(model);
        }

        public async Task<ReportPayload> AuditTrailAsync(
            ReportFilter filter,
            bool export)
        {
            filter.From ??= DateTime.Today.AddDays(-30);

            var query = _context.AuditLogs
                .Include(a => a.Employee)
                .AsNoTracking()
                .AsQueryable();

            if (filter.From.HasValue)
                query = query.Where(a => a.Timestamp >= filter.From.Value);

            if (filter.To.HasValue)
            {
                var to = filter.To.Value.Date.AddDays(1);
                query = query.Where(a => a.Timestamp < to);
            }

            if (!string.IsNullOrWhiteSpace(filter.AuditAction))
            {
                query = query.Where(a =>
                    a.Action == filter.AuditAction);
            }

            var rows = await query
                .OrderByDescending(a => a.Timestamp)
                .Take(2000)
                .Select(a => new AuditReportRow
                {
                    Timestamp = a.Timestamp,
                    Action = a.Action,
                    EntityName = a.EntityName,
                    EntityId = a.EntityId,
                    Actor = a.Employee != null
                        ? a.Employee.FirstName + " " + a.Employee.LastName
                        : "System",
                    Description = a.Description
                })
                .ToListAsync();

            if (export)
            {
                return ReportPayload.ForCsv(
                    "audit-trail",
                    new[]
                    {
                        "When", "Action", "Record type", "Record",
                        "Actor", "Detail"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Timestamp.ToString("yyyy-MM-dd HH:mm"),
                        r.Action, r.EntityName, r.EntityId, r.Actor,
                        r.Description
                    }));
            }

            var model = new ReportViewModel<AuditReportRow>
            {
                Title = "Audit trail",
                Description =
                    "Who did what, and when. Newest first, capped at " +
                    "2000 entries.",
                Action = "AuditTrail",
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Entries", rows.Count.ToString()),
                    new ReportStat(
                        "Distinct actions",
                        rows.Select(r => r.Action).Distinct()
                            .Count().ToString()),
                    new ReportStat(
                        "People involved",
                        rows.Select(r => r.Actor).Distinct()
                            .Count().ToString()),
                    new ReportStat(
                        "Busiest day",
                        rows.Count == 0
                            ? "--"
                            : rows.GroupBy(r => r.Timestamp.Date)
                                .OrderByDescending(g => g.Count())
                                .First().Key.ToString("d MMM"))
                },
                Coverage = ReportMath.Period(filter)
            };

            var byAction = new ReportBreakdown("By action", "entries");

            foreach (var g in rows
                .GroupBy(r => r.Action)
                .OrderByDescending(g => g.Count()))
            {
                byAction.Add(g.Key, g.Count());
            }

            var byActor = new ReportBreakdown("Most active", "entries");

            foreach (var g in rows
                .GroupBy(r => r.Actor)
                .OrderByDescending(g => g.Count())
                .Take(8))
            {
                byActor.Add(g.Key, g.Count());
            }

            var byRecord = new ReportBreakdown("By record type", "entries");

            foreach (var g in rows
                .GroupBy(r => r.EntityName)
                .OrderByDescending(g => g.Count()))
            {
                byRecord.Add(g.Key, g.Count());
            }

            model.Breakdowns.AddRange(
                new[] { byAction, byActor, byRecord }
                    .Where(b => b.Items.Count > 0));

            return ReportPayload.ForView(model);
        }
        public async Task<List<string>> AuditActionsAsync()
        {
            return await _context.AuditLogs
                .Select(a => a.Action)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();
        }
    }
}
