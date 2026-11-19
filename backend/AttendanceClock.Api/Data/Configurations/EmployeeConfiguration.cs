using AttendanceClock.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceClock.Api.Data.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> e)
    {
        e.Property(x => x.UserName).HasMaxLength(50);
        e.HasIndex(x => x.UserName).IsUnique();

        e.Property(x => x.DisplayName).HasMaxLength(100);
        e.Property(x => x.PasswordHash).HasMaxLength(200);

        // Stored as text ("Employee" / "Manager") so the database is readable.
        e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);

        // Record-keeping timestamp set by SQL Server. This is not an attendance time, so the
        // external-time rule doesn't apply here (PRD D-7).
        e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
