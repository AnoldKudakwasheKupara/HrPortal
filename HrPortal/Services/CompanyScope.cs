using HrPortal.Entities;

namespace HrPortal.Services
{
    /// <summary>
    /// The company whose data a request may see. It is set for each web
    /// request from the address it came in on, and the database then
    /// shows that company's records and the records of the people who
    /// work there. Work the portal does on its own, such as the daily
    /// reminders, has no company set and sees every company.
    /// </summary>
    public static class CompanyScope
    {
        private static readonly AsyncLocal<int?> Scope = new();
        private static IReadOnlyDictionary<string, int> _ids = new Dictionary<string, int>();

        /// <summary>The company in scope, or null for every company.</summary>
        public static int? Current => Scope.Value;

        public static void Enter(int? companyId) => Scope.Value = companyId;

        /// <summary>Remembers which company each brand belongs to.</summary>
        public static void Learn(IEnumerable<Company> companies) =>
            _ids = companies.ToDictionary(c => c.Key, c => c.Id);

        public static int? IdFor(string brandKey) =>
            _ids.TryGetValue(brandKey, out var id) ? id : null;
    }
}
