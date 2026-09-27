using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Infrastructure.Persistence.Configurations;

public class EmployeeEmergencyContactConfiguration : IEntityTypeConfiguration<EmployeeEmergencyContact>
{
    public void Configure(EntityTypeBuilder<EmployeeEmergencyContact> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(150);
        builder.Property(c => c.Relationship).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Phone).IsRequired().HasMaxLength(30);
        builder.Property(c => c.Email).HasMaxLength(200);
        builder.Property(c => c.Address).HasMaxLength(500);

        builder.HasOne(c => c.Employee)
            .WithMany(e => e.EmergencyContacts)
            .HasForeignKey(c => c.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.EmployeeId);
    }
}
