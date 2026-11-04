using System.Globalization;
using System.Text.RegularExpressions;
using HrPortal.Services.Sanctions;

namespace HrPortal.Services
{
    public sealed record LeaveBalanceEmployee(int Id, string Number, string Name, string? Email);

    public sealed record LeaveBalanceRow(int RowNumber, int EmployeeId, string EmployeeName, decimal Days);

    public sealed class LeaveBalanceImportResult
    {
        public string? Error { get; set; }

        public int Received { get; set; }

        public int LeftBlank { get; set; }

        public List<LeaveBalanceRow> Rows { get; } = new();

        /// <summary>Each row that was left out, and why.</summary>
        public List<string> Problems { get; } = new();
    }

    /// <summary>
    /// Reads a file of leave balances: a column saying who (employee
    /// number, email or name) and a column with the days. A row with no
    /// days is skipped, so a template can be filled in part by part. A
    /// row that cannot be tied to exactly one employee is left out and
    /// reported, never guessed at.
    /// </summary>
    public static class LeaveBalanceImport
    {
        private static readonly string[] NumberHeadings =
        {
            "employee number", "employee no", "employee no.", "employee code", "employee id",
            "emp no", "emp no.", "emp number", "staff number", "staff no", "staff no.", "number"
        };

        private static readonly string[] EmailHeadings = { "email", "email address", "e-mail" };

        private static readonly string[] NameHeadings =
        {
            "employee name", "employee", "name", "full name", "staff name"
        };

        private static readonly string[] DaysHeadings =
        {
            "balance", "leave balance", "days", "leave days", "days balance", "leave days balance",
            "annual leave", "annual leave balance", "annual leave days", "days remaining", "remaining"
        };

        public static LeaveBalanceImportResult Parse(
            ClientTable table,
            IReadOnlyList<LeaveBalanceEmployee> employees)
        {
            var result = new LeaveBalanceImportResult { Received = table.Rows.Count };

            var headings = table.Headings
                .Select(h => Regex.Replace(h.Trim().ToLowerInvariant(), @"[\s_]+", " "))
                .ToList();

            int Column(string[] names) => headings.FindIndex(names.Contains);

            var number = Column(NumberHeadings);
            var email = Column(EmailHeadings);
            var name = Column(NameHeadings);
            var days = Column(DaysHeadings);

            if (days < 0 || (number < 0 && email < 0 && name < 0))
            {
                result.Error =
                    "The file needs a heading row with a column saying who (Employee Number, Email " +
                    "or Employee Name) and a column for the days (Balance). The template has both.";
                return result;
            }

            var seen = new HashSet<int>();

            for (var i = 0; i < table.Rows.Count; i++)
            {
                var row = table.Rows[i];
                var rowNumber = i + 2;
                var daysText = table.Cell(row, days);

                if (daysText == null)
                {
                    result.LeftBlank++;
                    continue;
                }

                if (!TryParseDays(daysText, out var value))
                {
                    result.Problems.Add($"Row {rowNumber}: \"{daysText}\" is not a number of days.");
                    continue;
                }

                var matches = Find(employees, table.Cell(row, number), table.Cell(row, email), table.Cell(row, name));

                if (matches.Count != 1)
                {
                    var who = table.Cell(row, number) ?? table.Cell(row, email) ?? table.Cell(row, name) ?? "nobody given";

                    result.Problems.Add(matches.Count == 0
                        ? $"Row {rowNumber}: no employee found for \"{who}\"."
                        : $"Row {rowNumber}: more than one employee is called \"{who}\". Use the employee number.");
                    continue;
                }

                if (!seen.Add(matches[0].Id))
                {
                    result.Problems.Add($"Row {rowNumber}: {matches[0].Name} is already in the file higher up; the first row was used.");
                    continue;
                }

                result.Rows.Add(new LeaveBalanceRow(rowNumber, matches[0].Id, matches[0].Name, value));
            }

            return result;
        }

        private static List<LeaveBalanceEmployee> Find(
            IReadOnlyList<LeaveBalanceEmployee> employees,
            string? number,
            string? email,
            string? name)
        {
            if (number != null)
            {
                return employees
                    .Where(e => string.Equals(e.Number.Trim(), number, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (email != null)
            {
                return employees
                    .Where(e => string.Equals(e.Email?.Trim(), email, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (name == null)
                return new List<LeaveBalanceEmployee>();

            var wanted = NameKey(name);

            return employees.Where(e => NameKey(e.Name) == wanted).ToList();
        }

        private static string NameKey(string name) =>
            string.Join(' ', SanctionsListParser.Normalise(name).Split(' ').OrderBy(w => w, StringComparer.Ordinal));

        /// <summary>
        /// Days as people type them: 12.5 or 12,5. Limited to what a
        /// balance could sensibly be, so a stray ID number is caught.
        /// </summary>
        public static bool TryParseDays(string text, out decimal days)
        {
            var parsed = decimal.TryParse(
                text.Trim().Replace(',', '.'),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out days);

            days = Math.Round(days, 2);

            return parsed && Math.Abs(days) <= 1000;
        }
    }
}
