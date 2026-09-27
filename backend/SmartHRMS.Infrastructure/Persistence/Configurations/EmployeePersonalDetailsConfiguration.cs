using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Infrastructure.Persistence.Configurations;

public class EmployeePersonalDetailsConfiguration : IEntityTypeConfiguration<EmployeePersonalDetails>
{
    public void Configure(EntityTypeBuilder<EmployeePersonalDetails> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Gender).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.MaritalStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.BloodGroup).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Nationality).HasMaxLength(100);
        builder.Property(p => p.NationalId).HasMaxLength(17);
        builder.Property(p => p.PassportNo).HasMaxLength(20);

        // One personal-details row per employee (1 : 0..1).
        builder.HasOne(p => p.Employee)
            .WithOne(e => e.PersonalDetails)
            .HasForeignKey<EmployeePersonalDetails>(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.EmployeeId).IsUnique();

        // A national ID or passport number identifies one person, so it can't belong to two employees.
        builder.HasIndex(p => p.NationalId).IsUnique().HasFilter("[NationalId] IS NOT NULL");
        builder.HasIndex(p => p.PassportNo).IsUnique().HasFilter("[PassportNo] IS NOT NULL");
    }
}
