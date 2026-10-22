using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Sanctioned loans, which HR prints and takes to Finance. HR has
    /// no part in the decision, so this reads and prints and does
    /// nothing else.
    /// </summary>
    [Authorize(Roles = Roles.HrOnly)]
    public class LoanFinanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly LoanApplicationPdfService _pdf;

        public LoanFinanceController(
            ApplicationDbContext context,
            LoanApplicationPdfService pdf)
        {
            _context = context;
            _pdf = pdf;
        }

        public async Task<IActionResult> Index()
        {
            var applications = await _context.LoanApplications
                .AsNoTracking()
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Department)
                .Where(l => l.Status == LoanApplicationStatus.Approved)
                .OrderByDescending(l => l.CompletedAt)
                .ToListAsync();

            return View(applications);
        }

        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var application = await _context.LoanApplications
                .AsNoTracking()
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Department)
                .Include(l => l.Attachments)
                .Include(l => l.History)
                    .ThenInclude(h => h.ActionedByEmployee)
                .FirstOrDefaultAsync(l =>
                    l.Id == id &&
                    l.Status == LoanApplicationStatus.Approved);

            if (application == null)
                return NotFound();

            return File(
                _pdf.Render(application),
                "application/pdf",
                $"Loan application - " +
                $"{application.ApplicationNumber}.pdf");
        }
    }
}
