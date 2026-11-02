using FiapCloudGames.Users.Application;
using FiapCloudGames.Users.Domain;
using Microsoft.EntityFrameworkCore;

namespace FiapCloudGames.Users.Infrastructure;

public sealed class BootstrapAdmin(UsersDbContext db, IUserStore store, IPasswords passwords)
{
    public async Task<bool> Execute(string name, string email, string password, CancellationToken ct = default)
    {
        PasswordPolicy.Validate(password);
        var normalized = User.NormalizeEmail(email);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serializa somente bootstrap concorrente, sem impedir cadastros normais.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7202602)", ct);
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin, ct))
        {
            await transaction.CommitAsync(ct);
            return false;
        }
        if (await db.Users.AnyAsync(u => u.Email == normalized, ct)) throw new DuplicateEmailException();
        var admin = User.Create(name, normalized, passwords.Hash(password));
        admin.ChangeRole(UserRole.Admin);
        await store.CreateWithEvent(admin, ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
