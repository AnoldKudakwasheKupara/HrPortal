using HrPortal.Entities;
using HrPortal.Services.Sanctions;

namespace HrPortal.Tests.Sanctions
{
    public class SanctionsMatcherTests
    {
        private static readonly ScreeningTarget Mansur = new(
            "TAi.007",
            "ABDUL LATIF MANSUR",
            new[] { "Abdul Latif Mansoor", "Wali Mohammad" },
            new[] { "D0009720", "D0010442", "P05629010" },
            IsEntity: false,
            DateOfBirth: "a) Approximately 1968 b) 12 Nov. 1967",
            Nationality: "Afghanistan");

        private static readonly ScreeningTarget Company = new(
            "QDe.001",
            "SAMPLE HAWALA TRADING COMPANY LIMITED",
            new[] { "Sample Hawala" },
            Array.Empty<string>(),
            IsEntity: true);

        private static ScreeningSubject Person(string name, DateTime? dob = null, string? id = null, string? nationality = null) =>
            new("Client", 1, name, id, dob, nationality, IsOrganisation: false);

        [Fact]
        public void The_same_name_with_an_agreeing_date_of_birth_is_an_exact_match()
        {
            var hit = SanctionsMatcher.Match(Person("Abdul Latif Mansur", new DateTime(1967, 11, 12)), Mansur);

            Assert.Equal(SanctionsMatchStrength.Exact, hit!.Strength);
            Assert.Contains("Date of birth: the same (12 Nov 1967)", hit.Evidence);
        }

        [Fact]
        public void An_alias_matches_and_says_so()
        {
            var hit = SanctionsMatcher.Match(Person("Wali Mohammad"), Mansur);

            Assert.NotNull(hit);
            Assert.Contains("Matched on: an alias, \"Wali Mohammad\"", hit!.Evidence);
        }

        [Fact]
        public void A_matching_passport_number_is_an_exact_match_whatever_the_name()
        {
            var hit = SanctionsMatcher.Match(Person("Somebody Else", id: "p0562-9010"), Mansur);

            Assert.Equal(SanctionsMatchStrength.Exact, hit!.Strength);
            Assert.Equal(100, hit.Score);
        }

        [Fact]
        public void The_same_name_without_other_details_is_a_potential_match_not_dropped()
        {
            var hit = SanctionsMatcher.Match(Person("Abdul Latif Mansur"), Mansur);

            Assert.Equal(SanctionsMatchStrength.Potential, hit!.Strength);
            Assert.Contains("Date of birth: not available for this person", hit.Evidence);
            Assert.Contains("ID number: not available on both sides", hit.Evidence);
        }

        [Fact]
        public void The_same_name_with_a_different_date_of_birth_is_still_raised_as_potential()
        {
            var hit = SanctionsMatcher.Match(Person("Abdul Latif Mansur", new DateTime(1990, 1, 1)), Mansur);

            Assert.Equal(SanctionsMatchStrength.Potential, hit!.Strength);
            Assert.Contains("probably a different person", hit.Reason);
        }

        [Fact]
        public void A_similar_name_with_a_different_date_of_birth_is_set_aside_not_lost()
        {
            var hit = SanctionsMatcher.Match(Person("Abdul Mansoor", new DateTime(1990, 1, 1)), Mansur);

            Assert.Equal(SanctionsMatchStrength.SetAside, hit!.Strength);
            Assert.Contains(hit.Evidence, e => e.StartsWith("Date of birth: different"));
        }

        [Fact]
        public void An_approximate_year_allows_two_years_either_way()
        {
            var hit = SanctionsMatcher.Match(Person("Abdul Mansoor", new DateTime(1970, 5, 5)), Mansur);

            Assert.Equal(SanctionsMatchStrength.Potential, hit!.Strength);
            Assert.Contains(hit.Evidence, e => e.StartsWith("Date of birth: consistent"));
        }

        [Theory]
        [InlineData("Mansur, Abdul Latif")]
        [InlineData("ABDUL-LATIF MANSOOR")]
        [InlineData("Mr Abdul Latif Mansur")]
        [InlineData("Abdul Mansur")]
        public void Spelling_order_punctuation_and_missing_middle_names_still_match(string name) =>
            Assert.NotNull(SanctionsMatcher.Match(Person(name), Mansur));

        [Theory]
        [InlineData("Tendai Moyo")]
        [InlineData("Abdul Rahman")]
        [InlineData("Latif")]
        public void Different_people_with_partly_similar_names_do_not_match(string name) =>
            Assert.Null(SanctionsMatcher.Match(Person(name), Mansur));

        [Fact]
        public void A_nationality_that_agrees_with_the_same_name_makes_it_exact()
        {
            var hit = SanctionsMatcher.Match(Person("Abdul Latif Mansur", nationality: "Afghan"), Mansur);

            Assert.Equal(SanctionsMatchStrength.Exact, hit!.Strength);
        }

        [Fact]
        public void A_person_is_not_compared_with_a_listed_company()
        {
            var staff = new ScreeningSubject("Staff", 1, "Sample Hawala", null, IsOrganisation: false);

            Assert.Null(SanctionsMatcher.Match(staff, Company));
        }

        [Fact]
        public void A_company_matches_whatever_its_legal_ending()
        {
            var vendor = new ScreeningSubject("Vendor", 1, "Sample Hawala Trading Co (Pvt) Ltd", null);

            var hit = SanctionsMatcher.Match(vendor, Company);

            Assert.Equal(SanctionsMatchStrength.Exact, hit!.Strength);
            Assert.Contains("leaving out endings such as Ltd", hit.Reason);
        }

        [Fact]
        public void A_company_with_only_a_generic_word_in_common_does_not_match()
        {
            var vendor = new ScreeningSubject("Vendor", 1, "Trading Limited", null);

            Assert.Null(SanctionsMatcher.Match(vendor, Company));
        }

        [Fact]
        public void A_higher_threshold_raises_fewer_matches()
        {
            var subject = Person("Abdul Latif Mansur Khan Ahmadzai");

            Assert.NotNull(SanctionsMatcher.Match(subject, Mansur, 75));
            Assert.Null(SanctionsMatcher.Match(subject, Mansur, 90));
        }
    }
}
