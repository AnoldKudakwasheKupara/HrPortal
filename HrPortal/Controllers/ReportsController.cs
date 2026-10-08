using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Reporting across leave, training, people, documents, assets and
    /// the audit trail.
    ///
    /// Every report is restricted to the roles that may already see the
    /// records behind it, so reporting cannot be used as a way around
    /// the boundaries the rest of the portal enforces. The administrator
    /// therefore gets the system reports, not the HR ones.
    /// </summary>
    [Authorize(Roles = Roles.AnyManager)]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index() => View();


        // ============================================================
        // LEAVE
        // ============================================================

        [Authorize(Roles = Roles.HrOrMd)]
        public async Task<IActionResult> LeaveRequests(
            ReportFilter filter,
            string? export)
        {
            var query = _context.LeaveRequests
                .Include(r => r.Employee).ThenInclude(e => e.Department)
                .Include(r => r.LeaveType)
                .AsNoTracking()
                .AsQueryable();

            if (filter.From.HasValue)
                query = query.Where(r => r.EndDate >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(r => r.StartDate <= filter.To.Value);

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(r =>
                    r.Employee.DepartmentId == filter.DepartmentId.Value);
            }

            if (filter.EmployeeId.HasValue)
                query = query.Where(r => r.EmployeeId == filter.EmployeeId.Value);

            if (filter.LeaveTypeId.HasValue)
                query = query.Where(r => r.LeaveTypeId == filter.LeaveTypeId.Value);

            if (filter.Status.HasValue)
                query = query.Where(r => r.Status == filter.Status.Value);

            var requests = await query
                .OrderByDescending(r => r.SubmittedAt)
                .ToListAsync();

            var rows = requests.Select(r => new LeaveRequestReportRow
            {
                RequestNumber = r.RequestNumber,
                Employee = r.Employee.FirstName + " " + r.Employee.LastName,
                Department = r.Employee.Department?.Name ?? "--",
                LeaveType = r.LeaveType.Name,
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                Days = r.NumberOfDays,
                Status = r.Status,
                SubmittedAt = r.SubmittedAt,
                CompletedAt = r.CompletedAt,
                TurnaroundDays = r.CompletedAt.HasValue
                    ? Math.Round(
                        (r.CompletedAt.Value - r.SubmittedAt).TotalDays, 1)
                    : null
            }).ToList();

            if (export == "csv")
            {
                return Csv(
                    "leave-requests",
                    new[]
                    {
                        "Request", "Employee", "Department", "Leave type",
                        "Start", "End", "Days", "Status", "Submitted",
                        "Completed", "Turnaround (days)"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.RequestNumber, r.Employee, r.Department,
                        r.LeaveType, D(r.StartDate), D(r.EndDate),
                        N(r.Days), r.Status.ToString(), D(r.SubmittedAt),
                        D(r.CompletedAt), r.TurnaroundDays?.ToString("0.#")
                    }));
            }

            var approved = rows
                .Where(r => r.Status == LeaveRequestStatus.Approved)
                .ToList();

            var turnarounds = rows
                .Where(r => r.TurnaroundDays.HasValue)
                .Select(r => r.TurnaroundDays!.Value)
                .ToList();

            var model = new ReportViewModel<LeaveRequestReportRow>
            {
                Title = "Leave requests",
                Description =
                    "Every leave request, with how long each took to " +
                    "reach a decision.",
                Action = nameof(LeaveRequests),
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Requests", rows.Count.ToString()),
                    new ReportStat(
                        "Approved",
                        approved.Count.ToString(),
                        N(approved.Sum(r => r.Days)) + " days"),
                    new ReportStat(
                        "In progress",
                        rows.Count(r =>
                            !LeaveWorkflowService.IsFinal(r.Status))
                            .ToString()),
                    new ReportStat(
                        "Rejected",
                        rows.Count(r =>
                            r.Status == LeaveRequestStatus.Rejected ||
                            r.Status == LeaveRequestStatus.HRRejected)
                            .ToString()),
                    new ReportStat(
                        "Average turnaround",
                        turnarounds.Count == 0
                            ? "--"
                            : turnarounds.Average().ToString("0.#") + " d",
                        "Submission to decision")
                }
            };

            await LoadLookups();

            return View(model);
        }

        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> LeaveBalances(
            ReportFilter filter,
            string? export)
        {
            filter.Year ??= DateTime.Today.Year;

            var query = _context.LeaveBalances
                .Include(b => b.Employee).ThenInclude(e => e.Department)
                .Include(b => b.LeaveType)
                .AsNoTracking()
                .Where(b => b.Year == filter.Year.Value);

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(b =>
                    b.Employee.DepartmentId == filter.DepartmentId.Value);
            }

            if (filter.EmployeeId.HasValue)
                query = query.Where(b => b.EmployeeId == filter.EmployeeId.Value);

            if (filter.LeaveTypeId.HasValue)
                query = query.Where(b => b.LeaveTypeId == filter.LeaveTypeId.Value);

            var rows = await query
                .OrderBy(b => b.Employee.FirstName)
                .ThenBy(b => b.LeaveType.Name)
                .Select(b => new LeaveBalanceReportRow
                {
                    Employee = b.Employee.FirstName + " " + b.Employee.LastName,
                    Department = b.Employee.Department!.Name,
                    LeaveType = b.LeaveType.Name,
                    Year = b.Year,
                    Allocated = b.AllocatedDays,
                    Used = b.UsedDays,
                    Pending = b.PendingDays,
                    Remaining = b.RemainingDays
                })
                .ToListAsync();

            if (export == "csv")
            {
                return Csv(
                    "leave-balances",
                    new[]
                    {
                        "Employee", "Department", "Leave type", "Year",
                        "Allocated", "Used", "Pending", "Remaining"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Employee, r.Department, r.LeaveType,
                        r.Year.ToString(), N(r.Allocated), N(r.Used),
                        N(r.Pending), N(r.Remaining)
                    }));
            }

            var model = new ReportViewModel<LeaveBalanceReportRow>
            {
                Title = "Leave balances",
                Description =
                    "Entitlement, what has been taken and what is left.",
                Action = nameof(LeaveBalances),
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Balances", rows.Count.ToString()),
                    new ReportStat(
                        "Allocated", N(rows.Sum(r => r.Allocated)), "days"),
                    new ReportStat(
                        "Used", N(rows.Sum(r => r.Used)), "days"),
                    new ReportStat(
                        "Pending", N(rows.Sum(r => r.Pending)), "days"),
                    new ReportStat(
                        "Remaining", N(rows.Sum(r => r.Remaining)), "days"),
                    new ReportStat(
                        "Fully used",
                        rows.Count(r => r.Remaining <= 0).ToString(),
                        "nothing left")
                }
            };

            await LoadLookups();

            return View(model);
        }

        [Authorize(Roles = Roles.HrOrMd)]
        public async Task<IActionResult> Absence(
            ReportFilter filter,
            string? export)
        {
            filter.From ??= new DateTime(DateTime.Today.Year, 1, 1);
            filter.To ??= new DateTime(DateTime.Today.Year, 12, 31);

            var query = _context.LeaveRequests
                .Include(r => r.Employee).ThenInclude(e => e.Department)
                .AsNoTracking()
                .Where(r =>
                    r.EndDate >= filter.From.Value &&
                    r.StartDate <= filter.To.Value);

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(r =>
                    r.Employee.DepartmentId == filter.DepartmentId.Value);
            }

            var requests = await query.ToListAsync();

            var rows = requests
                .GroupBy(r => new
                {
                    r.EmployeeId,
                    Name = r.Employee.FirstName + " " + r.Employee.LastName,
                    Department = r.Employee.Department != null
                        ? r.Employee.Department.Name
                        : "--"
                })
                .Select(g => new AbsenceReportRow
                {
                    Employee = g.Key.Name,
                    Department = g.Key.Department,
                    ApprovedDays = g
                        .Where(r => r.Status == LeaveRequestStatus.Approved)
                        .Sum(r => r.NumberOfDays),
                    ApprovedRequests = g
                        .Count(r => r.Status == LeaveRequestStatus.Approved),
                    PendingDays = g
                        .Where(r => !LeaveWorkflowService.IsFinal(r.Status))
                        .Sum(r => r.NumberOfDays),
                    RejectedRequests = g.Count(r =>
                        r.Status == LeaveRequestStatus.Rejected ||
                        r.Status == LeaveRequestStatus.HRRejected),
                    LastLeaveEnd = g
                        .Where(r => r.Status == LeaveRequestStatus.Approved)
                        .Select(r => (DateTime?)r.EndDate)
                        .DefaultIfEmpty(null)
                        .Max()
                })
                .OrderByDescending(r => r.ApprovedDays)
                .ThenBy(r => r.Employee)
                .ToList();

            if (export == "csv")
            {
                return Csv(
                    "absence-summary",
                    new[]
                    {
                        "Employee", "Department", "Approved days",
                        "Approved requests", "Days in progress",
                        "Rejected requests", "Last day of leave"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Employee, r.Department, N(r.ApprovedDays),
                        r.ApprovedRequests.ToString(), N(r.PendingDays),
                        r.RejectedRequests.ToString(), D(r.LastLeaveEnd)
                    }));
            }

            var model = new ReportViewModel<AbsenceReportRow>
            {
                Title = "Absence summary",
                Description =
                    "Days taken per person over the period, highest first.",
                Action = nameof(Absence),
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Employees", rows.Count.ToString(),
                        "with leave in the period"),
                    new ReportStat(
                        "Approved days", N(rows.Sum(r => r.ApprovedDays))),
                    new ReportStat(
                        "Average per person",
                        rows.Count == 0
                            ? "--"
                            : N(rows.Sum(r => r.ApprovedDays) / rows.Count)),
                    new ReportStat(
                        "Days in progress", N(rows.Sum(r => r.PendingDays)))
                }
            };

            await LoadLookups();

            return View(model);
        }


        // ============================================================
        // TRAINING
        // ============================================================

        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> Training(
            ReportFilter filter,
            string? export)
        {
            var today = DateTime.Today;

            var query = _context.TrainingAssignments
                .Include(a => a.Employee).ThenInclude(e => e.Department)
                .Include(a => a.TrainingCourse)
                .Include(a => a.Feedback)
                .AsNoTracking()
                .AsQueryable();

            if (filter.From.HasValue)
                query = query.Where(a => a.AssignedAt >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(a => a.AssignedAt <= filter.To.Value);

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(a =>
                    a.Employee.DepartmentId == filter.DepartmentId.Value);
            }

            if (filter.EmployeeId.HasValue)
                query = query.Where(a => a.EmployeeId == filter.EmployeeId.Value);

            if (filter.TrainingCourseId.HasValue)
            {
                query = query.Where(a =>
                    a.TrainingCourseId == filter.TrainingCourseId.Value);
            }

            var assignments = await query
                .OrderBy(a => a.IsCompleted)
                .ThenBy(a => a.DueDate)
                .ToListAsync();

            var rows = assignments.Select(a => new TrainingReportRow
            {
                Course = a.TrainingCourse.Title,
                Employee = a.Employee.FirstName + " " + a.Employee.LastName,
                Department = a.Employee.Department?.Name ?? "--",
                AssignedAt = a.AssignedAt,
                DueDate = a.DueDate,
                IsCompleted = a.IsCompleted,
                CompletedAt = a.CompletedAt,
                IsOverdue = !a.IsCompleted &&
                            a.DueDate != null &&
                            a.DueDate < today,
                Rating = a.Feedback?.Rating
            }).ToList();

            if (export == "csv")
            {
                return Csv(
                    "training-assignments",
                    new[]
                    {
                        "Course", "Employee", "Department", "Assigned",
                        "Due", "Completed", "Completed on", "Overdue",
                        "Rating"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Course, r.Employee, r.Department,
                        D(r.AssignedAt), D(r.DueDate),
                        r.IsCompleted ? "Yes" : "No", D(r.CompletedAt),
                        r.IsOverdue ? "Yes" : "No", r.Rating?.ToString()
                    }));
            }

            var completed = rows.Count(r => r.IsCompleted);

            var model = new ReportViewModel<TrainingReportRow>
            {
                Title = "Training assignments",
                Description =
                    "Who has been assigned what, and whether it is done.",
                Action = nameof(Training),
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Assignments", rows.Count.ToString()),
                    new ReportStat("Completed", completed.ToString()),
                    new ReportStat(
                        "Completion rate",
                        rows.Count == 0
                            ? "--"
                            : (completed * 100m / rows.Count)
                                .ToString("0.#") + "%"),
                    new ReportStat(
                        "Overdue",
                        rows.Count(r => r.IsOverdue).ToString(),
                        "past the due date")
                }
            };

            await LoadLookups();

            return View(model);
        }

        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> TrainingCourses(
            ReportFilter filter,
            string? export)
        {
            var today = DateTime.Today;

            var courses = await _context.TrainingCourses
                .Include(c => c.Assignments).ThenInclude(a => a.Feedback)
                .AsNoTracking()
                .Where(c => filter.IncludeInactive || c.IsActive)
                .OrderBy(c => c.Title)
                .ToListAsync();

            var rows = courses.Select(c =>
            {
                var ratings = c.Assignments
                    .Where(a => a.Feedback != null)
                    .Select(a => a.Feedback!.Rating)
                    .ToList();

                var assigned = c.Assignments.Count;
                var done = c.Assignments.Count(a => a.IsCompleted);

                return new TrainingCourseReportRow
                {
                    Course = c.Title,
                    Trainer = c.Trainer,
                    TrainingDate = c.TrainingDate,
                    Assigned = assigned,
                    Completed = done,
                    Overdue = c.Assignments.Count(a =>
                        !a.IsCompleted &&
                        a.DueDate != null &&
                        a.DueDate < today),
                    CompletionRate = assigned == 0
                        ? 0
                        : Math.Round(done * 100m / assigned, 1),
                    AverageRating = ratings.Count == 0
                        ? null
                        : Math.Round(ratings.Average(), 2),
                    Responses = ratings.Count
                };
            }).ToList();

            if (export == "csv")
            {
                return Csv(
                    "training-courses",
                    new[]
                    {
                        "Course", "Trainer", "Training date", "Assigned",
                        "Completed", "Overdue", "Completion rate %",
                        "Average rating", "Responses"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Course, r.Trainer, D(r.TrainingDate),
                        r.Assigned.ToString(), r.Completed.ToString(),
                        r.Overdue.ToString(), N(r.CompletionRate),
                        r.AverageRating?.ToString("0.00"),
                        r.Responses.ToString()
                    }));
            }

            var allRatings = rows
                .Where(r => r.AverageRating.HasValue)
                .ToList();

            var model = new ReportViewModel<TrainingCourseReportRow>
            {
                Title = "Course effectiveness",
                Description =
                    "Take-up and feedback for each course.",
                Action = nameof(TrainingCourses),
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Courses", rows.Count.ToString()),
                    new ReportStat(
                        "Assignments", rows.Sum(r => r.Assigned).ToString()),
                    new ReportStat(
                        "Overall completion",
                        rows.Sum(r => r.Assigned) == 0
                            ? "--"
                            : (rows.Sum(r => r.Completed) * 100m /
                               rows.Sum(r => r.Assigned)).ToString("0.#") + "%"),
                    new ReportStat(
                        "Average rating",
                        allRatings.Count == 0
                            ? "--"
                            : allRatings
                                .Average(r => r.AverageRating!.Value)
                                .ToString("0.00"),
                        "out of 5")
                }
            };

            await LoadLookups();

            return View(model);
        }


        // ============================================================
        // PEOPLE
        // ============================================================

        [Authorize(Roles = Roles.AdminOrHr)]
        public async Task<IActionResult> Employees(
            ReportFilter filter,
            string? export)
        {
            var today = DateTime.Today;

            var query = _context.Employees
                .Include(e => e.Department)
                .Include(e => e.User)
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

            var rows = new List<EmployeeReportRow>();

            foreach (var e in employees)
            {
                var roles = e.User != null
                    ? await _userManager.GetRolesAsync(e.User)
                    : new List<string>();

                rows.Add(new EmployeeReportRow
                {
                    EmployeeNumber = e.EmployeeNumber,
                    Name = e.FirstName + " " + e.LastName,
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

            if (export == "csv")
            {
                return Csv(
                    "employees",
                    new[]
                    {
                        "Employee number", "Name", "Department", "Email",
                        "Date joined", "Years of service", "Active",
                        "Has login", "Roles"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.EmployeeNumber, r.Name, r.Department, r.Email,
                        D(r.DateJoined), r.YearsOfService.ToString(),
                        r.IsActive ? "Yes" : "No",
                        r.HasLogin ? "Yes" : "No", r.Roles
                    }));
            }

            var model = new ReportViewModel<EmployeeReportRow>
            {
                Title = "Employees",
                Description =
                    "Headcount with length of service and account status.",
                Action = nameof(Employees),
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
                }
            };

            await LoadLookups();

            return View(model);
        }

        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> Documents(
            ReportFilter filter,
            string? export)
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
                UploadedBy = !string.IsNullOrWhiteSpace(d.UploadedByName)
                    ? d.UploadedByName!
                    : d.UploadedByEmployee != null
                        ? d.UploadedByEmployee.FirstName + " " +
                          d.UploadedByEmployee.LastName
                        : "Not recorded"
            }).ToList();

            if (export == "csv")
            {
                return Csv(
                    "employee-documents",
                    new[]
                    {
                        "Employee", "Department", "Category", "File",
                        "Uploaded", "Uploaded by"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Employee, r.Department, r.Category, r.FileName,
                        D(r.UploadedAt), r.UploadedBy
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
                Action = nameof(Documents),
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
                }
            };

            await LoadLookups();

            return View(model);
        }


        // ============================================================
        // SYSTEM
        // ============================================================

        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> Assets(
            ReportFilter filter,
            string? export)
        {
            var query = _context.Assets
                .Include(a => a.AssignedToEmployee)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Company))
                query = query.Where(a => a.Company == filter.Company);

            if (filter.Condition.HasValue)
                query = query.Where(a => a.Condition == filter.Condition.Value);

            if (filter.EmployeeId.HasValue)
            {
                query = query.Where(a =>
                    a.AssignedToEmployeeId == filter.EmployeeId.Value);
            }

            var rows = await query
                .OrderBy(a => a.Company)
                .ThenBy(a => a.DeviceModel)
                .Select(a => new AssetReportRow
                {
                    Company = a.Company ?? "--",
                    DeviceModel = a.DeviceModel,
                    Processor = a.Processor,
                    SerialNumber = a.SerialNumber,
                    ProcuredOn = a.ProcuredOn,
                    Condition = a.Condition,
                    IsInUse = a.IsInUse,
                    AssignedTo = a.AssignedToEmployee != null
                        ? a.AssignedToEmployee.FirstName + " " +
                          a.AssignedToEmployee.LastName
                        : "Unassigned",
                    Notes = a.Notes
                })
                .ToListAsync();

            if (export == "csv")
            {
                return Csv(
                    "assets",
                    new[]
                    {
                        "Company", "Device / model", "Processor",
                        "Serial", "Procured", "Condition", "In use",
                        "Assigned to", "Notes"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Company, r.DeviceModel, r.Processor,
                        r.SerialNumber, D(r.ProcuredOn),
                        r.Condition.ToString(), r.IsInUse ? "Yes" : "No",
                        r.AssignedTo, r.Notes
                    }));
            }

            var model = new ReportViewModel<AssetReportRow>
            {
                Title = "Asset register",
                Description =
                    "Machines on the register, their condition and holder.",
                Action = nameof(Assets),
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Assets", rows.Count.ToString()),
                    new ReportStat(
                        "Good",
                        rows.Count(r => r.Condition == AssetCondition.Good)
                            .ToString()),
                    new ReportStat(
                        "Needing attention",
                        rows.Count(r =>
                            r.Condition == AssetCondition.Old ||
                            r.Condition == AssetCondition.Bad).ToString(),
                        "old or bad"),
                    new ReportStat(
                        "Unassigned",
                        rows.Count(r => r.AssignedTo == "Unassigned")
                            .ToString()),
                    new ReportStat(
                        "Not in use",
                        rows.Count(r => !r.IsInUse).ToString())
                }
            };

            await LoadLookups();

            return View(model);
        }

        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> AuditTrail(
            ReportFilter filter,
            string? export)
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

            if (export == "csv")
            {
                return Csv(
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

            ViewBag.Actions = await _context.AuditLogs
                .Select(a => a.Action)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            var model = new ReportViewModel<AuditReportRow>
            {
                Title = "Audit trail",
                Description =
                    "Who did what, and when. Newest first, capped at " +
                    "2000 entries.",
                Action = nameof(AuditTrail),
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
                }
            };

            await LoadLookups();

            return View(model);
        }


        // ============================================================
        // HELPERS
        // ============================================================

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

            ViewBag.Companies = await _context.Assets
                .Where(a => a.Company != null && a.Company != "")
                .Select(a => a.Company!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }
    }
}
