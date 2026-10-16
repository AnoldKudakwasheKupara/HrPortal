using System.ComponentModel.DataAnnotations;

namespace HrPortal.Entities
{
    public enum PolicyStatus
    {
        [Display(Name = "Draft")]
        Draft,

        [Display(Name = "In force")]
        Active,

        [Display(Name = "Replaced")]
        Superseded,

        [Display(Name = "Withdrawn")]
        Withdrawn
    }
}
