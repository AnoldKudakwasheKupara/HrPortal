using HrPortal.Entities;
using HrPortal.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System.Reflection.Emit;

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

                entity.Property(l => l.DefaultDays)
                    .HasPrecision(10, 2);

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

                entity.HasOne(d => d.UploadedByEmployee)
                    .WithMany()
                    .HasForeignKey(d => d.UploadedByEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
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
        }
    }
}