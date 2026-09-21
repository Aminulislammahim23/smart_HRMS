using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using smartHRMS.Domain.Entities;

namespace smartHRMS.Infrastructure.Persistence.Configurations;

public class DesignationConfiguration : IEntityTypeConfiguration<Designation>
{
    public void Configure(EntityTypeBuilder<Designation> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.Description)
            .HasMaxLength(500);

        builder.HasIndex(d => d.Name)
            .IsUnique();

        builder.Property(d => d.IsActive)
            .HasDefaultValue(true);
    }
}
