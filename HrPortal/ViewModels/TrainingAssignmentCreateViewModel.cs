using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class TrainingAssignmentCreateViewModel
    {
        [Required]
        [Display(Name = "Training Course")]
        public int TrainingCourseId { get; set; }

        [Required]
        [Display(Name = "Employees")]
        public List<int> EmployeeIds { get; set; } = new();
    }
}
