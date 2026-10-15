using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class EmployeeEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Employee Number")]
        public string EmployeeNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Job Title")]
        [StringLength(150)]
        public string? JobTitle { get; set; }

        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "National ID")]
        public string? NationalId { get; set; }

        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Gender")]
        public Gender Gender { get; set; } = Gender.Unspecified;

        [Display(Name = "Marital Status")]
        public MaritalStatus MaritalStatus { get; set; }
            = MaritalStatus.Unspecified;

        [Display(Name = "Home Address")]
        [StringLength(400)]
        public string? Address { get; set; }

        [Display(Name = "Emergency Contact")]
        [StringLength(200)]
        public string? EmergencyContactName { get; set; }

        [Display(Name = "Emergency Contact Phone")]
        [StringLength(50)]
        public string? EmergencyContactPhone { get; set; }

        [Display(Name = "Relationship")]
        [StringLength(100)]
        public string? EmergencyContactRelationship { get; set; }

        [Required]
        [Display(Name = "Date Joined")]
        [DataType(DataType.Date)]
        public DateTime DateJoined { get; set; }

        [Required]
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }

        [Required]
        [Display(Name = "Employment Type")]
        public EmploymentType EmploymentType { get; set; }

        [Display(Name = "Contract Ends")]
        [DataType(DataType.Date)]
        public DateTime? ContractEndDate { get; set; }

        [Display(Name = "Still Employed")]

        public bool IsActive { get; set; }

        [Display(Name = "Left On")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Reason For Leaving")]
        [StringLength(500)]
        public string? LeavingReason { get; set; }
    }
}
