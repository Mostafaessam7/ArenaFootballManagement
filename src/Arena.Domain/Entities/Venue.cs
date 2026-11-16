namespace Arena.Domain.Entities;

public sealed class Venue : Entity
{
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public string Address { get; set; } = "";
    public int Capacity { get; set; }
}
