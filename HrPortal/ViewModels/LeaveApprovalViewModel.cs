using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class LeaveApprovalViewModel
    {
        public LeaveRequest Request { get; set; } = null!;

        public LeaveBalance? Balance { get; set; }

        public bool CanActAsHr { get; set; }

        public bool CanActAsMd { get; set; }

        [Display(Name = "Comment")]
        [StringLength(2000)]
        public string? Comment { get; set; }
    }
}
