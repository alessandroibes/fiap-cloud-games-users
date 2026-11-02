using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FiapCloudGames.Users.Application;
using FiapCloudGames.Users.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FiapCloudGames.Users.Infrastructure;

public sealed class Passwords : IPasswords
{
    private readonly PasswordHasher<object> hasher = new();
    private readonly object context = new();
    public string Hash(string password) => hasher.HashPassword(context, password);
    public bool Verify(string hash, string password) => hasher.VerifyHashedPassword(context, hash, password) != PasswordVerificationResult.Failed;
}

public sealed class JwtOptions
{
    public string Key { get; init; } = "";
    public string Issuer { get; init; } = "fiap-cloud-games-users";
    public string Audience { get; init; } = "fiap-cloud-games";
    public int ExpirationMinutes { get; init; } = 60;
}

public sealed class Tokens(IOptions<JwtOptions> options, TimeProvider time) : ITokens
{
    public string Create(User user)
    {
        var settings = options.Value;
        var now = time.GetUtcNow();
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience,
            [new Claim("sub", user.Id.ToString()), new Claim("email", user.Email), new Claim("role", user.Role.ToString())],
            now.UtcDateTime, now.AddMinutes(settings.ExpirationMinutes).UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
