using System.Globalization;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.Services.FundReporting;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Quarterly pension fund reporting, for investment analysts only.
    /// Administrators do not see it unless they are given the role, and
    /// a company whose brand does not switch it on does not have it at all.
    /// </summary>
    [Authorize(Roles = Roles.InvestmentAnalystOnly)]
    public class FundReportsController : Controller
    {
        private const string Entity = "Fund Reporting";

        private readonly ApplicationDbContext _context;
        private readonly FundReportingService _funds;
        private readonly AuditService _audit;
        private readonly FundReportingOptions _options;

        public FundReportsController(
            ApplicationDbContext context,
            FundReportingService funds,
            AuditService audit,
            IOptions<FundReportingOptions> options)
        {
            _context = context;
            _funds = funds;
            _audit = audit;
            _options = options.Value;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!Brand.Current.FundReporting)
                context.Result = NotFound();

            base.OnActionExecuting(context);
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.LabelsToDecide = (await _funds.LabelsToDecideAsync()).Count;
            ViewBag.SecuritiesToConfirm = await _context.FundSecurities.CountAsync(s => !s.Confirmed);

            return View(await _context.PensionFunds.AsNoTracking()
                .Include(f => f.Accounts)
                .OrderBy(f => f.Name)
                .ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFund(string? name)
        {
            name = name?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                TempData["Error"] = "Give the fund a name.";
                return RedirectToAction(nameof(Index));
            }

            if (await _context.PensionFunds.AnyAsync(f => f.Name.ToLower() == name.ToLower()))
            {
                TempData["Error"] = $"{name} is already here.";
                return RedirectToAction(nameof(Index));
            }

            var fund = new PensionFund { Name = name, CreatedAt = DateTime.UtcNow };

            _context.PensionFunds.Add(fund);

            await _context.SaveChangesAsync();
            await _audit.LogAsync(AuditService.Created, Entity, fund.Id.ToString(), $"Added fund {name}");

            return RedirectToAction(nameof(Fund), new { id = fund.Id });
        }

        public async Task<IActionResult> Fund(int id)
        {
            var fund = await _context.PensionFunds.AsNoTracking()
                .Include(f => f.Accounts)
                .Include(f => f.PolicyLines)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (fund == null)
                return NotFound();

            return View(new FundPageViewModel
            {
                Fund = fund,
                Quarters = await _funds.QuartersAsync(id),
                Imports = await _context.FundStatementImports.AsNoTracking()
                    .Where(i => i.PensionFundId == id)
                    .OrderByDescending(i => i.Id)
                    .Take(20)
                    .ToListAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(FileStorageService.MaxFileSizeBytes * 3)]
        public async Task<IActionResult> Upload(int id, List<IFormFile>? files)
        {
            if (!await _context.PensionFunds.AnyAsync(f => f.Id == id))
                return NotFound();

            if (files == null || files.Count == 0)
            {
                TempData["Error"] = "Choose the positions, transactions and cash_recon files.";
                return RedirectToAction(nameof(Fund), new { id });
            }

            var contents = new List<(string Name, byte[] Content)>();

            foreach (var file in files)
            {
                if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = $"{file.FileName} is not a CSV file.";
                    return RedirectToAction(nameof(Fund), new { id });
                }

                using var memory = new MemoryStream();

                await file.CopyToAsync(memory);

                contents.Add((Path.GetFileName(file.FileName), memory.ToArray()));
            }

            var result = await _funds.ImportAsync(id, contents, User.Identity?.Name);

            if (result.Errors.Count > 0)
            {
                TempData["Error"] = "Nothing was loaded. " + string.Join(" ", result.Errors.Take(10)) +
                    (result.Errors.Count > 10 ? $" And {result.Errors.Count - 10} more." : "");

                return RedirectToAction(nameof(Fund), new { id });
            }

            await _audit.LogAsync(AuditService.DocumentUploaded, Entity, id.ToString(),
                $"Loaded {result.Rows} rows from {result.Files} file(s)");

            TempData["Success"] = $"Loaded {result.Rows} rows from {result.Files} file(s).";

            return RedirectToAction(nameof(Fund), new { id });
        }

        /// <summary>Says an account is closed from a month. An empty month reopens it.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CloseAccount(int accountId, string? month)
        {
            var account = await _context.FundAccounts.FirstOrDefaultAsync(a => a.Id == accountId);

            if (account == null)
                return NotFound();

            account.ClosedFromMonth = ParseMonth(month);

            await _context.SaveChangesAsync();
            await _audit.LogAsync(AuditService.Updated, Entity, accountId.ToString(),
                $"{account.Manager} {account.AccountNumber} closed from {month ?? "(reopened)"}");

            return RedirectToAction(nameof(Fund), new { id = account.PensionFundId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePolicy(int id, List<FundPolicyInput> lines)
        {
            if (!await _context.PensionFunds.AnyAsync(f => f.Id == id))
                return NotFound();

            var filled = lines.Where(l => l.Target != null).ToList();

            if (filled.Any(l => l.Low == null || l.High == null || l.Low > l.Target || l.High < l.Target))
            {
                TempData["Error"] = "Each class with a target needs a corridor around it. Nothing was saved.";
                return RedirectToAction(nameof(Fund), new { id });
            }

            if (filled.Count > 0 && filled.Sum(l => l.Target!.Value) != 100)
            {
                TempData["Error"] = $"Targets should add up to 100%, these add up to {filled.Sum(l => l.Target!.Value)}%. Nothing was saved.";
                return RedirectToAction(nameof(Fund), new { id });
            }

            await _funds.SavePolicyAsync(id, filled.Select(l => new FundPolicyLine
            {
                AssetClass = l.AssetClass,
                Target = l.Target!.Value / 100,
                CorridorLow = l.Low!.Value / 100,
                CorridorHigh = l.High!.Value / 100,
                IpecLimit = l.Ipec / 100
            }).ToList());

            await _audit.LogAsync(AuditService.Updated, Entity, id.ToString(), "Changed the fund's investment policy");

            TempData["Success"] = "Policy saved.";

            return RedirectToAction(nameof(Fund), new { id });
        }

        /// <summary>
        /// One fund's quarter. The figures appear only once every blocking
        /// check is clear: an unproven number is never shown.
        /// </summary>
        public async Task<IActionResult> Quarter(int id, int year, int number, decimal? scenarioRate)
        {
            if (number is < 1 or > 4 || year is < 2000 or > 2100)
                return NotFound();

            var fund = await _context.PensionFunds.AsNoTracking()
                .Include(f => f.PolicyLines)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (fund == null)
                return NotFound();

            var quarter = new FundQuarter(year, number);
            var data = await _funds.LoadAsync(id, quarter);
            var findings = FundReconciliation.Run(data, quarter, _options);
            var ready = FundReconciliation.IsReady(findings);

            FundQuarterReport? report = null;

            if (ready)
            {
                var markets = await _context.FundMarketQuarters.AsNoTracking().ToListAsync();

                report = FundReportBuilder.Build(data, quarter, fund.PolicyLines.ToList(), markets, _options, scenarioRate);
            }

            return View(new FundQuarterViewModel
            {
                Fund = fund,
                Quarter = quarter,
                Findings = findings,
                Report = report,
                ScenarioRate = scenarioRate
            });
        }

        /// <summary>The decisions waiting for an analyst, and those already made.</summary>
        public async Task<IActionResult> Review()
        {
            return View(new FundReviewViewModel
            {
                Labels = await _funds.LabelsToDecideAsync(),
                Rules = await _context.FundTransactionRules.AsNoTracking()
                    .OrderBy(r => r.Manager).ThenBy(r => r.Label).ToListAsync(),
                Securities = await _context.FundSecurities.AsNoTracking()
                    .OrderBy(s => s.Confirmed).ThenBy(s => s.Name).ToListAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DecideLabel(string manager, string label, FundReturnTreatment treatment)
        {
            if (treatment == FundReturnTreatment.Review || string.IsNullOrWhiteSpace(manager) || string.IsNullOrWhiteSpace(label))
            {
                TempData["Error"] = "Choose what this label means.";
                return RedirectToAction(nameof(Review));
            }

            var changed = await _funds.DecideLabelAsync(manager, label, treatment, User.Identity?.Name);

            await _audit.LogAsync(AuditService.Updated, Entity, null, $"{manager}: \"{label}\" is {treatment}");

            TempData["Success"] = $"{manager}: \"{label}\" is now {treatment}, on {changed} transaction(s) across all its clients.";

            return RedirectToAction(nameof(Review));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmSecurity(int id, FundAssetClass assetClass, bool prescribed, bool largeCap)
        {
            if (!await _context.FundSecurities.AnyAsync(s => s.Id == id))
                return NotFound();

            await _funds.ConfirmSecurityAsync(id, assetClass, prescribed, largeCap, User.Identity?.Name);

            await _audit.LogAsync(AuditService.Updated, Entity, id.ToString(),
                $"Security classed as {assetClass}, prescribed {prescribed}, large-cap {largeCap}");

            return RedirectToAction(nameof(Review));
        }

        public async Task<IActionResult> MarketData()
        {
            return View(new FundMarketDataViewModel
            {
                Rates = await _context.FundFxRates.AsNoTracking().OrderByDescending(r => r.Month).ToListAsync(),
                Quarters = await _context.FundMarketQuarters.AsNoTracking()
                    .OrderByDescending(m => m.Year).ThenByDescending(m => m.Quarter).ToListAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRate(string? month, decimal? rate)
        {
            if (ParseMonth(month) is not DateTime parsed || rate is not > 0)
            {
                TempData["Error"] = "Give the month and a rate above zero.";
                return RedirectToAction(nameof(MarketData));
            }

            await _funds.SaveRateAsync(parsed, rate.Value);
            await _audit.LogAsync(AuditService.Updated, Entity, null, $"ZWG rate for {month} is {rate}");

            return RedirectToAction(nameof(MarketData));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveMarketQuarter(FundMarketQuarterInput input)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Fill in every return for the quarter, as percentages.";
                return RedirectToAction(nameof(MarketData));
            }

            await _funds.SaveMarketQuarterAsync(new FundMarketQuarter
            {
                Year = input.Year,
                Quarter = input.Quarter,
                Cash = input.Cash!.Value / 100,
                MoneyMarket = input.MoneyMarket!.Value / 100,
                Bonds = input.Bonds!.Value / 100,
                ZseAllShareUsd = input.ZseAllShareUsd!.Value / 100,
                VfexAllShare = input.VfexAllShare!.Value / 100,
                UsdInflation = input.UsdInflation!.Value / 100
            });

            await _audit.LogAsync(AuditService.Updated, Entity, null, $"Market returns for Q{input.Quarter} {input.Year}");

            return RedirectToAction(nameof(MarketData));
        }

        private static DateTime? ParseMonth(string? month) =>
            DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;
    }
}
