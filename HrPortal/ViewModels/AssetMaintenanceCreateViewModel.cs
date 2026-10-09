using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class AssetMaintenanceCreateViewModel
    {
        // Narrows the asset list on the form. Not stored: the
        // maintenance record belongs to the asset, not the holder,
        // who may change.
        [Display(Name = "Employee")]
        public int? EmployeeFilterId { get; set; }

        [Required]
        [Display(Name = "Asset")]
        public int AssetId { get; set; }

        [Required]
        [Display(Name = "Work")]
        public MaintenanceType Type { get; set; }

        [Required]
        [Display(Name = "Scheduled for")]
        [DataType(DataType.Date)]
        public DateTime ScheduledFor { get; set; } = DateTime.Today;

        [Display(Name = "Vendor")]
        public int? VendorId { get; set; }

        [Display(Name = "Or performed by")]
        [StringLength(200)]
        public string? PerformedBy { get; set; }

        [Display(Name = "Cost")]
        [Range(0, 9999999)]
        public decimal? Cost { get; set; }

        [Display(Name = "Notes")]
        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}
