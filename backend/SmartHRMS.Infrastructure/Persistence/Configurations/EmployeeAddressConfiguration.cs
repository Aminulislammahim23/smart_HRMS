using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Infrastructure.Persistence.Configurations;

public class EmployeeAddressConfiguration : IEntityTypeConfiguration<EmployeeAddress>
{
    public void Configure(EntityTypeBuilder<EmployeeAddress> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AddressType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Address).IsRequired().HasMaxLength(500);
        builder.Property(a => a.City).IsRequired().HasMaxLength(100);
        builder.Property(a => a.District).IsRequired().HasMaxLength(100);
        builder.Property(a => a.PostalCode).HasMaxLength(20);

        builder.HasOne(a => a.Employee)
            .WithMany(e => e.Addresses)
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // At most one Present and one Permanent address per employee.
        builder.HasIndex(a => new { a.EmployeeId, a.AddressType }).IsUnique();
    }
}
