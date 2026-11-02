using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FiapCloudGames.Users.Application;
using FiapCloudGames.Users.Infrastructure;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FiapCloudGames.Users.Tests;

[Trait("Category", "Integration")]
public sealed class IntegrationTests(UsersFixture fixture) : IClassFixture<UsersFixture>
{
    private const string Password = "Test123!secure";
    private static string Email() => $"g2-{Guid.NewGuid():N}@example.com";
    private async Task<UserView> Register(string? email = null)
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/users", new { name = "Alice", email = email ?? Email(), password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UserView>())!;
    }
    private async Task<string> Login(string email, string password)
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }
    private async Task<HttpResponseMessage> Admin(string? token, HttpMethod? method = null, string path = "/api/admin/users", object? body = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    [Fact]
    public async Task Register_normalizes_hashes_and_delivers_canonical_event()
    {
        var email = Email();
        var user = await Register(" " + email.ToUpperInvariant() + " ");
        Assert.Equal(email, user.Email);
        Assert.Equal("User", user.Role);
        await using var db = fixture.Db();
        var saved = await db.Users.SingleAsync(u => u.Id == user.Id);
        Assert.NotEqual(Password, saved.PasswordHash);
        Assert.True(new Passwords().Verify(saved.PasswordHash, Password));
        await fixture.WaitForEvent(user.Id);
        var message = fixture.Events.First(e => e.UserId == user.Id);
        Assert.Equal(user.Email, message.Email);
        Assert.Equal(user.Name, message.Name);
        Assert.NotEqual(Guid.Empty, message.EventId);
        Assert.Equal(TimeSpan.Zero, message.OccurredAt.Offset);
        var token = await Login(" " + email.ToUpperInvariant() + " ", Password);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal(email, jwt.Claims.Single(c => c.Type == "email").Value);
        Assert.Equal("User", jwt.Claims.Single(c => c.Type == "role").Value);
        Assert.Equal(TimeSpan.FromMinutes(60), jwt.ValidTo - jwt.ValidFrom);
    }

    [Theory]
    [InlineData("", "valid@example.com", "Abcd123!")]
    [InlineData("Alice", "invalid", "Abcd123!")]
    [InlineData("Alice", "valid@example.com", "short")]
    public async Task Invalid_registration_has_no_user_or_event(string name, string email, string password)
    {
        await using var db = fixture.Db();
        var before = await db.Users.CountAsync();
        var response = await fixture.Client.PostAsJsonAsync("/api/users", new { name, email, password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(before, await db.Users.CountAsync());
        Assert.False(await db.Set<OutboxMessage>().AnyAsync(m => m.Body.Contains(email)));
    }

    [Fact]
    public async Task Concurrent_case_variants_create_only_one_user()
    {
        var email = Email();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => fixture.Client.PostAsJsonAsync("/api/users", new
        {
            name = "Concorrência",
            email = i % 2 == 0 ? email : " " + email.ToUpperInvariant() + " ",
            password = Password
        })));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var db = fixture.Db();
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == email));
    }

    [Fact]
    public async Task Invalid_login_and_authorization_do_not_expose_hash()
    {
        var user = await Register();
        var wrong = await fixture.Client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Admin(null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Admin(await Login(user.Email, Password))).StatusCode);
        var admin = await Login(fixture.AdminEmail, fixture.AdminPassword);
        var list = await Admin(admin);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.DoesNotContain("password", await list.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await Admin(admin, path: $"/api/admin/users/{user.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Admin(admin, path: $"/api/admin/users/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin(admin, HttpMethod.Put, $"/api/admin/users/{user.Id}/role", new { role = "2" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Admin(admin, HttpMethod.Put, $"/api/admin/users/{user.Id}/role", new { role = "Admin" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Admin(await Login(user.Email, Password))).StatusCode);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    public async Task Invalid_jwt_is_rejected(string invalid)
    {
        var now = DateTime.UtcNow;
        var key = invalid == "signature" ? new string('x', 64) : fixture.Env["Jwt__Key"];
        var token = new JwtSecurityToken(invalid == "issuer" ? "wrong" : "fiap-cloud-games-users",
            invalid == "audience" ? "wrong" : "fiap-cloud-games",
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("role", "Admin")],
            now.AddHours(-2), invalid == "expired" ? now.AddMinutes(-1) : now.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Admin(new JwtSecurityTokenHandler().WriteToken(token))).StatusCode);
    }

    [Fact]
    public async Task Bootstrap_is_idempotent()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<BootstrapAdmin>();
        await using var db = fixture.Db();
        var count = await db.Users.CountAsync();
        Assert.False(await bootstrap.Execute("Admin", fixture.AdminEmail, fixture.AdminPassword));
        Assert.Equal(count, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Rollback_removes_both_user_and_outbox_event()
    {
        var email = Email();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await scope.ServiceProvider.GetRequiredService<UserService>().Register("Rollback", email, Password, default);
            Assert.True(await db.Users.AnyAsync(u => u.Email == email));
            Assert.True(await db.Set<OutboxMessage>().AnyAsync(m => m.Body.Contains(email)));
            await transaction.RollbackAsync();
        }
        await using var verify = fixture.Db();
        Assert.False(await verify.Users.AnyAsync(u => u.Email == email));
        Assert.False(await verify.Set<OutboxMessage>().AnyAsync(m => m.Body.Contains(email)));
        Assert.DoesNotContain(fixture.Events, e => e.Email == email);
    }

    [Fact]
    public async Task Broker_outage_and_api_restart_preserve_and_deliver_committed_event()
    {
        await fixture.Docker("stop", "rabbitmq");
        Guid id;
        try
        {
            var user = await Register();
            id = user.Id;
            await using (var db = fixture.Db())
            {
                Assert.True(await db.Users.AnyAsync(u => u.Id == id));
                Assert.True(await db.Set<OutboxMessage>().AnyAsync(m => m.Body.Contains(user.Email)));
            }
            await fixture.RestartApi();
            Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/health/ready")).StatusCode);
        }
        finally { await fixture.Docker("up", "-d", "--wait", "--wait-timeout", "120", "rabbitmq"); }
        await fixture.WaitForEvent(id);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var verify = fixture.Db();
        while (await verify.Set<OutboxMessage>().AnyAsync(m => m.Body.Contains(id.ToString()), timeout.Token))
            await Task.Delay(100, timeout.Token);
    }

    [Fact]
    public async Task Health_is_live_and_database_ready()
    {
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task First_notifications_consumer_receives_previous_registration()
    {
        var warmup = await Register();
        await fixture.WaitForEvent(warmup.Id);
        await fixture.RestartWithoutNotificationsQueue();
        var user = await Register();
        await fixture.WaitForEvent(user.Id);
        await fixture.VerifyLateNotificationsConsumer(user.Id);
    }

    [Fact]
    public async Task Startup_does_not_apply_migrations()
    {
        var schema = "g2_empty_" + Guid.NewGuid().ToString("N");
        await using var db = fixture.Db();
        // Identificador gerado internamente e escapado; parâmetros SQL não representam identificadores.
        var quotedSchema = new Npgsql.NpgsqlCommandBuilder().QuoteIdentifier(schema);
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA " + quotedSchema);
        try
        {
            var connection = new Npgsql.NpgsqlConnectionStringBuilder(fixture.Config["ConnectionStrings:Users"]) { SearchPath = schema };
            var config = new Dictionary<string, string?>(fixture.Config) { ["ConnectionStrings:Users"] = connection.ConnectionString };
            await using var factory = new UsersFactory(config);
            using var client = factory.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
            await using var connectionCheck = new Npgsql.NpgsqlConnection(connection.ConnectionString);
            await connectionCheck.OpenAsync();
            await using var check = new Npgsql.NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema = @schema", connectionCheck);
            check.Parameters.AddWithValue("schema", schema);
            Assert.Equal(0L, await check.ExecuteScalarAsync());
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP SCHEMA " + quotedSchema); }
    }
}
