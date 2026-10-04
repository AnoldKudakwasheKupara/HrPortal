using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class MyProfileViewModel
    {
        public Employee Employee { get; set; } = null!;

        public List<EmployeeDocument> Documents { get; set; } = new();

        public List<Notification> Notifications { get; set; } = new();
    }
}
