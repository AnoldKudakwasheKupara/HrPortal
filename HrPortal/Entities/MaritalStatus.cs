using System.ComponentModel.DataAnnotations;

namespace HrPortal.Entities
{
    public enum MaritalStatus
    {
        [Display(Name = "Not stated")]
        Unspecified,

        [Display(Name = "Single")]
        Single,

        [Display(Name = "Married")]
        Married,

        [Display(Name = "Divorced")]
        Divorced,

        [Display(Name = "Widowed")]
        Widowed
    }
}
