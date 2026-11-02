using HrPortal.Entities;

namespace HrPortal.Services.Sanctions
{
    /// <summary>How sanctions terms are worded on screen and in reports.</summary>
    public static class SanctionsText
    {
        public static string Kind(SanctionsDocumentKind kind) =>
            kind switch
            {
                SanctionsDocumentKind.FullList => "Full list",
                SanctionsDocumentKind.Additions => "Notice of additions",
                SanctionsDocumentKind.Amendments => "Notice of amendments",
                SanctionsDocumentKind.Removals => "Notice of removals",
                _ => "Notice of changes"
            };

        /// <summary>A change's stored differences, "Field: before → after" a line, as rows.</summary>
        public static List<FieldDifference> Differences(string? stored)
        {
            var rows = new List<FieldDifference>();

            foreach (var line in SanctionsService.Lines(stored))
            {
                var colon = line.IndexOf(": ", StringComparison.Ordinal);
                var arrow = line.IndexOf(" → ", StringComparison.Ordinal);

                if (colon < 0 || arrow < colon)
                {
                    rows.Add(new FieldDifference(line, null, null));
                    continue;
                }

                string? Value(string v) => v == "none" ? null : v;

                rows.Add(new FieldDifference(
                    line[..colon],
                    Value(line[(colon + 2)..arrow]),
                    Value(line[(arrow + 3)..])));
            }

            return rows;
        }

        public static string ChangeType(SanctionsChangeType type) =>
            type switch
            {
                SanctionsChangeType.Added => "Added",
                SanctionsChangeType.Removed => "Removed",
                _ => "Amended"
            };

        public static string Size(long bytes) =>
            bytes >= 1024 * 1024
                ? $"{bytes / 1024d / 1024d:0.0} MB"
                : $"{Math.Max(1, bytes / 1024)} KB";
    }
}
