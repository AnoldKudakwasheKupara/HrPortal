using System.ComponentModel.DataAnnotations;

namespace HrPortal.Entities
{
    public class ApprovalConfiguration
    {
        public int Id { get; set; }

        public ApprovalRole Role { get; set; }

        public int EmployeeId { get; set; }

        public bool IsActive { get; set; }

        public Employee Employee { get; set; } = null!;
    }

    public enum ApprovalRole
    {
        [Display(Name = "HR Officer")]
        HROfficer,

        [Display(Name = "Managing Director")]
        MD
    }
}
