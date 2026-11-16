using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

public sealed class CommunityEventConfiguration : IEntityTypeConfiguration<CommunityEvent>
{
    public void Configure(EntityTypeBuilder<CommunityEvent> e) =>
        e.Property(x => x.Description).HasMaxLength(2000);
}
