using Arena.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Arena.Infrastructure.Repositories;

/// <summary>
/// UPDATE TicketOffers SET SoldQuantity = SoldQuantity ± n, Version = @new WHERE Id = @id AND (stock condition).
/// The database checks the stock and changes it in the same statement, holding the row lock until commit,
/// so concurrent bookings queue on the row instead of deadlocking.
/// </summary>
public sealed class TicketInventory(ArenaDbContext db) : ITicketInventory
{
    public async Task<bool> TryReserveAsync(int offerId, int quantity, CancellationToken ct)
    {
        var version = Guid.NewGuid();
        var rows = await db.TicketOffers
            .Where(x => x.Id == offerId && x.Capacity - x.SoldQuantity >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.SoldQuantity, x => x.SoldQuantity + quantity)
                .SetProperty(x => x.Version, version), ct);
        if (rows == 1) await RefreshTrackedAsync(offerId, ct);
        return rows == 1;
    }

    public async Task ReleaseAsync(int offerId, int quantity, CancellationToken ct)
    {
        var version = Guid.NewGuid();
        var rows = await db.TicketOffers
            .Where(x => x.Id == offerId && x.SoldQuantity >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.SoldQuantity, x => x.SoldQuantity - quantity)
                .SetProperty(x => x.Version, version), ct);
        if (rows != 1)
            throw new InvalidOperationException($"Ticket offer {offerId} cannot release {quantity} tickets.");
        await RefreshTrackedAsync(offerId, ct);
    }

    // ExecuteUpdate bypasses the change tracker; reload a tracked copy so it shows the new stock and Version.
    private async Task RefreshTrackedAsync(int offerId, CancellationToken ct)
    {
        var entry = db.ChangeTracker.Entries<TicketOffer>().FirstOrDefault(x => x.Entity.Id == offerId);
        if (entry is not null) await entry.ReloadAsync(ct);
    }
}
