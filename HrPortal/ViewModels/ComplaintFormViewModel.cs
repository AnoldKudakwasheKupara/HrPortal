using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class ComplaintFormViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Date received")]
        [DataType(DataType.Date)]
        public DateTime? ReceivedOn { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Enter the client's name.")]
        [Display(Name = "Client name")]
        [StringLength(200)]
        public string ClientName { get; set; } = string.Empty;

        [Display(Name = "Phone number")]
        [StringLength(50)]
        public string? ClientPhone { get; set; }

        [Display(Name = "Email address")]
        [EmailAddress(ErrorMessage = "That is not an email address.")]
        [StringLength(200)]
        public string? ClientEmail { get; set; }

        [Required(ErrorMessage = "Say what the client complained about.")]
        [Display(Name = "Nature of the complaint")]
        [StringLength(4000)]
        public string? Nature { get; set; }

        [Display(Name = "Staff member involved")]
        [StringLength(300)]
        public string? StaffInvolved { get; set; }

        [Display(Name = "Action taken")]
        [StringLength(4000)]
        public string? ActionTaken { get; set; }

        [Display(Name = "Status")]
        public ComplaintStatus Status { get; set; } = ComplaintStatus.Pending;

        [Display(Name = "Resolution date")]
        [DataType(DataType.Date)]
        public DateTime? ResolvedOn { get; set; }

        [Display(Name = "Remarks")]
        [StringLength(4000)]
        public string? Remarks { get; set; }

        public string? RecordedBy { get; set; }

        public DateTime? RecordedAt { get; set; }
    }
}
