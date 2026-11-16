namespace HrPortal.ViewModels
{
    /// <summary>
    /// A form that says which of the group's companies somebody works
    /// for, and which of them employs them.
    /// </summary>
    public interface IWorksForCompanies
    {
        List<int> CompanyIds { get; set; }

        int HomeCompanyId { get; set; }
    }
}
