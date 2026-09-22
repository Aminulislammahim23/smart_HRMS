using Microsoft.EntityFrameworkCore;
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
    }
}
