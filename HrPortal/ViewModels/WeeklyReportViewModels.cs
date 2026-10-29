using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class WeeklyReportCreateViewModel
    {
        /// <summary>
        /// Any day in the week; the report covers Monday to Friday of
        /// the week it falls in.
        /// </summary>
        [Required]
        [Display(Name = "Week")]
        [DataType(DataType.Date)]
        public DateTime PeriodStart { get; set; }
    }

    public class WeeklyReportSectionEditViewModel
    {
        public int Id { get; set; }

        public int Order { get; set; }

        [StringLength(200)]
        [Display(Name = "Piece of work")]
        public string? Heading { get; set; }

        [StringLength(200)]
        public string? Workstream { get; set; }

        public WorkstreamStatus WorkstreamStatus { get; set; }

        [StringLength(8000)]
        public string? Body { get; set; }

        public string? PreviousBody { get; set; }

        /// <summary>Ticked to take the item out of the report.</summary>
        public bool Remove { get; set; }
    }

    public class WeeklyReportEditViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Period start")]
        [DataType(DataType.Date)]
        public DateTime PeriodStart { get; set; }

        [Display(Name = "Period end")]
        [DataType(DataType.Date)]
        public DateTime PeriodEnd { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        public string? OwnerDepartment { get; set; }

        [Display(Name = "Summary of the week")]
        [StringLength(4000)]
        public string? ExecutiveOverview { get; set; }

        public WeeklyReportStatus Status { get; set; }

        /// <summary>
        /// The IT department's headings are fixed and filled from the
        /// activity log; anybody else names their own pieces of work.
        /// </summary>
        public bool FixedHeadings { get; set; }

        public List<WeeklyReportSectionEditViewModel> Sections { get; set; }
            = new();

        [Display(Name = "Priorities for next week")]
        public string? PrioritiesText { get; set; }
    }

    public enum WeeklyProgress
    {
        NotStarted,
        Draft,
        Submitted
    }

    /// <summary>One person's line on the weekly overview.</summary>
    public class WeeklyOverviewRow
    {
        public int EmployeeId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? JobTitle { get; set; }

        public string? Department { get; set; }

        public string? Supervisor { get; set; }

        public int? ReportId { get; set; }

        public WeeklyProgress Progress { get; set; }

        public DateTime? SubmittedOn { get; set; }

        public int Items { get; set; }

        public int Completed { get; set; }

        public string? Summary { get; set; }
    }

    public class WeeklyOverviewViewModel
    {
        public DateTime WeekStart { get; set; }

        public DateTime WeekEnd => WeekStart.AddDays(4);

        /// <summary>Everybody, for the MD; their own team otherwise.</summary>
        public bool AllStaff { get; set; }

        public List<WeeklyOverviewRow> Rows { get; set; } = new();

        public int Submitted => Rows.Count(r => r.Progress == WeeklyProgress.Submitted);

        public int Drafts => Rows.Count(r => r.Progress == WeeklyProgress.Draft);

        public int NotStarted => Rows.Count(r => r.Progress == WeeklyProgress.NotStarted);
    }
}
