using System.Globalization;
using System.Text;

namespace HrPortal.Services
{
    /// <summary>
    /// Builds the CSV behind every report export.
    ///
    /// Values are quoted and internal quotes doubled, and anything a
    /// spreadsheet would treat as a formula is prefixed so an exported
    /// note starting with "=" cannot execute when the file is opened.
    /// </summary>
    public static class CsvExportService
    {
        public static byte[] Build(
            IReadOnlyList<string> headers,
            IEnumerable<IReadOnlyList<string?>> rows)
        {
            var builder = new StringBuilder();

            builder.AppendLine(
                string.Join(",", headers.Select(Escape)));

            foreach (var row in rows)
            {
                builder.AppendLine(
                    string.Join(",", row.Select(Escape)));
            }

            // The BOM keeps accented names readable when the file is
            // opened in Excel.
            return Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(builder.ToString()))
                .ToArray();
        }

        public static string FileName(string report)
        {
            return $"{report}-{DateTime.Today:yyyy-MM-dd}.csv";
        }

        private static string Escape(string? value)
        {
            var text = value ?? string.Empty;

            // A plain negative number is data, not a formula, and
            // prefixing it would land in the spreadsheet as text and
            // stop the column sorting or totalling.
            if (text.Length > 0 &&
                (text[0] == '=' || text[0] == '+' ||
                 text[0] == '-' || text[0] == '@') &&
                !IsPlainNumber(text))
            {
                text = "'" + text;
            }

            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private static bool IsPlainNumber(string text) =>
            decimal.TryParse(
                text,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out _);
    }
}
