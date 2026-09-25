using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Tests;

public static class TestDbContextFactory
{
    public static ApplicationDbContext CreateInMemoryContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        SeedBaseData(context);
        return context;
    }

    private static void SeedBaseData(ApplicationDbContext context)
    {
        // Users
        var employee = new User
        {
            UserId = 1,
            Email = "employee1@contoso.com",
            DisplayName = "Ni Kang",
            Department = "Engineering",
            Role = UserRole.Employee,
            LastLoginDate = DateTime.UtcNow
        };

        var teamLead = new User
        {
            UserId = 2,
            Email = "teamlead1@contoso.com",
            DisplayName = "Sarah Connor",
            Department = "Engineering",
            Role = UserRole.TeamLead,
            LastLoginDate = DateTime.UtcNow
        };

        var projectManager = new User
        {
            UserId = 3,
            Email = "pm1@contoso.com",
            DisplayName = "Floris Kregel",
            Department = "Management",
            Role = UserRole.ProjectManager,
            LastLoginDate = DateTime.UtcNow
        };

        var administrator = new User
        {
            UserId = 4,
            Email = "admin@contoso.com",
            DisplayName = "Adele Vance",
            Department = "IT",
            Role = UserRole.Administrator,
            LastLoginDate = DateTime.UtcNow
        };

        var outsideEmployee = new User
        {
            UserId = 5,
            Email = "sales1@contoso.com",
            DisplayName = "John Doe",
            Department = "Sales",
            Role = UserRole.Employee,
            LastLoginDate = DateTime.UtcNow
        };

        context.Users.AddRange(employee, teamLead, projectManager, administrator, outsideEmployee);

        // Project
        var project = new Project
        {
            ProjectId = 10,
            Name = "Project Alpha",
            Description = "Test Project Alpha",
            ProjectManagerId = projectManager.UserId,
            StartDate = DateTime.UtcNow.AddMonths(-1),
            Status = ProjectStatus.Active
        };
        context.Projects.Add(project);

        // Project Members
        context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = 10, UserId = employee.UserId, Role = "Contributor" },
            new ProjectMember { ProjectId = 10, UserId = projectManager.UserId, Role = "Manager" }
        );

        // Task
        var task = new TaskItem
        {
            TaskId = 100,
            Title = "Task Alpha 1",
            Description = "Initial milestone task",
            ProjectId = 10,
            AssignedUserId = employee.UserId,
            CreatedByUserId = projectManager.UserId,
            Status = ContosoDashboard.Models.TaskStatus.InProgress,
            Priority = TaskPriority.Medium,
            CreatedDate = DateTime.UtcNow.AddDays(-5)
        };
        context.Tasks.Add(task);

        context.SaveChanges();
    }
}
