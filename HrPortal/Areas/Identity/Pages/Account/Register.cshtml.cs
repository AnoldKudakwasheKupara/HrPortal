using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HrPortal.Areas.Identity.Pages.Account;

/// <summary>
/// Nobody signs themselves up. Sign-ins are made by HR, from
/// Add Employee or Allow login, so every account belongs to a
/// person on the books and starts with only the Employee role.
///
/// The page is kept, answering not found, because removing it would
/// bring back the built-in one in its place.
/// </summary>
public class RegisterModel : PageModel
{
    public IActionResult OnGet() => NotFound();

    public IActionResult OnPost() => NotFound();
}
