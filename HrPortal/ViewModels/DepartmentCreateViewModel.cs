using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class DepartmentCreateViewModel
    {
        [Required]
        [Display(Name = "Department Name")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [StringLength(500)]
        public string? Description { get; set; }
    }
}
