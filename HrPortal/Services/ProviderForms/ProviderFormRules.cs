using HrPortal.Entities;

namespace HrPortal.Services.ProviderForms
{
    /// <summary>
    /// What each provider's form asks for, how many lines it has, and
    /// who may change it at each stage.
    /// </summary>
    public static class ProviderFormRules
    {
        public const int MaxDependants = 19;
        public const int MaxChildren = 4;
        public const int MaxNominees = 4;
        public const int MaxText = 150;

        public static readonly string[] Plans =
        {
            "Executive Plus", "Executive Standard",
            "Platinum Plus", "Platinum Standard",
            "Diamond Plus", "Diamond Standard",
            "Gold Plus", "Gold Standard",
            "Silver Plus", "Silver Standard",
            "Bronze Standard"
        };

        public static readonly int[] Levels = { 5, 4, 3, 2 };

        public static string Title(ProviderFormKind kind) => kind switch
        {
            ProviderFormKind.Nyaradzo => "Nyaradzo funeral cover form",
            ProviderFormKind.Minerva => "Minerva pension admission form",
            _ => kind.ToString()
        };

        public static string Provider(ProviderFormKind kind) => kind switch
        {
            ProviderFormKind.Nyaradzo => "Nyaradzo Life Assurance",
            ProviderFormKind.Minerva => "Minerva Benefits Consulting",
            _ => kind.ToString()
        };

        public static bool WithEmployee(ProviderFormStatus status) =>
            status is ProviderFormStatus.Draft or ProviderFormStatus.Returned;

        public static bool WithHr(ProviderFormStatus status) =>
            status is ProviderFormStatus.Submitted or ProviderFormStatus.Completed;

        /// <summary>
        /// Drops empty lines and anything that belongs to the other form,
        /// and tidies the text.
        /// </summary>
        public static void Clean(ProviderFormKind kind, ProviderFormMember m)
        {
            TidyAll(m);

            m.Dependants = Kept(m.Dependants);
            m.Children = Kept(m.Children);
            m.Nominees = Kept(m.Nominees);

            if (kind == ProviderFormKind.Minerva)
            {
                m.Dependants.Clear();
                m.OtherId = m.Plan = null;
                m.Breadwinner = m.SahwiraLite = null;
                m.BreadwinnerChange = m.SahwiraLiteChange = null;
                m.ToiletsFamily = m.TentFamily = m.ToiletsAll = m.TentAll = null;
                m.BeneficiarySurname = m.BeneficiaryFirstNames = m.BeneficiaryNationalId = null;
                m.BeneficiaryRelationship = m.BeneficiaryAddress = m.BeneficiaryEmail = m.BeneficiaryPhone = null;
            }
            else
            {
                m.Children.Clear();
                m.Nominees.Clear();
                m.SpouseName = m.WillHolder = m.WillAddress = m.SignedAt = null;
                m.SpouseDateOfBirth = null;

                if (m.Breadwinner == null)
                    m.BreadwinnerChange = null;

                if (m.SahwiraLite == null)
                    m.SahwiraLiteChange = null;
            }
        }

        public static void Clean(ProviderFormEmployer e) => TidyAll(e);

        /// <summary>
        /// What stops a draft being kept at all: more lines than the form
        /// has, or text too long to fit on it.
        /// </summary>
        public static List<string> SaveProblems(ProviderFormMember m)
        {
            var problems = new List<string>();

            if (m.Dependants.Count > MaxDependants)
                problems.Add($"The form has room for {MaxDependants} dependants.");

            if (m.Children.Count > MaxChildren)
                problems.Add($"The form has room for {MaxChildren} children.");

            if (m.Nominees.Count > MaxNominees)
                problems.Add($"The form has room for {MaxNominees} nominees.");

            if (Texts(m).Concat(m.Dependants.SelectMany(Texts))
                .Concat(m.Children.SelectMany(Texts))
                .Concat(m.Nominees.SelectMany(Texts))
                .Any(t => t.Length > MaxText))
            {
                problems.Add($"Keep each answer to {MaxText} characters so it fits on the form.");
            }

            return problems;
        }

