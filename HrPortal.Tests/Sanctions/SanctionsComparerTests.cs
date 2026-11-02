using HrPortal.Entities;
using HrPortal.Services.Sanctions;

namespace HrPortal.Tests.Sanctions
{
    public class SanctionsComparerTests
    {
        private static int _ids;

        private static SanctionsDocument Doc(SanctionsDocumentKind kind = SanctionsDocumentKind.FullList, string list = "1988 Sanctions List") =>
            new()
            {
                Id = ++_ids,
                Kind = kind,
                ListName = list,
                OriginalFileName = $"doc{_ids}.pdf",
                Sha256 = $"sha{_ids}",
                UploadedAt = DateTime.UtcNow
            };

        private static SanctionsEntry Entry(
            string reference,
            string name,
            string? passports = null,
            string? dob = null,
            string? aliases = null,
            string? added = null,
            string? removed = null)
        {
            var entry = new SanctionsEntry
            {
                Reference = reference,
                Name = name,
                Passports = passports,
                Identifiers = passports,
                DateOfBirth = dob,
                Aliases = aliases,
                Nationality = "Afghanistan",
                AddedText = added,
                RemovedText = removed,
                Details = $"{reference} Name: {name}"
            };

            entry.Fingerprint = SanctionsService.FingerprintOf(entry);

            return entry;
        }

        private static Comparison Full(SanctionsEntry[] before, SanctionsEntry[] after) =>
            SanctionsComparer.Compare(Doc(), after, Doc(), before, _ => null);

        private static ComparedChange Only(Comparison c, SanctionsChangeType type) =>
            Assert.Single(c.Changes, x => x.Type == type);

        [Fact]
        public void A_new_individual_is_added()
        {
            var c = Full(
                new[] { Entry("TAi.001", "ONE PERSON") },
                new[] { Entry("TAi.001", "ONE PERSON"), Entry("TAi.002", "TWO PERSON") });

            Assert.Equal("TAi.002", Only(c, SanctionsChangeType.Added).Entry.Reference);
            Assert.Equal(1, c.Unchanged);
            Assert.False(c.Inconclusive);
        }

        [Fact]
        public void A_missing_individual_is_removed()
        {
            var c = Full(
                new[] { Entry("TAi.001", "ONE PERSON"), Entry("TAi.002", "TWO PERSON") },
                new[] { Entry("TAi.001", "ONE PERSON") });

            Assert.Equal("TAi.002", Only(c, SanctionsChangeType.Removed).Entry.Reference);
        }

        [Fact]
        public void A_changed_alias_is_an_amendment_not_a_new_listing()
        {
            var c = Full(
                new[] { Entry("TAi.001", "ONE PERSON", aliases: "Old Alias") },
                new[] { Entry("TAi.001", "ONE PERSON", aliases: "New Alias") });

            var change = Only(c, SanctionsChangeType.Changed);
            Assert.Equal(new FieldDifference("Aliases", "Old Alias", "New Alias"), Assert.Single(change.Differences));
            Assert.DoesNotContain(c.Changes, x => x.Type == SanctionsChangeType.Added);
        }

        [Fact]
        public void A_changed_passport_shows_exactly_what_changed()
        {
            var c = Full(
                new[] { Entry("TAi.001", "ONE PERSON", passports: "ABC12345\nKEEP99999") },
                new[] { Entry("TAi.001", "ONE PERSON", passports: "KEEP99999\nXYZ45678") });

            var difference = Assert.Single(Only(c, SanctionsChangeType.Changed).Differences);
            Assert.Equal(new FieldDifference("Passport numbers", "ABC12345", "XYZ45678"), difference);
        }

        [Fact]
        public void A_changed_date_of_birth_is_shown()
        {
            var c = Full(
                new[] { Entry("TAi.001", "ONE PERSON", dob: "1968") },
                new[] { Entry("TAi.001", "ONE PERSON", dob: "1970") });

            Assert.Equal(
                new FieldDifference("Date of birth", "1968", "1970"),
                Assert.Single(Only(c, SanctionsChangeType.Changed).Differences));
        }

        [Fact]
        public void Unchanged_and_reordered_entries_are_not_amendments()
        {
            var c = Full(
                new[] { Entry("TAi.001", "ONE PERSON", passports: "AAA11111\nBBB22222"), Entry("TAi.002", "TWO PERSON") },
                new[] { Entry("TAi.002", "two person."), Entry("TAi.001", "One  Person", passports: "BBB22222\nAAA11111") });

            Assert.Empty(c.Changes);
            Assert.Equal(2, c.Unchanged);
        }

        [Fact]
        public void A_different_spelling_of_the_same_entry_is_an_amendment_not_a_new_person()
        {
            var c = Full(
                new[] { Entry("TAi.026", "AMIR KHAN MUTTAQI") },
                new[] { Entry("TAi.026", "AMIR KHAN MOTAQI") });

            Assert.Equal("Name", Assert.Single(Only(c, SanctionsChangeType.Changed).Differences).Field);
            Assert.Empty(c.Discrepancies);
        }

        [Fact]
        public void A_name_changed_beyond_recognition_is_raised()
        {
            var c = Full(
                new[] { Entry("TAi.026", "AMIR KHAN MUTTAQI") },
                new[] { Entry("TAi.026", "JOHN BROWN") });

            Assert.Contains(c.Discrepancies, d => d.Contains("Check it is the same listed party"));
        }

