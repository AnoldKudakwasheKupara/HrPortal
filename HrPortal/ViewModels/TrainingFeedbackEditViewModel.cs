using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class TrainingFeedbackEditViewModel
    {
        public int Id { get; set; }

        public int TrainingAssignmentId { get; set; }

        [Display(Name = "Training Assignment")]
        public string AssignmentSummary { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Rating")]
        [Range(
            1,
            5,
            ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; }

        [Display(Name = "Comments")]
        [StringLength(2000)]
        public string? Comments { get; set; }
    }
}
