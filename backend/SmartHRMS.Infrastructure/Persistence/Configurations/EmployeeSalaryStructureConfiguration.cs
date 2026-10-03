using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Infrastructure.Persistence.Configurations;

public class EmployeeSalaryStructureConfiguration : IEntityTypeConfiguration<EmployeeSalaryStructure>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryStructure> builder)
    {
        builder.ToTable("EmployeeSalaryStructures", t =>
        {
            t.HasCheckConstraint("CK_EmployeeSalaryStructures_NonNegative",
                "[HouseRent] >= 0 AND [MedicalAllowance] >= 0 AND [TransportAllowance] >= 0 AND [OtherAllowance] >= 0 AND [MonthlyTax] >= 0");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.HouseRent).HasPrecision(18, 2);
        builder.Property(s => s.MedicalAllowance).HasPrecision(18, 2);
        builder.Property(s => s.TransportAllowance).HasPrecision(18, 2);
        builder.Property(s => s.OtherAllowance).HasPrecision(18, 2);
        builder.Property(s => s.MonthlyTax).HasPrecision(18, 2);

        // One salary structure per employee.
        builder.HasIndex(s => s.EmployeeId)
            .IsUnique();

        builder.HasOne(s => s.Employee)
            .WithOne(e => e.SalaryStructure)
            .HasForeignKey<EmployeeSalaryStructure>(s => s.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
