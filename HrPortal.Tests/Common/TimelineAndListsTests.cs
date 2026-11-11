using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.ViewModels;

namespace HrPortal.Tests.Common
{
    public class TimelineAndListsTests
    {
        private static readonly DateTime Today = new(2026, 11, 11);

        [Theory]
        [InlineData("ApprovedByHR", "Approved by HR")]
        [InlineData("RejectedByMD", "Rejected by MD")]
        [InlineData("ReturnedBySupervisor", "Returned by the supervisor")]
        [InlineData("ApprovedByHr", "Approved by HR")]
        [InlineData("ApprovedByManagingConsultant", "Approved by the Managing Consultant")]
        [InlineData("CapturedAsApproved", "Captured as approved")]
        [InlineData("Submitted", "Submitted")]
        public void History_actions_read_as_plain_words(string action, string expected)
        {
            Assert.Equal(expected, Timeline.Describe(action));
        }

        [Fact]
        public void The_timeline_is_newest_first_and_coloured_by_outcome()
        {
            var boss = new Employee { FirstName = "Bea", LastName = "Boss" };

            var history = new[]
            {
                new LeaveRequestHistory { Action = LeaveRequestAction.Submitted, ActionDate = Today, ActionedByEmployee = boss },
                new LeaveRequestHistory { Action = LeaveRequestAction.RejectedByHR, ActionDate = Today.AddDays(1), ActionedByEmployee = boss, Comment = "  " }
            };

            var steps = Timeline.Of(history);

            Assert.Equal("Rejected by HR", steps[0].Text);
            Assert.Equal("is-red", steps[0].Tone);
            Assert.Null(steps[0].Comment);
            Assert.Equal("Bea Boss", steps[1].Who);
            Assert.Equal("is-blue", steps[1].Tone);
        }

        [Fact]
        public void A_loan_is_being_repaid_only_within_its_months()
        {
            var loan = new LoanApplication
            {
                Status = LoanApplicationStatus.Approved,
                RepaymentStartDate = new DateTime(2026, 9, 15),
                RepaymentMonths = 3
            };

            Assert.True(MyLoansIndexViewModel.IsRepaying(loan, Today));
            Assert.False(MyLoansIndexViewModel.IsRepaying(loan, new DateTime(2026, 12, 1)));
            Assert.False(MyLoansIndexViewModel.IsRepaying(loan, new DateTime(2026, 8, 31)));

            loan.Status = LoanApplicationStatus.Submitted;
            Assert.False(MyLoansIndexViewModel.IsRepaying(loan, Today));
        }

        [Fact]
        public void Training_groups_outstanding_completed_and_feedback_due()
        {
            var assignments = new List<TrainingAssignment>
            {
                new() { IsCompleted = false, DueDate = Today.AddDays(-1) },
                new() { IsCompleted = false, DueDate = Today },
                new() { IsCompleted = true },
                new() { IsCompleted = true, Feedback = new TrainingFeedback { Rating = 4 } }
            };

            var model = MyTrainingIndexViewModel.Build(assignments, "feedback", 1, Today);

            Assert.Equal(2, model.OutstandingCount);
            Assert.Equal(1, model.Overdue);
            Assert.Equal(2, model.CompletedCount);
            Assert.Equal(1, model.FeedbackDueCount);
            Assert.Equal(1, model.Page.TotalCount);
        }
    }
}
