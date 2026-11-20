using AttendanceClock.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceClock.Api.Data.Configurations;

public class ShiftAuditConfiguration : IEntityTypeConfiguration<ShiftAudit>
{
    public void Configure(EntityTypeBuilder<ShiftAudit> a)
    {
        a.Property(x => x.Field).HasMaxLength(20).IsUnicode(false);
        a.Property(x => x.OldValue).HasMaxLength(40).IsUnicode(false);
        a.Property(x => x.NewValue).HasMaxLength(40).IsUnicode(false);
        a.Property(x => x.Reason).HasMaxLength(500);

        // The audit trail must never disappear with its shift or its manager.
        a.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        a.HasOne(x => x.ChangedBy).WithMany().HasForeignKey(x => x.ChangedByEmployeeId).OnDelete(DeleteBehavior.Restrict);

        a.HasIndex(x => new { x.ShiftId, x.ChangedAt });
    }
}
