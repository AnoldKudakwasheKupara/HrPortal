using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class TrainingCourseEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Course Title")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [StringLength(2000)]
        public string? Description { get; set; }

        [Display(Name = "Training Date")]
        [DataType(DataType.Date)]
        public DateTime? TrainingDate { get; set; }

        [Display(Name = "Trainer")]
        [StringLength(200)]
        public string? Trainer { get; set; }

        [Required]
        [Display(Name = "Created By")]
        public int CreatedByEmployeeId { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }
    }
}
