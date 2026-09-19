using Microsoft.EntityFrameworkCore;

namespace smartHRMS.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Define your DbSets for your entities here
        // Example:
        // public DbSet<Employee> Employees { get; set; }
    }
}