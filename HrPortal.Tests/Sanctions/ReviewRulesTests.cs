using HrPortal.Entities;
using HrPortal.Services.Sanctions;

namespace HrPortal.Tests.Sanctions
{
    public class ReviewRulesTests
    {
        [Fact]
        public void A_confirmed_match_must_say_who_it_was_escalated_to()
        {
            Assert.NotNull(SanctionsService.CheckOutcome(
                SanctionsConcernCategory.PossibleMatch, SanctionsConcernStatus.Confirmed, "Same passport", null));

            Assert.Null(SanctionsService.CheckOutcome(
                SanctionsConcernCategory.PossibleMatch, SanctionsConcernStatus.Confirmed, "Same passport", "Managing Director"));
        }

        [Fact]
        public void Unable_to_determine_must_be_escalated_too() =>
            Assert.NotNull(SanctionsService.CheckOutcome(
                SanctionsConcernCategory.Discrepancy, SanctionsConcernStatus.UnableToDetermine, "FIU not reachable", " "));

        [Fact]
        public void An_outcome_needs_what_was_found() =>
            Assert.NotNull(SanctionsService.CheckOutcome(
                SanctionsConcernCategory.PossibleMatch, SanctionsConcernStatus.Cleared, "", null));

        [Fact]
        public void A_discrepancy_cannot_be_called_a_false_positive() =>
            Assert.NotNull(SanctionsService.CheckOutcome(
                SanctionsConcernCategory.Discrepancy, SanctionsConcernStatus.Cleared, "n/a", null));

        [Fact]
        public void Open_matters_and_further_investigation_stop_finalising_and_say_why()
        {
            var review = new SanctionsReview
            {
                Screened = true,
                Concerns =
                {
                    new SanctionsConcern { Status = SanctionsConcernStatus.Open },
                    new SanctionsConcern { Status = SanctionsConcernStatus.FurtherInvestigation },
                    new SanctionsConcern { Status = SanctionsConcernStatus.UnableToDetermine },
                    new SanctionsConcern { Status = SanctionsConcernStatus.Cleared }
                }
            };

            var reasons = SanctionsService.FinaliseBlockers(review);

            Assert.Equal(new[] { "1 matter has no outcome yet.", "1 matter is still under further investigation." }, reasons);
        }

        [Fact]
        public void A_screened_review_with_every_matter_settled_can_be_finalised()
        {
            var review = new SanctionsReview
            {
                Screened = true,
                Concerns = { new SanctionsConcern { Status = SanctionsConcernStatus.UnableToDetermine, EscalatedTo = "MD" } }
            };

            Assert.Empty(SanctionsService.FinaliseBlockers(review));
        }

        [Fact]
        public void A_review_not_yet_screened_cannot_be_finalised() =>
            Assert.Contains(
                "Staff, vendors and clients have not been checked yet.",
                SanctionsService.FinaliseBlockers(new SanctionsReview()));
    }
}
