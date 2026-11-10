using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.ViewModels;

namespace HrPortal.Tests.Courses
{
    public class CourseCaptureTests
    {
        private static readonly DateTime Today = new(2026, 11, 10);

        private static CaptureCourseViewModel Valid() => new()
        {
            EmployeeId = 7,
            ProgramIntended = "  CFA Level II ",
            Justification = " Needed for portfolio analysis ",
            ApprovedOn = new DateTime(2025, 3, 14),
            ApprovedBy = "Managing Consultant",
            Approved = true
        };

        [Fact]
        public void A_complete_approved_form_passes()
        {
            Assert.Empty(CourseCapture.Check(Valid(), Today));
        }

        [Fact]
        public void The_approved_box_must_be_ticked()
        {
            var model = Valid();
            model.Approved = false;

            Assert.Contains(CourseCapture.Check(model, Today),
                e => e.Field == nameof(model.Approved));
        }

        [Fact]
        public void The_approval_date_cannot_be_in_the_future()
        {
            var model = Valid();
            model.ApprovedOn = Today.AddDays(1);

            Assert.Contains(CourseCapture.Check(model, Today),
                e => e.Field == nameof(model.ApprovedOn));

            model.ApprovedOn = Today;

            Assert.Empty(CourseCapture.Check(model, Today));
        }

        [Fact]
        public void Having_sat_before_needs_a_sitting()
        {
            var model = Valid();
            model.HasSatBefore = true;

            Assert.Contains(CourseCapture.Check(model, Today),
                e => e.Field == nameof(model.Sitting));
        }

        [Fact]
        public void Build_makes_an_approved_application_for_the_employee_with_a_capture_record()
        {
            var now = Today.AddHours(9);

            var application = CourseCapture.Build(Valid(), "CA-2026-0042", 3, now);

            Assert.Equal("CA-2026-0042", application.ApplicationNumber);
            Assert.Equal(7, application.EmployeeId);
            Assert.Equal(CourseApplicationStatus.Approved, application.Status);
            Assert.Equal("CFA Level II", application.ProgramIntended);
            Assert.Equal("Needed for portfolio analysis", application.Justification);
            Assert.Equal(new DateTime(2025, 3, 14), application.CompletedAt);
            Assert.Equal(new DateTime(2025, 3, 14), application.SubmittedAt);
            Assert.Equal(now, application.CreatedAt);
            Assert.Null(application.PledgedAt);
            Assert.True(CourseApplicationWorkflowService.IsFinal(application.Status));

            var entry = Assert.Single(application.History);
            Assert.Equal(CourseApplicationAction.CapturedAsApproved, entry.Action);
            Assert.Equal(3, entry.ActionedByEmployeeId);
            Assert.Equal(CourseApplicationStatus.Approved, entry.NewStatus);
            Assert.Equal(now, entry.ActionDate);
            Assert.Contains("14 Mar 2025", entry.Comment);
            Assert.Contains("by Managing Consultant", entry.Comment);
        }

        [Fact]
        public void A_first_attempt_ignores_any_sitting_sent_with_it()
        {
            var model = Valid();
            model.Sitting = CourseSitting.Rewriting;

            Assert.Equal(CourseSitting.FirstAttempt,
                CourseCapture.Build(model, "CA-1", 3, Today).Sitting);
        }

        [Fact]
        public void The_edit_form_is_filled_from_the_captured_application()
        {
            var model = Valid();
            model.Notes = "Paper form in HR file 12";

            var form = CourseCapture.ToForm(CourseCapture.Build(model, "CA-1", 3, Today));

            Assert.Equal(7, form.EmployeeId);
            Assert.Equal("CFA Level II", form.ProgramIntended);
            Assert.Equal(new DateTime(2025, 3, 14), form.ApprovedOn);
            Assert.Equal("Managing Consultant", form.ApprovedBy);
            Assert.Equal("Paper form in HR file 12", form.Notes);
            Assert.True(form.Approved);
        }

        [Fact]
        public void A_capture_without_approver_or_notes_reads_back_empty()
        {
            var model = Valid();
            model.ApprovedBy = null;

            Assert.Equal((null, null),
                CourseCapture.ReadComment(CourseCapture.Build(model, "CA-1", 3, Today).History.Single().Comment));
        }

        [Fact]
        public void Apply_updates_the_application_and_records_what_changed()
        {
            var application = CourseCapture.Build(Valid(), "CA-1", 3, Today);
            var model = CourseCapture.ToForm(application);
            model.ProgramIntended = "CFA Level III";
            model.ApprovedOn = new DateTime(2025, 4, 1);
            model.Notes = "File 14";

            var changed = CourseCapture.Apply(model, application, 9, Today.AddHours(10));

            Assert.Equal(new[] { "programme", "approval date", "notes" }, changed);
            Assert.Equal("CFA Level III", application.ProgramIntended);
            Assert.Equal(new DateTime(2025, 4, 1), application.CompletedAt);
            Assert.Equal(CourseApplicationStatus.Approved, application.Status);

            var captured = application.History.Single(h => h.Action == CourseApplicationAction.CapturedAsApproved);
            Assert.Contains("1 Apr 2025", captured.Comment);
            Assert.EndsWith("File 14", captured.Comment);

            var edit = application.History.Single(h => h.Action == CourseApplicationAction.CaptureCorrected);
            Assert.Equal(9, edit.ActionedByEmployeeId);
            Assert.Equal("Changed: programme, approval date, notes.", edit.Comment);
        }

        [Fact]
        public void Saving_without_changes_records_nothing()
        {
            var application = CourseCapture.Build(Valid(), "CA-1", 3, Today);

            Assert.Empty(CourseCapture.Apply(CourseCapture.ToForm(application), application, 9, Today));
            Assert.Single(application.History);
        }

        [Theory]
        [InlineData("IT", true)]
        [InlineData("Information Technology", true)]
        [InlineData("Finance", false)]
        [InlineData(null, false)]
        public void Only_the_IT_department_counts_as_IT(string? department, bool expected)
        {
            Assert.Equal(expected, ItDepartment.Matches(department));
        }
    }
}
