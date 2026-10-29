using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class EmployeeAllowLoginViewModel
    {
        public int Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string EmployeeNumber { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Whether the portal can send email at all, which decides
        /// whether emailing them a link is on offer.
        /// </summary>
        public bool CanEmail { get; set; }

        /// <summary>
        /// Email them a link to choose their own password, rather
        /// than setting one here and handing it over.
        /// </summary>
        public bool SendLink { get; set; } = true;

        [DataType(DataType.Password)]
        [Display(Name = "Temporary Password")]
        public string? TemporaryPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Temporary Password")]
        [Compare(
            nameof(TemporaryPassword),
            ErrorMessage = "The two passwords do not match.")]
        public string? ConfirmPassword { get; set; }
    }
}
