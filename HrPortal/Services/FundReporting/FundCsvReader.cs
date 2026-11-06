using System.Globalization;
using System.Text.RegularExpressions;
using HrPortal.Entities;
using HrPortal.Services.Sanctions;

namespace HrPortal.Services.FundReporting
{
    public enum FundCsvKind
    {
        Positions,
        Transactions,
        CashRecon
    }

    /// <summary>Which account a row belongs to, and what it says.</summary>
    public sealed record FundCsvRow<T>(DateTime Month, string Manager, string Account, string? SubFund, T Value);

    /// <summary>A holding as read, with what the file proposes for the security.</summary>
    public sealed record FundCsvHolding(FundPosition Position, FundAssetClass? ProposedClass, bool ProposedPrescribed);

    public sealed class FundCsvFile
    {
        public FundCsvKind? Kind { get; set; }

        public List<FundCsvRow<FundCsvHolding>> Holdings { get; } = new();

        public List<FundCsvRow<FundTransaction>> Transactions { get; } = new();

        public List<FundCsvRow<FundStatementBalance>> Balances { get; } = new();

        public List<string> Errors { get; } = new();

        public int RowCount => Holdings.Count + Transactions.Count + Balances.Count;
    }

    /// <summary>
    /// Reads positions.csv, transactions.csv and cash_recon.csv, with the
    /// columns of section 4 of the reporting guide. A file with a bad row is
    /// refused whole, so a statement is never half loaded.
    /// </summary>
    public static class FundCsvReader
    {
        public static FundCsvFile Read(Stream stream, string fileName)
        {
            var result = new FundCsvFile();
            var rows = ClientListReader.ReadCsv(stream);

            if (rows.Count == 0)
            {
                result.Errors.Add($"{fileName} is empty.");
                return result;
            }

            var columns = rows[0]
                .Select((name, index) => (Name: Normalise(name), Index: index))
                .GroupBy(c => c.Name)
                .ToDictionary(g => g.Key, g => g.First().Index);

            if (columns.ContainsKey("marketvalue"))
                result.Kind = FundCsvKind.Positions;
            else if (columns.ContainsKey("netamount"))
                result.Kind = FundCsvKind.Transactions;
            else if (columns.ContainsKey("opening") || columns.ContainsKey("printedtotal"))
                result.Kind = FundCsvKind.CashRecon;
            else
            {
                result.Errors.Add($"{fileName}: the columns are not those of positions, transactions or cash_recon.");
                return result;
            }

            for (var i = 1; i < rows.Count; i++)
            {
                if (rows[i].All(string.IsNullOrWhiteSpace))
                    continue;

                var row = new Row(rows[i], columns, fileName, i + 1, result.Errors);

                switch (result.Kind)
                {
                    case FundCsvKind.Positions: ReadHolding(row, result); break;
                    case FundCsvKind.Transactions: ReadTransaction(row, result); break;
                    default: ReadBalance(row, result); break;
                }
            }

            return result;
        }

        private static void ReadHolding(Row row, FundCsvFile result)
        {
            var month = row.Month();
            var code = row.Required("Security_ID");
            var value = row.RequiredNumber("Market_Value");
            var (manager, account) = (row.Required("Manager"), row.Required("Sub_Acct"));

            if (month == null || code == null || value == null || manager == null || account == null)
                return;

            var position = new FundPosition
            {
                Month = month.Value,
                SecurityCode = code,
                SecurityName = row.Text("Security_Name") ?? code,
                Currency = row.Currency(),
                Units = row.Number("Units_Nominal"),
                BookCost = row.Number("Book_Cost"),
                MarketValue = value.Value,
                AccruedInterest = row.Number("Accrued_Interest"),
                RatePercent = row.Number("Rate_%"),
                ValueDate = row.Date("Value_Date"),
                MaturityDate = row.Date("Maturity_Date"),
                SourceFile = row.Text("Source_File") ?? row.FileName,
                SourceSection = row.Text("Source_Section"),
                Confidence = row.Text("Confidence")
            };

            var prescribed = string.Equals(row.Text("Prescribed"), "yes", StringComparison.OrdinalIgnoreCase);

            result.Holdings.Add(new(month.Value, manager, account, row.Text("Sub_Fund"),
                new FundCsvHolding(position, FundAssetClasses.FromCode(row.Text("Asset_Class_Code")), prescribed)));
        }

