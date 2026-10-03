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

        [Required]
        [Display(Name = "Default Days Per Year")]
        [Range(0, 365)]
        public decimal DefaultDays { get; set; }

        [Display(Name = "Requires Approval")]
        public bool RequiresApproval { get; set; } = true;
    }
}