        /// <summary>What has to be put right before the employee can sign and send it.</summary>
        public static List<string> MemberProblems(ProviderFormKind kind, ProviderFormMember m, DateTime today)
        {
            var problems = SaveProblems(m);

            void Need(string? value, string what)
            {
                if (string.IsNullOrWhiteSpace(value))
                    problems.Add($"Give {what}.");
            }

            void Past(DateTime? value, string what)
            {
                if (value > today)
                    problems.Add($"{what} cannot be in the future.");
            }

            Need(m.Surname, "your surname");
            Need(m.FirstNames, "your first names");
            Need(m.NationalId, "your national ID number");
            Need(m.Phone, "your phone number");
            Need(m.Address, "your address");

            if (m.DateOfBirth == null)
                problems.Add("Give your date of birth.");

            Past(m.DateOfBirth, "Your date of birth");

            if (m.Sex is not (Gender.Female or Gender.Male))
                problems.Add("Choose male or female, as the form asks.");

            if (m.MaritalStatus == MaritalStatus.Unspecified)
                problems.Add("Choose your marital status.");

            if (kind == ProviderFormKind.Nyaradzo)
                NyaradzoProblems(m, problems, Past);
            else
                MinervaProblems(m, problems, Past);

            return problems;
        }

        private static void NyaradzoProblems(
            ProviderFormMember m, List<string> problems, Action<DateTime?, string> past)
        {
            if (m.Plan != null && !Plans.Contains(m.Plan))
                problems.Add("Choose a plan from the list.");

            if ((m.Breadwinner is int b && !Levels.Contains(b)) ||
                (m.SahwiraLite is int s && !Levels.Contains(s)))
            {
                problems.Add("Choose a benefit from the list.");
            }

            if (m.Breadwinner != null && !IsAddOrRemove(m.BreadwinnerChange))
                problems.Add("Choose whether the breadwinner benefit is being added or removed.");

            if (m.SahwiraLite != null && !IsAddOrRemove(m.SahwiraLiteChange))
                problems.Add("Choose whether Sahwira Lite is being added or removed.");

            var extras = new[] { m.ToiletsFamily, m.TentFamily, m.ToiletsAll, m.TentAll };

            if (extras.Any(c => c == ProviderFormChange.Update))
                problems.Add("Choose whether each extra is being added or removed.");

            if (m.Plan == null && m.Breadwinner == null && m.SahwiraLite == null &&
                m.Dependants.Count == 0 && extras.All(c => c == null))
            {
                problems.Add("Choose a plan or benefit, or list a dependant.");
            }

            for (var i = 0; i < m.Dependants.Count; i++)
            {
                var p = m.Dependants[i];
                var who = $"{p.FirstNames} {p.Surname}".Trim();

                if (who.Length == 0)
                    who = $"dependant {i + 1}";

                if (p.Surname == null || p.FirstNames == null)
                    problems.Add($"Give both a surname and first names for dependant {i + 1}.");

                if (p.Sex is not (Gender.Female or Gender.Male))
                    problems.Add($"Choose male or female for {who}.");

                if (p.DateOfBirth == null)
                    problems.Add($"Give the date of birth of {who}.");

                past(p.DateOfBirth, $"The date of birth of {who}");

                if (p.Relationship == null)
                    problems.Add($"Give the relationship of {who} to you.");

                if (p.Change == null)
                    problems.Add($"Choose whether {who} is being added, removed or updated.");
            }
        }

