using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using Microsoft.AspNetCore.Http;

namespace HrPortal.Tests.Common
{
    public class OutstandingFixesTests
    {
        [Theory]
        [InlineData(LeaveRequestStatus.SupervisorReview, "your supervisor")]
        [InlineData(LeaveRequestStatus.Submitted, "HR")]
        [InlineData(LeaveRequestStatus.HRApproved, "the Managing Director")]
        public void A_leave_message_names_who_it_went_to(LeaveRequestStatus status, string expected)
        {
            Assert.Equal(expected, LeaveWorkflowService.WaitingOn(status));
        }

        [Theory]
        [InlineData(LoanApplicationStatus.SupervisorReview, "your supervisor")]
        [InlineData(LoanApplicationStatus.Submitted, "the Managing Director")]
        public void A_loan_message_names_who_it_went_to(LoanApplicationStatus status, string expected)
        {
            Assert.Equal(expected, LoanWorkflowService.WaitingOn(status));
        }

        [Theory]
        [InlineData(CourseApplicationStatus.SupervisorReview, "your supervisor")]
        [InlineData(CourseApplicationStatus.Submitted, "HR")]
        [InlineData(CourseApplicationStatus.HrApproved, "the Managing Consultant")]
        public void A_course_message_names_who_it_went_to(CourseApplicationStatus status, string expected)
        {
            Assert.Equal(expected, CourseApplicationWorkflowService.WaitingOn(status));
        }

        [Theory]
        [InlineData(1000, true)]
        [InlineData(250.50, true)]
        [InlineData(1000.01, false)]
        [InlineData(0, false)]
        public void A_loan_cannot_be_authorised_above_what_was_asked(decimal amount, bool allowed)
        {
            var application = new LoanApplication { AmountRequested = 1000m };

            Assert.Equal(allowed, LoanWorkflowService.CanAuthorise(application, amount));
        }

        [Fact]
        public void Pages_get_every_header_and_files_skip_only_the_content_policy()
        {
            var page = new HeaderDictionary();
            SecurityHeaders.Apply(page, "text/html; charset=utf-8");

            Assert.Contains("frame-ancestors 'self'", page["Content-Security-Policy"].ToString());
            Assert.Equal("SAMEORIGIN", page["X-Frame-Options"]);
            Assert.Equal("nosniff", page["X-Content-Type-Options"]);

            var pdf = new HeaderDictionary();
            SecurityHeaders.Apply(pdf, "application/pdf");

            Assert.False(pdf.ContainsKey("Content-Security-Policy"));
            Assert.Equal("nosniff", pdf["X-Content-Type-Options"]);
        }
    }
}
