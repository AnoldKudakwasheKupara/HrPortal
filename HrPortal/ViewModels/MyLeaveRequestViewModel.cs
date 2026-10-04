using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    /// <summary>
    /// What an employee fills in for their own leave. There is no
    /// employee field: the request is always for the signed-in
    /// user, and no status field: the workflow decides that.
    /// </summary>
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
