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
