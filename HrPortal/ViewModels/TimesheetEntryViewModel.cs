using HrPortal.Entities;
using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class TimesheetEntryViewModel
    {
        public int Id { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime WorkDate { get; set; } = DateTime.Today;

        [Display(Name = "Started")]
        public TimeOnly? StartTime { get; set; }

        [Display(Name = "Finished")]
        public TimeOnly? EndTime { get; set; }

        [Range(0.05, 24, ErrorMessage = "Hours must be between 0.05 and 24.")]
        [Display(Name = "Hours")]
        public decimal? Hours { get; set; }

        [Required]
        [Display(Name = "Type of work")]
        public WorkCategory Category { get; set; } = WorkCategory.Support;

        [Required(ErrorMessage = "Say what the task was.")]
        [StringLength(200)]
        [Display(Name = "Task")]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        [Display(Name = "Details")]
        public string? Details { get; set; }

        [StringLength(100)]
        [Display(Name = "Ticket / reference")]
        public string? Reference { get; set; }

        [Display(Name = "Asset worked on")]
        public int? AssetId { get; set; }
    }
}
