using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class TrainingMaterialEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Training Course")]
        public int TrainingCourseId { get; set; }

        [Display(Name = "Current File")]
        public string CurrentFileName { get; set; } = string.Empty;

        [Display(Name = "Replace File")]
        public IFormFile? File { get; set; }

        [Display(Name = "Description")]
        [StringLength(1000)]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Uploaded By")]
        public int UploadedByEmployeeId { get; set; }
    }
}
