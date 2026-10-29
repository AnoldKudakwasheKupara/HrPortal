using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HrPortal.Areas.Identity.Pages.Account;

/// <summary>
/// Only ever reached after signing up, which is closed.
///
/// The page is kept, answering not found, because removing it would
/// bring back the built-in one in its place.
/// </summary>
public class RegisterConfirmationModel : PageModel
{
    public IActionResult OnGet() => NotFound();

    public IActionResult OnPost() => NotFound();
}
