using HrPortal.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    public class EmployeeRecordService
    {
        private readonly ApplicationDbContext _context;

        public EmployeeRecordService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<string>> DeleteBlockersAsync(int employeeId)
        {
            var blockers = new List<string>();

            void Add(int count, string label)
            {
                if (count > 0)
                    blockers.Add($"{count} {label}");
            }

            Add(
                await _context.LeaveRequests
                    .CountAsync(r => r.EmployeeId == employeeId),
                "leave request(s)");

            Add(
                await _context.LeaveRequestHistories
                    .CountAsync(h => h.ActionedByEmployeeId == employeeId),
                "leave approval action(s)");

            Add(
                await _context.TrainingCourses
                    .CountAsync(c => c.CreatedByEmployeeId == employeeId),
                "training course(s) they created");

            Add(
                await _context.TrainingMaterials
                    .CountAsync(m => m.UploadedByEmployeeId == employeeId),
                "training material(s) they uploaded");

            Add(
                await _context.EmployeeDocuments
                    .CountAsync(d => d.UploadedByEmployeeId == employeeId),
                "document(s) they uploaded for others");

            Add(
                await _context.ApprovalConfigurations
                    .CountAsync(a => a.EmployeeId == employeeId),
                "approval role assignment(s)");

            Add(
                await _context.EmployeeDocuments
                    .CountAsync(d => d.EmployeeId == employeeId),
                "document(s) held on their file");

            return blockers;
        }

        public async Task<List<string>> DeleteRemovalsAsync(int employeeId)
        {
            var removals = new List<string>();

            void Add(int count, string label)
            {
                if (count > 0)
                    removals.Add($"{count} {label}");
            }

            Add(
                await _context.LeaveBalances
                    .CountAsync(b => b.EmployeeId == employeeId),
                "leave balance(s)");

            Add(
                await _context.TrainingAssignments
                    .CountAsync(a => a.EmployeeId == employeeId),
                "training assignment(s)");

            Add(
                await _context.EmployeeDocuments
                    .CountAsync(d => d.EmployeeId == employeeId),
                "document(s) held on file");

            Add(
                await _context.Notifications
                    .CountAsync(n => n.EmployeeId == employeeId),
                "notification(s)");

            return removals;
        }
        public const string EmployeeNumberField = "EmployeeNumber";
        public const string ContractEndField = "ContractEndDate";
        public const string DateOfBirthField = "DateOfBirth";
        public const string EndDateField = "EndDate";

        public async Task<List<EmployeeRuleError>> ValidateAsync(
            string employeeNumber,
            DateTime dateJoined,
            DateTime? dateOfBirth,
            DateTime? contractEndDate,
            DateTime? endDate,
            bool isActive,
            int? existingEmployeeId)
        {
            var errors = new List<EmployeeRuleError>();

            var numberTaken = await _context.Employees
                .AnyAsync(e =>
                    e.EmployeeNumber == employeeNumber &&
                    (existingEmployeeId == null ||
                     e.Id != existingEmployeeId.Value));

            if (numberTaken)
            {
                errors.Add(new EmployeeRuleError(
                    EmployeeNumberField,
                    "This employee number already exists."));
            }

            if (contractEndDate.HasValue &&
                contractEndDate.Value.Date <= dateJoined.Date)
            {
                errors.Add(new EmployeeRuleError(
                    ContractEndField,
                    "The contract cannot end on or before the start date."));
            }

            if (dateOfBirth.HasValue)
            {
                if (dateOfBirth.Value.Date >= DateTime.Today)
                {
                    errors.Add(new EmployeeRuleError(
                        DateOfBirthField,
                        "The date of birth must be in the past."));
                }
                else if (dateOfBirth.Value.Date > dateJoined.Date)
                {
                    errors.Add(new EmployeeRuleError(
                        DateOfBirthField,
                        "They cannot have been born after they started."));
                }
            }

            if (!isActive)
            {
                if (endDate == null)
                {
                    errors.Add(new EmployeeRuleError(
                        EndDateField,
                        "Give the date they left before marking them no " +
                        "longer employed."));
                }
                else if (endDate.Value.Date < dateJoined.Date)
                {
                    errors.Add(new EmployeeRuleError(
                        EndDateField,
                        "They cannot have left before they started."));
                }
            }

            return errors;
        }
    }

    public record EmployeeRuleError(string Field, string Message);
}
