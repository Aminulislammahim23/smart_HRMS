using Microsoft.EntityFrameworkCore;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Infrastructure.Data;

public class SmartHRMSDbContext : DbContext
{
    public SmartHRMSDbContext(
        DbContextOptions<SmartHRMSDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees { get; set; }
}