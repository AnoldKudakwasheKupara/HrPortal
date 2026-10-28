using HrPortal.Services;

namespace HrPortal.ViewModels
{
    public class WelcomeViewModel
    {
        public string? FirstName { get; set; }

        /// <summary>
        /// Everybody holding the HR Officer role. A list, because a
        /// company may have more than one and a new employee being
        /// shown only one of them has been told half the answer.
        /// </summary>
        public List<ApproverPerson> HrContacts { get; set; } = new();
    }
}
