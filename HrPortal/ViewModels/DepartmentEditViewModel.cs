using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class DepartmentEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Department Name")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [StringLength(500)]
        public string? Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }
    }
}