        private static void MinervaProblems(
            ProviderFormMember m, List<string> problems, Action<DateTime?, string> past)
        {
            if (m.SpouseName == null && m.SpouseDateOfBirth != null)
                problems.Add("Give the name of your spouse.");

            if (m.SpouseName != null && m.SpouseDateOfBirth == null)
                problems.Add("Give the date of birth of your spouse.");

            past(m.SpouseDateOfBirth, "The date of birth of your spouse");

            foreach (var child in m.Children)
            {
                var who = child.Name ?? "each child";

                if (child.Name == null)
                    problems.Add("Give the name of each child.");

                if (child.DateOfBirth == null)
                    problems.Add($"Give the date of birth of {who}.");

                past(child.DateOfBirth, $"The date of birth of {who}");
            }

            if (m.Nominees.Count == 0)
                problems.Add("Name at least one dependant or beneficiary on the nomination form.");

            foreach (var nominee in m.Nominees)
            {
                if (nominee.Name == null)
                    problems.Add("Give the name of each nominee.");
                else if (nominee.Relationship == null)
                    problems.Add($"Give the relationship of {nominee.Name} to you.");
            }

            if (m.SignedAt == null)
                problems.Add("Give the place where you are signing.");
        }

        /// <summary>What HR still has to fill in before the form is complete.</summary>
        public static List<string> EmployerProblems(ProviderFormKind kind, ProviderFormEmployer e)
        {
            var problems = new List<string>();

            if (Texts(e).Any(t => t.Length > MaxText))
                problems.Add($"Keep each answer to {MaxText} characters so it fits on the form.");

            if (kind == ProviderFormKind.Nyaradzo)
            {
                if (e.EffectiveDate == null)
                    problems.Add("Give the effective date.");

                return problems;
            }

            void Need(string? value, string what)
            {
                if (string.IsNullOrWhiteSpace(value))
                    problems.Add($"Give {what}.");
            }

            Need(e.FundName, "the name of the fund");
            Need(e.ContributionMonth, "the month contributions start");
            Need(e.EmployerName, "the employer");
            Need(e.SignatoryName, "the name of the person signing for the employer");
            Need(e.Designation, "their designation");

            if (e.DateJoined == null)
                problems.Add("Give the date the employee joined.");

            if (e.AnnualSalary is null or <= 0)
                problems.Add("Give the annual pensionable salary.");

            if (e.NotCountingTo < e.NotCountingFrom)
                problems.Add("The period not counting towards pension ends before it starts.");

            return problems;
        }

        private static bool IsAddOrRemove(ProviderFormChange? change) =>
            change is ProviderFormChange.Add or ProviderFormChange.Remove;

        private static List<T> Kept<T>(List<T>? rows) where T : class
        {
            var kept = (rows ?? new()).Where(r => r != null).ToList();

            foreach (var row in kept)
                TidyAll(row);

            return kept.Where(HasAnything).ToList();
        }

        private static bool HasAnything<T>(T row) where T : class =>
            row switch
            {
                ProviderFormPerson p =>
                    p.Surname != null || p.FirstNames != null || p.IdNumber != null ||
                    p.Relationship != null || p.DateOfBirth != null,
                ProviderFormName n =>
                    n.Name != null || n.Relationship != null || n.DateOfBirth != null,
                _ => true
            };

        private static IEnumerable<string> Texts(object of) =>
            of.GetType().GetProperties()
                .Where(p => p.PropertyType == typeof(string))
                .Select(p => (string?)p.GetValue(of))
                .OfType<string>();

        private static void TidyAll(object of)
        {
            foreach (var property in of.GetType().GetProperties().Where(p => p.PropertyType == typeof(string)))
                property.SetValue(of, Tidy((string?)property.GetValue(of)));
        }

        private static string? Tidy(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var parts = text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => new string(line.Where(c => !char.IsControl(c)).ToArray()).Trim().TrimEnd(','))
                .Where(line => line.Length > 0);

            var joined = string.Join(
                ' ',
                string.Join(", ", parts).Split(' ', StringSplitOptions.RemoveEmptyEntries));

            return joined.Length == 0 ? null : joined;
        }
    }
}
