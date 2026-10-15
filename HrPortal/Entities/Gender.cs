using System.ComponentModel.DataAnnotations;

namespace HrPortal.Entities
{
    public enum Gender
    {
        [Display(Name = "Not stated")]
        Unspecified,

        [Display(Name = "Female")]
        Female,

        [Display(Name = "Male")]
        Male,

        [Display(Name = "Other")]
        Other
    }
}
