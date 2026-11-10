using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    /// <summary>
    /// A course request that was approved before the portal, filled in on
    /// the employee's behalf from the paper form.
    /// </summary>
    public class CaptureCourseViewModel
    {
        [Required(ErrorMessage = "Choose the employee the course is for.")]
        [Display(Name = "Employee")]
        public int? EmployeeId { get; set; }

        [Required(ErrorMessage = "Say which programme was applied for.")]
        [StringLength(200)]
        [Display(Name = "Programme intended")]
        public string ProgramIntended { get; set; } = string.Empty;

        [Required(ErrorMessage = "Give the justification from the original request.")]
        [StringLength(2000)]
        [Display(Name = "Justification and relevance")]
        public string Justification { get; set; } = string.Empty;

        [Display(Name = "Had they sat for this programme before?")]
        public bool HasSatBefore { get; set; }

        [Display(Name = "If they had")]
        public CourseSitting Sitting { get; set; } = CourseSitting.FirstAttempt;

        [DataType(DataType.Date)]
        [Display(Name = "Target date to sit the exam")]
        public DateTime? TargetExamDate { get; set; }

        [Required(ErrorMessage = "Give the date the request was approved.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date approved")]
        public DateTime? ApprovedOn { get; set; }

        [StringLength(200)]
        [Display(Name = "Approved by")]
        public string? ApprovedBy { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "This course request was approved")]
        public bool Approved { get; set; }
    }
}
