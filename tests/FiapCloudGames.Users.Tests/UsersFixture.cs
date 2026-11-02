extern alias Consumer;
using System.Collections.Concurrent;
using System.Diagnostics;
using FiapCloudGames.Users.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using ReceivedEvent = Consumer::FiapCloudGames.Contracts.V1.UserCreatedEvent;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace FiapCloudGames.Users.Tests;

public sealed class UsersFactory(Dictionary<string, string?> config) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        foreach (var pair in config) builder.UseSetting(pair.Key, pair.Value);
    }
}

public sealed class UsersFixture : IAsyncLifetime
{
    public Dictionary<string, string> Env { get; }
    public Dictionary<string, string?> Config { get; }
    public string Root { get; }
    public UsersFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    private IBusControl receiver = null!;
    public ConcurrentQueue<ReceivedEvent> Events { get; } = new();
    public string AdminEmail => Env["AdminBootstrap__Email"];
    public string AdminPassword => Env["AdminBootstrap__Password"];

    public UsersFixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FiapCloudGames.Users.sln"))) directory = directory.Parent;
        Root = directory?.FullName ?? throw new InvalidOperationException("Raiz Users não encontrada.");
        var path = Path.Combine(Root, ".env");
        if (!File.Exists(path)) throw new InvalidOperationException("Prepare .env e os containers tests/compose.yaml.");
        Env = File.ReadAllLines(path).Where(l => l.Length > 0 && !l.StartsWith('#')).Select(l => l.Split('=', 2)).ToDictionary(p => p[0], p => p[1]);
        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = int.Parse(Env["TEST_DB_PORT"]),
            Database = "fcg_users_test",
            Username = "fcg_users_test",
            Password = Env["TEST_DB_PASSWORD"],
            IncludeErrorDetail = false
        }.ConnectionString;
        Config = new()
        {
            ["ConnectionStrings:Users"] = connection,
            ["Jwt:Key"] = Env["Jwt__Key"],
            ["Jwt:Issuer"] = "fiap-cloud-games-users",
            ["Jwt:Audience"] = "fiap-cloud-games",
            ["Jwt:ExpirationMinutes"] = "60",
            ["RabbitMq:Host"] = "127.0.0.1",
            ["RabbitMq:Port"] = Env["TEST_RABBIT_PORT"],
            ["RabbitMq:VirtualHost"] = "fcg_test",
            ["RabbitMq:Username"] = "users_test",
            ["RabbitMq:Password"] = Env["TEST_RABBIT_PASSWORD"]
        };
    }

    public UsersDbContext Db() => new(new DbContextOptionsBuilder<UsersDbContext>().UseNpgsql(Config["ConnectionStrings:Users"]).Options);

    public async Task InitializeAsync()
    {
        await using (var db = Db()) await db.Database.MigrateAsync();
        receiver = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host("127.0.0.1", ushort.Parse(Env["TEST_RABBIT_PORT"]), "fcg_test", h => { h.Username("users_test"); h.Password(Env["TEST_RABBIT_PASSWORD"]); });
            cfg.ReceiveEndpoint("users-g2-contract-observer", e =>
            {
                e.Handler<ReceivedEvent>(ctx => { Events.Enqueue(ctx.Message); return Task.CompletedTask; });
            });
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await receiver.StartAsync(timeout.Token);
        StartApi();
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<BootstrapAdmin>().Execute("Administrador teste", AdminEmail, AdminPassword);
    }

    private void StartApi() { Factory = new UsersFactory(Config); Client = Factory.CreateClient(); }
    public async Task RestartApi()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        StartApi();
    }
    public async Task RestartWithoutNotificationsQueue()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        // Fila exclusiva do broker de testes: simula primeiro deploy sem consumidor.
        await Docker("exec", "-T", "rabbitmq", "rabbitmqctl", "delete_queue", "-p", "fcg_test", "notifications-user-created-v1");
        StartApi();
    }
    public async Task WaitForEvent(Guid userId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (!Events.Any(e => e.UserId == userId)) await Task.Delay(100, timeout.Token);
    }
    public async Task VerifyLateNotificationsConsumer(Guid userId)
    {
        var received = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateConsumer = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host("127.0.0.1", ushort.Parse(Env["TEST_RABBIT_PORT"]), "fcg_test", h => { h.Username("users_test"); h.Password(Env["TEST_RABBIT_PASSWORD"]); });
            cfg.ReceiveEndpoint("notifications-user-created-v1", e =>
            {
                e.Handler<ReceivedEvent>(ctx =>
                {
                    if (ctx.Message.UserId == userId) received.TrySetResult(userId);
                    return Task.CompletedTask;
                });
            });
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await lateConsumer.StartAsync(timeout.Token);
            Assert.Equal(userId, await received.Task.WaitAsync(timeout.Token));
        }
        finally { await lateConsumer.StopAsync(); }
    }
    public async Task Docker(params string[] command)
    {
        var start = new ProcessStartInfo("docker") { WorkingDirectory = Root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in new[] { "compose", "--env-file", ".env", "-f", "tests/compose.yaml" }.Concat(command)) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(150));
        await process.WaitForExitAsync(timeout.Token);
        await Task.WhenAll(stdout, stderr);
        Assert.True(process.ExitCode == 0, "Comando Docker do ambiente de teste falhou; saída omitida para evitar dados sensíveis.");
    }
    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null) await Factory.DisposeAsync();
        if (receiver is not null) { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)); await receiver.StopAsync(timeout.Token); }
    }
}
