using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class TrainingFeedbackCreateViewModel
    {
        [Required]
        [Display(Name = "Training Assignment")]
        public int TrainingAssignmentId { get; set; }

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
