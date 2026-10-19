using System.ComponentModel.DataAnnotations;

namespace HrPortal.Entities
{
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
