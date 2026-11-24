using System.Globalization;
using HrPortal.Entities;

namespace HrPortal.Services.ProviderForms
{
    /// <summary>
    /// Something to put on a page of a provider's form, in points from
    /// the top left. Text stands on Y and shrinks to fit Width; a tick
    /// or a signature fills the box above Y.
    /// </summary>
    public sealed record FormMark(
        int Page, float X, float Y, float Width, float Height, string? Text = null, byte[]? Image = null)
    {
        public bool IsTick => Text == null && Image == null;
    }

    /// <summary>
    /// Where each answer goes on the providers' own forms. The places
    /// were measured from the blank forms in ProviderForms, so a new
    /// edition of a form needs measuring again.
    /// </summary>
    public static class ProviderFormLayout
    {
        /// <summary>The lines above and below each row of the dependants table.</summary>
        private static readonly float[] DependantRows =
        {
            515.5f, 526.3f, 537.7f, 549f, 560.3f, 571.7f, 583f, 594.3f, 605.7f, 617f,
            628.4f, 639.7f, 649.5f, 660.9f, 673.6f, 684.9f, 696.3f, 708.6f, 720f, 731.3f
        };

        private static readonly Dictionary<string, (float X, float Y)> PlanBoxes = new()
        {
            ["Executive Plus"] = (103.6f, 276.9f),
            ["Executive Standard"] = (104f, 291.6f),
            ["Platinum Plus"] = (209.1f, 276f),
            ["Platinum Standard"] = (209.6f, 290.7f),
            ["Diamond Plus"] = (315.5f, 275f),
            ["Diamond Standard"] = (315.5f, 289.7f),
            ["Gold Plus"] = (413.6f, 274f),
            ["Gold Standard"] = (413.6f, 288.7f),
            ["Silver Plus"] = (514.6f, 274f),
            ["Silver Standard"] = (514.6f, 288.7f),
            ["Bronze Standard"] = (514.6f, 302.7f)
        };

        private static readonly Dictionary<int, float> BreadwinnerBoxes = new()
        {
            [5] = 91.1f,
            [4] = 189.1f,
            [3] = 301.5f,
            [2] = 410.1f
        };

        private static readonly Dictionary<int, float> SahwiraBoxes = new()
        {
            [5] = 355.1f,
            [4] = 366.1f,
            [3] = 377.1f,
            [2] = 388.1f
        };

        private static readonly float[] ChildLines = { 453.7f, 470.7f, 487.4f, 504.3f };

        private static readonly float[] NomineeLines = { 221.3f, 241.7f, 262f, 286.2f };

        public static int Pages(ProviderFormKind kind) => kind == ProviderFormKind.Minerva ? 2 : 1;

        public static (float Width, float Height) PageSize(ProviderFormKind kind) =>
            kind == ProviderFormKind.Minerva ? (612f, 792f) : (595.2756f, 841.8898f);

        public static List<FormMark> Marks(ProviderForm form) =>
            form.Kind == ProviderFormKind.Minerva ? Minerva(form) : Nyaradzo(form);

