namespace Arena.Domain.Entities;

public sealed class Team : Entity
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string City { get; set; } = "";
    public int? ManagerId { get; set; }
    public bool IsActive { get; set; } = true;
}
