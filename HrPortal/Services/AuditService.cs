using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HrPortal.Services
{
    public class AuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public const string Created = "Created";
        public const string Updated = "Updated";
        public const string Activated = "Activated";
        public const string Deactivated = "Deactivated";
        public const string Deleted = "Deleted";
        public const string RoleChanged = "Role Changed";
        public const string PasswordReset = "Password Reset";
        public const string LeaveSubmitted = "Leave Submitted";
        public const string LeaveApproved = "Leave Approved";
        public const string LeaveRejected = "Leave Rejected";
        public const string LeaveReturned = "Leave Returned";
        public const string LeaveCancelled = "Leave Cancelled";
        public const string TrainingAssigned = "Training Assigned";
        public const string TrainingCompleted = "Training Completed";
        public const string DocumentUploaded = "Document Uploaded";

        public const string RequisitionSubmitted = "Requisition Submitted";
        public const string RequisitionApproved = "Requisition Approved";
        public const string RequisitionRejected = "Requisition Rejected";
        public const string RequisitionReturned = "Requisition Returned";
        public const string RequisitionCancelled = "Requisition Cancelled";

        public async Task LogAsync(
            string action,
            string entityName,
            string? entityId = null,
            string? description = null)
        {
            var entry = new AuditLog
            {
                EmployeeId = await GetCurrentEmployeeId(),
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Description = description,
                Timestamp = DateTime.UtcNow,
                IpAddress = GetIpAddress()
            };

            _context.AuditLogs.Add(entry);

            await _context.SaveChangesAsync();
        }

        private async Task<int?> GetCurrentEmployeeId()
        {
            var user = _httpContextAccessor.HttpContext?.User;

            var userIdValue = user?.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdValue, out var userId))
                return null;

            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.UserId == userId);

            return employee?.Id;
        }

        private string? GetIpAddress()
        {
            var address = _httpContextAccessor
                .HttpContext?
                .Connection
                .RemoteIpAddress;

            return address?.ToString();
        }
    }
}
