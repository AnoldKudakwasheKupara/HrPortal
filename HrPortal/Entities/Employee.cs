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

        public string? PhoneNumber { get; set; }

        public string? NationalId { get; set; }

        public DateTime DateJoined { get; set; }

        public int DepartmentId { get; set; }

        public int? UserId { get; set; }

        public bool IsActive { get; set; } = true;


        // Relationships
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