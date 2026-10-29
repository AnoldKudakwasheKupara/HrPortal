using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HrPortal.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// People cannot delete their own sign-in. Doing so cut them off from
/// their leave and records with nobody told, and an account is HR's
/// to close when somebody leaves, from the employee record.
///
/// The page is kept, answering not found, because removing it would
/// bring back the built-in one in its place.
/// </summary>
public class DeletePersonalDataModel : PageModel
{
    public IActionResult OnGet() => NotFound();

    public IActionResult OnPost() => NotFound();
}
