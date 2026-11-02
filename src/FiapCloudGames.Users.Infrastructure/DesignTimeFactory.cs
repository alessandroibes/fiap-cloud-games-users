using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FiapCloudGames.Users.Infrastructure;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<UsersDbContext>
{
    public UsersDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Users")
            ?? throw new InvalidOperationException("Configure ConnectionStrings__Users para os comandos EF.");
        return new UsersDbContext(new DbContextOptionsBuilder<UsersDbContext>().UseNpgsql(connection).Options);
    }
}
