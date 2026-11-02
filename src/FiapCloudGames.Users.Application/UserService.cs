using FiapCloudGames.Users.Domain;

namespace FiapCloudGames.Users.Application;

public sealed class DuplicateEmailException : Exception;
public sealed class InvalidCredentialsException : Exception;
public sealed class UserNotFoundException : Exception;

public interface IUserStore
{
    Task<User?> FindByEmail(string email, CancellationToken ct);
    Task<User?> FindById(Guid id, CancellationToken ct);
    Task<IReadOnlyList<User>> List(CancellationToken ct);
    Task CreateWithEvent(User user, CancellationToken ct);
    Task Save(CancellationToken ct);
}

public interface IPasswords
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public interface ITokens { string Create(User user); }
public sealed record UserView(Guid Id, string Name, string Email, string Role)
{
    public static UserView From(User user) => new(user.Id, user.Name, user.Email, user.Role.ToString());
}

public sealed class UserService(IUserStore store, IPasswords passwords, ITokens tokens)
{
    public async Task<UserView> Register(string name, string email, string password, CancellationToken ct)
    {
        var normalized = User.NormalizeEmail(email);
        PasswordPolicy.Validate(password);
        if (await store.FindByEmail(normalized, ct) is not null) throw new DuplicateEmailException();
        var user = User.Create(name, normalized, passwords.Hash(password));
        await store.CreateWithEvent(user, ct);
        return UserView.From(user);
    }

    public async Task<string> Login(string email, string password, CancellationToken ct)
    {
        var user = await store.FindByEmail(User.NormalizeEmail(email), ct);
        if (user is null || string.IsNullOrEmpty(password) || !passwords.Verify(user.PasswordHash, password))
            throw new InvalidCredentialsException();
        return tokens.Create(user);
    }

    public async Task<IReadOnlyList<UserView>> List(CancellationToken ct) => (await store.List(ct)).Select(UserView.From).ToList();
    public async Task<UserView> Get(Guid id, CancellationToken ct) => UserView.From(await store.FindById(id, ct) ?? throw new UserNotFoundException());
    public async Task ChangeRole(Guid id, string role, CancellationToken ct)
    {
        if (!Enum.GetNames<UserRole>().Contains(role, StringComparer.OrdinalIgnoreCase) || !Enum.TryParse<UserRole>(role, true, out var parsed))
            throw new ArgumentException("Papel inválido.");
        var user = await store.FindById(id, ct) ?? throw new UserNotFoundException();
        user.ChangeRole(parsed);
        await store.Save(ct);
    }
}
