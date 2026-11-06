using System.Security.Cryptography;
using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.FundReporting
{
    public sealed record FundImportResult(int Files, int Rows, IReadOnlyList<string> Errors);

    public sealed record FundLabelToDecide(string Manager, string Label, int Transactions);

    /// <summary>What the module keeps in the database: loading statements, and the analysts' decisions.</summary>
    public class FundReportingService
    {
        private readonly ApplicationDbContext _context;

        public FundReportingService(ApplicationDbContext context) => _context = context;

        /// <summary>
        /// Loads statement files into a fund. A file replaces what is held
        /// for the accounts and months it covers, so a re-issued statement is
        /// loaded the same way as the first. If any file has a bad row, none
        /// of them is loaded.
        /// </summary>
        public async Task<FundImportResult> ImportAsync(int fundId, IReadOnlyList<(string Name, byte[] Content)> files, string? userName)
        {
            var read = new List<(string Name, string Hash, FundCsvFile File)>();
            var errors = new List<string>();

            foreach (var group in files.GroupBy(f => Convert.ToHexString(SHA256.HashData(f.Content))))
            {
                var (name, content) = group.First();

                using var stream = new MemoryStream(content);

                var file = FundCsvReader.Read(stream, name);

                errors.AddRange(file.Errors);
                read.Add((name, group.Key, file));
            }

            if (errors.Count > 0)
                return new(0, 0, errors);

            var accounts = await _context.FundAccounts.Where(a => a.PensionFundId == fundId).ToListAsync();

            FundAccount AccountFor(string manager, string number, string? subFund)
            {
                var account = accounts.FirstOrDefault(a =>
                    string.Equals(a.Manager, manager, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(a.AccountNumber, number, StringComparison.OrdinalIgnoreCase));

                if (account == null)
                {
                    account = new FundAccount { PensionFundId = fundId, Manager = manager, AccountNumber = number };
                    accounts.Add(account);
                    _context.FundAccounts.Add(account);
                }

                if (subFund != null)
                    account.SubFund = subFund;

                return account;
            }

            var rules = await _context.FundTransactionRules.ToListAsync();

            FundReturnTreatment TreatmentFor(string manager, string label) =>
                rules.FirstOrDefault(r =>
                    string.Equals(r.Manager, manager, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.Label, label, StringComparison.OrdinalIgnoreCase))?.Treatment
                ?? FundReturnTreatment.Review;

            foreach (var (name, hash, file) in read)
            {
                await ClearAsync(fundId, file);

                foreach (var row in file.Holdings)
                {
                    row.Value.Position.Account = AccountFor(row.Manager, row.Account, row.SubFund);
                    _context.FundPositions.Add(row.Value.Position);

                    await ProposeSecurityAsync(row.Value);
                }

                foreach (var row in file.Transactions)
                {
                    row.Value.Account = AccountFor(row.Manager, row.Account, row.SubFund);
                    row.Value.Treatment = TreatmentFor(row.Manager, row.Value.SourceLabel);
                    _context.FundTransactions.Add(row.Value);
                }

                foreach (var row in file.Balances)
                {
                    row.Value.Account = AccountFor(row.Manager, row.Account, row.SubFund);
                    _context.FundStatementBalances.Add(row.Value);
                }

                _context.FundStatementImports.Add(new FundStatementImport
                {
                    PensionFundId = fundId,
                    Kind = file.Kind.ToString()!,
                    FileName = name,
                    Sha256 = hash,
                    RowCount = file.RowCount,
                    UploadedAt = DateTime.UtcNow,
                    UploadedByName = userName
                });
            }

            await _context.SaveChangesAsync();

            return new(read.Count, read.Sum(r => r.File.RowCount), []);
        }

        /// <summary>Removes what the file is about to replace.</summary>
        private async Task ClearAsync(int fundId, FundCsvFile file)
        {
            bool Covers<T>(IEnumerable<FundCsvRow<T>> rows, FundAccount? account, DateTime month) =>
                account != null && rows.Any(r =>
                    r.Month == month &&
                    string.Equals(r.Manager, account.Manager, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.Account, account.AccountNumber, StringComparison.OrdinalIgnoreCase));

            if (file.Kind == FundCsvKind.Positions)
            {
                var held = await _context.FundPositions.Include(p => p.Account)
                    .Where(p => p.Account!.PensionFundId == fundId).ToListAsync();

                _context.FundPositions.RemoveRange(held.Where(p => Covers(file.Holdings, p.Account, p.Month)));
            }
            else if (file.Kind == FundCsvKind.Transactions)
            {
                var held = await _context.FundTransactions.Include(t => t.Account)
                    .Where(t => t.Account!.PensionFundId == fundId).ToListAsync();

                _context.FundTransactions.RemoveRange(held.Where(t => Covers(file.Transactions, t.Account, t.Month)));
            }
            else
            {
                var held = await _context.FundStatementBalances.Include(b => b.Account)
                    .Where(b => b.Account!.PensionFundId == fundId).ToListAsync();

                _context.FundStatementBalances.RemoveRange(held.Where(b => Covers(file.Balances, b.Account, b.Month)));
            }
        }

        /// <summary>
        /// Adds a new security with the class the file proposes. Once an
        /// analyst has confirmed a security, a file never changes it.
        /// </summary>
        private async Task ProposeSecurityAsync(FundCsvHolding holding)
        {
            var code = holding.Position.SecurityCode;

            var security =
                _context.FundSecurities.Local.FirstOrDefault(s => s.SecurityCode == code) ??
                await _context.FundSecurities.FirstOrDefaultAsync(s => s.SecurityCode == code);

            if (security == null)
            {
                security = new FundSecurity { SecurityCode = code };
                _context.FundSecurities.Add(security);
            }

            if (security.Confirmed)
                return;

            security.Name = holding.Position.SecurityName;
            security.AssetClass = holding.ProposedClass ?? security.AssetClass;
            security.Prescribed = holding.ProposedPrescribed;
        }

        /// <summary>A fund's figures, far enough back for a three-year return ending this quarter.</summary>
        public async Task<FundDataSet> LoadAsync(int fundId, FundQuarter quarter)
        {
            var from = quarter.Trailing(12)[0].OpeningMonth;
            var to = quarter.LastMonth;

            var accounts = await _context.FundAccounts.AsNoTracking()
                .Where(a => a.PensionFundId == fundId).ToListAsync();

            var ids = accounts.Select(a => a.Id).ToList();

            var positions = await _context.FundPositions.AsNoTracking()
                .Where(p => ids.Contains(p.FundAccountId) && p.Month >= from && p.Month <= to).ToListAsync();

            var codes = positions.Select(p => p.SecurityCode).Distinct().ToList();

            return new FundDataSet
            {
                Accounts = accounts,
                Positions = positions,
                Transactions = await _context.FundTransactions.AsNoTracking()
                    .Where(t => ids.Contains(t.FundAccountId) && t.Month >= from && t.Month <= to).ToListAsync(),
                Balances = await _context.FundStatementBalances.AsNoTracking()
                    .Where(b => ids.Contains(b.FundAccountId) && b.Month >= from && b.Month <= to).ToListAsync(),
                Securities = await _context.FundSecurities.AsNoTracking()
                    .Where(s => codes.Contains(s.SecurityCode)).ToDictionaryAsync(s => s.SecurityCode),
                Rates = await _context.FundFxRates.AsNoTracking()
                    .Where(r => r.Month >= from && r.Month <= to).ToDictionaryAsync(r => r.Month, r => r.ZwgPerUsd)
            };
        }

        /// <summary>The quarters a fund has statements in, newest first.</summary>
        public async Task<IReadOnlyList<FundQuarter>> QuartersAsync(int fundId)
        {
            var months = await _context.FundPositions.AsNoTracking()
                .Where(p => p.Account!.PensionFundId == fundId)
                .Select(p => p.Month)
                .Distinct()
                .ToListAsync();

            return months.Select(FundQuarter.Containing).Distinct()
                .OrderByDescending(q => q.Year).ThenByDescending(q => q.Number)
                .ToList();
        }

        public async Task<IReadOnlyList<FundLabelToDecide>> LabelsToDecideAsync()
        {
            var rows = await _context.FundTransactions.AsNoTracking()
                .Where(t => t.Treatment == FundReturnTreatment.Review)
                .Select(t => new { t.Account!.Manager, t.SourceLabel })
                .ToListAsync();

            return rows
                .GroupBy(r => (Manager: r.Manager.ToUpperInvariant(), Label: r.SourceLabel.ToUpperInvariant()))
                .Select(g => new FundLabelToDecide(g.First().Manager, g.First().SourceLabel, g.Count()))
                .OrderBy(l => l.Manager).ThenBy(l => l.Label)
                .ToList();
        }

        /// <summary>
        /// Records a ruling on a label and applies it to every client of that
        /// manager, so it is never asked again. Returns the transactions changed.
        /// </summary>
        public async Task<int> DecideLabelAsync(string manager, string label, FundReturnTreatment treatment, string? userName)
        {
            var managerKey = manager.ToLower();
            var labelKey = label.ToLower();

            var rule = await _context.FundTransactionRules
                .FirstOrDefaultAsync(r => r.Manager.ToLower() == managerKey && r.Label.ToLower() == labelKey);

            if (rule == null)
            {
                rule = new FundTransactionRule { Manager = manager, Label = label };
                _context.FundTransactionRules.Add(rule);
            }

            rule.Treatment = treatment;
            rule.DecidedByName = userName;
            rule.DecidedAt = DateTime.UtcNow;

            var transactions = await _context.FundTransactions
                .Where(t => t.Account!.Manager.ToLower() == managerKey && t.SourceLabel.ToLower() == labelKey)
                .ToListAsync();

            foreach (var transaction in transactions)
                transaction.Treatment = treatment;

            await _context.SaveChangesAsync();

            return transactions.Count;
        }

        public async Task ConfirmSecurityAsync(int securityId, FundAssetClass assetClass, bool prescribed, bool largeCap, string? userName)
        {
            var security = await _context.FundSecurities.FirstAsync(s => s.Id == securityId);

            security.AssetClass = assetClass;
            security.Prescribed = prescribed;
            security.LargeCap = largeCap;
            security.Confirmed = true;
            security.ConfirmedByName = userName;
            security.ConfirmedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task SaveRateAsync(DateTime month, decimal zwgPerUsd)
        {
            var first = new DateTime(month.Year, month.Month, 1);

            var rate = await _context.FundFxRates.FirstOrDefaultAsync(r => r.Month == first);

            if (rate == null)
            {
                rate = new FundFxRate { Month = first };
                _context.FundFxRates.Add(rate);
            }

            rate.ZwgPerUsd = zwgPerUsd;

            await _context.SaveChangesAsync();
        }

        public async Task SaveMarketQuarterAsync(FundMarketQuarter values)
        {
            var market = await _context.FundMarketQuarters
                .FirstOrDefaultAsync(m => m.Year == values.Year && m.Quarter == values.Quarter);

            if (market == null)
            {
                market = new FundMarketQuarter { Year = values.Year, Quarter = values.Quarter };
                _context.FundMarketQuarters.Add(market);
            }

            market.Cash = values.Cash;
            market.MoneyMarket = values.MoneyMarket;
            market.Bonds = values.Bonds;
            market.ZseAllShareUsd = values.ZseAllShareUsd;
            market.VfexAllShare = values.VfexAllShare;
            market.UsdInflation = values.UsdInflation;

            await _context.SaveChangesAsync();
        }

        /// <summary>Replaces a fund's policy with these lines.</summary>
        public async Task SavePolicyAsync(int fundId, IReadOnlyList<FundPolicyLine> lines)
        {
            _context.FundPolicyLines.RemoveRange(
                await _context.FundPolicyLines.Where(l => l.PensionFundId == fundId).ToListAsync());

            foreach (var line in lines)
            {
                line.PensionFundId = fundId;
                _context.FundPolicyLines.Add(line);
            }

            await _context.SaveChangesAsync();
        }
    }
}
