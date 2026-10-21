using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    /// <summary>
    /// The employee's own next of kin form. The employment details at
    /// the top are shown for checking only; HR keeps those, and the
    /// employee edits the emergency contact and confirms the lot.
    /// </summary>
    public class NextOfKinViewModel
    {
        public string EmployeeName { get; set; } = string.Empty;

        public string DepartmentName { get; set; } = string.Empty;

        public string? JobTitle { get; set; }

        public string? Address { get; set; }

        public string? PhoneNumber { get; set; }

        public string Email { get; set; } = string.Empty;

        public DateTime? ConfirmedAt { get; set; }

        [Required(ErrorMessage = "Give the name of your emergency contact.")]
        [StringLength(150)]
        [Display(Name = "Emergency contact name")]
        public string EmergencyContactName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Say how this person is related to you.")]
        [StringLength(100)]
        [Display(Name = "Relationship to you")]
        public string EmergencyContactRelationship { get; set; }
            = string.Empty;

        [StringLength(500)]
        [Display(Name = "Address, if different from yours")]
        public string? EmergencyContactAddress { get; set; }

        [Required(ErrorMessage = "Give at least one phone number.")]
        [StringLength(100)]
        [Display(Name = "Phone number(s)")]
        public string EmergencyContactPhone { get; set; } = string.Empty;

        [Display(Name =
            "I confirm the details above are accurate and up to date")]
        public bool Confirmed { get; set; }
    }
}
