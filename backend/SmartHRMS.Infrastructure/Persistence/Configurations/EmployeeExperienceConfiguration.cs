using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Infrastructure.Persistence.Configurations;

public class EmployeeExperienceConfiguration : IEntityTypeConfiguration<EmployeeExperience>
{
    public void Configure(EntityTypeBuilder<EmployeeExperience> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CompanyName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Designation).IsRequired().HasMaxLength(150);
        builder.Property(x => x.StartDate).IsRequired();
        builder.Property(x => x.Responsibilities).HasMaxLength(2000);

        builder.HasOne(x => x.Employee)
            .WithMany(e => e.Experiences)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.EmployeeId);
    }
}
