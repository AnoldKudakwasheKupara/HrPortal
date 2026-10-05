using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class RoleCreateViewModel
    {
        [Required]
        [StringLength(
            50,
            MinimumLength = 2,
            ErrorMessage = "Role name must be between 2 and 50 characters.")]
        [Display(Name = "Role Name")]
        public string Name { get; set; } = string.Empty;
    }
}
