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
    }
}
