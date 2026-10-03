using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.HasKey(u => u.Id);

        // One account per employee; accounts without an employee (an administrator) are allowed, so the unique index
        // ignores NULLs (SQL Server would otherwise allow only one NULL).
        builder.HasIndex(u => u.EmployeeId)
            .IsUnique()
            .HasFilter("[EmployeeId] IS NOT NULL");

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(100);

        // Case-insensitive through the database's default collation.
        builder.HasIndex(u => u.Username)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(UserRole.Employee)
            .HasSentinel(UserRole.Employee);

        builder.Property(u => u.SecurityStamp)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);
    }
}
