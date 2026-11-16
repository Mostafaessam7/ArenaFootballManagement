namespace Arena.Domain.Entities;

public sealed class Player : Entity
{
    public string FullName { get; set; } = "";
    public DateOnly BirthDate { get; set; }
    public string Nationality { get; set; } = "";
    public string Position { get; set; } = "";
}