        private static List<FormMark> Nyaradzo(ProviderForm form)
        {
            var m = form.Member;
            var e = form.Employer;
            var marks = new List<FormMark>();

            void Text(float x, float y, float right, string? text, float height = 10f)
            {
                if (!string.IsNullOrWhiteSpace(text))
                    marks.Add(new FormMark(0, x, y, right - x, height, text));
            }

            void Tick(float x, float y, float width, float height = 8.6f) =>
                marks.Add(new FormMark(0, x, y + height, width, height));

            void AddOrRemove(float x, float y, ProviderFormChange? change)
            {
                if (change == ProviderFormChange.Add)
                    Tick(x, y, 17.2f);
                else if (change == ProviderFormChange.Remove)
                    Tick(x + 17.2f, y, 17.2f);
            }

            Text(88, 141, 239, Date(e.EffectiveDate));
            Text(402, 141, 555, e.PolicyNumber);
            Text(71, 170.2f, 291, m.Surname);
            Text(370, 170.2f, 555, m.FirstNames);
            Text(77, 186, 113.5f, Date(m.DateOfBirth, "dd/MM/yyyy"), 8.5f);
            Text(173.5f, 185.8f, 184.5f, Sex(m.Sex), 7.5f);
            Text(247, 185.6f, 271.5f, Marital(m.MaritalStatus), 7.5f);
            Text(374, 185.8f, 555, m.NationalId);
            Text(177, 201.6f, 293, m.OtherId);
            Text(374, 201.6f, 555, m.Email);
            Text(104, 217.7f, 224, Date(e.CoverDate ?? e.EffectiveDate));
            Text(314, 217.7f, 555, m.Phone);
            Text(86, 232.9f, 555, m.Address);

            if (m.Plan != null && PlanBoxes.TryGetValue(m.Plan, out var plan))
                Tick(plan.X, plan.Y, 34.5f);

            if (m.Breadwinner is int level && BreadwinnerBoxes.TryGetValue(level, out var breadwinner))
                AddOrRemove(breadwinner, 331.8f, m.BreadwinnerChange);

            if (m.SahwiraLite is int lite && SahwiraBoxes.TryGetValue(lite, out var sahwira))
                AddOrRemove(392f, sahwira, m.SahwiraLiteChange);

            AddOrRemove(237.3f, 354f, m.ToiletsFamily);
            AddOrRemove(237.3f, 364.8f, m.TentFamily);
            AddOrRemove(237.9f, 385f, m.ToiletsAll);
            AddOrRemove(237.9f, 395.8f, m.TentAll);

            Text(64, 436.6f, 190, m.BeneficiarySurname);
            Text(232, 436.6f, 322, m.BeneficiaryFirstNames);
            Text(371, 435, 528, m.BeneficiaryNationalId);
            Text(74, 451.7f, 221, m.BeneficiaryRelationship);
            Text(272, 451.7f, 529, m.BeneficiaryAddress);
            Text(272, 466.1f, 529, m.BeneficiaryEmail);
            Text(313, 481.2f, 529, m.BeneficiaryPhone);

            for (var i = 0; i < Math.Min(m.Dependants.Count, ProviderFormRules.MaxDependants); i++)
            {
                var p = m.Dependants[i];
                var top = DependantRows[i] + 1;
                var y = DependantRows[i + 1] - 1.4f;

                Text(38, y, 118, p.Surname, 8.5f);
                Text(121.5f, y, 191, p.FirstNames, 8.5f);

                if (p.Sex == Gender.Male)
                    Tick(205.4f, top, 13.2f);
                else if (p.Sex == Gender.Female)
                    Tick(232.7f, top, 15.2f);

                Text(250.5f, y, 300, Date(p.DateOfBirth, "dd/MM/yyyy"), 8.5f);
                Text(303.5f, y, 381, p.IdNumber, 8.5f);
                Text(384, y, 445, p.Relationship, 8.5f);

                if (p.Change == ProviderFormChange.Add)
                    Tick(445.8f, top, 36.4f);
                else if (p.Change == ProviderFormChange.Remove)
                    Tick(482.2f, top, 36f);
                else if (p.Change == ProviderFormChange.Update)
                    Tick(518.2f, top, 36f);
            }

            Text(122, 741, 247, e.Premium, 8.5f);

            if (form.Signature != null)
                marks.Add(new FormMark(0, 128, 800, 115, 28, Image: form.Signature));

            return marks;
        }

