using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

public sealed class TicketOfferConfiguration : IEntityTypeConfiguration<TicketOffer>
{
    public void Configure(EntityTypeBuilder<TicketOffer> e)
    {
        e.ToTable("TicketOffers", t =>
        {
            // BR10: sold quantity stays between zero and the offer capacity.
            t.HasCheckConstraint("CK_Offer_Inventory", "[Capacity] > 0 AND [SoldQuantity] >= 0 AND [SoldQuantity] <= [Capacity]");
            t.HasCheckConstraint("CK_Offer_Price", "[Price] >= 0");
            t.HasCheckConstraint("CK_Offer_Dates", "[SalesEndUtc] > [SalesStartUtc]");
        });
        e.Property(x => x.Name).HasMaxLength(80).IsRequired();
        e.Property(x => x.Price).HasPrecision(12, 2);
        e.HasOne<SportEvent>().WithMany().HasForeignKey(x => x.EventId);
        e.HasIndex(x => new { x.EventId, x.Name }).IsUnique();
    }
}
