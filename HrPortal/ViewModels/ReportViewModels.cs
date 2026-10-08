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

    public class ReportViewModel<TRow>
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

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
