using HrPortal.Security;

namespace HrPortal.Entities
{
    public class Notification : IEmployeeOwned
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string? Link { get; set; }

        /// <summary>
        /// The company whose portal the link belongs to: the one that
        /// decides the request it is about. Opening it on the other
        /// company's portal goes across, since that portal cannot show
        /// the request. Null for a link that works on either.
        /// </summary>
        public int? CompanyId { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; }

        public Employee Employee { get; set; } = null!;
    }
}
