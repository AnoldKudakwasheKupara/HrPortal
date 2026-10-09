using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class LicenseCreateViewModel
    {
        [Required]
        [Display(Name = "Licence name")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Type")]
        public LicenseType Type { get; set; }

        [Display(Name = "Reference or key")]
        [StringLength(200)]
        public string? Reference { get; set; }

        [Display(Name = "Vendor")]
        public int? VendorId { get; set; }

        [Display(Name = "Held by")]
        public int? AssignedToEmployeeId { get; set; }

        [Display(Name = "Seats")]
        [Range(1, 100000)]
        public int? Seats { get; set; }

        [Display(Name = "Start date")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Required]
        [Display(Name = "Expires on")]
        [DataType(DataType.Date)]
        public DateTime ExpiryDate { get; set; } = DateTime.Today.AddYears(1);

        [Display(Name = "Cost")]
        [Range(0, 99999999)]
        public decimal? Cost { get; set; }

        [Display(Name = "Renews automatically")]
        public bool AutoRenews { get; set; }

        [Display(Name = "Notes")]
        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}
