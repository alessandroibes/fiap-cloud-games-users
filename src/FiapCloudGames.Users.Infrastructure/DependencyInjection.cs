using FiapCloudGames.Users.Application;
using FiapCloudGames.Contracts.V1;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FiapCloudGames.Users.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connection = config.GetConnectionString("Users") ?? throw new InvalidOperationException("ConnectionStrings:Users obrigatório.");
        services.AddDbContext<UsersDbContext>(o => o.UseNpgsql(connection));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IUserStore, UserStore>();
        services.AddScoped<IPasswords, Passwords>();
        services.AddScoped<ITokens, Tokens>();
        services.AddScoped<UserService>();
        services.AddScoped<BootstrapAdmin>();
        services.AddMassTransit(bus =>
        {
            bus.AddEntityFrameworkOutbox<UsersDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
                outbox.QueryDelay = TimeSpan.FromSeconds(1);
            });
            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.DeployPublishTopology = true;
                // Retém cadastros mesmo antes do primeiro deploy do consumidor Notifications.
                cfg.Publish<UserCreatedEvent>(p => p.BindQueue("FiapCloudGames.Contracts.V1:UserCreatedEvent", "notifications-user-created-v1", binding =>
                {
                    binding.Durable = true;
                    binding.AutoDelete = false;
                }));
                cfg.Host(config["RabbitMq:Host"] ?? "localhost", config.GetValue<ushort>("RabbitMq:Port", 5672), config["RabbitMq:VirtualHost"] ?? "fcg", host =>
                {
                    host.Username(config["RabbitMq:Username"] ?? throw new InvalidOperationException("RabbitMq:Username obrigatório."));
                    host.Password(config["RabbitMq:Password"] ?? throw new InvalidOperationException("RabbitMq:Password obrigatório."));
                });
            });
        });
        // A API aceita cadastros com broker indisponível, pois a Outbox é durável.
        services.Configure<MassTransitHostOptions>(o => { o.WaitUntilStarted = false; o.StopTimeout = TimeSpan.FromSeconds(10); });
        return services;
    }
}
