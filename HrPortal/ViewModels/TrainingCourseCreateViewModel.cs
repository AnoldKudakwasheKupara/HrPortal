using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class TrainingCourseCreateViewModel
    {
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

        [Required]
        [Display(Name = "Created By")]
        public int CreatedByEmployeeId { get; set; }

        /// <summary>
        /// When everybody has to have completed it by. Optional: a
        /// course with no date is simply outstanding until it is
        /// done.
        /// </summary>
        [Display(Name = "Due by")]
        [DataType(DataType.Date)]
        public DateTime? AssignmentDueDate { get; set; }
    }
}
