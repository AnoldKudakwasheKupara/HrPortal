using System.Globalization;
using System.Text;

namespace HrPortal.Services
{
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
