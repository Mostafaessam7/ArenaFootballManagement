using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

public sealed class ResultAuditConfiguration : IEntityTypeConfiguration<ResultAudit>
{
    public void Configure(EntityTypeBuilder<ResultAudit> e)
    {
        e.ToTable("ResultAudits");
        e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        e.HasOne<MatchEvent>().WithMany().HasForeignKey(x => x.MatchId);
        e.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorId);
    }
}
