using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Infrastructure.Persistence.Configurations;

public class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> builder)
    {
        builder.ToTable("PayrollPeriods", t => t.HasCheckConstraint("CK_PayrollPeriods_DateRange", "[StartDate] < [EndDate]"));

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.StartDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(p => p.EndDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.Notes)
            .HasMaxLength(1000);

        builder.Property(p => p.RowVersion)
            .IsRowVersion();

        // The same dates can't be used twice, except by periods that were cancelled. Overlaps are checked by the service.
        builder.HasIndex(p => new { p.StartDate, p.EndDate })
            .IsUnique()
            .HasFilter("[Status] <> 'Cancelled'");

        builder.HasIndex(p => p.Status);

        foreach (var userKey in new[]
                 {
                     nameof(PayrollPeriod.CreatedByUserId), nameof(PayrollPeriod.CalculatedByUserId), nameof(PayrollPeriod.SubmittedByUserId),
                     nameof(PayrollPeriod.ApprovedByUserId), nameof(PayrollPeriod.FinalizedByUserId), nameof(PayrollPeriod.PaidByUserId), nameof(PayrollPeriod.CancelledByUserId),
                 })
        {
            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(userKey)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
