using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

public sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> e)
    {
        e.ToTable("Venues", t => t.HasCheckConstraint("CK_Venue_Capacity", "[Capacity] > 0"));
        e.Property(x => x.Name).HasMaxLength(160).IsRequired();
        e.Property(x => x.City).HasMaxLength(80).IsRequired();
        e.Property(x => x.Address).HasMaxLength(300).IsRequired();
    }
}
