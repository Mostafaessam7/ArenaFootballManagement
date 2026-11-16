namespace Arena.Domain.Entities;

public sealed class Ticket : Entity
{
    public int BookingId { get; set; }
    public string Code { get; set; } = "";
    public DateTime? CheckedInUtc { get; set; }
    public int? CheckedInById { get; set; }
}
