using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class EmployeeDocumentCreateViewModel
    {
        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [Required]
        [Display(Name = "Document Category")]
        public int DocumentCategoryId { get; set; }

        [Required]
        [Display(Name = "File")]
        public IFormFile? File { get; set; }

        [Display(Name = "Description")]
        [StringLength(1000)]
        public string? Description { get; set; }
    }
}
