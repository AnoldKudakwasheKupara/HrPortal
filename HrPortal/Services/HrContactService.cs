using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    public record HrContact(string? Name, string? Email, string? Phone)
    {
        public bool IsKnown => !string.IsNullOrWhiteSpace(Name);

        public static readonly HrContact Unknown =
            new(null, null, null);
    }

    public class HrContactService
    {
        private readonly ApplicationDbContext _context;

        public HrContactService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<HrContact> GetAsync()
        {
            var contact = await _context.ApprovalConfigurations
                .AsNoTracking()
                .Where(a =>
                    a.Role == ApprovalRole.HROfficer &&
                    a.IsActive &&
                    a.Employee.IsActive)
                .Select(a => new HrContact(
                    a.Employee.FirstName + " " + a.Employee.LastName,
                    a.Employee.Email,
                    a.Employee.PhoneNumber))
                .FirstOrDefaultAsync();

            return contact ?? HrContact.Unknown;
        }
    }
}
