using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class LicenseRenewViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        [Display(Name = "Current expiry")]
        public DateTime CurrentExpiry { get; set; }

        [Required]
        [Display(Name = "New expiry date")]
        [DataType(DataType.Date)]
        public DateTime NewExpiry { get; set; }

        [Display(Name = "Renewed on")]
        [DataType(DataType.Date)]
        public DateTime RenewedOn { get; set; } = DateTime.Today;

        [Display(Name = "Cost")]
        [Range(0, 99999999)]
        public decimal? Cost { get; set; }

        [Display(Name = "Notes")]
        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}
