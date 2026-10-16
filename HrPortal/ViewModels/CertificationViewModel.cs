using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class CertificationViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [Required]
        [Display(Name = "Certification")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Issued By")]
        [StringLength(200)]
        public string? IssuedBy { get; set; }

        [Display(Name = "Reference")]
        [StringLength(100)]
        public string? Reference { get; set; }

        [Required]
        [Display(Name = "Issued On")]
        [DataType(DataType.Date)]
        public DateTime IssuedOn { get; set; } = DateTime.Today;

        [Display(Name = "Expires On")]
        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }

        [Display(Name = "From Course")]
        public int? TrainingCourseId { get; set; }

        [Display(Name = "Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
