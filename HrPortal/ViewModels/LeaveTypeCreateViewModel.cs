using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class LeaveTypeCreateViewModel
    {
        [Required]
        [Display(Name = "Leave Type Name")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [StringLength(500)]
        public string? Description { get; set; }
    }
}
