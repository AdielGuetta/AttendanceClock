namespace AttendanceClock.Api.Domain;

/// <summary>A person who can log in and punch the clock.</summary>
public class Employee
{
    public int Id { get; set; }

    /// <summary>Login name. Unique.</summary>
    public required string UserName { get; set; }

    /// <summary>Name shown in the UI, e.g. "Dana Cohen".</summary>
    public required string DisplayName { get; set; }

    /// <summary>Hashed password (never the password itself). Filled in by the login step.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public EmployeeRole Role { get; set; } = EmployeeRole.Employee;

    /// <summary>Deactivated employees can't log in, but their shifts are kept.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>When the employee record was created (set by the database). Not an attendance time.</summary>
    public DateTime CreatedAtUtc { get; set; }

    public List<Shift> Shifts { get; set; } = [];
}

public enum EmployeeRole
{
    Employee,
    Manager,
}
