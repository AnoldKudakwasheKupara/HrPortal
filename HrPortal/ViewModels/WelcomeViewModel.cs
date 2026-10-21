using HrPortal.Services;

namespace HrPortal.ViewModels
{
    public class WelcomeViewModel
    {
        public string? FirstName { get; set; }

        public HrContact HrContact { get; set; } = HrContact.Unknown;
    }
}
