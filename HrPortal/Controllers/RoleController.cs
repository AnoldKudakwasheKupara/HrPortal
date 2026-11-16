using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
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
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;
        private readonly CompanyService _companies;

        public RoleController(
            RoleManager<IdentityRole<int>> roleManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            AuditService audit,
            CompanyService companies)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _context = context;
            _audit = audit;
            _companies = companies;
        }

        /// <summary>
        /// Who holds the role: everywhere for a role that holds at
        /// every company, otherwise at this portal's company.
        /// </summary>
        private async Task<HashSet<int>> HolderIdsAsync(IdentityRole<int> role)
        {
            if (!Roles.IsCompanyRole(role.Name))
            {
                return (await _userManager.GetUsersInRoleAsync(role.Name!))
                    .Select(u => u.Id)
                    .ToHashSet();
            }

            var here = await _companies.CurrentIdAsync();

            return (await _context.CompanyRoles
                    .Where(cr => cr.CompanyId == here && cr.RoleId == role.Id)
                    .Select(cr => cr.UserId)
                    .ToListAsync())
                .ToHashSet();
        }

        /// <summary>
        /// Who can be given the role. A role held at one company can
        /// only go to somebody who works there, or to a sign-in with
        /// no staff record.
        /// </summary>
        private async Task<List<ApplicationUser>> CandidatesAsync(IdentityRole<int> role)
        {
            var users = _userManager.Users;

            if (Roles.IsCompanyRole(role.Name))
            {
                var here = await _companies.CurrentIdAsync();

                users = users.Where(u =>
                    !_context.Employees.Any(e => e.UserId == u.Id) ||
                    _context.Employees.Any(e =>
                        e.UserId == u.Id &&
                        e.Companies.Any(m => m.CompanyId == here)));
            }

            return await users.OrderBy(u => u.Email).ToListAsync();
        }

        private async Task<string?> CompanyNameForAsync(IdentityRole<int> role) =>
            Roles.IsCompanyRole(role.Name)
                ? CompanyService.NameOf(await _companies.CurrentAsync())
                : null;

        private async Task<IdentityResult> GiveAsync(ApplicationUser user, IdentityRole<int> role)
        {
            if (!Roles.IsCompanyRole(role.Name))
                return await _userManager.AddToRoleAsync(user, role.Name!);

            _context.CompanyRoles.Add(new CompanyRole
            {
                UserId = user.Id,
                CompanyId = await _companies.CurrentIdAsync(),
                RoleId = role.Id
            });

            await _context.SaveChangesAsync();

            return IdentityResult.Success;
        }

        private async Task<IdentityResult> TakeAsync(ApplicationUser user, IdentityRole<int> role)
        {
            if (!Roles.IsCompanyRole(role.Name))
                return await _userManager.RemoveFromRoleAsync(user, role.Name!);

            var here = await _companies.CurrentIdAsync();

            var held = await _context.CompanyRoles
                .Where(cr => cr.UserId == user.Id && cr.CompanyId == here && cr.RoleId == role.Id)
                .ToListAsync();

            _context.CompanyRoles.RemoveRange(held);

            await _context.SaveChangesAsync();

            return IdentityResult.Success;
        }

        private async Task<int> HoldersAnywhereAsync(IdentityRole<int> role) =>
            await _context.UserRoles.CountAsync(ur => ur.RoleId == role.Id) +
            await _context.CompanyRoles.CountAsync(cr => cr.RoleId == role.Id);

        public async Task<IActionResult> Index()
        {
            var here = await _companies.CurrentAsync();

            var counts = await _context.UserRoles
                .AsNoTracking()
                .Select(ur => ur.RoleId)
                .Concat(_context.CompanyRoles
                    .Where(cr => cr.CompanyId == here.Id)
                    .Select(cr => cr.RoleId))
                .GroupBy(id => id)
                .Select(g => new
                {
                    RoleId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.RoleId, x => x.Count);

            ViewBag.CompanyName = CompanyService.NameOf(here);

            var model = await _roleManager.Roles
                .AsNoTracking()
                .OrderBy(r => r.Name)
                .Select(r => new RoleEditViewModel
                {
                    Id = r.Id,
                    Name = r.Name ?? string.Empty
                })
                .ToListAsync();

            foreach (var row in model)
            {
                row.UserCount =
                    counts.TryGetValue(row.Id, out var count) ? count : 0;
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

            var model = new RoleEditViewModel
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                UserCount = await HoldersAnywhereAsync(role)
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

            var holders = await HoldersAnywhereAsync(role);

            if (holders > 0)
            {
                TempData["ErrorMessage"] =
                    $"The role '{roleName}' cannot be deleted because " +
                    $"{holders} user(s) are assigned to it, at this " +
                    "company or another.";

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

            var users = await CandidatesAsync(role);

            var usersInRoleIds = await HolderIdsAsync(role);

            var model = new RoleUsersViewModel
            {
                RoleId = role.Id,
                RoleName = role.Name ?? string.Empty,
                CompanyName = await CompanyNameForAsync(role),
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

            var currentUserIds = await HolderIdsAsync(role);

            var candidateIds = (await CandidatesAsync(role))
                .Select(u => u.Id)
                .ToHashSet();

            var selectedUserIds =
                model.Users
                    .Where(u => u.IsInRole && candidateIds.Contains(u.UserId))
                    .Select(u => u.UserId)
                    .ToHashSet();

            var added = new List<string>();
            var removed = new List<string>();
            var kept = new List<string>();

            foreach (var userId in selectedUserIds.Except(currentUserIds))
            {
                var user = await _userManager.FindByIdAsync(
                    userId.ToString());

                if (user != null)
                {
                    var result = await GiveAsync(user, role);

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

                    if (string.Equals(
                        roleName,
                        Roles.Employee,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        kept.Add(user.Email ?? user.UserName ?? "unknown");
                        continue;
                    }

                    var result = await TakeAsync(user, role);

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

            if (kept.Count > 0)
            {
                TempData["ErrorMessage"] =
                    "Employee is the baseline role and cannot be removed. " +
                    $"{kept.Count} account(s) kept it.";
            }

            if (ModelState.ErrorCount == 0 &&
                (added.Count > 0 || removed.Count > 0))
            {
                var changes = new List<string>();

                if (added.Count > 0)
                    changes.Add("added " + string.Join(", ", added));

                if (removed.Count > 0)
                    changes.Add("removed " + string.Join(", ", removed));

                var companyName = await CompanyNameForAsync(role);

                await _audit.LogAsync(
                    AuditService.RoleChanged,
                    "Role",
                    companyName == null ? roleName : $"{roleName} at {companyName}",
                    char.ToUpper(string.Join("; ", changes)[0]) +
                    string.Join("; ", changes).Substring(1) + ".");
            }

            if (ModelState.ErrorCount > 0)
            {
                var refreshedUsers = await CandidatesAsync(role);

                var refreshedIds = await HolderIdsAsync(role);

                model.RoleName = roleName;
                model.CompanyName = await CompanyNameForAsync(role);

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

            if (added.Count > 0 || removed.Count > 0)
            {
                TempData["SuccessMessage"] =
                    $"Users assigned to '{roleName}' were updated " +
                    "successfully.";
            }

            return RedirectToAction(nameof(Users), new { id });
        }
    }
}
