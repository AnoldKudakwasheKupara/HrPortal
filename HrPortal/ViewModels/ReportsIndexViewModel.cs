namespace HrPortal.ViewModels
{
    /// <summary>
    /// Headline figures for the reports hub, so the landing page says
    /// something rather than being a list of links.
    /// </summary>
    public class ReportsIndexViewModel
    {
        public bool ShowLeave { get; set; }

        public bool ShowBalances { get; set; }

        public bool ShowTraining { get; set; }

        public bool ShowPeople { get; set; }

        public bool ShowDocuments { get; set; }

        public bool ShowSystem { get; set; }

        /// <summary>
        /// Requisitions are seen by the reviewer and the MD, which is
        /// a different pair of roles from the system reports.
        /// </summary>
        public bool ShowProcurement { get; set; }

        public int LeaveRequests { get; set; }

        public int LeaveAwaitingDecision { get; set; }

        public decimal ApprovedDaysThisYear { get; set; }

        public int EmployeesOnLeaveThisYear { get; set; }

        public decimal RemainingDays { get; set; }

        public int BalancesFullyUsed { get; set; }

        public int TrainingAssignments { get; set; }

        public int TrainingOverdue { get; set; }

        public int Courses { get; set; }

        public double? AverageCourseRating { get; set; }

        public int ActiveEmployees { get; set; }

        public int EmployeesWithoutLogin { get; set; }

        public int Documents { get; set; }

        public int EmployeesWithoutDocuments { get; set; }

        public int Vendors { get; set; }

        public int VendorsWithoutContact { get; set; }

        public int Assets { get; set; }

        public int AssetsNeedingAttention { get; set; }

        public int RequisitionsAwaiting { get; set; }

        public decimal RequisitionsApprovedValue { get; set; }

        public int ItActivity30Days { get; set; }

        public int ItActivityOutstanding { get; set; }

        public int MaintenanceOpen { get; set; }

        public int MaintenanceOverdue { get; set; }

        public int Licences { get; set; }

        public int LicencesDueSoon { get; set; }

        public int AuditEntries30Days { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }
}
