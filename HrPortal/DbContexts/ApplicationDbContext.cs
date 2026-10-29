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

        public DbSet<TrainingReminder> TrainingReminders { get; set; }

        public DbSet<ExpiryReminder> ExpiryReminders { get; set; }

        public DbSet<TrainingAssignment> TrainingAssignments { get; set; }
        public DbSet<TrainingFeedback> TrainingFeedbacks { get; set; }
        public DbSet<EmployeeDocument> EmployeeDocuments { get; set; }
        public DbSet<DocumentCategory> DocumentCategories { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<AssetMaintenance> AssetMaintenances { get; set; }
        public DbSet<Certification> Certifications { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }
        public DbSet<Policy> Policies { get; set; }
        public DbSet<PolicyAcknowledgement> PolicyAcknowledgements { get; set; }
        public DbSet<RepositoryFolder> RepositoryFolders { get; set; }
        public DbSet<RepositoryDocument> RepositoryDocuments { get; set; }
        public DbSet<PublicHoliday> PublicHolidays { get; set; }

        public DbSet<LoanApplication> LoanApplications { get; set; }
        public DbSet<LoanApplicationAttachment> LoanApplicationAttachments { get; set; }
        public DbSet<LoanApplicationHistory> LoanApplicationHistories { get; set; }

        public DbSet<CourseApplication> CourseApplications { get; set; }
        public DbSet<CourseApplicationHistory> CourseApplicationHistories { get; set; }
        public DbSet<ItActivity> ItActivities { get; set; }
        public DbSet<WeeklyReport> WeeklyReports { get; set; }
        public DbSet<WeeklyReportSection> WeeklyReportSections { get; set; }
        public DbSet<WeeklyReportPriority> WeeklyReportPriorities { get; set; }
        public DbSet<License> Licenses { get; set; }
        public DbSet<LicenseRenewal> LicenseRenewals { get; set; }

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

                entity.HasIndex(e => e.ContractEndDate);

                entity.Property(e => e.NationalId)
                    .HasMaxLength(50);

                entity.Property(e => e.JobTitle)
                    .HasMaxLength(150);

                entity.HasOne(e => e.Department)
                    .WithMany(d => d.Employees)
                    .HasForeignKey(e => e.DepartmentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.User)
                    .WithOne(u => u.Employee)
                    .HasForeignKey<Employee>(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Supervisor)
                    .WithMany(e => e.DirectReports)
                    .HasForeignKey(e => e.SupervisorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.SupervisorId);
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

                entity.HasOne(t => t.TrainerEmployee)
                    .WithMany()
                    .HasForeignKey(t => t.TrainerEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(t => t.TrainerEmployeeId);

                entity.Navigation(t => t.TrainerEmployee)
                    .AutoInclude();
            });

            builder.Entity<TrainingReminder>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.Property(r => r.Kind)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.HasOne(r => r.TrainingCourse)
                    .WithMany()
                    .HasForeignKey(r => r.TrainingCourseId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Employee)
                    .WithMany()
                    .HasForeignKey(r => r.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(r => new
                    {
                        r.Kind,
                        r.TrainingCourseId,
                        r.EmployeeId,
                        r.SentOn
                    })
                    .IsUnique();
            });

            builder.Entity<ExpiryReminder>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.Property(r => r.Kind)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.HasIndex(r => new
                    {
                        r.Kind,
                        r.RecordId,
                        r.ExpiryDate,
                        r.DaysBefore
                    })
                    .IsUnique();
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

                entity.HasOne(m => m.Asset)
                    .WithMany()
                    .HasForeignKey(m => m.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);

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

            builder.Entity<Policy>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(p => p.Version)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(p => p.Reference).HasMaxLength(50);
                entity.Property(p => p.Category).HasMaxLength(100);
                entity.Property(p => p.Owner).HasMaxLength(200);
                entity.Property(p => p.Summary).HasMaxLength(1000);
                entity.Property(p => p.CreatedByName).HasMaxLength(200);

                entity.Property(p => p.FileName)
                    .IsRequired()
                    .HasMaxLength(260);

                entity.Property(p => p.OriginalFileName)
                    .IsRequired()
                    .HasMaxLength(260);

                entity.Property(p => p.FilePath)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(p => p.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(p => p.Audience)
                    .HasConversion<string>()
                    .HasMaxLength(30);

                entity.HasOne(p => p.Supersedes)
                    .WithMany()
                    .HasForeignKey(p => p.SupersedesPolicyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(p => p.Status);
                entity.HasIndex(p => p.ReviewDate);
            });

            builder.Entity<PolicyAcknowledgement>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.HasOne(a => a.Policy)
                    .WithMany(p => p.Acknowledgements)
                    .HasForeignKey(a => a.PolicyId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Employee)
                    .WithMany()
                    .HasForeignKey(a => a.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(a => new { a.PolicyId, a.EmployeeId })
                    .IsUnique();
            });

            builder.Entity<RepositoryFolder>(entity =>
            {
                entity.HasKey(f => f.Id);

                entity.Property(f => f.Name)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(f => f.Description).HasMaxLength(500);

                entity.Property(f => f.Audience)
                    .HasConversion<string>()
                    .HasMaxLength(30);

                entity.HasIndex(f => f.Name).IsUnique();
            });

            builder.Entity<RepositoryDocument>(entity =>
            {
                entity.HasKey(d => d.Id);

                entity.Property(d => d.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(d => d.Description).HasMaxLength(1000);

                entity.Property(d => d.FileName)
                    .IsRequired()
                    .HasMaxLength(260);

                entity.Property(d => d.OriginalFileName)
                    .IsRequired()
                    .HasMaxLength(260);

                entity.Property(d => d.FilePath)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(d => d.UploadedByName).HasMaxLength(200);

                entity.HasOne(d => d.Folder)
                    .WithMany(f => f.Documents)
                    .HasForeignKey(d => d.RepositoryFolderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(d => d.RepositoryFolderId);
            });

            builder.Entity<AppSetting>(entity =>
            {
                entity.HasKey(s => s.Id);

                entity.Property(s => s.Key)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(s => s.Value).HasMaxLength(1000);
                entity.Property(s => s.UpdatedBy).HasMaxLength(200);

                entity.HasIndex(s => s.Key).IsUnique();
            });

            builder.Entity<Certification>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(c => c.IssuedBy).HasMaxLength(200);
                entity.Property(c => c.Reference).HasMaxLength(100);
                entity.Property(c => c.Notes).HasMaxLength(500);

                entity.HasOne(c => c.Employee)
                    .WithMany()
                    .HasForeignKey(c => c.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.TrainingCourse)
                    .WithMany()
                    .HasForeignKey(c => c.TrainingCourseId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(c => c.ExpiryDate);
            });

            builder.Entity<PublicHoliday>(entity =>
            {
                entity.HasKey(h => h.Id);

                entity.Property(h => h.Name)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(h => h.Notes).HasMaxLength(500);

                entity.HasIndex(h => h.Date).IsUnique();
            });

            builder.Entity<LoanApplication>(entity =>
            {
                entity.HasKey(l => l.Id);

                entity.Property(l => l.ApplicationNumber)
                    .IsRequired()
                    .HasMaxLength(30);

                entity.Property(l => l.Status)
                    .HasConversion<string>()
                    .HasMaxLength(30);

                entity.Property(l => l.Currency)
                    .HasConversion<string>()
                    .HasMaxLength(10);

                entity.Property(l => l.Purpose)
                    .IsRequired()
                    .HasMaxLength(2000);

                entity.Property(l => l.AmountRequested).HasPrecision(14, 2);
                entity.Property(l => l.AmountAuthorised).HasPrecision(14, 2);

                entity.HasOne(l => l.Employee)
                    .WithMany()
                    .HasForeignKey(l => l.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => l.ApplicationNumber).IsUnique();
                entity.HasIndex(l => l.Status);
                entity.HasIndex(l => new { l.EmployeeId, l.Status });

                entity.Ignore(l => l.MonthlyInstalment);
            });

            builder.Entity<LoanApplicationAttachment>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.Property(a => a.FileName)
                    .IsRequired()
                    .HasMaxLength(260);

                entity.Property(a => a.OriginalFileName)
                    .IsRequired()
                    .HasMaxLength(260);

                entity.Property(a => a.FilePath)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.HasOne(a => a.LoanApplication)
                    .WithMany(l => l.Attachments)
                    .HasForeignKey(a => a.LoanApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.UploadedByEmployee)
                    .WithMany()
                    .HasForeignKey(a => a.UploadedByEmployeeId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<LoanApplicationHistory>(entity =>
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

                entity.HasOne(h => h.LoanApplication)
                    .WithMany(l => l.History)
                    .HasForeignKey(h => h.LoanApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(h => h.ActionedByEmployee)
                    .WithMany()
                    .HasForeignKey(h => h.ActionedByEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<CourseApplication>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.ApplicationNumber)
                    .IsRequired()
                    .HasMaxLength(30);

                entity.Property(c => c.Status)
                    .HasConversion<string>()
                    .HasMaxLength(30);

                entity.Property(c => c.Sitting)
                    .HasConversion<string>()
                    .HasMaxLength(30);

                entity.Property(c => c.ProgramIntended)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(c => c.Justification)
                    .IsRequired()
                    .HasMaxLength(2000);

                entity.HasOne(c => c.Employee)
                    .WithMany()
                    .HasForeignKey(c => c.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => c.ApplicationNumber).IsUnique();
                entity.HasIndex(c => c.Status);
                entity.HasIndex(c => new { c.EmployeeId, c.Status });
            });

            builder.Entity<CourseApplicationHistory>(entity =>
            {
                entity.HasKey(h => h.Id);

                entity.Property(h => h.Action)
                    .HasConversion<string>()
                    .HasMaxLength(40);

                entity.Property(h => h.PreviousStatus)
                    .HasConversion<string>()
                    .HasMaxLength(30);

                entity.Property(h => h.NewStatus)
                    .HasConversion<string>()
                    .HasMaxLength(30);

                entity.Property(h => h.Comment).HasMaxLength(1000);

                entity.HasOne(h => h.CourseApplication)
                    .WithMany(c => c.History)
                    .HasForeignKey(h => h.CourseApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);

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

                entity.HasIndex(n => new
                {
                    n.EmployeeId,
                    n.IsRead
                });

                entity.HasIndex(n => new
                {
                    n.EmployeeId,
                    n.CreatedAt
                });
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

                entity.HasIndex(a => a.Timestamp);

                entity.HasIndex(a => new
                {
                    a.EntityName,
                    a.Timestamp
                });
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

        }
    }
}
