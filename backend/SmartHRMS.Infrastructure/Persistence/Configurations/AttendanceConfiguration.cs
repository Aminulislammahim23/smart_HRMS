using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Infrastructure.Persistence.Configurations;

public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.ToTable("Attendances");

        builder.HasKey(a => a.Id);

        // Calendar date and wall-clock times in the office time zone (whole seconds).
        builder.Property(a => a.AttendanceDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(a => a.CheckInTime)
            .HasColumnType("time(0)");

        builder.Property(a => a.CheckOutTime)
            .HasColumnType("time(0)");

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(a => a.Remarks)
            .HasMaxLength(500);

        // One attendance per employee per day; also catches two simultaneous check-ins (mapped to 409).
        builder.HasIndex(a => new { a.EmployeeId, a.AttendanceDate })
            .IsUnique();

        builder.HasIndex(a => a.AttendanceDate);

        // Restrict, like every other FK here: attendance is HR history and never disappears with an employee delete.
        builder.HasOne(a => a.Employee)
            .WithMany(e => e.Attendances)
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
