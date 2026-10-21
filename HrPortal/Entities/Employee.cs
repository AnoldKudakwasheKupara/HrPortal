using System.ComponentModel.DataAnnotations.Schema;
using HrPortal.Data;

namespace HrPortal.Entities
{
    public class Employee
    {
        public int Id { get; set; }

        public string EmployeeNumber { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public int? PositionId { get; set; }

        [NotMapped]
        public string? JobTitle => Position?.Title;

        public string? PhoneNumber { get; set; }

        public string? NationalId { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public Gender Gender { get; set; } = Gender.Unspecified;

        public MaritalStatus MaritalStatus { get; set; }
            = MaritalStatus.Unspecified;

        public string? Address { get; set; }

        public string? EmergencyContactName { get; set; }

        public string? EmergencyContactPhone { get; set; }

        public string? EmergencyContactRelationship { get; set; }

        public string? EmergencyContactAddress { get; set; }

        /// <summary>
        /// When the employee last confirmed their next of kin details
        /// are correct. Null until they have completed the form.
        /// </summary>
        public DateTime? NextOfKinConfirmedAt { get; set; }

        public DateTime DateJoined { get; set; }

        public EmploymentType EmploymentType { get; set; }
            = EmploymentType.Permanent;

        public DateTime? ContractEndDate { get; set; }

        public DateTime? EndDate { get; set; }

        public string? LeavingReason { get; set; }

        public int DepartmentId { get; set; }

        public int? UserId { get; set; }

        public bool IsActive { get; set; } = true;

        public Department Department { get; set; } = null!;

        public Position? Position { get; set; }

        public ApplicationUser? User { get; set; }

        public ICollection<LeaveRequest> LeaveRequests { get; set; }
            = new List<LeaveRequest>();

        public ICollection<EmployeeDocument> Documents { get; set; }
            = new List<EmployeeDocument>();

        public ICollection<TrainingAssignment> TrainingAssignments { get; set; }
            = new List<TrainingAssignment>();

        public ICollection<Notification> Notifications { get; set; }
            = new List<Notification>();
    }
}
