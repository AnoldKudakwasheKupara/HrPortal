using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    /// <summary>
    /// One filter shape shared by every report. Each report binds only
    /// the fields it offers, so the query string stays consistent and
    /// a filter means the same thing wherever it appears.
    /// </summary>
    public class ReportFilter
    {
        [DataType(DataType.Date)]
        [Display(Name = "From")]
        public DateTime? From { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "To")]
        public DateTime? To { get; set; }

        [Display(Name = "Department")]
        public int? DepartmentId { get; set; }

        [Display(Name = "Employee")]
        public int? EmployeeId { get; set; }

        [Display(Name = "Leave type")]
        public int? LeaveTypeId { get; set; }

        [Display(Name = "Status")]
        public LeaveRequestStatus? Status { get; set; }

        [Display(Name = "Course")]
        public int? TrainingCourseId { get; set; }

        [Display(Name = "Category")]
        public int? DocumentCategoryId { get; set; }

        [Display(Name = "Year")]
        public int? Year { get; set; }

        [Display(Name = "Company")]
        public string? Company { get; set; }

        [Display(Name = "Condition")]
        public AssetCondition? Condition { get; set; }

        // Not named "Action": that collides with the MVC route value
        // of the same name and the filter would bind to the action
        // being executed instead of the audit action chosen.
        [Display(Name = "Action")]
        public string? AuditAction { get; set; }

        [Display(Name = "Vendor")]
        public int? VendorId { get; set; }

        // Spelled the British way the portal uses, which also keeps it
        // clear of the LicenseType enum name.
        [Display(Name = "Type")]
        public LicenseType? LicenceType { get; set; }

        /// <summary>
        /// A named slice a report defines for itself, such as
        /// "expiring" or "overdue". Keeps one-off toggles out of this
        /// shared shape.
        /// </summary>
        [Display(Name = "Show")]
        public string? Show { get; set; }

        [Display(Name = "Status")]
        public RequisitionStatus? RequisitionStatus { get; set; }

        [Display(Name = "Category")]
        public ItActivityCategory? ActivityCategory { get; set; }

        [Display(Name = "Status")]
        public ItActivityStatus? ActivityStatus { get; set; }

        [Display(Name = "Include inactive")]
        public bool IncludeInactive { get; set; }
    }

    public class ReportStat
    {
        public ReportStat(string label, string value, string? note = null)
        {
            Label = label;
            Value = value;
            Note = note;
        }

        public string Label { get; }

        public string Value { get; }

        public string? Note { get; }
    }

    /// <summary>
    /// A secondary cut of the same data, shown as a short ranked list
    /// beside the table so a report answers "how is this distributed"
    /// as well as "what are the rows".
    /// </summary>
    public class ReportBreakdown
    {
        public ReportBreakdown(string title, string unit = "")
        {
            Title = title;
            Unit = unit;
        }

        public string Title { get; }

        public string Unit { get; }

        public List<ReportBreakdownItem> Items { get; } = new();

        public decimal Max =>
            Items.Count == 0 ? 0 : Items.Max(i => i.Value);

        public decimal Total => Items.Sum(i => i.Value);

        public ReportBreakdown Add(
            string label,
            decimal value,
            string? tone = null)
        {
            Items.Add(new ReportBreakdownItem(label, value, tone));
            return this;
        }
    }

    public class ReportBreakdownItem
    {
        public ReportBreakdownItem(
            string label,
            decimal value,
            string? tone)
        {
            Label = label;
            Value = value;
            Tone = tone;
        }

        public string Label { get; }

        public decimal Value { get; }

        /// <summary>good, warning or critical; null for the default.</summary>
        public string? Tone { get; }
    }

    public class ReportColumnTotal
    {
        public ReportColumnTotal(int columnIndex, string value)
        {
            ColumnIndex = columnIndex;
            Value = value;
        }

        public int ColumnIndex { get; }

        public string Value { get; }
    }

    public class ReportViewModel<TRow>
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// What the filter currently covers, written out so a printed
        /// or exported report says what it is showing.
        /// </summary>
        public string Coverage { get; set; } = string.Empty;

        public List<ReportBreakdown> Breakdowns { get; set; } = new();

        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// The action to post the filter form back to, and to export
        /// from. Keeps the shared filter partial generic.
        /// </summary>
        public string Action { get; set; } = string.Empty;

        public ReportFilter Filter { get; set; } = new();

        public List<ReportStat> Stats { get; set; } = new();

        public List<TRow> Rows { get; set; } = new();
    }

    public class LeaveRequestReportRow
    {
        public string RequestNumber { get; set; } = string.Empty;
        public string Employee { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string LeaveType { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Days { get; set; }
        public LeaveRequestStatus Status { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public double? TurnaroundDays { get; set; }
    }

    public class LeaveBalanceReportRow
    {
        public string Employee { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string LeaveType { get; set; } = string.Empty;
        public int Year { get; set; }
        public decimal Allocated { get; set; }
        public decimal Used { get; set; }
        public decimal Pending { get; set; }
        public decimal Remaining { get; set; }
    }

    public class AbsenceReportRow
    {
        public string Employee { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public decimal ApprovedDays { get; set; }
        public int ApprovedRequests { get; set; }
        public decimal PendingDays { get; set; }
        public int RejectedRequests { get; set; }
        public DateTime? LastLeaveEnd { get; set; }
    }

    public class TrainingReportRow
    {
        public string Course { get; set; } = string.Empty;
        public string Employee { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsOverdue { get; set; }
        public int? Rating { get; set; }
    }

    public class TrainingCourseReportRow
    {
        public string Course { get; set; } = string.Empty;
        public string? Trainer { get; set; }
        public DateTime? TrainingDate { get; set; }
        public int Assigned { get; set; }
        public int Completed { get; set; }
        public int Overdue { get; set; }
        public decimal CompletionRate { get; set; }
        public double? AverageRating { get; set; }
        public int Responses { get; set; }
    }

    public class EmployeeReportRow
    {
        public string EmployeeNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime DateJoined { get; set; }
        public int YearsOfService { get; set; }
        public bool IsActive { get; set; }
        public bool HasLogin { get; set; }
        public string Roles { get; set; } = string.Empty;
    }

    public class DocumentReportRow
    {
        public string Employee { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
    }

    public class AssetReportRow
    {
        public string Company { get; set; } = string.Empty;
        public string DeviceModel { get; set; } = string.Empty;
        public string? Processor { get; set; }
        public string? SerialNumber { get; set; }
        public DateTime? ProcuredOn { get; set; }
        public AssetCondition Condition { get; set; }
        public bool IsInUse { get; set; }
        public string AssignedTo { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class VendorReportRow
    {
        public string Name { get; set; } = string.Empty;
        public string ProductOrService { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? ContactPerson { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; }
    }

    public class LicenseReportRow
    {
        public string Name { get; set; } = string.Empty;
        public LicenseType Type { get; set; }
        public LicenseStatus Status { get; set; }
        public string? Reference { get; set; }
        public string Vendor { get; set; } = string.Empty;
        public string HeldBy { get; set; } = string.Empty;
        public int? Seats { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public decimal? Cost { get; set; }
        public bool AutoRenews { get; set; }
        public int Renewals { get; set; }
        public DateTime? LastRenewedOn { get; set; }

        /// <summary>
        /// Negative once it has lapsed, so one field sorts and reads
        /// for both cases.
        /// </summary>
        public int DaysToExpiry { get; set; }
    }

    public class MaintenanceReportRow
    {
        public string Asset { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string HeldBy { get; set; } = string.Empty;
        public MaintenanceType Type { get; set; }
        public MaintenanceStatus Status { get; set; }
        public DateTime ScheduledFor { get; set; }
        public DateTime? CompletedOn { get; set; }
        public string DoneBy { get; set; } = string.Empty;
        public decimal? Cost { get; set; }
        public string? Notes { get; set; }

        /// <summary>
        /// Scheduled, still open and already past its date.
        /// </summary>
        public bool IsOverdue { get; set; }

        /// <summary>
        /// Days from the scheduled date to completion, for work that
        /// has been finished.
        /// </summary>
        public int? DaysToComplete { get; set; }
    }

    public class RequisitionReportRow
    {
        public string RequisitionNumber { get; set; } = string.Empty;
        public string RaisedBy { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public RequisitionStatus Status { get; set; }
        public string Vendor { get; set; } = string.Empty;
        public int Lines { get; set; }
        public decimal EstimatedTotal { get; set; }
        public bool RequiresMdApproval { get; set; }
        public DateTime? NeededBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Days from submission to the final decision, for the ones
        /// that have been decided.
        /// </summary>
        public double? TurnaroundDays { get; set; }
    }

    public class ItActivityReportRow
    {
        public DateTime LoggedOn { get; set; }
        public ItActivityCategory Category { get; set; }
        public ItActivityStatus Status { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string? ActionTaken { get; set; }
        public string RaisedBy { get; set; } = string.Empty;
        public string Asset { get; set; } = string.Empty;
        public bool IsNotable { get; set; }
        public DateTime? ResolvedOn { get; set; }
        public string? LoggedBy { get; set; }

        /// <summary>
        /// Days from being logged to being resolved, for the ones that
        /// have been closed.
        /// </summary>
        public int? DaysToResolve { get; set; }
    }

    public class AuditReportRow
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string Actor { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
