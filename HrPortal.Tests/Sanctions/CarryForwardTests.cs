using HrPortal.Entities;
using HrPortal.Services.Sanctions;
using static HrPortal.Services.Sanctions.SanctionsService;

namespace HrPortal.Tests.Sanctions
{
    public class CarryForwardTests
    {
        [Fact]
        public void A_false_positive_with_nothing_changed_is_carried_forward() =>
            Assert.Equal(CarryForward.KeepCleared,
                EarlierDecision(SanctionsConcernStatus.Cleared, "person", "entry", "person", "entry"));

        [Fact]
        public void A_false_positive_is_reopened_when_the_person_changes() =>
            Assert.Equal(CarryForward.Reopen,
                EarlierDecision(SanctionsConcernStatus.Cleared, "person", "entry", "person with new ID", "entry"));

        [Fact]
        public void A_false_positive_is_reopened_when_the_listed_entry_changes() =>
            Assert.Equal(CarryForward.Reopen,
                EarlierDecision(SanctionsConcernStatus.Cleared, "person", "entry", "person", "entry with new alias"));

        [Fact]
        public void A_confirmed_match_is_never_carried_forward_as_cleared() =>
            Assert.Equal(CarryForward.ConfirmedBefore,
                EarlierDecision(SanctionsConcernStatus.Confirmed, "person", "entry", "person", "entry"));

        [Fact]
        public void A_decision_recorded_without_fingerprints_is_not_carried_forward() =>
            Assert.Equal(CarryForward.Reopen,
                EarlierDecision(SanctionsConcernStatus.Cleared, null, "entry", "person", "entry"));

        [Fact]
        public void A_person_fingerprint_changes_with_their_date_of_birth_but_not_with_spacing()
        {
            var a = FingerprintOf(new ScreeningSubject("Staff", 1, "Grace  Chikore", "63-111111A11", new DateTime(1990, 1, 1)));
            var b = FingerprintOf(new ScreeningSubject("Staff", 1, "GRACE CHIKORE", "63-111111A11", new DateTime(1990, 1, 1)));
            var c = FingerprintOf(new ScreeningSubject("Staff", 1, "Grace Chikore", "63-111111A11", new DateTime(1991, 1, 1)));

            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
        }
    }
}
