using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Infrastructure.Persistence.Configurations;

public class PaymentBatchConfiguration : IEntityTypeConfiguration<PaymentBatch>
{
    public void Configure(EntityTypeBuilder<PaymentBatch> builder)
    {
        builder.ToTable("PaymentBatches", t => t.HasCheckConstraint("CK_PaymentBatches_Totals", "[TotalEmployees] >= 0 AND [TotalAmount] >= 0"));

        builder.HasKey(b => b.Id);

        builder.Property(b => b.BatchNumber).IsRequired().HasMaxLength(30);
        builder.HasIndex(b => b.BatchNumber).IsUnique();

        builder.Property(b => b.PaymentDate).IsRequired().HasColumnType("date");
        builder.Property(b => b.PaymentMethod).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.TotalAmount).HasPrecision(18, 2);
        builder.Property(b => b.Notes).HasMaxLength(500);
        builder.Property(b => b.RowVersion).IsRowVersion();

        // At most one open batch per payroll: even two simultaneous requests can't both create one.
        builder.HasIndex(b => b.PayrollPeriodId)
            .IsUnique()
            .HasFilter("[Status] <> 'Paid' AND [Status] <> 'Cancelled'") // filtered indexes don't accept NOT IN
            .HasDatabaseName("IX_PaymentBatches_PayrollPeriodId_Open");

        builder.HasIndex(b => b.Status);

        builder.HasOne(b => b.PayrollPeriod)
            .WithMany()
            .HasForeignKey(b => b.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(b => b.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentBatchItemConfiguration : IEntityTypeConfiguration<PaymentBatchItem>
{
    public void Configure(EntityTypeBuilder<PaymentBatchItem> builder)
    {
        builder.ToTable("PaymentBatchItems", t => t.HasCheckConstraint("CK_PaymentBatchItems_Amount", "[Amount] > 0"));

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Amount).HasPrecision(18, 2);
        builder.Property(i => i.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.PaymentReference).HasMaxLength(100);
        builder.Property(i => i.FailureReason).HasMaxLength(500);

        // One item per employee in a batch.
        builder.HasIndex(i => new { i.PaymentBatchId, i.EmployeeId }).IsUnique();

        // Duplicate-payment protection: a payroll record (payroll + employee) can be in only one active payment.
        builder.HasIndex(i => i.PayrollRecordId)
            .IsUnique()
            .HasFilter("[Status] <> 'Cancelled'")
            .HasDatabaseName("IX_PaymentBatchItems_PayrollRecordId_Active");

        builder.HasIndex(i => i.EmployeeId);

        builder.HasOne(i => i.PaymentBatch)
            .WithMany(b => b.Items)
            .HasForeignKey(i => i.PaymentBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.PayrollRecord)
            .WithMany()
            .HasForeignKey(i => i.PayrollRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(i => i.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransactions", t =>
        {
            t.HasCheckConstraint("CK_PaymentTransactions_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_PaymentTransactions_PaidHasDate", "[Status] <> 'Paid' OR ([PaymentDate] IS NOT NULL AND [ProcessedAt] IS NOT NULL)");
        });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.PaymentMethod).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.TransactionReference).HasMaxLength(100);
        builder.Property(t => t.FailureReason).HasMaxLength(500);
        builder.Property(t => t.PaymentDate).HasColumnType("date");
        builder.Property(t => t.RowVersion).IsRowVersion();

        // One transaction per batch item.
        builder.HasIndex(t => t.PaymentBatchItemId).IsUnique();
        builder.HasIndex(t => new { t.EmployeeId, t.Status });
        builder.HasIndex(t => t.PaymentBatchId);

        builder.HasOne(t => t.PaymentBatchItem)
            .WithOne(i => i.Transaction)
            .HasForeignKey<PaymentTransaction>(t => t.PaymentBatchItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.PaymentBatch)
            .WithMany()
            .HasForeignKey(t => t.PaymentBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(t => t.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentStatusHistoryConfiguration : IEntityTypeConfiguration<PaymentStatusHistory>
{
    public void Configure(EntityTypeBuilder<PaymentStatusHistory> builder)
    {
        builder.ToTable("PaymentStatusHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.PreviousStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.NewStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Reason).HasMaxLength(500);

        builder.HasIndex(h => new { h.PaymentTransactionId, h.CreatedAt });

        builder.HasOne<PaymentTransaction>()
            .WithMany(t => t.History)
            .HasForeignKey(h => h.PaymentTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
