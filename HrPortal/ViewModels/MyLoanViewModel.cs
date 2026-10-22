using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class MyLoanViewModel
    {
        public int Id { get; set; }

        public string? ApplicationNumber { get; set; }

        [Required]
        [Display(Name = "Currency")]
        public LoanCurrency Currency { get; set; } = LoanCurrency.Usd;

        [Required]
        [Range(1, 100000000, ErrorMessage =
            "Enter the amount you are asking for.")]
        [Display(Name = "Amount required")]
        public decimal AmountRequested { get; set; }

        [Required]
        [Range(1, 120, ErrorMessage =
            "Repayment must be between 1 and 120 months.")]
        [Display(Name = "Repay over (months)")]
        public int RepaymentMonths { get; set; } = 12;

        [Required(ErrorMessage =
            "Say what the loan is for. Attach receipts or quotations " +
            "if you have them.")]
        [StringLength(2000)]
        [Display(Name = "Purpose")]
        public string Purpose { get; set; } = string.Empty;

        /// <summary>
        /// An optional receipt or quotation attached as the
        /// application is made. More can be added afterwards.
        /// </summary>
        [Display(Name = "Receipt or quotation")]
        public IFormFile? Receipt { get; set; }
    }
}
