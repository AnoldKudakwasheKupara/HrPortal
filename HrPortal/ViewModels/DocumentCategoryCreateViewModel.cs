using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class DocumentCategoryCreateViewModel
    {
        [Required]
        [Display(Name = "Category Name")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [StringLength(500)]
        public string? Description { get; set; }

        [Display(Name = "HR only")]
        public bool IsRestricted { get; set; }

    }
}
