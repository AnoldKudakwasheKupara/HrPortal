namespace HrPortal.ViewModels
{
    /// <summary>
    /// An asset as offered in a picker, carrying who holds it so the
    /// list can be narrowed to one person without a round trip.
    /// </summary>
    public class AssetOptionViewModel
    {
        public int Id { get; set; }

        public string Label { get; set; } = string.Empty;

        public int? EmployeeId { get; set; }
    }
}
