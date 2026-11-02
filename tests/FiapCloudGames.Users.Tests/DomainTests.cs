using FiapCloudGames.Users.Domain;
using Xunit;

namespace FiapCloudGames.Users.Tests;

public class DomainTests
{
    [Fact]
    public void Email_is_trimmed_and_lowercase() => Assert.Equal("alice@example.com", User.NormalizeEmail(" Alice@Example.COM "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a@b")]
    [InlineData("a b@example.com")]
    public void Invalid_email_is_rejected(string? email) => Assert.Throws<ArgumentException>(() => User.NormalizeEmail(email));

    [Theory]
    [InlineData(null)]
    [InlineData("Ab1!")]
    [InlineData("abcdefgh!")]
    [InlineData("12345678!")]
    [InlineData("Abcdefg1")]
    public void Invalid_password_is_rejected(string? password) => Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate(password));

    [Fact]
    public void Public_user_defaults_to_User_and_requires_name()
    {
        Assert.Throws<ArgumentException>(() => User.Create(" ", "a@example.com", "hash"));
        var user = User.Create("Alice", "a@example.com", "hash");
        Assert.Equal(UserRole.User, user.Role);
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Throws<ArgumentException>(() => user.ChangeRole((UserRole)99));
    }
}
