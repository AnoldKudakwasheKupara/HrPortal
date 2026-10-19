using HrPortal.ViewModels;

namespace HrPortal.Services.Reporting
{
    public class ReportPayload
    {
        public bool IsCsv { get; private init; }

        public object? Model { get; private init; }

        public string CsvName { get; private init; } = string.Empty;

        public IReadOnlyList<string> CsvHeaders { get; private init; } =
            Array.Empty<string>();

        public IEnumerable<IReadOnlyList<string?>> CsvRows
        { get; private init; } = Array.Empty<IReadOnlyList<string?>>();

        public static ReportPayload ForView(object model) =>
            new() { Model = model };

        public static ReportPayload ForCsv(
            string name,
            IReadOnlyList<string> headers,
            IEnumerable<IReadOnlyList<string?>> rows) =>
            new()
            {
                IsCsv = true,
                CsvName = name,
                CsvHeaders = headers,
                CsvRows = rows
            };
    }
}
