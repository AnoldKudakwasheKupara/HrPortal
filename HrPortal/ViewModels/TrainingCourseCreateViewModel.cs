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
        /// Assigns the course to everybody on the staff list as it
        /// stands. A one-off: somebody who joins next month is not
        /// caught by it and has to be assigned the course like
        /// anybody else.
        /// </summary>
        [Display(Name = "Assign this course to everyone")]
        public bool AssignCompanyWide { get; set; }

        [Display(Name = "Due by")]
        [DataType(DataType.Date)]
        public DateTime? AssignmentDueDate { get; set; }
    }
}
