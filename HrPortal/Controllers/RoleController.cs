using HrPortal.Data;
using HrPortal.ViewModels;
using HrPortal.Security;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.AdminOnly)]
    public class RoleController : Controller
    {
        private readonly RoleManager<IdentityRole<int>> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditService _audit;

        public RoleController(
            RoleManager<IdentityRole<int>> roleManager,
            UserManager<ApplicationUser> userManager,
            AuditService audit)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _audit = audit;
        }

        public async Task<IActionResult> Index()
        {
            var roles = await _roleManager.Roles
                .OrderBy(r => r.Name)
                .ToListAsync();

            var model = new List<RoleEditViewModel>();

            foreach (var role in roles)
            {
                var users = await _userManager.GetUsersInRoleAsync(role.Name!);

                model.Add(new RoleEditViewModel
                {
                    Id = role.Id,
                    Name = role.Name ?? string.Empty,
                    UserCount = users.Count
                });
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new RoleCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RoleCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var roleName = model.Name.Trim();

            var existingRole = await _roleManager.FindByNameAsync(roleName);

            if (existingRole != null)
            {
                ModelState.AddModelError(
                    "Name",
                    "A role with this name already exists.");

                return View(model);
            }

            var role = new IdentityRole<int>(roleName);

            var result = await _roleManager.CreateAsync(role);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] =
                    $"Role '{roleName}' was created successfully.";

                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var role = await _roleManager.FindByIdAsync(id.ToString());

            if (role == null)
            {
                return NotFound();
            }

            var users = await _userManager.GetUsersInRoleAsync(role.Name!);

            var model = new RoleEditViewModel
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                UserCount = users.Count
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            RoleEditViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var role = await _roleManager.FindByIdAsync(id.ToString());

            if (role == null)
            {
                return NotFound();
            }

            var newName = model.Name.Trim();

            if (string.Equals(
                role.Name,
                newName,
                StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Index));
            }

            var existingRole = await _roleManager.FindByNameAsync(newName);

            if (existingRole != null && existingRole.Id != id)
            {
                ModelState.AddModelError(
                    "Name",
                    "A role with this name already exists.");

                return View(model);
            }

            role.Name = newName;

            var result = await _roleManager.UpdateAsync(role);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] =
                    $"Role renamed to '{newName}' successfully.";

                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var role = await _roleManager.FindByIdAsync(id.ToString());

            if (role == null)
            {
                TempData["ErrorMessage"] = "Role not found.";
                return RedirectToAction(nameof(Index));
            }

            var roleName = role.Name ?? string.Empty;

            if (string.Equals(
                roleName,
                "Administrator",
                StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] =
                    "The Administrator role cannot be deleted.";

                return RedirectToAction(nameof(Index));
            }

            var users = await _userManager.GetUsersInRoleAsync(roleName);

            if (users.Count > 0)
            {
                TempData["ErrorMessage"] =
                    $"The role '{roleName}' cannot be deleted because " +
                    $"{users.Count} user(s) are assigned to it.";

                return RedirectToAction(nameof(Index));
            }

            var result = await _roleManager.DeleteAsync(role);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] =
                    $"Role '{roleName}' was deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] =
                    "The role could not be deleted.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Users(int id)
        {
            var role = await _roleManager.FindByIdAsync(id.ToString());

            if (role == null)
            {
                return NotFound();
            }

            var users = await _userManager.Users
                .OrderBy(u => u.Email)
                .ToListAsync();

            var usersInRole =
                await _userManager.GetUsersInRoleAsync(role.Name!);

            var usersInRoleIds =
                usersInRole.Select(u => u.Id).ToHashSet();

            var model = new RoleUsersViewModel
            {
                RoleId = role.Id,
                RoleName = role.Name ?? string.Empty,
                Users = users.Select(user => new RoleUserViewModel
                {
                    UserId = user.Id,
                    Email = user.Email ?? string.Empty,
                    FullName = user.FullName ?? user.UserName ?? string.Empty,
                    IsInRole = usersInRoleIds.Contains(user.Id)
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Users(
            int id,
            RoleUsersViewModel model)
        {
            var role = await _roleManager.FindByIdAsync(id.ToString());

            if (role == null)
            {
                return NotFound();
            }

            var roleName = role.Name ?? string.Empty;

            if (string.Equals(
                roleName,
                "Administrator",
                StringComparison.OrdinalIgnoreCase))
            {
                var currentAdministrators =
                    await _userManager.GetUsersInRoleAsync("Administrator");

                var selectedAdministrators =
                    model.Users.Count(u => u.IsInRole);

                if (selectedAdministrators == 0)
                {
                    TempData["ErrorMessage"] =
                        "There must always be at least one Administrator.";

                    return RedirectToAction(nameof(Users), new { id });
                }
            }

            var currentUsers =
                await _userManager.GetUsersInRoleAsync(roleName);

            var currentUserIds =
                currentUsers.Select(u => u.Id).ToHashSet();

            var selectedUserIds =
                model.Users
                    .Where(u => u.IsInRole)
                    .Select(u => u.UserId)
                    .ToHashSet();

            var added = new List<string>();
            var removed = new List<string>();

            foreach (var userId in selectedUserIds.Except(currentUserIds))
            {
                var user = await _userManager.FindByIdAsync(
                    userId.ToString());

                if (user != null)
                {
                    var result =
                        await _userManager.AddToRoleAsync(user, roleName);

                    if (result.Succeeded)
                    {
                        added.Add(user.Email ?? user.UserName ?? "unknown");
                    }

                    if (!result.Succeeded)
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError(
                                "",
                                error.Description);
                        }
                    }
                }
            }

            foreach (var userId in currentUserIds.Except(selectedUserIds))
            {
                var user = await _userManager.FindByIdAsync(
                    userId.ToString());

                if (user != null)
                {
                    if (string.Equals(
                        roleName,
                        "Administrator",
                        StringComparison.OrdinalIgnoreCase) &&
                        currentUserIds.Count <= 1)
                    {
                        continue;
                    }

                    var result =
                        await _userManager.RemoveFromRoleAsync(
                            user,
                            roleName);

                    if (result.Succeeded)
                    {
                        removed.Add(user.Email ?? user.UserName ?? "unknown");
                    }

                    if (!result.Succeeded)
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError(
                                "",
                                error.Description);
                        }
                    }
                }
            }

            if (ModelState.ErrorCount == 0 &&
                (added.Count > 0 || removed.Count > 0))
            {
                var changes = new List<string>();

                if (added.Count > 0)
                    changes.Add("added " + string.Join(", ", added));

                if (removed.Count > 0)
                    changes.Add("removed " + string.Join(", ", removed));

                await _audit.LogAsync(
                    AuditService.RoleChanged,
                    "Role",
                    roleName,
                    char.ToUpper(string.Join("; ", changes)[0]) +
                    string.Join("; ", changes).Substring(1) + ".");
            }

            if (ModelState.ErrorCount > 0)
            {
                var refreshedUsers =
                    await _userManager.Users
                        .OrderBy(u => u.Email)
                        .ToListAsync();

                var refreshedRoleUsers =
                    await _userManager.GetUsersInRoleAsync(roleName);

                var refreshedIds =
                    refreshedRoleUsers
                        .Select(u => u.Id)
                        .ToHashSet();

                model.RoleName = roleName;

                model.Users = refreshedUsers.Select(user =>
                    new RoleUserViewModel
                    {
                        UserId = user.Id,
                        Email = user.Email ?? string.Empty,
                        FullName =
                            user.FullName ??
                            user.UserName ??
                            string.Empty,
                        IsInRole = refreshedIds.Contains(user.Id)
                    }).ToList();

                return View(model);
            }

            TempData["SuccessMessage"] =
                $"Users assigned to '{roleName}' were updated successfully.";

            return RedirectToAction(nameof(Users), new { id });
        }
    }
}
