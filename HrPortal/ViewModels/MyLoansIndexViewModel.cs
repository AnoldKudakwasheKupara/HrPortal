using HrPortal.Entities;
using HrPortal.Services;

namespace HrPortal.ViewModels
{
    public class MyLoansIndexViewModel
    {
        public List<LoanApplication> Applications { get; set; } = new();

        public string Show { get; set; } = RequestGroups.All;

        public PagedResult<LoanApplication> Page { get; set; } = new();

        public List<ListTab> Tabs { get; set; } = new();

        public int InProgress { get; set; }

        public int Approved { get; set; }

        /// <summary>Approved loans whose repayments are running this month.</summary>
        public int Repaying { get; set; }

        /// <summary>
        /// Approved, and today falls within its repayment months. One with
        /// no start date yet is treated as about to start.
        /// </summary>
        public static bool IsRepaying(LoanApplication loan, DateTime today)
        {
            if (loan.Status != LoanApplicationStatus.Approved)
                return false;

            if (loan.RepaymentStartDate == null)
                return true;

            var start = new DateTime(loan.RepaymentStartDate.Value.Year, loan.RepaymentStartDate.Value.Month, 1);
            var month = new DateTime(today.Year, today.Month, 1);

            return month >= start && month < start.AddMonths(loan.RepaymentMonths);
        }

        public static MyLoansIndexViewModel Build(
            List<LoanApplication> applications, string? show, int page, DateTime today)
        {
            static bool Final(LoanApplication l) => LoanWorkflowService.IsFinal(l.Status);
            static bool IsApproved(LoanApplication l) => l.Status == LoanApplicationStatus.Approved;

            var group = RequestGroups.Normalise(show);

            return new MyLoansIndexViewModel
            {
                Applications = applications,
                Show = group,
                Page = RequestGroups.Page(applications, Final, IsApproved, group, page),
                Tabs = RequestGroups.Tabs(applications, Final, IsApproved, "All Applications"),
                InProgress = applications.Count(l => !Final(l)),
                Approved = applications.Count(IsApproved),
                Repaying = applications.Count(l => IsRepaying(l, today))
            };
        }
    }
}
