namespace HrPortal.Services
{
    public class LeaveOptions
    {
        public const string Section = "Leave";

        public int BackdateGraceDays { get; set; } = 7;
    }
}
