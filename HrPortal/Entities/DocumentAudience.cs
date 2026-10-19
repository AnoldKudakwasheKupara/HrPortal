using System.ComponentModel.DataAnnotations;

namespace HrPortal.Entities
{
    public enum DocumentAudience
    {
        [Display(Name = "Everyone")]
        Everyone,

        [Display(Name = "Managers only")]
        Managers,

        [Display(Name = "HR and administrators only")]
        HrAndAdministrators
    }
}
