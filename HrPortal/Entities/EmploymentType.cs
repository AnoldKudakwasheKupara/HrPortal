using System.ComponentModel.DataAnnotations;

namespace HrPortal.Entities
{
    /// <summary>
    /// The basis someone is employed on. Separate from whether they are
    /// currently employed, which stays on IsActive.
    /// </summary>
    public enum EmploymentType
    {
        [Display(Name = "Permanent")]
        Permanent,

        [Display(Name = "Fixed term")]
        FixedTerm,

        [Display(Name = "Probation")]
        Probation,

        [Display(Name = "Intern")]
        Intern,

        [Display(Name = "Contractor")]
        Contractor
    }
}
