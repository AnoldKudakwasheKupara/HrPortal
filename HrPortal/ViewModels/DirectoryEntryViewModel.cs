namespace HrPortal.ViewModels
{
    /// <summary>
    /// One row of the staff directory. Only what a colleague needs to
    /// find and contact someone at work.
    /// </summary>
    public class DirectoryEntryViewModel
    {
        public string Name { get; set; } = string.Empty;

        public string? JobTitle { get; set; }

        public string Department { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }
    }
}
