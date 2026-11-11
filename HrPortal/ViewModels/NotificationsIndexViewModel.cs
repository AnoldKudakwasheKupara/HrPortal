using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class NotificationsIndexViewModel
    {
        public const string All = "all";
        public const string Unread = "unread";
        public const string Read = "read";

        public PagedResult<Notification> Page { get; set; } = new();

        public string Show { get; set; } = All;

        public int Total { get; set; }

        public int UnreadCount { get; set; }

        public int ThisWeek { get; set; }

        public List<ListTab> Tabs => new()
        {
            new(All, "All", "fa-list-ul", "is-teal", Total),
            new(Unread, "Unread", "fa-envelope", "is-blue", UnreadCount),
            new(Read, "Read", "fa-envelope-open", "is-green", Total - UnreadCount)
        };

        public static string Normalise(string? show) => show is Unread or Read ? show : All;
    }
}
