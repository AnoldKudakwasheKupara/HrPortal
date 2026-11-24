using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    /// <summary>The employee's part of a provider's form, being filled in.</summary>
    public class ProviderFormEditViewModel
    {
        public int Id { get; set; }
        public ProviderFormKind Kind { get; set; }
        public string? Reference { get; set; }
        public string? ReturnComment { get; set; }
        public ProviderFormMember Member { get; set; } = new();
    }

    /// <summary>One form as HR checks it, and the part HR fills in.</summary>
    public class ProviderFormReviewViewModel
    {
        public ProviderForm Form { get; set; } = null!;
        public ProviderFormEmployer Employer { get; set; } = new();
    }

    /// <summary>A box to sign in: the field it fills and what to call it.</summary>
    public sealed record SignaturePad(string Name, string Label, bool Required);
}
