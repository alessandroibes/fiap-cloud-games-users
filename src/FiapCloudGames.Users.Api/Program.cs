using System.Text;
using FiapCloudGames.Users.Api;
using FiapCloudGames.Users.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args.Where(a => a is not "--migrate" and not "--bootstrap").ToArray());
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddUsersInfrastructure(builder.Configuration);
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32 || string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience) || jwt.ExpirationMinutes != 60)
    throw new InvalidOperationException("JWT exige chave externa de 32 bytes, issuer, audience e expiração de 60 minutos.");
builder.Services.Configure<JwtOptions>(jwtSection);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        NameClaimType = "sub",
        RoleClaimType = "role",
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };
    options.Events = new JwtBearerEvents
    {
        OnChallenge = context => { context.HandleResponse(); return ErrorMiddleware.Write(context.HttpContext, 401, "Autenticação necessária."); },
        OnForbidden = context => ErrorMiddleware.Write(context.HttpContext, 403, "Acesso negado.")
    };
});
builder.Services.AddAuthorization();
await using var app = builder.Build();
if (args.Contains("--migrate") || args.Contains("--bootstrap"))
{
    await using var scope = app.Services.CreateAsyncScope();
    if (args.Contains("--migrate"))
        await scope.ServiceProvider.GetRequiredService<UsersDbContext>().Database.MigrateAsync();
    if (args.Contains("--bootstrap"))
    {
        var config = app.Configuration;
        var created = await scope.ServiceProvider.GetRequiredService<BootstrapAdmin>().Execute(
            config["AdminBootstrap:Name"] ?? "", config["AdminBootstrap:Email"] ?? "", config["AdminBootstrap:Password"] ?? "");
        app.Logger.LogInformation("Bootstrap concluído. Administrador criado: {Created}", created);
    }
    return;
}
app.UseMiddleware<ErrorMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/ready", async (UsersDbContext db, CancellationToken ct) =>
{
    try
    {
        var ready = await db.Database.CanConnectAsync(ct) && !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
        return Results.StatusCode(ready ? 200 : 503);
    }
    catch { return Results.StatusCode(503); }
});
app.Run();
public partial class Program;
