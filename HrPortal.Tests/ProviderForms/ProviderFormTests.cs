using HrPortal.Controllers;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services.ProviderForms;
using Microsoft.AspNetCore.Authorization;
using UglyToad.PdfPig;

namespace HrPortal.Tests.ProviderForms
{
    public class ProviderFormTests
    {
        private static readonly DateTime Today = new(2026, 11, 24);

        static ProviderFormTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        private static ProviderFormMember Person() => new()
        {
            Surname = "Moyo",
            FirstNames = "Rudo",
            DateOfBirth = new DateTime(1990, 2, 1),
            Sex = Gender.Female,
            MaritalStatus = MaritalStatus.Married,
            NationalId = "00-000000A00",
            Phone = "0770 000 000",
            Email = "rudo@example.test",
            Address = "1 Example Road, Harare"
        };

        private static ProviderForm Nyaradzo()
        {
            var m = Person();
            m.Plan = "Gold Plus";
            m.Dependants.Add(new ProviderFormPerson
            {
                Surname = "Moyo",
                FirstNames = "Tino",
                Sex = Gender.Male,
                DateOfBirth = new DateTime(2015, 6, 5),
                Relationship = "Son",
                Change = ProviderFormChange.Add
            });

            return new ProviderForm { Reference = "PF-2026-0001", Kind = ProviderFormKind.Nyaradzo, Member = m };
        }

        private static ProviderForm Minerva()
        {
            var m = Person();
            m.SpouseName = "Tendai Moyo";
            m.SpouseDateOfBirth = new DateTime(1989, 3, 3);
            m.Children.Add(new ProviderFormName { Name = "Tino Moyo", DateOfBirth = new DateTime(2015, 6, 5) });
            m.Nominees.Add(new ProviderFormName { Name = "Tendai Moyo", Relationship = "Spouse" });
            m.SignedAt = "Harare";

            return new ProviderForm { Reference = "PF-2026-0002", Kind = ProviderFormKind.Minerva, Member = m };
        }

        private static ProviderFormEmployer Employer() => new()
        {
            EffectiveDate = new DateTime(2026, 12, 1),
            FundName = "Example Pension Fund",
            ContributionMonth = "December 2026",
            EmployerName = "Example Employer",
            SignatoryName = "Hope Dube",
            Designation = "HR Officer",
            DateJoined = new DateTime(2026, 1, 5),
            AnnualSalary = 12000
        };

        [Theory]
        [InlineData(ProviderFormKind.Nyaradzo)]
        [InlineData(ProviderFormKind.Minerva)]
        public void The_blank_form_in_the_build_is_a_pdf_with_the_pages_expected(ProviderFormKind kind)
        {
            using var pdf = PdfDocument.Open(ProviderFormPdfService.Blank(kind));

            Assert.Equal(ProviderFormLayout.Pages(kind), pdf.NumberOfPages);

            var (width, height) = ProviderFormLayout.PageSize(kind);

            foreach (var page in pdf.GetPages())
            {
                Assert.Equal(width, page.Width, 0.1);
                Assert.Equal(height, page.Height, 0.1);
            }
        }

        [Fact]
        public void Filling_a_form_leaves_every_word_the_provider_printed_where_it_was()
        {
            foreach (var form in new[] { Nyaradzo(), Minerva() })
            {
                form.Employer = Employer();

                using var blank = PdfDocument.Open(ProviderFormPdfService.Blank(form.Kind));
                using var filled = PdfDocument.Open(new ProviderFormPdfService().Render(form));

                Assert.Equal(blank.NumberOfPages, filled.NumberOfPages);

                for (var number = 1; number <= blank.NumberOfPages; number++)
                {
                    var before = blank.GetPage(number);
                    var after = filled.GetPage(number);

                    Assert.Equal(before.Width, after.Width, 0.1);
                    Assert.Equal(before.Height, after.Height, 0.1);

                    var letters = after.Letters
                        .Select(l => (l.Value, Math.Round(l.StartBaseLine.X), Math.Round(l.StartBaseLine.Y)))
                        .ToHashSet();

                    Assert.All(before.Letters, l => Assert.Contains(
                        (l.Value, Math.Round(l.StartBaseLine.X), Math.Round(l.StartBaseLine.Y)), letters));
                }
            }
        }

        [Fact]
        public void The_answers_are_on_the_filled_form()
        {
            var form = Minerva();
            form.Employer = Employer();

            using var filled = PdfDocument.Open(new ProviderFormPdfService().Render(form));

            var text = string.Concat(filled.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));

            Assert.Contains("Rudo", text);
            Assert.Contains("12,000.00", text);
            Assert.Contains("Tendai", text);
        }

