using HrPortal.Services;
using Microsoft.AspNetCore.Mvc;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Answers "who do I ask". It points at the HR officer named in
    /// the approval settings rather than a hard-coded address, so it
    /// follows whoever currently holds the role.
    /// </summary>
    public class HelpController : Controller
    {
        private readonly HrContactService _hrContact;

        public HelpController(HrContactService hrContact)
        {
            _hrContact = hrContact;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _hrContact.GetAsync());
        }
    }
}