        private static void ReadTransaction(Row row, FundCsvFile result)
        {
            var month = row.Month();
            var net = row.RequiredNumber("Net_Amount");
            var ledger = row.RequiredNumber("Ledger_Delta");
            var effect = row.Required("Cash_Effect");
            var (manager, account) = (row.Required("Manager"), row.Required("Sub_Acct"));
            var type = row.Text("Txn_Type");
            var label = row.Text("Source_Label") ?? type;

            if (label == null)
                row.Fail("needs a Source_Label or a Txn_Type");

            bool? cashIn = effect?.ToLowerInvariant() switch
            {
                "cash in" or "in" => true,
                "cash out" or "out" => false,
                null => null,
                _ => null
            };

            if (effect != null && cashIn == null)
                row.Fail($"Cash_Effect \"{effect}\" should be Cash In or Cash Out");

            if (month == null || net == null || ledger == null || cashIn == null || manager == null || account == null || label == null)
                return;

            result.Transactions.Add(new(month.Value, manager, account, row.Text("Sub_Fund"), new FundTransaction
            {
                Month = month.Value,
                TxnDate = row.Date("Txn_Date"),
                Currency = row.Currency(),
                SecurityCode = row.Text("Security_ID"),
                SecurityName = row.Text("Security_Name"),
                TxnType = type ?? label,
                Units = row.Number("Units"),
                Price = row.Number("Price"),
                GrossAmount = row.Number("Gross_Amount"),
                Fees = row.Number("Fees"),
                NetAmount = net.Value,
                CashIn = cashIn.Value,
                LedgerDelta = ledger.Value,
                SourceFile = row.Text("Source_File") ?? row.FileName,
                SourceLabel = label,
                Comment = row.Text("Comment")
            }));
        }

        private static void ReadBalance(Row row, FundCsvFile result)
        {
            var month = row.Month();
            var (manager, account) = (row.Required("Manager"), row.Required("Sub_Acct"));

            if (month == null || manager == null || account == null)
                return;

            result.Balances.Add(new(month.Value, manager, account, row.Text("Sub_Fund"), new FundStatementBalance
            {
                Month = month.Value,
                Currency = row.Currency(),
                PrintedTotal = row.Number("Printed_Total"),
                OpeningCash = row.Number("Opening"),
                ClosingCash = row.Number("Closing"),
                SourceFile = row.Text("Source_File") ?? row.FileName
            }));
        }

        private static string Normalise(string name) =>
            Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9%]", "");

        /// <summary>One row, and the ways of reading a cell from it.</summary>
        private sealed class Row
        {
            private readonly IReadOnlyList<string> _cells;
            private readonly IReadOnlyDictionary<string, int> _columns;
            private readonly int _line;
            private readonly List<string> _errors;

            public Row(IReadOnlyList<string> cells, IReadOnlyDictionary<string, int> columns, string fileName, int line, List<string> errors)
            {
                _cells = cells;
                _columns = columns;
                FileName = fileName;
                _line = line;
                _errors = errors;
            }

            public string FileName { get; }

            public void Fail(string message) => _errors.Add($"{FileName} line {_line}: {message}.");

            public string? Text(string column)
            {
                if (!_columns.TryGetValue(Normalise(column), out var index) || index >= _cells.Count)
                    return null;

                var text = _cells[index].Trim();

                return text.Length == 0 ? null : text;
            }

            public string? Required(string column)
            {
                var text = Text(column);

                if (text == null)
                    Fail($"{column} is empty");

                return text;
            }

            public string Currency() => (Text("Currency") ?? "USD").ToUpperInvariant();

            public decimal? Number(string column)
            {
                var text = Text(column);

                if (text == null)
                    return null;

                var negative = text.StartsWith('(') && text.EndsWith(')');
                var plain = text.Trim('(', ')').Replace(",", "");

                if (decimal.TryParse(plain, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                    return negative ? -value : value;

                Fail($"{column} \"{text}\" is not a number");

                return null;
            }

            public decimal? RequiredNumber(string column)
            {
                if (Text(column) == null)
                {
                    Fail($"{column} is empty");
                    return null;
                }

                return Number(column);
            }

            public DateTime? Date(string column)
            {
                var text = Text(column);

                if (text == null)
                    return null;

                string[] formats = ["yyyy-MM-dd", "yyyy-M-d", "d/M/yyyy", "d-MMM-yyyy", "d MMM yyyy"];

                if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    return date;

                Fail($"{column} \"{text}\" is not a date");

                return null;
            }

            /// <summary>The Period column: Sep-2026 or 2026-09.</summary>
            public DateTime? Month()
            {
                var text = Required("Period");

                if (text == null)
                    return null;

                string[] formats = ["yyyy-MM", "MMM-yyyy", "MMMM-yyyy", "MMM yyyy", "MMMM yyyy"];

                if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
                    return new DateTime(month.Year, month.Month, 1);

                Fail($"Period \"{text}\" should look like Sep-2026 or 2026-09");

                return null;
            }
        }
    }
}
