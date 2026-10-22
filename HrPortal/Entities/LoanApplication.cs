using HrPortal.Security;

namespace HrPortal.Entities
{
    /// <summary>
    /// A staff loan application. The portal records the request and
    /// the Managing Director's decision; it does not run the salary
    /// deduction, which stays with payroll.
    /// </summary>
    public class LoanApplication : IEmployeeOwned
    {
        public int Id { get; set; }

        public string ApplicationNumber { get; set; } = string.Empty;

        public int EmployeeId { get; set; }

        public LoanApplicationStatus Status { get; set; }
            = LoanApplicationStatus.Draft;

        /// <summary>What the applicant asked for.</summary>
        public decimal AmountRequested { get; set; }

        /// <summary>
        /// What was sanctioned, which need not match what was asked
        /// for. Null until the application is approved.
        /// </summary>
        public decimal? AmountAuthorised { get; set; }

        /// <summary>
        /// The number of equal monthly instalments the applicant
        /// authorises the company to deduct.
        /// </summary>
        public int RepaymentMonths { get; set; }

        /// <summary>
        /// The "with effect from" date on the form: when deductions
        /// start. Set when the loan is sanctioned.
        /// </summary>
        public DateTime? RepaymentStartDate { get; set; }

        public string Purpose { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public Employee Employee { get; set; } = null!;

        public ICollection<LoanApplicationAttachment> Attachments
        { get; set; } = new List<LoanApplicationAttachment>();

        public ICollection<LoanApplicationHistory> History
        { get; set; } = new List<LoanApplicationHistory>();

        /// <summary>
        /// The instalment the applicant is committing to, from the
        /// amount sanctioned where there is one and the amount asked
        /// for otherwise. Null when there is nothing to divide.
        /// </summary>
        public decimal? MonthlyInstalment =>
            RepaymentMonths <= 0
                ? null
                : Math.Round(
                    (AmountAuthorised ?? AmountRequested)
                        / RepaymentMonths,
                    2,
                    MidpointRounding.AwayFromZero);
    }
}
