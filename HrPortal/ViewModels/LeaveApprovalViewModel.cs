using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class LeaveApprovalViewModel
    {
        public LeaveRequest Request { get; set; } = null!;

        /// <summary>
        /// The employee's balance for this leave type and year,
        /// shown so the approver can see what the request costs.
        /// Null when the leave type is not rationed.
        /// </summary>
        public LeaveBalance? Balance { get; set; }

        /// <summary>
        /// Whether the signed-in user may act at this stage.
        /// </summary>
        public bool CanActAsHr { get; set; }

        public bool CanActAsMd { get; set; }

        [Display(Name = "Comment")]
        [StringLength(2000)]
        public string? Comment { get; set; }
    }
}
