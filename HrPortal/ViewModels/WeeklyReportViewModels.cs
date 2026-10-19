using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class WeeklyReportCreateViewModel
    {
        [Required]
        [Display(Name = "Period start")]
        [DataType(DataType.Date)]
        public DateTime PeriodStart { get; set; }

        [Required]
        [Display(Name = "Period end")]
        [DataType(DataType.Date)]
        public DateTime PeriodEnd { get; set; }

        [Required]
        [Display(Name = "Prepared by")]
        [StringLength(200)]
        public string PreparedBy { get; set; } = string.Empty;
    }

    public class WeeklyReportSectionEditViewModel
    {
        public int Id { get; set; }

        public int Order { get; set; }

        [Required]
        [StringLength(200)]
        public string Heading { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Workstream { get; set; }

        public WorkstreamStatus WorkstreamStatus { get; set; }

        [StringLength(8000)]
        public string? Body { get; set; }

        public string? PreviousBody { get; set; }
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

        [Required]
        [Display(Name = "Prepared by")]
        [StringLength(200)]
        public string PreparedBy { get; set; } = string.Empty;

        [Display(Name = "Executive overview")]
        [StringLength(4000)]
        public string? ExecutiveOverview { get; set; }

        public WeeklyReportStatus Status { get; set; }

        public List<WeeklyReportSectionEditViewModel> Sections { get; set; }
            = new();

        [Display(Name = "Priorities for the next reporting period")]
        public string? PrioritiesText { get; set; }
    }
}
