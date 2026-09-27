using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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
