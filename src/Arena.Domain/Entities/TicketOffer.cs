namespace Arena.Domain.Entities;

public sealed class TicketOffer : Entity
{
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Capacity { get; set; }
    public int SoldQuantity { get; set; }
    public DateTime SalesStartUtc { get; set; }
    public DateTime SalesEndUtc { get; set; }
}
