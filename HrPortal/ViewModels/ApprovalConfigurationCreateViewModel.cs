using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class ApprovalConfigurationCreateViewModel
    {
        [Required]
        [Display(Name = "Approval Role")]
        public ApprovalRole Role { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }
    }
}
