namespace Arena.Domain.Entities;

public sealed class Tournament : Entity
{
    public string Name { get; set; } = "";
    public string Season { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int OrganizerId { get; set; }
    public TournamentStatus Status { get; set; } = TournamentStatus.RegistrationOpen;
}
