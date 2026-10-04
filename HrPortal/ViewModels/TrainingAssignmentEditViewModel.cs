using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class TrainingAssignmentEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Training Course")]
        public int TrainingCourseId { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Completed")]
        public bool IsCompleted { get; set; }

        [Display(Name = "Completed On")]
        [DataType(DataType.Date)]
        public DateTime? CompletedAt { get; set; }
    }
}
