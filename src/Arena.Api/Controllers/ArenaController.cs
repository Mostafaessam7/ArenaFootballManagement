using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Arena.Api.Controllers;

public abstract class ArenaController : ControllerBase
{
    /// <summary>Roles allowed to manage sports data and ticket offers.</summary>
    protected const string Management = "Admin,Organizer";

    /// <summary>Id of the signed-in user, read from the JWT "sub" claim.</summary>
    protected int ActorId => int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
