namespace HrPortal.Entities
{
    public class LoanApplicationHistory
    {
        public int Id { get; set; }

        public int LoanApplicationId { get; set; }

        public int ActionedByEmployeeId { get; set; }

        public LoanApplicationAction Action { get; set; }

        public LoanApplicationStatus PreviousStatus { get; set; }

        public LoanApplicationStatus NewStatus { get; set; }

        public string? Comment { get; set; }

        public DateTime ActionDate { get; set; }

        public LoanApplication LoanApplication { get; set; } = null!;

        public Employee ActionedByEmployee { get; set; } = null!;
    }
}
