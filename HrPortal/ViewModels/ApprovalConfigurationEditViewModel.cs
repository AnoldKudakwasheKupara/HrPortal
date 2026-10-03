using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class ApprovalConfigurationEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Approval Role")]
        public ApprovalRole Role { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }
    }
}
