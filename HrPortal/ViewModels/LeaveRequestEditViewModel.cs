using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class LeaveRequestEditViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Request Number")]
        public string RequestNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [Required]
        [Display(Name = "Leave Type")]
        public int LeaveTypeId { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Display(Name = "Reason")]
        [StringLength(1000)]
        public string? Reason { get; set; }

        [Required]
        [Display(Name = "Status")]
        public LeaveRequestStatus Status { get; set; }

        [Display(Name = "Comment")]
        [StringLength(2000)]
        public string? Comment { get; set; }
    }
}
