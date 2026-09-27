using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Infrastructure.Persistence.Configurations;

public class EmployeeEducationConfiguration : IEntityTypeConfiguration<EmployeeEducation>
{
    public void Configure(EntityTypeBuilder<EmployeeEducation> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Degree).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Institution).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Major).HasMaxLength(150);
        builder.Property(e => e.Result).HasMaxLength(50);
        builder.Property(e => e.PassingYear).IsRequired();

        builder.HasOne(e => e.Employee)
            .WithMany(employee => employee.Educations)
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.EmployeeId);
    }
}
