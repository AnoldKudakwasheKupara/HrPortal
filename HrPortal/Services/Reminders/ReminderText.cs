namespace HrPortal.Services.Reminders
{
    public static class ReminderText
    {
        public static string Due(DateTime today, DateTime date)
        {
            var days = (date.Date - today).Days;

            return days switch
            {
                < 0 => $"expired {-days} day{Plural(-days)} ago",
                0 => "expires today",
                _ => $"expires in {days} day{Plural(days)}"
            };
        }

        public static string Plural(int count) => count == 1 ? "" : "s";

        public static string Humanise(Enum value)
        {
            var name = value.ToString();

            var spaced = string.Concat(
                name.Select((c, i) =>
                    i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));

            return spaced.ToLowerInvariant();
        }
    }
}