        private static List<FormMark> Minerva(ProviderForm form)
        {
            var m = form.Member;
            var e = form.Employer;
            var marks = new List<FormMark>();

            void Text(int page, float x, float y, float right, string? text)
            {
                if (!string.IsNullOrWhiteSpace(text))
                    marks.Add(new FormMark(page, x, y, right - x, 11.5f, text));
            }

            void Sign(int page, float x, float y, float right, byte[]? image)
            {
                if (image != null)
                    marks.Add(new FormMark(page, x, y, Math.Min(right - x, 120), 25, Image: image));
            }

            var (first, second) = SplitAddress(m.Address, 62);

            Text(0, 113, 141, 576, e.FundName);
            Text(0, 91, 184, 576, m.Surname);
            Text(0, 108, 205.4f, 394, m.FirstNames);
            Text(0, 468, 205.4f, 576, e.EmployeeNumber);
            Text(0, 155, 226.8f, 576, first);
            Text(0, 38, 248.3f, 394, second);
            Text(0, 445, 248.3f, 576, m.Phone);
            Text(0, 119, 269.8f, 394, Date(m.DateOfBirth));
            Text(0, 420, 269.8f, 576, Sex(m.Sex, true));
            Text(0, 161, 291.3f, 394, Date(e.DateJoined));
            Text(0, 427, 291.3f, 576, m.NationalId);
            Text(0, 319, 312.7f, 576, e.ContributionMonth);
            Text(0, 205, 334.2f, 576, e.AnnualSalary?.ToString("#,##0.00", CultureInfo.InvariantCulture));
            Text(0, 283, 355.7f, 394, Date(e.NotCountingFrom));
            Text(0, 414, 355.7f, 576, Date(e.NotCountingTo));
            Text(0, 127, 377.2f, 576, Marital(m.MaritalStatus));

            Text(0, 144, 415.4f, 392, m.SpouseName);
            Text(0, 397, 415.4f, 576, Date(m.SpouseDateOfBirth));

            for (var i = 0; i < Math.Min(m.Children.Count, ChildLines.Length); i++)
            {
                Text(0, 144, ChildLines[i], 392, m.Children[i].Name);
                Text(0, 397, ChildLines[i], 576, Date(m.Children[i].DateOfBirth));
            }

            if (form.Signature != null)
                Text(0, 62, 558.8f, 355, LocalDate(form.SubmittedAt));

            Sign(0, 400, 561, 576, form.Signature);

            Text(0, 79, 603.3f, 358, e.EmployerName);
            Text(0, 393, 603.3f, 576, e.Branch);
            Text(0, 115, 624.8f, 360, e.CompanyNumber);

            if (form.CompletedAt != null)
            {
                Sign(0, 392, 670, 542, form.EmployerSignature);
                Text(0, 423, 689.2f, 542, e.SignatoryName);
                Text(0, 398, 710.7f, 542, e.Designation);
                Text(0, 357, 732.2f, 542, LocalDate(form.CompletedAt));
            }

            Text(1, 202, 107, 576, $"{m.FirstNames} {m.Surname}".Trim());
            Text(1, 132, 128.5f, 576, e.EmployerName);

            for (var i = 0; i < Math.Min(m.Nominees.Count, NomineeLines.Length); i++)
            {
                Text(1, 144, NomineeLines[i], 356, m.Nominees[i].Name);
                Text(1, 360, NomineeLines[i], 576, m.Nominees[i].Relationship);
            }

            Text(1, 207, 423, 576, m.WillHolder);
            Text(1, 127, 444.5f, 576, m.WillAddress);
            Text(1, 116, 487.5f, 288, m.SignedAt);

            if (form.Signature != null && form.SubmittedAt is DateTime signed)
            {
                var day = Local(signed);

                Text(1, 314, 487.5f, 431, Ordinal(day.Day));
                Text(1, 464, 487.5f, 576, Date(day, "MMMM yyyy"));
            }

            Sign(1, 114, 511.5f, 322, form.WitnessSignature);
            Sign(1, 414, 511.5f, 576, form.Signature);

            return marks;
        }

        /// <summary>Breaks a long address at a comma or a space so the rest goes on the second line.</summary>
        public static (string? First, string? Second) SplitAddress(string? address, int fits)
        {
            if (address == null || address.Length <= fits)
                return (address, null);

            var comma = address.LastIndexOf(", ", fits, StringComparison.Ordinal);

            if (comma > 0)
                return (address[..(comma + 1)], address[(comma + 2)..]);

            var space = address.LastIndexOf(' ', fits);

            return space > 0 ? (address[..space], address[(space + 1)..]) : (address, null);
        }

        public static string Ordinal(int day)
        {
            if (day % 100 is 11 or 12 or 13)
                return day + "th";

            return day + (day % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };
        }

        private static DateTime Local(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();

        private static string? LocalDate(DateTime? utc) => utc == null ? null : Date(Local(utc.Value));

        private static string? Date(DateTime? date, string format = "d MMMM yyyy") =>
            date?.ToString(format, CultureInfo.InvariantCulture);

        private static string? Sex(Gender sex, bool whole = false) => sex switch
        {
            Gender.Female => whole ? "Female" : "F",
            Gender.Male => whole ? "Male" : "M",
            _ => null
        };

        private static string? Marital(MaritalStatus status) =>
            status == MaritalStatus.Unspecified ? null : status.ToString();
    }
}
