namespace FiapCloudGames.Users.Domain;

public static class PasswordPolicy
{
    public static void Validate(string? password)
    {
        if (password is null || password.Length < 8 || !password.Any(char.IsLetter) ||
            !password.Any(char.IsDigit) || !password.Any(c => char.IsPunctuation(c) || char.IsSymbol(c)))
            throw new ArgumentException("Senha deve ter ao menos oito caracteres, letra, número e caractere especial.");
    }
}
