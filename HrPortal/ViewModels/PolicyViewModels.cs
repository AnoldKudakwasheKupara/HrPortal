using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class PolicyListViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Reference { get; set; }

        public string? Category { get; set; }

        public string Version { get; set; } = string.Empty;

        public PolicyStatus Status { get; set; }

        public DocumentAudience Audience { get; set; }

        public DateTime EffectiveDate { get; set; }

        public DateTime? ReviewDate { get; set; }

        public bool RequiresAcknowledgement { get; set; }

        public bool Acknowledged { get; set; }

        public int AcknowledgedCount { get; set; }

        public bool IsOverdueForReview =>
            ReviewDate != null && ReviewDate.Value.Date < DateTime.Today;
    }

    public class PolicyDetailsViewModel
    {
        public Policy Policy { get; set; } = null!;

        public bool Acknowledged { get; set; }

        public bool CanManage { get; set; }

        public List<string> Outstanding { get; set; } = new();

        public int ActiveStaff { get; set; }

        public int AcknowledgedCount => Policy.Acknowledgements.Count;

        public decimal Coverage =>
            ActiveStaff == 0
                ? 0
                : Math.Round(
                    (decimal)AcknowledgedCount / ActiveStaff * 100, 0);
    }

    public class PolicyEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Title")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Reference")]
        [StringLength(50)]
        public string? Reference { get; set; }

        [Display(Name = "Category")]
        [StringLength(100)]
        public string? Category { get; set; }

        [Required]
        [Display(Name = "Version")]
        [StringLength(20)]
        public string Version { get; set; } = "1.0";

        [Required]
        [Display(Name = "Who it applies to")]
        public DocumentAudience Audience { get; set; }
            = DocumentAudience.Everyone;

        [Display(Name = "Owner")]
        [StringLength(200)]
        public string? Owner { get; set; }

        [Display(Name = "Summary")]
        [StringLength(1000)]
        public string? Summary { get; set; }

        [Required]
        [Display(Name = "Takes effect")]
        [DataType(DataType.Date)]
        public DateTime EffectiveDate { get; set; } = DateTime.Today;

        [Display(Name = "Review by")]
        [DataType(DataType.Date)]
        public DateTime? ReviewDate { get; set; }

        [Display(Name = "Staff must confirm they have read it")]
        public bool RequiresAcknowledgement { get; set; } = true;

        [Display(Name = "Policy document")]
        public IFormFile? File { get; set; }

        public int? SupersedesPolicyId { get; set; }
    }
}
