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

        /// <summary>
        /// Who is delivering it. A member of staff is picked from
        /// the list; anybody else is typed, because an outside
        /// provider is not on it.
        /// </summary>
        [Display(Name = "Trainer (a member of staff)")]
        public int? TrainerEmployeeId { get; set; }

        [Display(Name = "Or an outside provider")]
        [StringLength(200)]
        public string? Trainer { get; set; }

        /// <summary>
        /// When the trainer has to have posted the material.
        /// </summary>
        [Display(Name = "Trainer posts the content by")]
        [DataType(DataType.Date)]
        public DateTime? MaterialsDueDate { get; set; }

        [Required]
        [Display(Name = "Created By")]
        public int CreatedByEmployeeId { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }
    }
}
