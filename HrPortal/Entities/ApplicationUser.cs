using Microsoft.AspNetCore.Identity;

namespace HrPortal.Entities
{
    public class ApplicationUser : IdentityUser<int>
    {
        public string? FullName { get; set; }

        public int? EmployeeId { get; set; }

        public Employee? Employee { get; set; }
    }
}