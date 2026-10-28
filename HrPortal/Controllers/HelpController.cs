using HrPortal.Services;
using Microsoft.AspNetCore.Mvc;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Answers "who do I ask". Human Resources comes from whoever
    /// currently holds the role in the approval settings, and IT from
    /// whoever is currently in the IT department, so neither is a
    /// hard-coded address that goes stale when somebody leaves.
    /// </summary>
    public class HelpController : Controller
    {
        private readonly SupportContactsService _contacts;

        public HelpController(SupportContactsService contacts)
        {
            _contacts = contacts;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _contacts.GetAsync());
        }
    }
}
