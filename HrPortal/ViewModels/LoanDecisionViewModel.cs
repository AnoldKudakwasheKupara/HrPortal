using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    /// <summary>
    /// The Managing Director's decision on a loan. The amount
    /// sanctioned need not match the amount asked for, which is why
    /// the paper form has a box for each.
    /// </summary>
    public class LoanDecisionViewModel
    {
        public int Id { get; set; }

        [Required]
        [Range(1, 100000000, ErrorMessage =
            "Enter the amount being sanctioned.")]
        [Display(Name = "Amount authorised")]
        public decimal AmountAuthorised { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Deductions start (with effect from)")]
        public DateTime RepaymentStartDate { get; set; }
            = DateTime.Today;

        [StringLength(1000)]
        [Display(Name = "Comment")]
        public string? Comment { get; set; }
    }
}
