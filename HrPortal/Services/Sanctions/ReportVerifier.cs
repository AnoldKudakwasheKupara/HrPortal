using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace HrPortal.Services.Sanctions
{
    public enum ReportVerdict
    {
        /// <summary>Byte for byte the report the portal made and kept.</summary>
        Genuine,

        /// <summary>Says it is one of the portal's reports, but is not the one kept.</summary>
        Altered,

        /// <summary>Not a report the portal made, or too damaged to say which.</summary>
        NotFound
    }

    /// <summary>
    /// Works out which review a report says it belongs to. This is only
    /// ever used to explain a failed check: a report is genuine only
    /// when its SHA-256 is the one the portal kept, never because of
    /// anything written inside it.
    /// </summary>
    public static class ReportVerifier
    {
        private static readonly Regex ReviewNumber = new(@"\bSAN-\d{4}-\d{4}\b", RegexOptions.Compiled);

        public static string? ClaimedReviewNumber(byte[] pdf)
        {
            try
            {
                using var document = PdfDocument.Open(pdf);

                foreach (var text in new[] { document.Information.Subject, document.Information.Title, document.Information.Keywords })
                {
                    if (text != null && ReviewNumber.Match(text) is { Success: true } m)
                        return m.Value;
                }

                if (document.NumberOfPages > 0 &&
                    ReviewNumber.Match(document.GetPage(1).Text) is { Success: true } onPage)
                {
                    return onPage.Value;
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }
    }
}
