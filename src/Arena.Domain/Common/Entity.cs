namespace Arena.Domain.Common;

/// <summary>Base for every table: surrogate key, creation time and optimistic concurrency token.</summary>
public abstract class Entity
{
    public int Id { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
}
