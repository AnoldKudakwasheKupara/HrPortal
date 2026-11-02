using HrPortal.Entities;
using HrPortal.Services.Sanctions;

namespace HrPortal.Tests.Sanctions
{
    public class SanctionsListParserTests
    {
        private static ParsedSanctionsList Sample()
        {
            using var pdf = TestPdfs.Sample();
            return SanctionsListParser.ParsePdf(pdf);
        }

        [Fact]
        public void Sample_notice_has_its_five_entries_on_the_right_pages()
        {
            var list = Sample();

            Assert.Equal(
                new[] { "TAi.007", "TAi.024", "TAi.026", "TAi.082", "TAi.093" },
                list.Entries.Select(e => e.Reference));

            Assert.Equal(new int?[] { 1, 2, 2, 3, 3 }, list.Entries.Select(e => e.Page));
            Assert.All(list.Entries, e => Assert.False(e.IsEntity));
            Assert.Empty(list.Warnings);
        }

        [Fact]
        public void Sample_notice_is_recognised_as_amendments_to_the_1988_list()
        {
            var list = Sample();

            Assert.Equal(SanctionsDocumentKind.Amendments, list.Kind);
            Assert.Equal("1988 Sanctions List", list.ListName);
            Assert.Equal("United Nations Security Council", list.Source);
            Assert.Equal("SC/16494", list.Reference);
            Assert.Equal(new DateTime(2026, 10, 29), list.IssuedOn);
            Assert.Equal(4, list.PageCount);
        }

        [Fact]
        public void Sample_entry_details_are_read_without_inventing_any()
        {
            var entry = Sample().Entries[0];

            Assert.Equal("ABDUL LATIF MANSUR", entry.Name);
            Assert.Contains("Abdul Latif Mansoor", entry.Aliases);
            Assert.Contains("Wali Mohammad", entry.Aliases);
            Assert.Equal("a) Approximately 1968 b) 12 Nov. 1967", entry.DateOfBirth);
            Assert.Equal("Afghanistan", entry.Nationality);
            Assert.Equal(new[] { "D0009720", "D0010442", "P05629010" }, entry.Passports);
            Assert.Empty(entry.NationalIds);
            Assert.NotNull(entry.OriginalScriptName);
            Assert.StartsWith("31 Jan. 2001 (amended on", entry.ListedOn);
        }

        [Fact]
        public void Sample_underlined_amendments_are_picked_out_and_links_are_not()
        {
            var list = Sample();

            Assert.All(list.Entries, e =>
            {
                Assert.Contains(e.AddedText, a => a.Contains("29 Oct. 2026"));
                Assert.Empty(e.RemovedText);
                Assert.DoesNotContain(e.AddedText, a => a.Contains("interpol", StringComparison.OrdinalIgnoreCase));
            });

            Assert.Contains("c) Afghanistan number P05629010", list.Entries[0].AddedText);
            Assert.Contains("b) Afghanistan number P04411676", list.Entries[4].AddedText);
        }

        [Fact]
        public void Struck_through_text_is_taken_out_and_kept_as_removed()
        {
            using var pdf = TestPdfs.Document(
                "Security Council 1988 Committee Amends One Name on Its Sanctions List",
                new[]
                {
                    new Span("TAi.500 Name: 1: TENDAI 2: TESTER 3: na 4: na Title: na Designation: na DOB: 3 Mar. 1980 " +
                             "POB: Harare, Zimbabwe Good quality a.k.a.: na Low quality a.k.a.: na Nationality: Zimbabwe " +
                             "Passport no: a) Zimbabwe number "),
                    new Span("FN111111", Struck: true),
                    new Span(" "),
                    new Span("b) Zimbabwe number FN222222", Underlined: true),
                    new Span(" National identification no: na Address: Harare, Zimbabwe Listed on: 1 Nov. 2026")
                });

            var entry = SanctionsListParser.ParsePdf(pdf).Entries.Single();

            Assert.Equal(new[] { "FN222222" }, entry.Passports);
            Assert.Equal(new[] { "FN111111" }, entry.RemovedText);
            Assert.Equal(new[] { "b) Zimbabwe number FN222222" }, entry.AddedText);
            Assert.DoesNotContain("FN111111", entry.Details);
        }

