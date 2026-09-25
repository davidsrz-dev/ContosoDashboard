using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Models;

namespace ContosoDashboard.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<TaskItem> Tasks { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<TaskComment> TaskComments { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<ProjectMember> ProjectMembers { get; set; } = null!;
    public DbSet<Announcement> Announcements { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<DocumentShare> DocumentShares { get; set; } = null!;
    public DbSet<DocumentAuditLog> DocumentAuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Document relationships
        modelBuilder.Entity<Document>()
            .HasOne(d => d.UploadedByUser)
            .WithMany()
            .HasForeignKey(d => d.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Document>()
            .HasOne(d => d.Project)
            .WithMany()
            .HasForeignKey(d => d.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Document>()
            .HasOne(d => d.Task)
            .WithMany()
            .HasForeignKey(d => d.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Document>()
            .HasMany(d => d.Shares)
            .WithOne(s => s.Document)
            .HasForeignKey(s => s.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Document>()
            .HasMany(d => d.AuditLogs)
            .WithOne(a => a.Document)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Document>()
            .HasIndex(d => d.UploadedByUserId);

        modelBuilder.Entity<Document>()
            .HasIndex(d => d.ProjectId);

        modelBuilder.Entity<Document>()
            .HasIndex(d => d.Category);

        modelBuilder.Entity<DocumentShare>()
            .HasIndex(s => s.SharedWithUserId);

        modelBuilder.Entity<DocumentShare>()
            .HasIndex(s => s.SharedWithDepartment);

        modelBuilder.Entity<DocumentAuditLog>()
            .HasIndex(a => a.DocumentId);

        modelBuilder.Entity<DocumentAuditLog>()
            .HasIndex(a => a.UserId);

        // Configure User relationships
        modelBuilder.Entity<User>()
            .HasMany(u => u.AssignedTasks)
            .WithOne(t => t.AssignedUser)
            .HasForeignKey(t => t.AssignedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasMany(u => u.CreatedTasks)
            .WithOne(t => t.CreatedByUser)
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasMany(u => u.ManagedProjects)
            .WithOne(p => p.ProjectManager)
            .HasForeignKey(p => p.ProjectManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure indexes for performance
        modelBuilder.Entity<TaskItem>()
            .HasIndex(t => t.AssignedUserId);

        modelBuilder.Entity<TaskItem>()
            .HasIndex(t => t.Status);

        modelBuilder.Entity<TaskItem>()
            .HasIndex(t => t.DueDate);

        modelBuilder.Entity<Project>()
            .HasIndex(p => p.ProjectManagerId);

        modelBuilder.Entity<Project>()
            .HasIndex(p => p.Status);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.IsRead });

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Seed initial data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Seed an admin user
        modelBuilder.Entity<User>().HasData(
            new User
            {
                UserId = 1,
                Email = "admin@contoso.com",
                DisplayName = "System Administrator",
                Department = "IT",
                JobTitle = "Administrator",
                Role = UserRole.Administrator,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = DateTime.UtcNow,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            },
            new User
            {
                UserId = 2,
                Email = "camille.nicole@contoso.com",
                DisplayName = "Camille Nicole",
                Department = "Engineering",
                JobTitle = "Project Manager",
                Role = UserRole.ProjectManager,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = DateTime.UtcNow,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            },
            new User
            {
                UserId = 3,
                Email = "floris.kregel@contoso.com",
                DisplayName = "Floris Kregel",
                Department = "Engineering",
                JobTitle = "Team Lead",
                Role = UserRole.TeamLead,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = DateTime.UtcNow,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            },
            new User
            {
                UserId = 4,
                Email = "ni.kang@contoso.com",
                DisplayName = "Ni Kang",
                Department = "Engineering",
                JobTitle = "Software Engineer",
                Role = UserRole.Employee,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedDate = DateTime.UtcNow,
                EmailNotificationsEnabled = true,
                InAppNotificationsEnabled = true
            }
        );

        // Seed a sample project
        modelBuilder.Entity<Project>().HasData(
            new Project
            {
                ProjectId = 1,
                Name = "ContosoDashboard Development",
                Description = "Internal employee productivity dashboard",
                ProjectManagerId = 2,
                StartDate = DateTime.UtcNow.AddDays(-30),
                TargetCompletionDate = DateTime.UtcNow.AddDays(60),
                Status = ProjectStatus.Active,
                CreatedDate = DateTime.UtcNow.AddDays(-30),
                UpdatedDate = DateTime.UtcNow
            }
        );

        // Seed sample tasks
        modelBuilder.Entity<TaskItem>().HasData(
            new TaskItem
            {
                TaskId = 1,
                Title = "Design database schema",
                Description = "Create entity relationship diagram and database design",
                Priority = TaskPriority.High,
                Status = Models.TaskStatus.Completed,
                DueDate = DateTime.UtcNow.AddDays(-20),
                AssignedUserId = 4,
                CreatedByUserId = 2,
                ProjectId = 1,
                CreatedDate = DateTime.UtcNow.AddDays(-30),
                UpdatedDate = DateTime.UtcNow.AddDays(-20)
            },
            new TaskItem
            {
                TaskId = 2,
                Title = "Implement authentication",
                Description = "Set up Microsoft Entra ID authentication",
                Priority = TaskPriority.Critical,
                Status = Models.TaskStatus.InProgress,
                DueDate = DateTime.UtcNow.AddDays(5),
                AssignedUserId = 4,
                CreatedByUserId = 2,
                ProjectId = 1,
                CreatedDate = DateTime.UtcNow.AddDays(-25),
                UpdatedDate = DateTime.UtcNow
            },
            new TaskItem
            {
                TaskId = 3,
                Title = "Create UI mockups",
                Description = "Design user interface mockups for all main pages",
                Priority = TaskPriority.Medium,
                Status = Models.TaskStatus.NotStarted,
                DueDate = DateTime.UtcNow.AddDays(10),
                AssignedUserId = 4,
                CreatedByUserId = 2,
                ProjectId = 1,
                CreatedDate = DateTime.UtcNow.AddDays(-20),
                UpdatedDate = DateTime.UtcNow.AddDays(-20)
            }
        );

        // Seed project members
        modelBuilder.Entity<ProjectMember>().HasData(
            new ProjectMember
            {
                ProjectMemberId = 1,
                ProjectId = 1,
                UserId = 3,
                Role = "TeamLead",
                AssignedDate = DateTime.UtcNow.AddDays(-30)
            },
            new ProjectMember
            {
                ProjectMemberId = 2,
                ProjectId = 1,
                UserId = 4,
                Role = "Developer",
                AssignedDate = DateTime.UtcNow.AddDays(-30)
            }
        );

        // Seed announcement
        modelBuilder.Entity<Announcement>().HasData(
            new Announcement
            {
                AnnouncementId = 1,
                Title = "Welcome to ContosoDashboard",
                Content = "Welcome to the new ContosoDashboard application. This platform will help you manage your tasks and projects more efficiently.",
                CreatedByUserId = 1,
                PublishDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(30),
                IsActive = true
            }
        );

        // Seed sample documents
        modelBuilder.Entity<Document>().HasData(
            new Document
            {
                DocumentId = 1,
                Title = "Contoso Dashboard Architecture Specification",
                Description = "Core architecture guide and security requirements specification for ContosoDashboard.",
                Category = "Project Documents",
                OriginalFileName = "ContosoDashboard_Architecture.pdf",
                StorageKey = "1/1/sample-architecture.pdf",
                FileSize = 1048576,
                ContentType = "application/pdf",
                CreatedDate = DateTime.UtcNow.AddDays(-15),
                UpdatedDate = DateTime.UtcNow.AddDays(-15),
                UploadedByUserId = 1,
                ProjectId = 1,
                TaskId = 1,
                Tags = "architecture, security, spec"
            },
            new Document
            {
                DocumentId = 2,
                Title = "Employee Onboarding Guide",
                Description = "Welcome pack and engineering handbook for new Contoso employees.",
                Category = "Team Resources",
                OriginalFileName = "Onboarding_Guide.docx",
                StorageKey = "2/personal/sample-onboarding.docx",
                FileSize = 524288,
                ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                CreatedDate = DateTime.UtcNow.AddDays(-10),
                UpdatedDate = DateTime.UtcNow.AddDays(-10),
                UploadedByUserId = 2,
                ProjectId = null,
                TaskId = null,
                Tags = "onboarding, team, hr"
            },
            new Document
            {
                DocumentId = 3,
                Title = "Q3 Financial Performance Report",
                Description = "Executive summary of Q3 financial metrics and engineering budget utilization.",
                Category = "Reports",
                OriginalFileName = "Q3_Financial_Performance.xlsx",
                StorageKey = "1/1/sample-q3-report.xlsx",
                FileSize = 2097152,
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                CreatedDate = DateTime.UtcNow.AddDays(-45),
                UpdatedDate = DateTime.UtcNow.AddDays(-45),
                UploadedByUserId = 2,
                ProjectId = 1,
                TaskId = null,
                Tags = "financial, q3, report"
            },
            new Document
            {
                DocumentId = 4,
                Title = "Sprint Planning Presentation",
                Description = "Slide deck for Sprint 4 planning and roadmap alignment.",
                Category = "Presentations",
                OriginalFileName = "Sprint_Planning_Review.pptx",
                StorageKey = "2/1/sample-sprint-deck.pptx",
                FileSize = 5242880,
                ContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                CreatedDate = DateTime.UtcNow.AddDays(-30),
                UpdatedDate = DateTime.UtcNow.AddDays(-30),
                UploadedByUserId = 3,
                ProjectId = 1,
                TaskId = 2,
                Tags = "presentation, sprint, review"
            },
            new Document
            {
                DocumentId = 5,
                Title = "Product Architecture Diagram",
                Description = "High-resolution system architecture and network boundary diagram.",
                Category = "Team Resources",
                OriginalFileName = "System_Architecture_Diagram.png",
                StorageKey = "1/1/sample-diagram.png",
                FileSize = 1572864,
                ContentType = "image/png",
                CreatedDate = DateTime.UtcNow.AddDays(-60),
                UpdatedDate = DateTime.UtcNow.AddDays(-60),
                UploadedByUserId = 1,
                ProjectId = 1,
                TaskId = 1,
                Tags = "diagram, architecture, image"
            },
            new Document
            {
                DocumentId = 6,
                Title = "Team Workstation Setup Notes",
                Description = "Developer setup notes and environment configuration instructions.",
                Category = "Personal Files",
                OriginalFileName = "Workstation_Setup.txt",
                StorageKey = "4/personal/sample-setup.txt",
                FileSize = 8192,
                ContentType = "text/plain",
                CreatedDate = DateTime.UtcNow.AddDays(-5),
                UpdatedDate = DateTime.UtcNow.AddDays(-5),
                UploadedByUserId = 4,
                ProjectId = null,
                TaskId = null,
                Tags = "setup, dev, notes"
            }
        );

        // Seed sample document share
        modelBuilder.Entity<DocumentShare>().HasData(
            new DocumentShare
            {
                DocumentShareId = 1,
                DocumentId = 2,
                SharedWithDepartment = "Engineering",
                SharedByUserId = 2,
                SharedDate = DateTime.UtcNow.AddDays(-10),
                Permission = "ReadOnly"
            }
        );

        // Seed sample document audit access logs
        modelBuilder.Entity<DocumentAuditLog>().HasData(
            new DocumentAuditLog
            {
                DocumentAuditLogId = 1,
                DocumentId = 1,
                ActionType = "Preview",
                UserId = 2,
                Timestamp = DateTime.UtcNow.AddDays(-14),
                Details = "Inline preview generated"
            },
            new DocumentAuditLog
            {
                DocumentAuditLogId = 2,
                DocumentId = 1,
                ActionType = "Download",
                UserId = 3,
                Timestamp = DateTime.UtcNow.AddDays(-12),
                Details = "File downloaded"
            },
            new DocumentAuditLog
            {
                DocumentAuditLogId = 3,
                DocumentId = 2,
                ActionType = "Preview",
                UserId = 4,
                Timestamp = DateTime.UtcNow.AddDays(-8),
                Details = "Inline preview generated"
            },
            new DocumentAuditLog
            {
                DocumentAuditLogId = 4,
                DocumentId = 3,
                ActionType = "Download",
                UserId = 1,
                Timestamp = DateTime.UtcNow.AddDays(-40),
                Details = "File downloaded"
            },
            new DocumentAuditLog
            {
                DocumentAuditLogId = 5,
                DocumentId = 4,
                ActionType = "Preview",
                UserId = 2,
                Timestamp = DateTime.UtcNow.AddDays(-28),
                Details = "Inline preview generated"
            },
            new DocumentAuditLog
            {
                DocumentAuditLogId = 6,
                DocumentId = 4,
                ActionType = "Download",
                UserId = 4,
                Timestamp = DateTime.UtcNow.AddDays(-25),
                Details = "File downloaded"
            },
            new DocumentAuditLog
            {
                DocumentAuditLogId = 7,
                DocumentId = 5,
                ActionType = "Preview",
                UserId = 3,
                Timestamp = DateTime.UtcNow.AddDays(-50),
                Details = "Inline preview generated"
            }
        );
    }
}
