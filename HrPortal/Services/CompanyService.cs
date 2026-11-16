using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>
    /// Which of the group's companies this request is for, worked out
    /// from the brand the web address chose, and who belongs to it.
    /// </summary>
    public class CompanyService
    {
        private readonly ApplicationDbContext _context;
        private Company? _current;
        private IReadOnlyList<Company>? _all;

        public CompanyService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>The company whose portal is being used.</summary>
        public async Task<Company> CurrentAsync()
        {
            var key = Brand.Current.Key;

            if (_current?.Key == key)
                return _current;

            _current = (await AllAsync()).FirstOrDefault(c => c.Key == key)
                ?? throw new InvalidOperationException(
                    $"There is no company for the brand '{key}'. It is " +
                    "added when the portal starts, so restart it.");

            return _current;
        }

        public async Task<int> CurrentIdAsync() => (await CurrentAsync()).Id;

        public async Task<IReadOnlyList<Company>> AllAsync() =>
            _all ??= await _context.Companies
                .AsNoTracking()
                .OrderBy(c => c.Id)
                .ToListAsync();

        /// <summary>
        /// Whether there is more than one company, so forms need to ask
        /// which one somebody works for.
        /// </summary>
        public async Task<bool> IsGroupAsync() => (await AllAsync()).Count > 1;

        /// <summary>
        /// Whether somebody works for the company whose portal this is.
        /// </summary>
        public async Task<bool> WorksHereAsync(int employeeId)
        {
            var here = await CurrentIdAsync();

            return await _context.EmployeeCompanies
                .AnyAsync(m => m.EmployeeId == employeeId && m.CompanyId == here);
        }

        /// <summary>The company's short name, as its brand gives it.</summary>
        public static string NameOf(Company company) =>
            Brand.ForKey(company.Key)?.ShortName ?? company.Name;

        /// <summary>The name of the company's portal.</summary>
        public static string PortalOf(Company company) =>
            Brand.ForKey(company.Key)?.PortalName ?? company.Name;

        /// <summary>
        /// Adds a company for every brand the portal serves that does
        /// not have one yet, so a new brand file is all a new company
        /// needs.
        /// </summary>
        public static async Task EnsureForBrandsAsync(
            ApplicationDbContext context,
            IEnumerable<Brand> brands)
        {
            var known = await context.Companies
                .Select(c => new { c.Id, c.Key })
                .ToListAsync();

            var next = known.Count == 0 ? 1 : known.Max(c => c.Id) + 1;
            var added = false;

            foreach (var brand in brands.OrderBy(b => b.Key, StringComparer.Ordinal))
            {
                if (known.Any(c => c.Key == brand.Key))
                    continue;

                context.Companies.Add(new Company
                {
                    Id = next++,
                    Key = brand.Key,
                    Name = brand.FullName
                });

                added = true;
            }

            if (added)
                await context.SaveChangesAsync();
        }
    }
}
