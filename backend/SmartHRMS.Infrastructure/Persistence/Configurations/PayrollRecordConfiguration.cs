using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Infrastructure.Persistence.Configurations;

public class PayrollRecordConfiguration : IEntityTypeConfiguration<PayrollRecord>
{
    public void Configure(EntityTypeBuilder<PayrollRecord> builder)
    {
        builder.ToTable("PayrollRecords", t => t.HasCheckConstraint("CK_PayrollRecords_NonNegative",
            "[BasicSalary] >= 0 AND [HouseRent] >= 0 AND [MedicalAllowance] >= 0 AND [TransportAllowance] >= 0 AND [OtherAllowance] >= 0 "
            + "AND [OvertimeAmount] >= 0 AND [Bonus] >= 0 AND [Tax] >= 0 AND [LeaveDeduction] >= 0 AND [AdvanceDeduction] >= 0 "
            + "AND [LoanDeduction] >= 0 AND [OtherDeduction] >= 0"));

        builder.HasKey(r => r.Id);

        builder.Property(r => r.EmployeeCode).IsRequired().HasMaxLength(50);
        builder.Property(r => r.EmployeeName).IsRequired().HasMaxLength(201);
        builder.Property(r => r.DepartmentName).HasMaxLength(100);
        builder.Property(r => r.DesignationName).HasMaxLength(100);
        builder.Property(r => r.Remarks).HasMaxLength(500);

        foreach (var money in new[]
                 {
                     nameof(PayrollRecord.BasicSalary), nameof(PayrollRecord.HouseRent), nameof(PayrollRecord.MedicalAllowance),
                     nameof(PayrollRecord.TransportAllowance), nameof(PayrollRecord.OtherAllowance), nameof(PayrollRecord.OvertimeAmount),
                     nameof(PayrollRecord.Bonus), nameof(PayrollRecord.GrossSalary), nameof(PayrollRecord.Tax), nameof(PayrollRecord.LeaveDeduction),
                     nameof(PayrollRecord.AdvanceDeduction), nameof(PayrollRecord.LoanDeduction), nameof(PayrollRecord.OtherDeduction),
                     nameof(PayrollRecord.TotalDeduction), nameof(PayrollRecord.NetSalary),
                 })
        {
            builder.Property(money).HasPrecision(18, 2);
        }

        foreach (var days in new[]
                 {
                     nameof(PayrollRecord.WorkingDays), nameof(PayrollRecord.PresentDays), nameof(PayrollRecord.PaidLeaveDays),
                     nameof(PayrollRecord.UnpaidLeaveDays), nameof(PayrollRecord.AbsentDays),
                 })
        {
            builder.Property(days).HasPrecision(5, 1);
        }

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // One record per employee per payroll period; also catches two simultaneous calculations (mapped to 409).
        builder.HasIndex(r => new { r.PayrollPeriodId, r.EmployeeId })
            .IsUnique();

        builder.HasIndex(r => r.EmployeeId);

        builder.HasOne(r => r.PayrollPeriod)
            .WithMany(p => p.Records)
            .HasForeignKey(r => r.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Employee)
            .WithMany()
            .HasForeignKey(r => r.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
