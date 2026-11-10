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

        /// <summary>The captured application as it would appear on the form.</summary>
        public static CaptureCourseViewModel ToForm(CourseApplication application)
        {
            var entry = CaptureEntry(application);
            var (approvedBy, notes) = ReadComment(entry?.Comment);

            return new CaptureCourseViewModel
            {
                EmployeeId = application.EmployeeId,
                ProgramIntended = application.ProgramIntended,
                Justification = application.Justification,
                HasSatBefore = application.HasSatBefore,
                Sitting = application.Sitting,
                TargetExamDate = application.TargetExamDate,
                ApprovedOn = application.CompletedAt?.Date,
                ApprovedBy = approvedBy,
                Notes = notes,
                Approved = true
            };
        }

        /// <summary>
        /// Puts the corrected form onto the application and records what
        /// changed. Returns the names of the changed parts, empty when
        /// nothing changed.
        /// </summary>
        public static List<string> Apply(
            CaptureCourseViewModel model,
            CourseApplication application,
            int editedByEmployeeId,
            DateTime now)
        {
            var before = ToForm(application);
            var changed = new List<string>();

            void Note(bool differs, string name)
            {
                if (differs)
                    changed.Add(name);
            }

            var sitting = model.HasSatBefore ? model.Sitting : CourseSitting.FirstAttempt;
            var approvedBy = Clean(model.ApprovedBy);
            var notes = Clean(model.Notes);

            Note(before.EmployeeId != model.EmployeeId, "employee");
            Note(before.ProgramIntended != model.ProgramIntended.Trim(), "programme");
            Note(before.Justification != model.Justification.Trim(), "justification");
            Note(before.HasSatBefore != model.HasSatBefore || before.Sitting != sitting, "previous sittings");
            Note(before.TargetExamDate?.Date != model.TargetExamDate?.Date, "target exam date");
            Note(before.ApprovedOn?.Date != model.ApprovedOn?.Date, "approval date");
            Note(Clean(before.ApprovedBy) != approvedBy, "approved by");
            Note(Clean(before.Notes) != notes, "notes");

            if (changed.Count == 0)
                return changed;

            var approvedOn = DateTime.SpecifyKind(model.ApprovedOn!.Value.Date, DateTimeKind.Utc);

            application.EmployeeId = model.EmployeeId!.Value;
            application.ProgramIntended = model.ProgramIntended.Trim();
            application.Justification = model.Justification.Trim();
            application.HasSatBefore = model.HasSatBefore;
            application.Sitting = sitting;
            application.TargetExamDate = model.TargetExamDate?.Date;
            application.SubmittedAt = approvedOn;
            application.CompletedAt = approvedOn;

            var entry = CaptureEntry(application);

            if (entry != null)
                entry.Comment = Comment(model, approvedOn);

            application.History.Add(new CourseApplicationHistory
            {
                ActionedByEmployeeId = editedByEmployeeId,
                Action = CourseApplicationAction.CaptureCorrected,
                PreviousStatus = application.Status,
                NewStatus = application.Status,
                Comment = "Changed: " + string.Join(", ", changed) + ".",
                ActionDate = now
            });

            return changed;
        }

        private static CourseApplicationHistory? CaptureEntry(CourseApplication application) =>
            application.History
                .FirstOrDefault(h => h.Action == CourseApplicationAction.CapturedAsApproved);

        private const string Middle = ", before the portal. Captured on the employee's behalf.";

        /// <summary>Reads the approver and notes back out of a capture comment.</summary>
        public static (string? ApprovedBy, string? Notes) ReadComment(string? comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
                return (null, null);

            var split = comment.IndexOf(Middle, StringComparison.Ordinal);

            if (split < 0)
                return (null, Clean(comment));

            var head = comment[..split];
            var by = head.IndexOf(" by ", StringComparison.Ordinal);

            return (
                by < 0 ? null : Clean(head[(by + 4)..]),
                Clean(comment[(split + Middle.Length)..]));
        }

        private static string? Clean(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string Comment(CaptureCourseViewModel model, DateTime approvedOn)
        {
            var comment = $"Approved on {approvedOn:d MMM yyyy}";

            if (!string.IsNullOrWhiteSpace(model.ApprovedBy))
                comment += $" by {model.ApprovedBy.Trim()}";

            comment += Middle;

            if (!string.IsNullOrWhiteSpace(model.Notes))
                comment += " " + model.Notes.Trim();

            return comment.Length > 1000 ? comment[..1000] : comment;
        }
    }
}
