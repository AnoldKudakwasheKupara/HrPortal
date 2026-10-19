using HrPortal.ViewModels;

namespace HrPortal.Services.Reporting
{
    public static class ReportMath
    {
        public static ReportTrend MonthlyTrend(
            string title,
            IEnumerable<(DateTime When, decimal Value)> values,
            string unit = "",
            string format = "N0")
        {
            var trend = new ReportTrend(title, unit, format);

            var start = new DateTime(
                DateTime.Today.Year,
                DateTime.Today.Month,
                1).AddMonths(-11);

            var buckets = values
                .Where(v => v.When.Date >= start)
                .GroupBy(v => new { v.When.Year, v.When.Month })
                .ToDictionary(
                    g => (g.Key.Year, g.Key.Month),
                    g => g.Sum(v => v.Value));

            for (var i = 0; i < 12; i++)
            {
                var month = start.AddMonths(i);

                buckets.TryGetValue(
                    (month.Year, month.Month),
                    out var value);

                trend.Add(month.ToString("MMM"), value);
            }

            return trend;
        }

        public static string Period(ReportFilter filter)
        {
            if (filter.From.HasValue && filter.To.HasValue)
            {
                return $"{filter.From.Value:d MMM yyyy} to " +
                       $"{filter.To.Value:d MMM yyyy}";
            }

            if (filter.From.HasValue)
                return $"From {filter.From.Value:d MMM yyyy}";

            if (filter.To.HasValue)
                return $"Up to {filter.To.Value:d MMM yyyy}";

            return "All dates";
        }

        public static string Slice(ReportFilter filter, DateTime soon) =>
            filter.Show switch
            {
                "expired" => "Live licences already past their expiry date",
                "expiring" =>
                    "Live licences expiring on or before " +
                    soon.ToString("d MMM yyyy"),
                "autorenew" => "Licences set to renew automatically",
                _ => filter.IncludeInactive
                    ? "All licences, cancelled ones included"
                    : "Live licences"
            };

        public static string D(DateTime? value) =>
            value?.ToString("yyyy-MM-dd") ?? string.Empty;

        public static string N(decimal value) =>
            value.ToString("0.##");
    }
}
