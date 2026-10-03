using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Infrastructure.Persistence.Configurations;

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("LeaveRequests");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.LeaveType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(l => l.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(l => l.StartDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(l => l.EndDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(l => l.Reason)
            .HasMaxLength(500);

        builder.Property(l => l.ReviewComment)
            .HasMaxLength(500);

        builder.ToTable(t => t.HasCheckConstraint("CK_LeaveRequests_DateRange", "[StartDate] <= [EndDate]"));

        builder.HasIndex(l => new { l.EmployeeId, l.StartDate });
        builder.HasIndex(l => l.Status);

        builder.HasOne(l => l.Employee)
            .WithMany(e => e.LeaveRequests)
            .HasForeignKey(l => l.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(l => l.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(l => l.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
