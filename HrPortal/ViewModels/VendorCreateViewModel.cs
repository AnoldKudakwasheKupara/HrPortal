using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class VendorCreateViewModel
    {
        [Required]
        [Display(Name = "Vendor Name")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Product or Service")]
        [StringLength(300)]
        public string ProductOrService { get; set; } = string.Empty;

        [Display(Name = "Category")]
        [StringLength(100)]
        public string? Category { get; set; }

        [Display(Name = "Contact Person")]
        [StringLength(150)]
        public string? ContactPerson { get; set; }

        [Display(Name = "Email")]
        [EmailAddress]
        [StringLength(200)]
        public string? Email { get; set; }

        [Display(Name = "Phone Number")]
        [Phone]
        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Alternative Phone")]
        [Phone]
        [StringLength(50)]
        public string? AlternatePhone { get; set; }

        [Display(Name = "Website")]
        [StringLength(200)]
        public string? Website { get; set; }

        [Display(Name = "Address")]
        [StringLength(300)]
        public string? Address { get; set; }

        [Display(Name = "Notes")]
        [StringLength(1000)]
        public string? Notes { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }
}