        [Fact]
        public void Several_individuals_and_an_entity_are_read_with_missing_details_left_empty()
        {
            using var pdf = TestPdfs.Document(
                "Security Council Consolidated Sanctions List as at 1 November 2026",
                TestPdfs.Plain("QDi.501 Name: 1: GRACE 2: TESTER 3: na 4: na Title: na Designation: na DOB: na POB: na " +
                               "Good quality a.k.a.: na Low quality a.k.a.: na Nationality: na Passport no: na " +
                               "National identification no: na Address: na Listed on: 1 Nov. 2026"),
                TestPdfs.Plain("QDi.502 Name: 1: PETER 2: SAMPLE 3: na 4: na Title: na Designation: na DOB: 1975 " +
                               "POB: na Good quality a.k.a.: Pete Sample Low quality a.k.a.: na Nationality: Kenya " +
                               "Passport no: na National identification no: 12345678 Address: na Listed on: 1 Nov. 2026"),
                TestPdfs.Plain("QDe.503 Name: SAMPLE TRADING COMPANY A.k.a.: Sample Traders F.k.a.: na " +
                               "Address: Nairobi, Kenya Listed on: 1 Nov. 2026"));

            var list = SanctionsListParser.ParsePdf(pdf);

            Assert.Equal(SanctionsDocumentKind.FullList, list.Kind);
            Assert.Equal(3, list.Entries.Count);

            var grace = list.Entries[0];
            Assert.Null(grace.DateOfBirth);
            Assert.Null(grace.Nationality);
            Assert.Empty(grace.Passports);
            Assert.Empty(grace.Aliases);
            Assert.Null(grace.Address);

            Assert.Equal(new[] { "12345678" }, list.Entries[1].NationalIds);
            Assert.True(list.Entries[2].IsEntity);
            Assert.Equal("SAMPLE TRADING COMPANY", list.Entries[2].Name);
            Assert.Contains("Sample Traders", list.Entries[2].Aliases);
        }

        [Fact]
        public void A_document_without_text_is_reported_as_unreadable()
        {
            using var pdf = TestPdfs.NoText();

            var list = SanctionsListParser.ParsePdf(pdf);

            Assert.Empty(list.Entries);
            Assert.Single(list.Warnings);
            Assert.StartsWith("No text could be read from any page", list.Warnings[0]);
        }

        [Fact]
        public void A_damaged_file_cannot_be_opened()
        {
            using var junk = new MemoryStream("%PDF-1.7 this is not really a pdf"u8.ToArray());

            Assert.ThrowsAny<Exception>(() => SanctionsListParser.ParsePdf(junk));
        }

        [Fact]
        public void An_amendment_notice_without_markings_says_so()
        {
            var list = SanctionsListParser.ParseText(
                "Security Council 1988 Committee Amends One Name on Its Sanctions List\n" +
                "TAi.007 Name: 1: ABDUL LATIF 2: MANSUR 3: na 4: na Nationality: Afghanistan Listed on: 31 Jan. 2001");

            Assert.Equal(SanctionsDocumentKind.Amendments, list.Kind);
            Assert.Contains(list.Warnings, w => w.Contains("no underlined or struck-through text was found in TAi.007"));
        }

        [Theory]
        [InlineData("Security Council Committee Adds Two Entries to Its Sanctions List", SanctionsDocumentKind.Additions)]
        [InlineData("Security Council Committee Amends Five Names on Its Sanctions List", SanctionsDocumentKind.Amendments)]
        [InlineData("Security Council Committee Removes One Name from Its Sanctions List", SanctionsDocumentKind.Removals)]
        [InlineData("Security Council Committee Adds One Entry and Amends Two", SanctionsDocumentKind.Notice)]
        [InlineData("Security Council Consolidated List, amended as at 1 November 2026", SanctionsDocumentKind.FullList)]
        [InlineData("1988 Sanctions List", SanctionsDocumentKind.FullList)]
        public void The_kind_of_document_comes_from_its_title(string title, SanctionsDocumentKind kind) =>
            Assert.Equal(kind, SanctionsListParser.KindOf(title, title));
    }
}
