using HrPortal.Entities;
using HrPortal.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace HrPortal.DbContexts
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Employee> Employees { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<LeaveType> LeaveTypes { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<LeaveRequestHistory> LeaveRequestHistories { get; set; }
        public DbSet<LeaveBalance> LeaveBalances { get; set; }
        public DbSet<TrainingCourse> TrainingCourses { get; set; }
        public DbSet<TrainingMaterial> TrainingMaterials { get; set; }
        public DbSet<TrainingAssignment> TrainingAssignments { get; set; }
        public DbSet<TrainingFeedback> TrainingFeedbacks { get; set; }
        public DbSet<EmployeeDocument> EmployeeDocuments { get; set; }
        public DbSet<DocumentCategory> DocumentCategories { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<ApprovalConfiguration> ApprovalConfigurations { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<AssetMaintenance> AssetMaintenances { get; set; }
        public DbSet<Requisition> Requisitions { get; set; }
        public DbSet<RequisitionLine> RequisitionLines { get; set; }
        public DbSet<RequisitionHistory> RequisitionHistories { get; set; }
        public DbSet<ItActivity> ItActivities { get; set; }
        public DbSet<WeeklyReport> WeeklyReports { get; set; }
        public DbSet<WeeklyReportSection> WeeklyReportSections { get; set; }
        public DbSet<WeeklyReportPriority> WeeklyReportPriorities { get; set; }
        public DbSet<License> Licenses { get; set; }
        public DbSet<LicenseRenewal> LicenseRenewals { get; set; }
        public DbSet<TimesheetEntry> TimesheetEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Employee>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.EmployeeNumber)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.HasIndex(e => e.EmployeeNumber)
                    .IsUnique();

                entity.Property(e => e.FirstName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.LastName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Email)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.HasIndex(e => e.Email)
                    .IsUnique();

                entity.Property(e => e.JobTitle)
                    .HasMaxLength(150);

                entity.Property(e => e.EmploymentType)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(e => e.LeavingReason)
                    .HasMaxLength(500);

                entity.Property(e => e.Gender)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(e => e.MaritalStatus)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(e => e.Address).HasMaxLength(400);

                entity.Property(e => e.EmergencyContactName)
                    .HasMaxLength(200);

                entity.Property(e => e.EmergencyContactPhone)
                    .HasMaxLength(50);

                entity.Property(e => e.EmergencyContactRelationship)
                    .HasMaxLength(100);


                // Looked up when listing contracts that are running out.
                entity.HasIndex(e => e.ContractEndDate);


                entity.Property(e => e.NationalId)
                    .HasMaxLength(50);

                entity.HasOne(e => e.Department)
                    .WithMany(d => d.Employees)
                    .HasForeignKey(e => e.DepartmentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.User)
                    .WithOne(u => u.Employee)
                    .HasForeignKey<Employee>(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Department>(entity =>
            {
                entity.HasKey(d => d.Id);

                entity.Property(d => d.Name)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.HasIndex(d => d.Name)
                    .IsUnique();
            });

            builder.Entity<LeaveType>(entity =>
            {
                entity.HasKey(l => l.Id);

                entity.Property(l => l.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.HasIndex(l => l.Name)
                    .IsUnique();
            });

            builder.Entity<LeaveRequest>(entity =>
            {
                entity.HasKey(l => l.Id);

                entity.Property(l => l.RequestNumber)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.HasIndex(l => l.RequestNumber)
                    .IsUnique();

                entity.Property(l => l.NumberOfDays)
                    .HasPrecision(10, 2);

                entity.Property(l => l.Status)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.Property(l => l.Reason)
                    .HasMaxLength(1000);

                entity.HasOne(l => l.Employee)
                    .WithMany(e => e.LeaveRequests)
                    .HasForeignKey(l => l.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.LeaveType)
                    .WithMany(t => t.LeaveRequests)
                    .HasForeignKey(l => l.LeaveTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<LeaveRequestHistory>(entity =>
            {
                entity.HasKey(h => h.Id);

                entity.Property(h => h.Action)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.Property(h => h.PreviousStatus)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.Property(h => h.NewStatus)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.Property(h => h.Comment)
                    .HasMaxLength(2000);

                entity.HasOne(h => h.LeaveRequest)
                    .WithMany(l => l.History)
                    .HasForeignKey(h => h.LeaveRequestId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(h => h.ActionedByEmployee)
                    .WithMany()
                    .HasForeignKey(h => h.ActionedByEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<LeaveBalance>(entity =>
            {
                entity.HasKey(l => l.Id);

                entity.Property(l => l.AllocatedDays)
                    .HasPrecision(10, 2);

                entity.Property(l => l.UsedDays)
                    .HasPrecision(10, 2);

                entity.Property(l => l.PendingDays)
                    .HasPrecision(10, 2);

                entity.Property(l => l.RemainingDays)
                    .HasPrecision(10, 2);

                entity.HasOne(l => l.Employee)
                    .WithMany()
                    .HasForeignKey(l => l.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.LeaveType)
                    .WithMany()
                    .HasForeignKey(l => l.LeaveTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => new
                {
                    l.EmployeeId,
                    l.LeaveTypeId,
                    l.Year
                }).IsUnique();
            });

            builder.Entity<TrainingCourse>(entity =>
            {
                entity.HasKey(t => t.Id);

                entity.Property(t => t.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(t => t.Description)
                    .HasMaxLength(2000);

                entity.Property(t => t.Trainer)
                    .HasMaxLength(200);

                entity.HasOne(t => t.CreatedByEmployee)
                    .WithMany()
                    .HasForeignKey(t => t.CreatedByEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<TrainingMaterial>(entity =>
            {
                entity.HasKey(t => t.Id);

                entity.Property(t => t.FileName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(t => t.OriginalFileName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(t => t.FilePath)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(t => t.Description)
                    .HasMaxLength(1000);

                entity.HasOne(t => t.TrainingCourse)
                    .WithMany(c => c.Materials)
                    .HasForeignKey(t => t.TrainingCourseId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(t => t.UploadedByEmployee)
                    .WithMany()
                    .HasForeignKey(t => t.UploadedByEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<TrainingAssignment>(entity =>
            {
                entity.HasKey(t => t.Id);

                entity.HasOne(t => t.TrainingCourse)
                    .WithMany(c => c.Assignments)
                    .HasForeignKey(t => t.TrainingCourseId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(t => t.Employee)
                    .WithMany(e => e.TrainingAssignments)
                    .HasForeignKey(t => t.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(t => new
                {
                    t.TrainingCourseId,
                    t.EmployeeId
                }).IsUnique();
            });

            builder.Entity<TrainingFeedback>(entity =>
            {
                entity.HasKey(t => t.Id);

                entity.Property(t => t.Rating)
                    .IsRequired();

                entity.Property(t => t.Comments)
                    .HasMaxLength(2000);

                entity.HasOne(t => t.TrainingAssignment)
                    .WithOne(a => a.Feedback)
                    .HasForeignKey<TrainingFeedback>(t => t.TrainingAssignmentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<DocumentCategory>(entity =>
            {
                entity.HasKey(d => d.Id);

                entity.Property(d => d.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.HasIndex(d => d.Name)
                    .IsUnique();

                entity.Property(d => d.Description)
                    .HasMaxLength(500);
            });

            builder.Entity<EmployeeDocument>(entity =>
            {
                entity.HasKey(d => d.Id);

                entity.Property(d => d.FileName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(d => d.OriginalFileName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(d => d.FilePath)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(d => d.Description)
                    .HasMaxLength(1000);

                entity.HasOne(d => d.Employee)
                    .WithMany(e => e.Documents)
                    .HasForeignKey(d => d.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.DocumentCategory)
                    .WithMany(c => c.Documents)
                    .HasForeignKey(d => d.DocumentCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(d => d.UploadedByName)
                    .HasMaxLength(200);

                entity.HasOne(d => d.UploadedByEmployee)
                    .WithMany()
                    .HasForeignKey(d => d.UploadedByEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AssetMaintenance>(entity =>
            {
                entity.HasKey(m => m.Id);

                entity.Property(m => m.Type)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(m => m.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(m => m.PerformedBy).HasMaxLength(200);
                entity.Property(m => m.Notes).HasMaxLength(1000);

                entity.Property(m => m.Cost).HasPrecision(12, 2);

                // Maintenance belongs to the machine, so it goes when
                // the machine goes.
                entity.HasOne(m => m.Asset)
                    .WithMany()
                    .HasForeignKey(m => m.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Removing a vendor must not erase the service history.
                entity.HasOne(m => m.Vendor)
                    .WithMany()
                    .HasForeignKey(m => m.VendorId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(m => m.ScheduledFor);
            });

            builder.Entity<License>(entity =>
            {
                entity.HasKey(l => l.Id);

                entity.Property(l => l.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(l => l.Type)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(l => l.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(l => l.Reference).HasMaxLength(200);
                entity.Property(l => l.Notes).HasMaxLength(1000);
                entity.Property(l => l.Cost).HasPrecision(12, 2);

                // Losing a vendor or a seat holder must not take the
                // licence with it.
                entity.HasOne(l => l.Vendor)
                    .WithMany()
                    .HasForeignKey(l => l.VendorId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(l => l.AssignedToEmployee)
                    .WithMany()
                    .HasForeignKey(l => l.AssignedToEmployeeId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(l => l.ExpiryDate);
            });

            builder.Entity<LicenseRenewal>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.Property(r => r.Notes).HasMaxLength(1000);
                entity.Property(r => r.Cost).HasPrecision(12, 2);

                entity.HasOne(r => r.License)
                    .WithMany(l => l.Renewals)
                    .HasForeignKey(r => r.LicenseId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Requisition>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.Property(r => r.RequisitionNumber)
                    .IsRequired()
                    .HasMaxLength(30);

                entity.Property(r => r.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(r => r.Status)
                    .HasConversion<string>()
                    .HasMaxLength(30);

                entity.Property(r => r.Justification).HasMaxLength(2000);
                entity.Property(r => r.EstimatedTotal).HasPrecision(14, 2);

                // The requisition is part of the approval record, so
                // it outlives the requester leaving.
                entity.HasOne(r => r.Employee)
                    .WithMany()
                    .HasForeignKey(r => r.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Vendor)
                    .WithMany()
                    .HasForeignKey(r => r.VendorId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(r => r.RequisitionNumber).IsUnique();
                entity.HasIndex(r => r.Status);
                entity.HasIndex(r => new { r.EmployeeId, r.Status });
            });

            builder.Entity<RequisitionLine>(entity =>
            {
                entity.HasKey(l => l.Id);

                entity.Property(l => l.Description)
                    .IsRequired()
                    .HasMaxLength(300);

                entity.Property(l => l.UnitOfMeasure).HasMaxLength(30);
                entity.Property(l => l.Notes).HasMaxLength(500);
                entity.Property(l => l.Quantity).HasPrecision(12, 2);
                entity.Property(l => l.EstimatedUnitCost).HasPrecision(14, 2);

                entity.HasOne(l => l.Requisition)
                    .WithMany(r => r.Lines)
                    .HasForeignKey(l => l.RequisitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Ignore(l => l.LineTotal);
            });

            builder.Entity<RequisitionHistory>(entity =>
            {
                entity.HasKey(h => h.Id);

                entity.Property(h => h.Action)
                    .HasConversion<string>()
                    .HasMaxLength(30);

                entity.Property(h => h.PreviousStatus)
                    .HasConversion<string>()
                    .HasMaxLength(30);

                entity.Property(h => h.NewStatus)
                    .HasConversion<string>()
                    .HasMaxLength(30);

                entity.Property(h => h.Comment).HasMaxLength(1000);

                entity.HasOne(h => h.Requisition)
                    .WithMany(r => r.History)
                    .HasForeignKey(h => h.RequisitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Who approved what must survive the approver leaving.
                entity.HasOne(h => h.ActionedByEmployee)
                    .WithMany()
                    .HasForeignKey(h => h.ActionedByEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ItActivity>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.Property(a => a.Summary)
                    .IsRequired()
                    .HasMaxLength(300);

                entity.Property(a => a.Category)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(a => a.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(a => a.ActionTaken).HasMaxLength(2000);
                entity.Property(a => a.RaisedByOther).HasMaxLength(200);
                entity.Property(a => a.LoggedBy).HasMaxLength(200);

                // Removing a person or retiring a machine must not
                // erase the record of the work done.
                entity.HasOne(a => a.RaisedByEmployee)
                    .WithMany()
                    .HasForeignKey(a => a.RaisedByEmployeeId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(a => a.Asset)
                    .WithMany()
                    .HasForeignKey(a => a.AssetId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(a => a.LoggedOn);
                entity.HasIndex(a => new { a.Status, a.LoggedOn });
            });

            builder.Entity<WeeklyReport>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.Property(r => r.PreparedBy)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(r => r.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(r => r.ExecutiveOverview).HasMaxLength(4000);

                // One report per reporting period.
                entity.HasIndex(r => r.PeriodStart).IsUnique();
            });

            builder.Entity<WeeklyReportSection>(entity =>
            {
                entity.HasKey(s => s.Id);

                entity.Property(s => s.Heading)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(s => s.Workstream).HasMaxLength(200);

                entity.Property(s => s.WorkstreamStatus)
                    .HasConversion<string>()
                    .HasMaxLength(40);

                entity.Property(s => s.Body).HasMaxLength(8000);

                entity.HasOne(s => s.Report)
                    .WithMany(r => r.Sections)
                    .HasForeignKey(s => s.WeeklyReportId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<WeeklyReportPriority>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Text)
                    .IsRequired()
                    .HasMaxLength(1000);

                entity.HasOne(p => p.Report)
                    .WithMany(r => r.Priorities)
                    .HasForeignKey(p => p.WeeklyReportId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Notification>(entity =>
            {
                entity.HasKey(n => n.Id);

                entity.Property(n => n.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(n => n.Message)
                    .IsRequired()
                    .HasMaxLength(2000);

                entity.Property(n => n.Link)
                    .HasMaxLength(500);

                entity.HasOne(n => n.Employee)
                    .WithMany(e => e.Notifications)
                    .HasForeignKey(n => n.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.Property(a => a.Action)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(a => a.EntityName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(a => a.EntityId)
                    .HasMaxLength(100);

                entity.Property(a => a.Description)
                    .HasMaxLength(2000);

                entity.Property(a => a.IpAddress)
                    .HasMaxLength(50);

                entity.HasOne(a => a.Employee)
                    .WithMany()
                    .HasForeignKey(a => a.EmployeeId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<ApprovalConfiguration>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.Property(a => a.Role)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.HasOne(a => a.Employee)
                    .WithMany()
                    .HasForeignKey(a => a.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(a => a.Role)
                    .IsUnique()
                    .HasFilter("[IsActive] = 1");
            });

            builder.Entity<Vendor>(entity =>
            {
                entity.HasKey(v => v.Id);

                entity.Property(v => v.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(v => v.ProductOrService)
                    .IsRequired()
                    .HasMaxLength(300);

                entity.Property(v => v.Category).HasMaxLength(100);
                entity.Property(v => v.ContactPerson).HasMaxLength(150);
                entity.Property(v => v.Email).HasMaxLength(200);
                entity.Property(v => v.PhoneNumber).HasMaxLength(50);
                entity.Property(v => v.AlternatePhone).HasMaxLength(50);
                entity.Property(v => v.Website).HasMaxLength(200);
                entity.Property(v => v.Address).HasMaxLength(300);
                entity.Property(v => v.Notes).HasMaxLength(1000);

                entity.HasIndex(v => v.Name);
            });

            builder.Entity<Asset>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.Property(a => a.DeviceModel)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(a => a.Company)
                    .HasMaxLength(100);

                entity.Property(a => a.Processor)
                    .HasMaxLength(200);

                entity.Property(a => a.SerialNumber)
                    .HasMaxLength(100);

                entity.Property(a => a.Notes)
                    .HasMaxLength(1000);

                entity.Property(a => a.Condition)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.HasOne(a => a.AssignedToEmployee)
                    .WithMany()
                    .HasForeignKey(a => a.AssignedToEmployeeId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<TimesheetEntry>(entity =>
            {
                entity.HasKey(t => t.Id);

                entity.Property(t => t.WorkDate)
                    .HasColumnType("date");

                entity.Property(t => t.Category)
                    .HasConversion<string>()
                    .HasMaxLength(30);

                entity.Property(t => t.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(t => t.Details).HasMaxLength(2000);
                entity.Property(t => t.Reference).HasMaxLength(100);

                // The timesheet is the employee's own record and goes
                // with them.
                entity.HasOne(t => t.Employee)
                    .WithMany()
                    .HasForeignKey(t => t.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Retiring a machine must not erase the work done on it.
                entity.HasOne(t => t.Asset)
                    .WithMany()
                    .HasForeignKey(t => t.AssetId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(t => new { t.EmployeeId, t.WorkDate });
                entity.HasIndex(t => t.WorkDate);
            });
        }
    }
}
