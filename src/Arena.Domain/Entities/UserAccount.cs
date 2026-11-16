namespace Arena.Domain.Entities;

public sealed class UserAccount : Entity
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Spectator;
    public bool IsActive { get; set; } = true;
}
