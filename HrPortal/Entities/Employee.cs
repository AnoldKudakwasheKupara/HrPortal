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

        /// <summary>What they do, as it appears on the contract.</summary>
        public string? JobTitle { get; set; }

        public string? PhoneNumber { get; set; }

        public string? NationalId { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public Gender Gender { get; set; } = Gender.Unspecified;

        public MaritalStatus MaritalStatus { get; set; }
            = MaritalStatus.Unspecified;

        public string? Address { get; set; }

        /// <summary>
        /// Who to call if something happens at work. Held on the
        /// employee record rather than a separate table, since there
        /// is only ever one and it is read, not reported on.
        /// </summary>
        public string? EmergencyContactName { get; set; }

        public string? EmergencyContactPhone { get; set; }

        public string? EmergencyContactRelationship { get; set; }


        public DateTime DateJoined { get; set; }

        public EmploymentType EmploymentType { get; set; }
            = EmploymentType.Permanent;

        /// <summary>
        /// When a fixed-term, probation or internship engagement is due
        /// to end. Null for permanent staff and for anyone whose
        /// engagement has no agreed end.
        /// </summary>
        public DateTime? ContractEndDate { get; set; }

        /// <summary>
        /// The day they actually left. Set when they are marked no
        /// longer employed, so a departure is dated rather than just
        /// switched off.
        /// </summary>
        public DateTime? EndDate { get; set; }

        public string? LeavingReason { get; set; }


        public int DepartmentId { get; set; }

        public int? UserId { get; set; }

        public bool IsActive { get; set; } = true;

        public Department Department { get; set; } = null!;

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
