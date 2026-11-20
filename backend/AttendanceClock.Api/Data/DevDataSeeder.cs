using AttendanceClock.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AttendanceClock.Api.Data;

/// <summary>
/// Development only: creates demo accounts on an empty database, so reviewers can log in right away.
/// The passwords are documented in the README. They are local demo accounts, not real secrets.
/// </summary>
public static class DevDataSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher<Employee> hasher)
    {
        if (await db.Employees.AnyAsync())
            return;

        Employee Create(string userName, string displayName, EmployeeRole role, string password)
        {
            var employee = new Employee { UserName = userName, DisplayName = displayName, Role = role };
            employee.PasswordHash = hasher.HashPassword(employee, password);
            return employee;
        }

        db.Employees.AddRange(
            Create("manager", "Maya Manager", EmployeeRole.Manager, "Manager123!"),
            Create("dana", "Dana Cohen", EmployeeRole.Employee, "Employee123!"),
            Create("yossi", "Yossi Levi", EmployeeRole.Employee, "Employee123!"));
        await db.SaveChangesAsync();
    }
}
