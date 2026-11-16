namespace Arena.Application.Interfaces;

/// <summary>
/// Changes a ticket offer's sold quantity with one conditional UPDATE, so concurrent bookings
/// never read-then-write the same row and can never sell past capacity.
/// Runs inside the caller's transaction: a rollback also undoes the reservation.
/// </summary>
public interface ITicketInventory
{
    /// <summary>Adds <paramref name="quantity"/> to the sold count if enough remain; false when sold out.</summary>
    Task<bool> TryReserveAsync(int offerId, int quantity, CancellationToken ct);

    /// <summary>Returns <paramref name="quantity"/> tickets to the offer.</summary>
    Task ReleaseAsync(int offerId, int quantity, CancellationToken ct);
}
