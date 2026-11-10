using HrPortal.Entities;
using HrPortal.ViewModels;

namespace HrPortal.Services
{
    /// <summary>
    /// Turns a course request that was approved on paper into an approved
    /// application, so it shows on the employee's My Courses and in HR's
    /// approved list like any other.
    /// </summary>
    public static class CourseCapture
    {
        /// <summary>What is wrong with the form, field by field.</summary>
        public static List<(string Field, string Error)> Check(CaptureCourseViewModel model, DateTime today)
        {
            var errors = new List<(string, string)>();

            if (!model.Approved)
                errors.Add((nameof(model.Approved),
                    "Tick to confirm the request was approved. Only approved requests can be captured."));

            if (model.ApprovedOn != null && model.ApprovedOn.Value.Date > today.Date)
                errors.Add((nameof(model.ApprovedOn), "The approval date cannot be in the future."));

            if (model.HasSatBefore && model.Sitting == CourseSitting.FirstAttempt)
                errors.Add((nameof(model.Sitting), "Say whether they were going up a level or re-writing."));

            return errors;
        }

        public static CourseApplication Build(
            CaptureCourseViewModel model,
            string applicationNumber,
            int capturedByEmployeeId,
            DateTime now)
        {
            var approvedOn = DateTime.SpecifyKind(model.ApprovedOn!.Value.Date, DateTimeKind.Utc);

            var application = new CourseApplication
            {
                ApplicationNumber = applicationNumber,
                EmployeeId = model.EmployeeId!.Value,
                Status = CourseApplicationStatus.Approved,
                ProgramIntended = model.ProgramIntended.Trim(),
                Justification = model.Justification.Trim(),
                HasSatBefore = model.HasSatBefore,
                Sitting = model.HasSatBefore ? model.Sitting : CourseSitting.FirstAttempt,
                TargetExamDate = model.TargetExamDate?.Date,
                CreatedAt = now,
                SubmittedAt = approvedOn,
                CompletedAt = approvedOn
            };

            application.History.Add(new CourseApplicationHistory
            {
                ActionedByEmployeeId = capturedByEmployeeId,
                Action = CourseApplicationAction.CapturedAsApproved,
                PreviousStatus = CourseApplicationStatus.Draft,
                NewStatus = CourseApplicationStatus.Approved,
                Comment = Comment(model, approvedOn),
                ActionDate = now
            });

            return application;
        }

        private static string Comment(CaptureCourseViewModel model, DateTime approvedOn)
        {
            var comment = $"Approved on {approvedOn:d MMM yyyy}";

            if (!string.IsNullOrWhiteSpace(model.ApprovedBy))
                comment += $" by {model.ApprovedBy.Trim()}";

            comment += ", before the portal. Captured on the employee's behalf.";

            if (!string.IsNullOrWhiteSpace(model.Notes))
                comment += " " + model.Notes.Trim();

            return comment.Length > 1000 ? comment[..1000] : comment;
        }
    }
}
