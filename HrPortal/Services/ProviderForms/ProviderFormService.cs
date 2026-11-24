using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.ProviderForms
{
    /// <summary>
    /// Starts a provider's form for somebody and moves it on: the
    /// employee signs and sends it, HR sends it back or fills in the
    /// employer's part, and records when it went to the provider.
    /// </summary>
    public class ProviderFormService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;

        public ProviderFormService(
            ApplicationDbContext context,
            NotificationService notifications)
        {
            _context = context;
            _notifications = notifications;
        }

        /// <summary>
        /// The form they are already filling in, or a new one with what
        /// the portal knows already in place.
        /// </summary>
        public async Task<ProviderForm> StartAsync(int employeeId, ProviderFormKind kind)
        {
            var mine = await _context.ProviderForms
                .Where(f => f.EmployeeId == employeeId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            var open = mine.FirstOrDefault(f =>
                f.Kind == kind && ProviderFormRules.WithEmployee(f.Status));

            if (open != null)
                return open;

            var employee = await _context.Employees
                .AsNoTracking()
                .FirstAsync(e => e.Id == employeeId);

            var form = new ProviderForm
            {
                Reference = await NextReferenceAsync(),
                EmployeeId = employeeId,
                Kind = kind,
                Status = ProviderFormStatus.Draft,
                Member = Prefill(
                    kind,
                    employee,
                    mine.FirstOrDefault(f => f.Kind == kind)?.Member,
                    mine.FirstOrDefault(f => f.Kind != kind)?.Member),
                CreatedAt = DateTime.UtcNow
            };

            _context.ProviderForms.Add(form);

            await _context.SaveChangesAsync();

            return form;
        }

        /// <summary>
        /// What goes on a new form before anybody types: the employment
        /// record first, then what they gave on their last form of this
        /// kind, then the family they listed on the other one.
        /// </summary>
        public static ProviderFormMember Prefill(
            ProviderFormKind kind,
            Employee employee,
            ProviderFormMember? last,
            ProviderFormMember? other)
        {
            var member = new ProviderFormMember
            {
                Surname = employee.LastName,
                FirstNames = last?.FirstNames ?? other?.FirstNames ?? employee.FirstName,
                DateOfBirth = employee.DateOfBirth ?? last?.DateOfBirth ?? other?.DateOfBirth,
                Sex = employee.Gender is Gender.Female or Gender.Male
                    ? employee.Gender
                    : last?.Sex ?? other?.Sex ?? Gender.Unspecified,
                MaritalStatus = employee.MaritalStatus != MaritalStatus.Unspecified
                    ? employee.MaritalStatus
                    : last?.MaritalStatus ?? other?.MaritalStatus ?? MaritalStatus.Unspecified,
                NationalId = employee.NationalId ?? last?.NationalId ?? other?.NationalId,
                Phone = employee.PhoneNumber ?? last?.Phone ?? other?.Phone,
                Email = employee.Email,
                Address = employee.Address ?? last?.Address ?? other?.Address
            };

            if (kind == ProviderFormKind.Nyaradzo)
            {
                member.OtherId = last?.OtherId;
                member.BeneficiarySurname = last?.BeneficiarySurname;
                member.BeneficiaryFirstNames = last?.BeneficiaryFirstNames;
                member.BeneficiaryNationalId = last?.BeneficiaryNationalId;
                member.BeneficiaryRelationship = last?.BeneficiaryRelationship;
                member.BeneficiaryAddress = last?.BeneficiaryAddress;
                member.BeneficiaryEmail = last?.BeneficiaryEmail;
                member.BeneficiaryPhone = last?.BeneficiaryPhone;

                member.Dependants = last != null
                    ? last.Dependants
                        .Where(p => p.Change != ProviderFormChange.Remove)
                        .Select(p => Copy(p, null))
                        .ToList()
                    : FromFamily(other);
            }
            else
            {
                member.WillHolder = last?.WillHolder;
                member.WillAddress = last?.WillAddress;

                if (last != null)
                {
                    member.SpouseName = last.SpouseName;
                    member.SpouseDateOfBirth = last.SpouseDateOfBirth;
                    member.Children = last.Children.Select(Copy).ToList();
                    member.Nominees = last.Nominees.Select(Copy).ToList();
                }
                else
                {
                    FromDependants(member, other);
                }
            }

            return member;
        }

        private static List<ProviderFormPerson> FromFamily(ProviderFormMember? other)
        {
            var people = new List<ProviderFormPerson>();

            if (other == null)
                return people;

            void Add(string? name, DateTime? born, string relationship)
            {
                if (string.IsNullOrWhiteSpace(name))
                    return;

                var (first, surname) = SplitName(name);

                people.Add(new ProviderFormPerson
                {
                    FirstNames = first,
                    Surname = surname,
                    DateOfBirth = born,
                    Relationship = relationship,
                    Change = ProviderFormChange.Add
                });
            }

            Add(other.SpouseName, other.SpouseDateOfBirth, "Spouse");

            foreach (var child in other.Children)
                Add(child.Name, child.DateOfBirth, "Child");

            return people;
        }

        private static void FromDependants(ProviderFormMember member, ProviderFormMember? other)
        {
            var listed = other?.Dependants
                .Where(p => p.Change != ProviderFormChange.Remove)
                .ToList() ?? new();

            var spouse = listed.FirstOrDefault(p => Is(p, "spouse", "wife", "husband"));

            if (spouse != null)
            {
                member.SpouseName = NameOf(spouse);
                member.SpouseDateOfBirth = spouse.DateOfBirth;
            }

            member.Children = listed
                .Where(p => Is(p, "child", "son", "daughter"))
                .Take(ProviderFormRules.MaxChildren)
                .Select(p => new ProviderFormName { Name = NameOf(p), DateOfBirth = p.DateOfBirth })
                .ToList();
        }

        private static bool Is(ProviderFormPerson person, params string[] words) =>
            words.Contains(person.Relationship?.Trim().ToLowerInvariant());

        private static string NameOf(ProviderFormPerson p) => $"{p.FirstNames} {p.Surname}".Trim();

        private static (string? First, string Surname) SplitName(string name)
        {
            var at = name.Trim().LastIndexOf(' ');

            return at < 0 ? (null, name.Trim()) : (name.Trim()[..at], name.Trim()[(at + 1)..]);
        }

        private static ProviderFormPerson Copy(ProviderFormPerson p, ProviderFormChange? change) => new()
        {
            Surname = p.Surname,
            FirstNames = p.FirstNames,
            Sex = p.Sex,
            DateOfBirth = p.DateOfBirth,
            IdNumber = p.IdNumber,
            Relationship = p.Relationship,
            Change = change
        };

        private static ProviderFormName Copy(ProviderFormName n) => new()
        {
            Name = n.Name,
            DateOfBirth = n.DateOfBirth,
            Relationship = n.Relationship
        };

        /// <summary>
        /// Sets out the employer's part for HR, with what was given on
        /// the company's last Minerva form where it is the same for
        /// everybody.
        /// </summary>
        public async Task<ProviderFormEmployer> SuggestEmployerAsync(ProviderForm form, Employee signatory)
        {
            var e = form.Employer;

            if (form.Kind != ProviderFormKind.Minerva || form.CompletedAt != null)
                return e;

            var home = await _context.Companies
                .AsNoTracking()
                .FirstAsync(c => c.Id == form.Employee.HomeCompanyId);

            var last = (await _context.ProviderForms
                    .AsNoTracking()
                    .Where(f =>
                        f.Kind == ProviderFormKind.Minerva &&
                        f.CompletedAt != null &&
                        f.Employee.HomeCompanyId == home.Id)
                    .OrderByDescending(f => f.CompletedAt)
                    .FirstOrDefaultAsync())
                ?.Employer;

            return new ProviderFormEmployer
            {
                FundName = e.FundName ?? last?.FundName,
                EmployeeNumber = e.EmployeeNumber ?? form.Employee.EmployeeNumber,
                DateJoined = e.DateJoined ?? form.Employee.DateJoined.Date,
                ContributionMonth = e.ContributionMonth,
                AnnualSalary = e.AnnualSalary,
                NotCountingFrom = e.NotCountingFrom,
                NotCountingTo = e.NotCountingTo,
                EmployerName = e.EmployerName ?? last?.EmployerName ?? Brand.ForKey(home.Key)?.LegalName ?? home.Name,
                Branch = e.Branch ?? last?.Branch,
                CompanyNumber = e.CompanyNumber ?? last?.CompanyNumber,
                SignatoryName = e.SignatoryName ?? $"{signatory.FirstName} {signatory.LastName}",
                Designation = e.Designation ?? signatory.JobTitle
            };
        }

        public async Task SubmitAsync(ProviderForm form, byte[] signature, byte[]? witness)
        {
            Ensure(ProviderFormRules.WithEmployee(form.Status), form);

            form.Status = ProviderFormStatus.Submitted;
            form.Signature = signature;
            form.WitnessSignature = witness;
            form.ReturnComment = null;
            form.SubmittedAt = DateTime.UtcNow;

            await _notifications.NotifyApproverAsync(
                ApprovalRole.HROfficer,
                form.EmployeeId,
                $"{ProviderFormRules.Title(form.Kind)} to check",
                $"{form.Reference} from {Who(form)} is signed and waiting for you to check.",
                $"/ProviderForm/Details/{form.Id}",
                EmailKind.ActionNeeded);

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Sends it back to be changed. The signatures go too, because
        /// they were put to what it said before.
        /// </summary>
        public async Task ReturnAsync(ProviderForm form, string comment)
        {
            Ensure(ProviderFormRules.WithHr(form.Status), form);

            form.Status = ProviderFormStatus.Returned;
            form.ReturnComment = comment;
            form.Signature = null;
            form.WitnessSignature = null;
            form.EmployerSignature = null;
            form.SubmittedAt = null;
            form.CompletedAt = null;
            form.CompletedByEmployeeId = null;

            _notifications.Notify(
                form.EmployeeId,
                $"{ProviderFormRules.Title(form.Kind)} sent back",
                $"{form.Reference} needs changes before it can go to " +
                $"{ProviderFormRules.Provider(form.Kind)}. {comment}",
                $"/MyForms/Details/{form.Id}",
                EmailKind.Returned,
                form.Employee.HomeCompanyId);

            await _context.SaveChangesAsync();
        }

        public async Task CompleteAsync(
            ProviderForm form,
            ProviderFormEmployer employer,
            byte[]? signature,
            int completedByEmployeeId)
        {
            Ensure(ProviderFormRules.WithHr(form.Status), form);

            form.Status = ProviderFormStatus.Completed;
            form.Employer = employer;
            form.EmployerSignature = signature;
            form.CompletedByEmployeeId = completedByEmployeeId;
            form.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task MarkSentAsync(ProviderForm form)
        {
            Ensure(form.Status == ProviderFormStatus.Completed, form);

            form.Status = ProviderFormStatus.Sent;
            form.SentAt = DateTime.UtcNow;

            _notifications.Notify(
                form.EmployeeId,
                $"{ProviderFormRules.Title(form.Kind)} sent",
                $"{form.Reference} has been sent to {ProviderFormRules.Provider(form.Kind)}.",
                $"/MyForms/Details/{form.Id}",
                companyId: form.Employee.HomeCompanyId);

            await _context.SaveChangesAsync();
        }

        private static void Ensure(bool allowed, ProviderForm form)
        {
            if (!allowed)
                throw new InvalidOperationException($"{form.Reference} is {form.Status}, so that cannot be done now.");
        }

        private static string Who(ProviderForm form) =>
            $"{form.Employee.FirstName} {form.Employee.LastName}";

        private async Task<string> NextReferenceAsync()
        {
            var prefix = $"PF-{DateTime.UtcNow.Year}-";

            var last = await _context.ProviderForms
                .IgnoreQueryFilters()
                .Where(f => f.Reference.StartsWith(prefix))
                .OrderByDescending(f => f.Reference)
                .Select(f => f.Reference)
                .FirstOrDefaultAsync();

            var next = last != null && int.TryParse(last[prefix.Length..], out var number) ? number + 1 : 1;

            return $"{prefix}{next:D4}";
        }
    }
}
