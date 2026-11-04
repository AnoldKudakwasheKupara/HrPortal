using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.ViewModels;
using HrPortal.Security;
using HrPortal.Services.Sanctions;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.HrOnly)]
    public class LeaveBalanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RecordScope _scope;

        private readonly RecordDocumentPdfService _pdf;
        private readonly LeaveBalanceService _balances;
        private readonly AuditService _audit;

        public LeaveBalanceController(
            ApplicationDbContext context,
            RecordScope scope,
            RecordDocumentPdfService pdf,
            LeaveBalanceService balances,
            AuditService audit)
        {
            _context = context;
            _scope = scope;
            _pdf = pdf;
            _balances = balances;
            _audit = audit;
        }

        public async Task<IActionResult> Upload()
        {
            await LoadRatedTypes();

            return View();
        }

        /// <summary>
        /// Sets each employee in the file to the figure given. Rows that
        /// cannot be used are reported and the rest still go in, so
        /// fixing a mistake only takes uploading those rows again.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(FileStorageService.MaxFileSizeBytes + 1024 * 1024)]
        public async Task<IActionResult> Upload(int leaveTypeId, IFormFile? file)
        {
            await LoadRatedTypes();

            var extension = Path.GetExtension(file?.FileName ?? string.Empty).ToLowerInvariant();

            if (file == null || file.Length == 0 || extension is not (".xlsx" or ".csv"))
            {
                ViewBag.Error = "Choose the balances as an Excel workbook (.xlsx) or a CSV file.";
                return View();
            }

            if (file.Length > FileStorageService.MaxFileSizeBytes)
            {
                ViewBag.Error = $"{file.FileName} is larger than 10 MB.";
                return View();
            }

            var type = await _context.LeaveTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == leaveTypeId && t.MonthlyAccrualDays > 0);

            if (type == null)
            {
                ViewBag.Error = "Choose a leave type that earns days each month.";
                return View();
            }

            ClientTable table;

            try
            {
                await using var stream = file.OpenReadStream();
                table = ClientListReader.ReadTable(stream, file.FileName);
            }
            catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or IOException)
            {
                ViewBag.Error = "The file could not be read. " + ex.Message;
                return View();
            }

            var employees = (await _context.Employees.AsNoTracking()
                    .Select(e => new { e.Id, e.EmployeeNumber, e.FirstName, e.LastName, e.Email })
                    .ToListAsync())
                .Select(e => new LeaveBalanceEmployee(e.Id, e.EmployeeNumber, $"{e.FirstName} {e.LastName}", e.Email))
                .ToList();

            var import = LeaveBalanceImport.Parse(table, employees);

            if (import.Error != null)
            {
                ViewBag.Error = import.Error;
                return View();
            }

            var changed = 0;

            foreach (var row in import.Rows)
            {
                if (await _balances.SetBalanceAsync(
                        row.EmployeeId,
                        type.Id,
                        row.Days,
                        DateTime.Today,
                        $"Uploaded by {User.Identity?.Name} in {Path.GetFileName(file.FileName)}"))
                {
                    changed++;
                }
            }

            await _audit.LogAsync(
                AuditService.LeaveBalancesUploaded,
                nameof(LeaveBalance),
                type.Name,
                $"{Path.GetFileName(file.FileName)}: {import.Received} rows, {changed} balances changed, " +
                $"{import.Rows.Count - changed} already right, {import.LeftBlank} blank, " +
                $"{import.Problems.Count} left out.");

            ViewBag.Changed = changed;
            ViewBag.Unchanged = import.Rows.Count - changed;
            ViewBag.Import = import;

            return View();
        }

        /// <summary>
        /// A file to fill in: every active employee, with what they have
        /// now where there is a balance already.
        /// </summary>
        public async Task<IActionResult> Template(int? leaveTypeId)
        {
            var current = leaveTypeId == null
                ? new Dictionary<int, decimal>()
                : (await _context.LeaveBalances.AsNoTracking()
                        .Where(b => b.LeaveTypeId == leaveTypeId)
                        .ToListAsync())
                    .GroupBy(b => b.EmployeeId)
                    .ToDictionary(
                        g => g.Key,
                        g => LeaveBalanceService.Available(g.OrderByDescending(b => b.Year).First()));

            var employees = await _context.Employees.AsNoTracking()
                .Where(e => e.IsActive)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .Select(e => new { e.Id, e.EmployeeNumber, e.FirstName, e.LastName })
                .ToListAsync();

            static string Cell(string value)
            {
                if (value.Length > 0 && "=+-@".Contains(value[0]))
                    value = "'" + value;

                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            var csv = new StringBuilder("Employee Number,Employee Name,Balance\r\n");

            foreach (var e in employees)
            {
                var balance = current.TryGetValue(e.Id, out var days)
                    ? days.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    : string.Empty;

                csv.Append(Cell(e.EmployeeNumber)).Append(',')
                    .Append(Cell($"{e.FirstName} {e.LastName}")).Append(',')
                    .Append(balance).Append("\r\n");
            }

            return File(
                Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
                "text/csv",
                "Leave balances template.csv");
        }

        private async Task LoadRatedTypes()
        {
            ViewBag.RatedTypes = await _context.LeaveTypes.AsNoTracking()
                .Where(t => t.IsActive && t.MonthlyAccrualDays > 0)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        /// <summary>
        /// An employee's balance for one leave type and year, as a
        /// printable record.
        /// </summary>
        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var balance = await _context.LeaveBalances
                .AsNoTracking()
                .Include(b => b.Employee)
                    .ThenInclude(e => e.Department)
                .Include(b => b.LeaveType)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (balance == null)
                return NotFound();

            var who = $"{balance.Employee.FirstName} " +
                      $"{balance.Employee.LastName}";

            var record = new RecordDocument(
                "Leave Balance",
                balance.Employee.EmployeeNumber)
            {
                Headline = who,
                Caption = balance.Employee.Department?.Name,
                Chip = $"{balance.LeaveType.Name} {balance.Year}"
            };

            record.Section("Employee", "Whose balance this is")
                .Add("Name", who)
                .Add("Employee number", balance.Employee.EmployeeNumber)
                .Add("Department", balance.Employee.Department?.Name);

            record.Section(
                    "Balance",
                    $"{balance.LeaveType.Name}, {balance.Year}")
                .Add("Leave type", balance.LeaveType.Name)
                .Add("Year", balance.Year)
                .Add("Days allocated", balance.AllocatedDays)
                .Add("Days used", balance.UsedDays)
                .Add("Days pending approval", balance.PendingDays)
                .Add("Days remaining", balance.RemainingDays);

            return File(
                _pdf.Render(record),
                "application/pdf",
                record.FileName);
        }

        public async Task<IActionResult> Index(int? year)
        {
            var selectedYear = year ?? DateTime.Today.Year;

            var visible = await _scope.VisibleToAsync(
                _context.LeaveBalances
                    .AsNoTracking()
                    .Include(b => b.Employee)
                    .Include(b => b.LeaveType),
                User);

            var balances = await visible
                .Where(b => b.Year == selectedYear)
                .OrderBy(b => b.Employee.FirstName)
                .ThenBy(b => b.Employee.LastName)
                .ThenBy(b => b.LeaveType.Name)
                .ToListAsync();

            var years = await _context.LeaveBalances
                .Select(b => b.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            if (!years.Contains(selectedYear))
                years.Add(selectedYear);

            await LoadRatedTypes();

            var ratedTypes = (List<LeaveType>)ViewBag.RatedTypes;
            var ratedIds = ratedTypes.Select(t => t.Id).ToList();

            ViewBag.WithoutBalance = await _context.Employees
                .CountAsync(e =>
                    e.IsActive &&
                    !_context.LeaveBalances.Any(b => b.EmployeeId == e.Id && ratedIds.Contains(b.LeaveTypeId)));

            ViewBag.Years = years.OrderByDescending(y => y).ToList();
            ViewBag.SelectedYear = selectedYear;

            return View(balances);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var balance = await _context.LeaveBalances
                .AsNoTracking()
                .Include(b => b.Employee)
                    .ThenInclude(e => e.Department)
                .Include(b => b.LeaveType)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (balance == null)
                return NotFound();

            ViewBag.Statement = await _balances.StatementAsync(balance.EmployeeId, balance.LeaveTypeId);

            return View(balance);
        }

        public async Task<IActionResult> Create()
        {
            await LoadLookups();

            return View(new LeaveBalanceCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            LeaveBalanceCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var exists = await _context.LeaveBalances
                .AnyAsync(b =>
                    b.EmployeeId == model.EmployeeId &&
                    b.LeaveTypeId == model.LeaveTypeId &&
                    b.Year == model.Year);

            if (exists)
            {
                ModelState.AddModelError(
                    "",
                    "This employee already has a balance for that " +
                    "leave type and year. Edit the existing one instead.");

                await LoadLookups();
                return View(model);
            }

            var balance = new LeaveBalance
            {
                EmployeeId = model.EmployeeId,
                LeaveTypeId = model.LeaveTypeId,
                Year = model.Year,
                AllocatedDays = model.AllocatedDays,
                UsedDays = model.UsedDays,
                PendingDays = model.PendingDays,
                RemainingDays =
                    model.AllocatedDays
                    - model.UsedDays
                    - model.PendingDays
            };

            _context.LeaveBalances.Add(balance);

            await _balances.RecordManualChangeAsync(balance, null, User.Identity?.Name);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Leave balance created successfully.";

            return RedirectToAction(
                nameof(Index),
                new { year = balance.Year });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var balance = await _context.LeaveBalances
                .FindAsync(id);

            if (balance == null)
                return NotFound();

            var model = new LeaveBalanceEditViewModel
            {
                Id = balance.Id,
                EmployeeId = balance.EmployeeId,
                LeaveTypeId = balance.LeaveTypeId,
                Year = balance.Year,
                AllocatedDays = balance.AllocatedDays,
                UsedDays = balance.UsedDays,
                PendingDays = balance.PendingDays
            };

            await LoadLookups();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            LeaveBalanceEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var balance = await _context.LeaveBalances
                .FindAsync(id);

            if (balance == null)
                return NotFound();

            var duplicateExists = await _context.LeaveBalances
                .AnyAsync(b =>
                    b.Id != id &&
                    b.EmployeeId == model.EmployeeId &&
                    b.LeaveTypeId == model.LeaveTypeId &&
                    b.Year == model.Year);

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    "",
                    "This employee already has another balance for " +
                    "that leave type and year.");

                await LoadLookups();
                return View(model);
            }

            var was = LeaveBalanceService.Available(balance);

            balance.EmployeeId = model.EmployeeId;
            balance.LeaveTypeId = model.LeaveTypeId;
            balance.Year = model.Year;
            balance.AllocatedDays = model.AllocatedDays;
            balance.UsedDays = model.UsedDays;
            balance.PendingDays = model.PendingDays;

            balance.RemainingDays =
                model.AllocatedDays
                - model.UsedDays
                - model.PendingDays;

            await _balances.RecordManualChangeAsync(balance, was, User.Identity?.Name);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Leave balance updated successfully.";

            return RedirectToAction(
                nameof(Index),
                new { year = balance.Year });
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var balance = await _context.LeaveBalances
                .AsNoTracking()
                .Include(b => b.Employee)
                .Include(b => b.LeaveType)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (balance == null)
                return NotFound();

            return View(balance);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var balance = await _context.LeaveBalances
                .FindAsync(id);

            if (balance == null)
                return NotFound();

            var year = balance.Year;

            _context.LeaveBalances.Remove(balance);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Leave balance deleted successfully.";

            return RedirectToAction(
                nameof(Index),
                new { year });
        }

        private async Task LoadLookups()
        {
            var employees = await _context.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .Select(e => new
                {
                    e.Id,
                    FullName = e.FirstName + " " + e.LastName
                })
                .ToListAsync();

            ViewBag.Employees =
                new SelectList(
                    employees,
                    "Id",
                    "FullName");

            var leaveTypes = await _context.LeaveTypes
                .Where(l => l.IsActive)
                .OrderBy(l => l.Name)
                .ToListAsync();

            ViewBag.LeaveTypes =
                new SelectList(
                    leaveTypes,
                    "Id",
                    "Name");
        }
    }
}