        [Fact]
        public void Nothing_is_put_outside_the_page_on_a_full_form()
        {
            var nyaradzo = Nyaradzo();
            nyaradzo.Employer = Employer();
            nyaradzo.Member.Dependants = Enumerable.Range(0, 25)
                .Select(_ => new ProviderFormPerson { Surname = "A", FirstNames = "B", Change = ProviderFormChange.Update })
                .ToList();

            var minerva = Minerva();
            minerva.Employer = Employer();
            minerva.Member.Children = Enumerable.Range(0, 9).Select(_ => new ProviderFormName { Name = "C" }).ToList();
            minerva.Member.Nominees = Enumerable.Range(0, 9).Select(_ => new ProviderFormName { Name = "N" }).ToList();

            foreach (var form in new[] { nyaradzo, minerva })
            {
                var (width, height) = ProviderFormLayout.PageSize(form.Kind);

                Assert.All(ProviderFormLayout.Marks(form), m =>
                {
                    Assert.InRange(m.Page, 0, ProviderFormLayout.Pages(form.Kind) - 1);
                    Assert.True(m.X >= 0 && m.X + m.Width <= width, $"{m}");
                    Assert.True(m.Y - m.Height >= 0 && m.Y <= height, $"{m}");
                    Assert.True(m.Width > 0 && m.Height > 0, $"{m}");
                });
            }

            Assert.Equal(19, ProviderFormLayout.Marks(nyaradzo).Count(m => m.Text == "A"));
            Assert.Equal(4, ProviderFormLayout.Marks(minerva).Count(m => m.Text == "C"));
            Assert.Equal(4, ProviderFormLayout.Marks(minerva).Count(m => m.Text == "N"));
        }

        [Fact]
        public void Every_plan_and_level_has_its_own_box()
        {
            var ticks = new HashSet<(float, float)>();

            foreach (var plan in ProviderFormRules.Plans)
            {
                var form = new ProviderForm { Member = new ProviderFormMember { Plan = plan } };
                var tick = Assert.Single(ProviderFormLayout.Marks(form));

                Assert.True(tick.IsTick);
                Assert.True(ticks.Add((tick.X, tick.Y)), plan);
            }

            foreach (var level in ProviderFormRules.Levels)
            {
                foreach (var change in new[] { ProviderFormChange.Add, ProviderFormChange.Remove })
                {
                    var breadwinner = new ProviderForm { Member = new ProviderFormMember { Breadwinner = level, BreadwinnerChange = change } };
                    var sahwira = new ProviderForm { Member = new ProviderFormMember { SahwiraLite = level, SahwiraLiteChange = change } };

                    foreach (var form in new[] { breadwinner, sahwira })
                    {
                        var tick = Assert.Single(ProviderFormLayout.Marks(form));
                        Assert.True(ticks.Add((tick.X, tick.Y)), $"{level} {change}");
                    }
                }
            }
        }

        [Fact]
        public void A_complete_form_has_nothing_left_to_do()
        {
            Assert.Empty(ProviderFormRules.MemberProblems(ProviderFormKind.Nyaradzo, Nyaradzo().Member, Today));
            Assert.Empty(ProviderFormRules.MemberProblems(ProviderFormKind.Minerva, Minerva().Member, Today));
            Assert.Empty(ProviderFormRules.EmployerProblems(ProviderFormKind.Nyaradzo, Employer()));
            Assert.Empty(ProviderFormRules.EmployerProblems(ProviderFormKind.Minerva, Employer()));
        }

        [Fact]
        public void What_is_missing_is_listed()
        {
            var nyaradzo = Nyaradzo().Member;
            nyaradzo.NationalId = null;
            nyaradzo.Dependants[0].Change = null;
            nyaradzo.Dependants[0].DateOfBirth = Today.AddDays(1);

            var problems = ProviderFormRules.MemberProblems(ProviderFormKind.Nyaradzo, nyaradzo, Today);

            Assert.Contains("Give your national ID number.", problems);
            Assert.Contains("Choose whether Tino Moyo is being added, removed or updated.", problems);
            Assert.Contains("The date of birth of Tino Moyo cannot be in the future.", problems);

            var minerva = Minerva().Member;
            minerva.Nominees.Clear();
            minerva.SpouseDateOfBirth = null;

            problems = ProviderFormRules.MemberProblems(ProviderFormKind.Minerva, minerva, Today);

            Assert.Contains("Name at least one dependant or beneficiary on the nomination form.", problems);
            Assert.Contains("Give the date of birth of your spouse.", problems);

            Assert.Contains(
                "Give the annual pensionable salary.",
                ProviderFormRules.EmployerProblems(ProviderFormKind.Minerva, new ProviderFormEmployer()));
        }

