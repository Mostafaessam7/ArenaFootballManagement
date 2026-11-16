namespace Arena.Application.Features.Teams;

public sealed record TeamDto(int Id, string Name, string Code, string City, bool IsActive, Guid Version);
