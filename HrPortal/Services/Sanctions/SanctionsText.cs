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

        public static string Size(long bytes) =>
            bytes >= 1024 * 1024
                ? $"{bytes / 1024d / 1024d:0.0} MB"
                : $"{Math.Max(1, bytes / 1024)} KB";
    }
}
