using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class RequisitionLineViewModel
    {
        public int Id { get; set; }

        // Nullable on purpose. The form carries blank spare rows, and
        // a spare the requester never touched must not fail
        // validation -- and a non-nullable string would pick up an
        // implicit Required from model binding even without the
        // attribute. The controller checks the rows that were
        // actually filled in.
        [Display(Name = "Item or service")]
        [StringLength(300)]
        public string? Description { get; set; }

        [Display(Name = "Quantity")]
        public decimal Quantity { get; set; } = 1;

        [Display(Name = "Unit")]
        [StringLength(30)]
        public string? UnitOfMeasure { get; set; }

        [Display(Name = "Estimated unit cost")]
        [Range(0, 99999999)]
        public decimal? EstimatedUnitCost { get; set; }

        [Display(Name = "Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }

        /// <summary>
        /// Ticked on an existing line to drop it on the next save.
        /// </summary>
        public bool Remove { get; set; }

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Description) &&
            EstimatedUnitCost == null;
    }

    public class RequisitionEditViewModel
    {
        public int Id { get; set; }

        public string? RequisitionNumber { get; set; }

        public RequisitionStatus Status { get; set; }

        [Required]
        [Display(Name = "What is it for")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Justification")]
        [StringLength(2000)]
        public string? Justification { get; set; }

        [Display(Name = "Needed by")]
        [DataType(DataType.Date)]
        public DateTime? NeededBy { get; set; }

        [Display(Name = "Suggested vendor")]
        public int? VendorId { get; set; }

        public List<RequisitionLineViewModel> Lines { get; set; } = new();

        public decimal EstimatedTotal =>
            Lines.Where(l => !l.Remove)
                .Sum(l => l.Quantity * (l.EstimatedUnitCost ?? 0m));
    }

    public class RequisitionDecisionViewModel
    {
        public Requisition Requisition { get; set; } = null!;

        public bool CanReview { get; set; }

        public bool CanDecideAsMd { get; set; }

        public bool IsRequesterTheReviewer { get; set; }

        public decimal Threshold { get; set; }

        public string Currency { get; set; } = "USD";

        [Display(Name = "Comment")]
        [StringLength(1000)]
        public string? Comment { get; set; }
    }
}
