using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> e)
    {
        e.ToTable("Tickets", t => t.HasCheckConstraint("CK_Ticket_CheckIn", "([CheckedInUtc] IS NULL AND [CheckedInById] IS NULL) OR ([CheckedInUtc] IS NOT NULL AND [CheckedInById] IS NOT NULL)"));
        e.Property(x => x.Code).HasMaxLength(64).IsRequired();
        e.HasIndex(x => x.Code).IsUnique();
        e.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId);
        e.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CheckedInById);
    }
}
