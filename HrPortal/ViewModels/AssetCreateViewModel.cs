using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class AssetCreateViewModel
    {
        [Required]
        [Display(Name = "Device / Model")]
        [StringLength(200)]
        public string DeviceModel { get; set; } = string.Empty;

        [Display(Name = "Company")]
        [StringLength(100)]
        public string? Company { get; set; }

        [Display(Name = "Processor")]
        [StringLength(200)]
        public string? Processor { get; set; }

        [Display(Name = "Serial Number")]
        [StringLength(100)]
        public string? SerialNumber { get; set; }

        [Display(Name = "Procured")]
        [DataType(DataType.Date)]
        public DateTime? ProcuredOn { get; set; }

        [Required]
        [Display(Name = "Condition")]
        public AssetCondition Condition { get; set; }

        [Display(Name = "Assigned To")]
        public int? AssignedToEmployeeId { get; set; }

        [Display(Name = "In Use")]
        public bool IsInUse { get; set; } = true;

        [Display(Name = "Notes / Issues")]
        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}
