using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class PositionViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Job Title")]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [StringLength(500)]
        public string? Description { get; set; }

        [Display(Name = "Department")]
        public int? DepartmentId { get; set; }

        [Display(Name = "Budgeted Posts")]
        [Range(0, 10000)]
        public int? Establishment { get; set; }

        [Display(Name = "In Use")]
        public bool IsActive { get; set; } = true;
    }
}
