using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Domain.Common;
using smartHRMS.Domain.Entities;
using smartHRMS.Infrastructure.Persistence.Configurations;

namespace smartHRMS.Infrastructure.Persistence;

public class SmartHRMSDbContext : DbContext
{
    public SmartHRMSDbContext(DbContextOptions<SmartHRMSDbContext> options)
        : base(options)
    {
    }

    public DbSet<Department> Departments { get; set; }

    public DbSet<Designation> Designations { get; set; }

    public DbSet<Employee> Employees { get; set; }

    public DbSet<ApplicationUser> ApplicationUsers { get; set; }

    public DbSet<EmployeeDocument> EmployeeDocuments { get; set; }

    public DbSet<EmployeePersonalDetails> EmployeePersonalDetails { get; set; }

    public DbSet<EmployeeAddress> EmployeeAddresses { get; set; }

    public DbSet<EmployeeEmergencyContact> EmployeeEmergencyContacts { get; set; }

    public DbSet<EmployeeEducation> EmployeeEducations { get; set; }

    public DbSet<EmployeeExperience> EmployeeExperiences { get; set; }

    public DbSet<Attendance> Attendances { get; set; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Services check uniqueness before saving, but two simultaneous requests can both pass that check.
            // The unique index then rejects the second one; report it as a 409 instead of an unhandled 500.
            throw new ConflictException("A record with the same unique value already exists.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartHRMSDbContext).Assembly);

        // Audit timestamps are always written as UTC, but SQL Server's datetime2 doesn't store a Kind, so EF would
        // read them back as Unspecified and they'd serialize without a "Z" (clients would treat them as local time).
        // Deliberately limited to CreatedAt/UpdatedAt: date-only values like DateOfBirth must not be tagged as UTC.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            value => value,
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(entityType => typeof(BaseEntity).IsAssignableFrom(entityType.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).Property(nameof(BaseEntity.CreatedAt)).HasConversion(utcConverter);
            modelBuilder.Entity(entityType.ClrType).Property(nameof(BaseEntity.UpdatedAt)).HasConversion(utcConverter);
        }
    }
}
