using HrPortal.Security;

namespace HrPortal.Entities
{
    /// <summary>An outside provider's own form that staff fill in here.</summary>
    public enum ProviderFormKind
    {
        Nyaradzo,
        Minerva
    }

    public enum ProviderFormStatus
    {
        /// <summary>Being filled in by the employee.</summary>
        Draft,

        /// <summary>Signed and with HR.</summary>
        Submitted,

        /// <summary>Sent back for the employee to change.</summary>
        Returned,

        /// <summary>HR has checked it and filled in the employer's part.</summary>
        Completed,

        /// <summary>Sent to the provider, so nothing on it changes again.</summary>
        Sent
    }

    /// <summary>
    /// One person's answers to a provider's form. The answers and the
    /// signatures are kept here, not the filled file: the file is made
    /// from them and the provider's own blank form whenever it is
    /// opened, so it reads the same on either company's portal.
    /// </summary>
    public class ProviderForm : IEmployeeOwned
    {
        public int Id { get; set; }

        public string Reference { get; set; } = string.Empty;

        public int EmployeeId { get; set; }

        public ProviderFormKind Kind { get; set; }

        public ProviderFormStatus Status { get; set; }

        public ProviderFormMember Member { get; set; } = new();

        public ProviderFormEmployer Employer { get; set; } = new();

        public byte[]? Signature { get; set; }

        public byte[]? WitnessSignature { get; set; }

        public byte[]? EmployerSignature { get; set; }

        /// <summary>What HR asked to be changed when it was last sent back.</summary>
        public string? ReturnComment { get; set; }

        public int? CompletedByEmployeeId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime? SentAt { get; set; }

        public Employee Employee { get; set; } = null!;

        public Employee? CompletedByEmployee { get; set; }
    }

    public enum ProviderFormChange
    {
        Add,
        Remove,
        Update
    }

    public class ProviderFormPerson
    {
        public string? Surname { get; set; }
        public string? FirstNames { get; set; }
        public Gender Sex { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? IdNumber { get; set; }
        public string? Relationship { get; set; }
        public ProviderFormChange? Change { get; set; }
    }

    public class ProviderFormName
    {
        public string? Name { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Relationship { get; set; }
    }

    /// <summary>The employee's part, for whichever form it is.</summary>
    public class ProviderFormMember
    {
        public string? Surname { get; set; }
        public string? FirstNames { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public Gender Sex { get; set; }
        public MaritalStatus MaritalStatus { get; set; }
        public string? NationalId { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }

        public string? OtherId { get; set; }
        public string? Plan { get; set; }
        public int? Breadwinner { get; set; }
        public ProviderFormChange? BreadwinnerChange { get; set; }
        public int? SahwiraLite { get; set; }
        public ProviderFormChange? SahwiraLiteChange { get; set; }
        public ProviderFormChange? ToiletsFamily { get; set; }
        public ProviderFormChange? TentFamily { get; set; }
        public ProviderFormChange? ToiletsAll { get; set; }
        public ProviderFormChange? TentAll { get; set; }
        public string? BeneficiarySurname { get; set; }
        public string? BeneficiaryFirstNames { get; set; }
        public string? BeneficiaryNationalId { get; set; }
        public string? BeneficiaryRelationship { get; set; }
        public string? BeneficiaryAddress { get; set; }
        public string? BeneficiaryEmail { get; set; }
        public string? BeneficiaryPhone { get; set; }
        public List<ProviderFormPerson> Dependants { get; set; } = new();

        public string? SpouseName { get; set; }
        public DateTime? SpouseDateOfBirth { get; set; }
        public List<ProviderFormName> Children { get; set; } = new();
        public List<ProviderFormName> Nominees { get; set; } = new();
        public string? WillHolder { get; set; }
        public string? WillAddress { get; set; }
        public string? SignedAt { get; set; }
    }

    /// <summary>The part HR fills in before the form goes to the provider.</summary>
    public class ProviderFormEmployer
    {
        public DateTime? EffectiveDate { get; set; }
        public DateTime? CoverDate { get; set; }
        public string? PolicyNumber { get; set; }
        public string? Premium { get; set; }

        public string? FundName { get; set; }
        public string? EmployeeNumber { get; set; }
        public DateTime? DateJoined { get; set; }
        public string? ContributionMonth { get; set; }
        public decimal? AnnualSalary { get; set; }
        public DateTime? NotCountingFrom { get; set; }
        public DateTime? NotCountingTo { get; set; }
        public string? EmployerName { get; set; }
        public string? Branch { get; set; }
        public string? CompanyNumber { get; set; }
        public string? SignatoryName { get; set; }
        public string? Designation { get; set; }
    }
}
