using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class TrainingMaterialCreateViewModel
    {
        [Required]
        [Display(Name = "Training Course")]
        public int TrainingCourseId { get; set; }

        [Required]
        [Display(Name = "File")]
        public IFormFile? File { get; set; }

        [Display(Name = "Description")]
        [StringLength(1000)]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Uploaded By")]
        public int UploadedByEmployeeId { get; set; }
    }
}
