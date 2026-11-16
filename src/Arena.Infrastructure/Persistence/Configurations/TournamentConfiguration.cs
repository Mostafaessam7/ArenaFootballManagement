using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

public sealed class TournamentConfiguration : IEntityTypeConfiguration<Tournament>
{
    public void Configure(EntityTypeBuilder<Tournament> e)
    {
        e.ToTable("Tournaments", t =>
        {
            t.HasCheckConstraint("CK_Tournament_Dates", "[EndDate] >= [StartDate]");
            t.HasCheckConstraint("CK_Tournament_Status", "[Status] IN ('Draft','RegistrationOpen','InProgress','Completed')");
        });
        e.Property(x => x.Name).HasMaxLength(160).IsRequired();
        e.Property(x => x.Season).HasMaxLength(30).IsRequired();
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        e.HasIndex(x => new { x.Name, x.Season }).IsUnique();
        e.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OrganizerId);
    }
}
