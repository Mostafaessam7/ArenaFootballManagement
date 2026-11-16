using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> e)
    {
        e.ToTable("Bookings", t =>
        {
            t.HasCheckConstraint("CK_Booking_Quantity", "[Quantity] BETWEEN 1 AND 10 AND [UnitPrice] >= 0");
            t.HasCheckConstraint("CK_Booking_Status", "([Status] = 'Confirmed' AND [CancelledUtc] IS NULL) OR ([Status] = 'Cancelled' AND [CancelledUtc] IS NOT NULL)");
        });
        e.Property(x => x.IdempotencyKey).HasMaxLength(64).IsRequired();
        e.Property(x => x.UnitPrice).HasPrecision(12, 2);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId);
        e.HasOne<TicketOffer>().WithMany().HasForeignKey(x => x.TicketOfferId);
        // BR11: an idempotency key is unique per user.
        e.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
    }
}
