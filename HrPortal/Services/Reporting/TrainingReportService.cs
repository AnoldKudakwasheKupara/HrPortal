using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reporting
{
    public class TrainingReportService
    {
        private readonly ApplicationDbContext _context;

        public TrainingReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReportPayload> TrainingAsync(
            ReportFilter filter,
            bool export)
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

            if (export)
            {
                return ReportPayload.ForCsv(
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
                        ReportMath.D(r.AssignedAt), ReportMath.D(r.DueDate),
                        r.IsCompleted ? "Yes" : "No", ReportMath.D(r.CompletedAt),
                        r.IsOverdue ? "Yes" : "No", r.Rating?.ToString()
                    }));
            }

            var completed = rows.Count(r => r.IsCompleted);

            var model = new ReportViewModel<TrainingReportRow>
            {
                Title = "Training assignments",
                Description =
                    "Who has been assigned what, and whether it is done.",
                Action = "Training",
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
                },
                Coverage = ReportMath.Period(filter)
            };

            var progress = new ReportBreakdown("Progress", "assignments")
                .Add("Completed", rows.Count(r => r.IsCompleted), "good")
                .Add("Outstanding",
                    rows.Count(r => !r.IsCompleted && !r.IsOverdue))
                .Add("Overdue", rows.Count(r => r.IsOverdue), "critical");

            var byCourse =
                new ReportBreakdown("Assignments by course", "assignments");

            foreach (var g in rows
                .GroupBy(r => r.Course)
                .OrderByDescending(g => g.Count()))
            {
                byCourse.Add(g.Key, g.Count());
            }

            var outstandingByDept =
                new ReportBreakdown("Outstanding by department", "assignments");

            foreach (var g in rows
                .Where(r => !r.IsCompleted)
                .GroupBy(r => r.Department)
                .OrderByDescending(g => g.Count()))
            {
                outstandingByDept.Add(g.Key, g.Count());
            }

            model.Breakdowns.AddRange(
                new[] { progress, byCourse, outstandingByDept }
                    .Where(b => b.Total > 0));

            return ReportPayload.ForView(model);
        }

        public async Task<ReportPayload> TrainingCoursesAsync(
            ReportFilter filter,
            bool export)
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
                    // Spelled out rather than read from
                    // TrainerName: this is a projection, and EF
                    // cannot translate a property that lives in C#.
                    Trainer = c.TrainerEmployee == null
                        ? c.Trainer
                        : c.TrainerEmployee.FirstName + " " +
                          c.TrainerEmployee.LastName,
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

            if (export)
            {
                return ReportPayload.ForCsv(
                    "training-courses",
                    new[]
                    {
                        "Course", "Trainer", "Training date", "Assigned",
                        "Completed", "Overdue", "Completion rate %",
                        "Average rating", "Responses"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Course, r.Trainer, ReportMath.D(r.TrainingDate),
                        r.Assigned.ToString(), r.Completed.ToString(),
                        r.Overdue.ToString(), ReportMath.N(r.CompletionRate),
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
                Action = "TrainingCourses",
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
                },
                Coverage = filter.IncludeInactive
                    ? "All courses"
                    : "Active courses"
            };

            var completion =
                new ReportBreakdown("Completion rate by course", "%");

            foreach (var r in rows
                .Where(r => r.Assigned > 0)
                .OrderByDescending(r => r.CompletionRate))
            {
                completion.Add(
                    r.Course,
                    r.CompletionRate,
                    r.CompletionRate >= 80 ? "good"
                        : r.CompletionRate < 40 ? "critical"
                        : "warning");
            }

            var ratingsByCourse =
                new ReportBreakdown("Average rating", "out of 5");

            foreach (var r in rows
                .Where(r => r.AverageRating.HasValue)
                .OrderByDescending(r => r.AverageRating))
            {
                ratingsByCourse.Add(
                    r.Course,
                    (decimal)r.AverageRating!.Value,
                    r.AverageRating >= 4 ? "good"
                        : r.AverageRating < 3 ? "critical"
                        : null);
            }

            model.Breakdowns.AddRange(
                new[] { completion, ratingsByCourse }
                    .Where(b => b.Items.Count > 0));

            return ReportPayload.ForView(model);
        }
    }
}
