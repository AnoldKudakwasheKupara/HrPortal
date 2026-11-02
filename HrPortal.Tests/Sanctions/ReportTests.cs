using System.Text;
using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.Services.Sanctions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig;

namespace HrPortal.Tests.Sanctions
{
    public class ReportTests
    {
        private sealed class Env : IWebHostEnvironment
        {
            public string WebRootPath { get; set; } = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HrPortal", "wwwroot"));
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ApplicationName { get; set; } = "HrPortal";
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = Path.GetTempPath();
            public string EnvironmentName { get; set; } = "Testing";
        }

        static ReportTests()
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
            DocumentFonts.Register(new Env(), NullLogger.Instance);
        }

        private static SanctionsReview Review()
        {
            var document = new SanctionsDocument
            {
                Id = 2,
                Title = "Security Council 1988 Committee Amends Five Names on Its Sanctions List",
                DocumentReference = "SC/16494",
                IssuedOn = new DateTime(2026, 10, 29),
                Kind = SanctionsDocumentKind.Amendments,
                ListName = "1988 Sanctions List",
                Source = "United Nations Security Council",
                EntryCount = 5,
                PageCount = 4,
                OriginalFileName = "notice.pdf",
                Sha256 = new string('a', 64),
                UploadedAt = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc),
                UploadedByName = "Compliance Officer"
            };

            return new SanctionsReview
            {
                Id = 1,
                ReviewNumber = "SAN-2026-0042",
                NewDocument = document,
                CreatedAt = new DateTime(2026, 11, 2, 9, 5, 0, DateTimeKind.Utc),
                CreatedByName = "Compliance Officer",
                Screened = true,
                ScreenedStaff = 40,
                ScreenedVendors = 12,
                FinalisedAt = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc),
                FinalisedByName = "Compliance Officer",
                Changes =
                {
                    new SanctionsChange
                    {
                        Type = SanctionsChangeType.Changed,
                        Reference = "TAi.007",
                        Name = "ABDUL LATIF MANSUR",
                        Page = 1,
                        Differences = "Passport numbers: none → P05629010"
                    }
                },
                Concerns =
                {
                    new SanctionsConcern
                    {
                        Category = SanctionsConcernCategory.PossibleMatch,
                        Strength = SanctionsMatchStrength.Potential,
                        Status = SanctionsConcernStatus.UnableToDetermine,
                        SubjectType = "Vendor contact",
                        SubjectName = "Abdul Mansoor",
                        Reference = "TAi.007",
                        ListedName = "ABDUL LATIF MANSUR",
                        Reason = "Name resembles \"ABDUL LATIF MANSUR\".",
                        Evidence = "Name similarity: medium (84%)\nDate of birth: not available for this person",
                        Resolution = "The vendor could not provide ID.",
                        EscalatedTo = "Managing Director",
                        ResolvedByName = "Compliance Officer",
                        ResolvedAt = new DateTime(2026, 11, 2, 9, 30, 0, DateTimeKind.Utc)
                    }
                }
            };
        }

        private static byte[] Render() =>
            new SanctionsReportPdfService(new BrandAssets(new Env(), NullLogger<BrandAssets>.Instance)).Render(Review());

        private static string Text(byte[] pdf)
        {
            using var document = PdfDocument.Open(pdf);
            return string.Join("\n", document.GetPages().Select(p => p.Text));
        }

        [Fact]
        public void The_report_covers_what_an_audit_needs()
        {
            var text = Text(Render());

            foreach (var expected in new[]
                     {
                         "SAN-2026-0042", "Intellego Investment Consultants", "Notice of amendments", "1988 Sanctions List",
                         "United Nations Security Council", "TAi.007", "P05629010", "Unable to determine",
                         "Managing Director", "Outstanding matters and limitations", "not a finding"
                     })
            {
                Assert.True(text.Contains(expected, StringComparison.OrdinalIgnoreCase), $"Missing: {expected}");
            }
        }

        [Fact]
        public void A_report_says_which_review_it_belongs_to()
        {
            Assert.Equal("SAN-2026-0042", ReportVerifier.ClaimedReviewNumber(Render()));
        }

        [Fact]
        public void An_altered_copy_has_a_different_fingerprint_but_still_says_which_review()
        {
            var original = Render();
            var altered = original.Concat(Encoding.ASCII.GetBytes("\n% changed\n")).ToArray();

            Assert.NotEqual(SanctionsService.Sha256Of(original), SanctionsService.Sha256Of(altered));
            Assert.Equal("SAN-2026-0042", ReportVerifier.ClaimedReviewNumber(altered));
        }

        [Fact]
        public void A_file_that_is_not_a_report_claims_nothing()
        {
            Assert.Null(ReportVerifier.ClaimedReviewNumber("not a pdf"u8.ToArray()));

            using var other = TestPdfs.NoText();
            Assert.Null(ReportVerifier.ClaimedReviewNumber(other.ToArray()));
        }

        [Fact]
        public void The_same_review_renders_to_the_same_findings()
        {
            Assert.Equal(SanctionsFindings.Sentences(Review()), SanctionsFindings.Sentences(Review()));
        }
    }
}
