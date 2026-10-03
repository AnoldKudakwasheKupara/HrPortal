using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class EmployeeDocumentEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [Required]
        [Display(Name = "Document Category")]
        public int DocumentCategoryId { get; set; }

        [Display(Name = "Current File")]
        public string CurrentFileName { get; set; } = string.Empty;

        [Display(Name = "Replace File")]
        public IFormFile? File { get; set; }

        [Display(Name = "Description")]
        [StringLength(1000)]
        public string? Description { get; set; }

        [Display(Name = "Expiry Date")]
        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }

        [Required]
        [Display(Name = "Uploaded By")]
        public int UploadedByEmployeeId { get; set; }
    }
}
