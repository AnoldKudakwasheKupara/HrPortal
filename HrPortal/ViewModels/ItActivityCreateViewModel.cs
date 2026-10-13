using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class ItActivityCreateViewModel
    {
        [Required]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime LoggedOn { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Category")]
        public ItActivityCategory Category { get; set; }

        [Required]
        [Display(Name = "What came up")]
        [StringLength(300)]
        public string Summary { get; set; } = string.Empty;

        [Display(Name = "What was done")]
        [StringLength(2000)]
        public string? ActionTaken { get; set; }

        [Display(Name = "Raised by")]
        public int? RaisedByEmployeeId { get; set; }

        [Display(Name = "Or someone else")]
        [StringLength(200)]
        public string? RaisedByOther { get; set; }

        [Display(Name = "Machine")]
        public int? AssetId { get; set; }

        [Required]
        [Display(Name = "Status")]
        public ItActivityStatus Status { get; set; } = ItActivityStatus.Resolved;

        [Display(Name = "Resolved on")]
        [DataType(DataType.Date)]
        public DateTime? ResolvedOn { get; set; } = DateTime.Today;

        [Display(Name = "Worth naming in the weekly report")]
        public bool IsNotable { get; set; }
    }
}
