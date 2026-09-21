using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EmployeeCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(e => e.EmployeeCode)
            .IsUnique();

        builder.Property(e => e.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(e => e.Email)
            .IsUnique();

        builder.Property(e => e.Phone)
            .HasMaxLength(30);

        builder.Property(e => e.JoiningDate)
            .IsRequired();

        builder.Property(e => e.DepartmentId)
            .IsRequired();

        builder.Property(e => e.DesignationId)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasDefaultValue("Active");

        builder.HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Designation)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DesignationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ApplicationUser)
            .WithOne(u => u.Employee)
            .HasForeignKey<ApplicationUser>(u => u.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
