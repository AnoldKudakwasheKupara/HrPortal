using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class EmployeeResetPasswordViewModel
    {
        public int Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string EmployeeNumber { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare(
            nameof(NewPassword),
            ErrorMessage = "The two passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
