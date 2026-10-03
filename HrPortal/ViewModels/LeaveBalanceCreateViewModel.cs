using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class LeaveBalanceCreateViewModel
    {
        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [Required]
        [Display(Name = "Leave Type")]
        public int LeaveTypeId { get; set; }

        [Required]
        [Display(Name = "Year")]
        [Range(2000, 2100)]
        public int Year { get; set; } = DateTime.Today.Year;

        [Required]
        [Display(Name = "Allocated Days")]
        [Range(0, 365)]
        public decimal AllocatedDays { get; set; }

        [Required]
        [Display(Name = "Used Days")]
        [Range(0, 365)]
        public decimal UsedDays { get; set; }

        [Required]
        [Display(Name = "Pending Days")]
        [Range(0, 365)]
        public decimal PendingDays { get; set; }
    }
}
