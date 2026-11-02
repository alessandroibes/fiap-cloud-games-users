using FiapCloudGames.Contracts.V1;
using FiapCloudGames.Users.Application;
using FiapCloudGames.Users.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FiapCloudGames.Users.Infrastructure;

public sealed class UserStore(UsersDbContext db, IPublishEndpoint publisher, TimeProvider time) : IUserStore
{
    public Task<User?> FindByEmail(string email, CancellationToken ct) => db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
    public Task<User?> FindById(Guid id, CancellationToken ct) => db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
    public async Task<IReadOnlyList<User>> List(CancellationToken ct) => await db.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync(ct);
    public async Task CreateWithEvent(User user, CancellationToken ct)
    {
        db.Users.Add(user);
        await publisher.Publish(new UserCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAt = time.GetUtcNow(),
            UserId = user.Id,
            Email = user.Email,
            Name = user.Name
        }, ct);
        // UseBusOutbox adiciona evento ao mesmo DbContext; um SaveChanges confirma ambos.
        await Save(ct);
    }

    public async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UX_Users_Email" })
        {
            throw new DuplicateEmailException();
        }
    }
}