        [Fact]
        public void An_amendment_notice_does_not_remove_the_names_it_leaves_out()
        {
            var notice = Doc(SanctionsDocumentKind.Amendments);
            var earlier = new[] { Entry("TAi.001", "ONE PERSON", passports: "AAA11111"), Entry("TAi.002", "TWO PERSON"), Entry("TAi.003", "THREE PERSON") };

            var c = SanctionsComparer.Compare(
                notice,
                new[] { Entry("TAi.001", "ONE PERSON", passports: "AAA11111\nBBB22222", added: "b) number BBB22222") },
                null,
                Array.Empty<SanctionsEntry>(),
                r => earlier.FirstOrDefault(e => e.Reference == r) is { } e ? new EntryVersion(e, "the list of 1 Oct") : null);

            var change = Assert.Single(c.Changes);
            Assert.Equal(SanctionsChangeType.Changed, change.Type);
            Assert.Equal(new FieldDifference("Passport numbers", null, "BBB22222"), Assert.Single(change.Differences));
            Assert.Contains("the list of 1 Oct", change.Note);
            Assert.DoesNotContain(c.Changes, x => x.Type == SanctionsChangeType.Removed);
            Assert.Contains(c.Notes, n => n.Contains("not counted as removed"));
        }

        [Fact]
        public void An_amendment_with_no_earlier_version_is_taken_from_the_markings()
        {
            var c = SanctionsComparer.Compare(
                Doc(SanctionsDocumentKind.Amendments),
                new[] { Entry("TAi.001", "ONE PERSON", added: "c) number P05629010", removed: "expires 12 Oct. 2026") },
                null,
                Array.Empty<SanctionsEntry>(),
                _ => null);

            var change = Assert.Single(c.Changes);
            Assert.Equal(SanctionsChangeType.Changed, change.Type);
            Assert.Contains(new FieldDifference("Underlined", null, "c) number P05629010"), change.Differences);
            Assert.Contains(new FieldDifference("Struck through", "expires 12 Oct. 2026", null), change.Differences);
            Assert.False(c.Inconclusive);
        }

        [Fact]
        public void An_amendment_that_cannot_be_worked_out_is_inconclusive()
        {
            var c = SanctionsComparer.Compare(
                Doc(SanctionsDocumentKind.Amendments),
                new[] { Entry("TAi.001", "ONE PERSON") },
                null,
                Array.Empty<SanctionsEntry>(),
                _ => null);

            Assert.True(c.Inconclusive);
            Assert.Single(c.Discrepancies);
            Assert.Equal(SanctionsChangeType.Changed, Assert.Single(c.Changes).Type);
        }

        [Fact]
        public void A_full_list_compared_with_a_notice_only_compares_what_the_notice_gives()
        {
            var c = SanctionsComparer.Compare(
                Doc(),
                new[] { Entry("TAi.001", "ONE PERSON"), Entry("TAi.002", "TWO PERSON"), Entry("TAi.003", "THREE PERSON") },
                Doc(SanctionsDocumentKind.Amendments),
                new[] { Entry("TAi.001", "ONE PERSON") },
                _ => null);

            Assert.Empty(c.Changes);
            Assert.Equal(2, c.NotCompared);
            Assert.True(c.Inconclusive);
        }

        [Fact]
        public void A_removal_notice_removes_its_names()
        {
            var c = SanctionsComparer.Compare(
                Doc(SanctionsDocumentKind.Removals),
                new[] { Entry("TAi.082", "ABDUL-HAQ WASSIQ") },
                null,
                Array.Empty<SanctionsEntry>(),
                r => new EntryVersion(Entry(r, "ABDUL-HAQ WASSIQ"), "earlier"));

            Assert.Equal("TAi.082", Only(c, SanctionsChangeType.Removed).Entry.Reference);
        }

        [Fact]
        public void A_document_with_no_names_is_inconclusive_and_removes_nothing()
        {
            var c = Full(new[] { Entry("TAi.001", "ONE PERSON") }, Array.Empty<SanctionsEntry>());

            Assert.True(c.Inconclusive);
            Assert.Empty(c.Changes);
            Assert.Single(c.Discrepancies);
        }

        [Fact]
        public void Two_different_lists_are_flagged()
        {
            var c = SanctionsComparer.Compare(
                Doc(list: "1988 Sanctions List"),
                new[] { Entry("TAi.001", "ONE PERSON") },
                Doc(list: SanctionsListParser.ConsolidatedList),
                new[] { Entry("TAi.001", "ONE PERSON") },
                _ => null);

            Assert.Contains(c.Discrepancies, d => d.StartsWith("The documents are different lists"));
        }

        [Fact]
        public void The_sample_notice_amends_five_existing_names_and_adds_none()
        {
            ParsedSanctionsList parsed;

            using (var pdf = TestPdfs.Sample())
                parsed = SanctionsListParser.ParsePdf(pdf);

            var entries = parsed.Entries.Select(p =>
            {
                var e = new SanctionsEntry
                {
                    Reference = p.Reference,
                    Name = p.Name,
                    Passports = string.Join("\n", p.Passports),
                    AddedText = string.Join("\n", p.AddedText),
                    Details = p.Details
                };
                e.Fingerprint = SanctionsService.FingerprintOf(e);
                return e;
            }).ToArray();

            var c = SanctionsComparer.Compare(
                Doc(parsed.Kind),
                entries,
                null,
                Array.Empty<SanctionsEntry>(),
                _ => null);

            Assert.Equal(5, c.Changes.Count);
            Assert.All(c.Changes, x => Assert.Equal(SanctionsChangeType.Changed, x.Type));
            Assert.Contains(
                new FieldDifference("Underlined", null, "c) Afghanistan number P05629010"),
                c.Changes.Single(x => x.Entry.Reference == "TAi.007").Differences);
            Assert.False(c.Inconclusive);
        }
    }
}