        [Fact]
        public void More_lines_than_the_form_has_are_refused()
        {
            var m = Person();
            m.Dependants = Enumerable.Range(0, 20).Select(_ => new ProviderFormPerson { Surname = "A" }).ToList();
            m.Children = Enumerable.Range(0, 5).Select(_ => new ProviderFormName { Name = "A" }).ToList();
            m.Nominees = Enumerable.Range(0, 5).Select(_ => new ProviderFormName { Name = "A" }).ToList();

            Assert.Equal(3, ProviderFormRules.SaveProblems(m).Count);
        }

        [Fact]
        public void Cleaning_drops_blank_lines_and_the_other_forms_answers()
        {
            var m = Minerva().Member;
            m.Plan = "Gold Plus";
            m.Address = " 1 Example Road,\r\nHarare\u0007 ";
            m.Dependants.Add(new ProviderFormPerson { Surname = "X" });
            m.Children.Add(new ProviderFormName { Name = "  " });
            m.Children.Add(new ProviderFormName { DateOfBirth = new DateTime(2020, 1, 1) });

            ProviderFormRules.Clean(ProviderFormKind.Minerva, m);

            Assert.Null(m.Plan);
            Assert.Empty(m.Dependants);
            Assert.Equal(2, m.Children.Count);
            Assert.Equal("1 Example Road, Harare", m.Address);
        }

        [Fact]
        public void Family_given_on_one_form_is_offered_on_the_other()
        {
            var employee = new Employee { FirstName = "Rudo", LastName = "Moyo", Email = "r@x.test", Gender = Gender.Female };

            var minerva = ProviderFormService.Prefill(ProviderFormKind.Minerva, employee, null, Nyaradzo().Member);

            Assert.Equal("Tino Moyo", Assert.Single(minerva.Children).Name);
            Assert.Equal("00-000000A00", minerva.NationalId);

            var nyaradzo = ProviderFormService.Prefill(ProviderFormKind.Nyaradzo, employee, null, Minerva().Member);

            Assert.Equal(new[] { "Tendai", "Tino" }, nyaradzo.Dependants.Select(d => d.FirstNames));
            Assert.All(nyaradzo.Dependants, d => Assert.Equal(ProviderFormChange.Add, d.Change));
        }

        [Fact]
        public void A_new_form_takes_the_employment_record_over_an_old_answer_and_leaves_out_people_removed()
        {
            var employee = new Employee { FirstName = "Rudo", LastName = "Ncube", Email = "r@x.test", PhoneNumber = "0711" };

            var last = Nyaradzo().Member;
            last.Dependants.Add(new ProviderFormPerson { Surname = "Gone", FirstNames = "G", Change = ProviderFormChange.Remove });

            var next = ProviderFormService.Prefill(ProviderFormKind.Nyaradzo, employee, last, null);

            Assert.Equal("Ncube", next.Surname);
            Assert.Equal("0711", next.Phone);
            Assert.Equal("00-000000A00", next.NationalId);
            Assert.Null(next.Plan);
            Assert.Null(Assert.Single(next.Dependants).Change);
        }

        [Fact]
        public void The_employers_signature_and_date_wait_until_HR_completes_it()
        {
            var form = Minerva();
            form.Employer = Employer();
            form.EmployerSignature = new byte[] { 1 };

            Assert.DoesNotContain(ProviderFormLayout.Marks(form), m => m.Image != null);

            form.CompletedAt = Today;

            Assert.Single(ProviderFormLayout.Marks(form), m => m.Image != null);
        }

        [Theory]
        [InlineData(1, "1st")]
        [InlineData(2, "2nd")]
        [InlineData(3, "3rd")]
        [InlineData(11, "11th")]
        [InlineData(13, "13th")]
        [InlineData(22, "22nd")]
        [InlineData(30, "30th")]
        public void Days_are_written_as_the_form_asks(int day, string written) =>
            Assert.Equal(written, ProviderFormLayout.Ordinal(day));

        [Fact]
        public void A_long_address_runs_on_to_the_second_line()
        {
            var (first, second) = ProviderFormLayout.SplitAddress("12 Very Long Road Name, Another Suburb, Harare", 30);

            Assert.Equal("12 Very Long Road Name,", first);
            Assert.Equal("Another Suburb, Harare", second);
        }

        [Fact]
        public void Only_HR_reaches_the_providers_forms_of_others()
        {
            var attribute = typeof(ProviderFormController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), false)
                .Cast<AuthorizeAttribute>()
                .Single();

            Assert.Equal(Roles.HrOfficer, attribute.Roles);
        }
    }
}
