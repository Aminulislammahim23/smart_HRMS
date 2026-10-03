using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Infrastructure.Persistence.Configurations;

public class PayslipConfiguration : IEntityTypeConfiguration<Payslip>
{
    public void Configure(EntityTypeBuilder<Payslip> builder)
    {
        builder.ToTable("Payslips", t => t.HasCheckConstraint("CK_Payslips_PaidHasDate",
            "([PaymentStatus] = 'Paid' AND [PaymentDate] IS NOT NULL) OR ([PaymentStatus] = 'Unpaid' AND [PaymentDate] IS NULL)"));

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PayslipNumber)
            .IsRequired()
            .HasMaxLength(80);

        builder.HasIndex(p => p.PayslipNumber)
            .IsUnique();

        builder.Property(p => p.PaymentStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(PaymentStatus.Unpaid)
            .HasSentinel(PaymentStatus.Unpaid);

        builder.Property(p => p.PaymentDate)
            .HasColumnType("date");

        // One payslip per payroll record (and so per employee and period).
        builder.HasIndex(p => p.PayrollRecordId)
            .IsUnique();

        builder.HasIndex(p => new { p.EmployeeId, p.PayrollPeriodId });
        builder.HasIndex(p => p.PaymentStatus);

        builder.HasOne(p => p.PayrollRecord)
            .WithOne(r => r.Payslip)
            .HasForeignKey<Payslip>(p => p.PayrollRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PayrollPeriod>()
            .WithMany()
            .HasForeignKey(p => p.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.GeneratedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.PaidByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
