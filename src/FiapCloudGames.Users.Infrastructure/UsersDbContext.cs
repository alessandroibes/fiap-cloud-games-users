using FiapCloudGames.Users.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FiapCloudGames.Users.Infrastructure;

public sealed class UsersDbContext(DbContextOptions<UsersDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        var user = model.Entity<User>();
        user.ToTable("Users");
        user.HasKey(u => u.Id);
        user.Property(u => u.Id).ValueGeneratedNever();
        user.Property(u => u.Name).IsRequired();
        user.Property(u => u.Email).IsRequired();
        user.Property(u => u.PasswordHash).IsRequired();
        user.Property(u => u.Role).HasConversion<string>().IsRequired();
        user.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UX_Users_Email");
        model.AddInboxStateEntity();
        model.AddOutboxMessageEntity();
        model.AddOutboxStateEntity();
    }
}
