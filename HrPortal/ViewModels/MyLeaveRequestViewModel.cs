using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class MyLeaveRequestViewModel
    {
        public int Id { get; set; }

        public string? RequestNumber { get; set; }

        [Required]
        [Display(Name = "Leave Type")]
        public int LeaveTypeId { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today;

        [Display(Name = "Reason")]
        [StringLength(1000)]
        public string? Reason { get; set; }
    }
}
