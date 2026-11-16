namespace Arena.Domain.Entities;

public sealed class Booking : Entity
{
    public int UserId { get; set; }
    public int TicketOfferId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public DateTime? CancelledUtc { get; set; }
}
