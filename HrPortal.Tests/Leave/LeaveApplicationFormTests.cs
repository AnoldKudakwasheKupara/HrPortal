using HrPortal.Entities;
using HrPortal.Services;

namespace HrPortal.Tests.Leave
{
    public class LeaveApplicationFormTests
    {
        private static readonly DateTime Today = new(2026, 11, 17);

        [Fact]
        public void The_printed_form_says_when_there_is_no_balance()
        {
            var request = new LeaveRequest { LeaveType = new LeaveType { Name = "Annual Leave" } };

            Assert.Equal(
                "No Annual Leave balance has been set up, so HR checks the days.",
                LeaveApplicationPdfService.BalanceNote(request, null, Today));
        }

        [Fact]
        public void The_printed_form_dates_the_figures_and_counts_days_awaiting_approval()
        {
            var request = new LeaveRequest { LeaveType = new LeaveType { Name = "Annual Leave" } };

            Assert.Equal(
                "Days as they stood on 17 November 2026.",
                LeaveApplicationPdfService.BalanceNote(request, new LeaveBalance(), Today));

            Assert.Equal(
                "Days as they stood on 17 November 2026. The balance already takes off 3 days awaiting approval.",
                LeaveApplicationPdfService.BalanceNote(request, new LeaveBalance { PendingDays = 3 }, Today));
        }
    }
}
