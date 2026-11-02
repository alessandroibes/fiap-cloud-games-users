using System.Text.RegularExpressions;

namespace FiapCloudGames.Users.Domain;

public enum UserRole { User = 1, Admin = 2 }

public sealed partial class User
{
    private User() { }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public UserRole Role { get; private set; }

    public static User Create(string name, string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nome obrigatório.");
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Hash obrigatório.");
        return new User { Id = Guid.NewGuid(), Name = name.Trim(), Email = NormalizeEmail(email), PasswordHash = passwordHash, Role = UserRole.User };
    }

    public static string NormalizeEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || !EmailFormat().IsMatch(normalized))
            throw new ArgumentException("E-mail inválido.");
        return normalized;
    }

    public void ChangeRole(UserRole role)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentException("Papel inválido.");
        Role = role;
    }

    [GeneratedRegex("^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailFormat();
}
