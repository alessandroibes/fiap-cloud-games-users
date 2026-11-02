extern alias Consumer;
using System.Text.Json;
using System.Text.RegularExpressions;
using FiapCloudGames.Contracts.V1;
using MassTransit;
using Xunit;
using ReceivedEvent = Consumer::FiapCloudGames.Contracts.V1.UserCreatedEvent;

namespace FiapCloudGames.Users.Tests;

public class ContractTests
{
    [Fact]
    public void Production_contract_matches_canonical_example_and_independent_consumer()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "user-created-v1.md"));
        var json = Regex.Match(text, @"```json\s*(.*?)\s*```", RegexOptions.Singleline).Groups[1].Value;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var message = JsonSerializer.Deserialize<UserCreatedEvent>(json, options)!;
        var serialized = JsonSerializer.Serialize(message, options);
        var received = JsonSerializer.Deserialize<ReceivedEvent>(serialized, options)!;
        Assert.Equal(message.UserId, received.UserId);
        Assert.Equal(serialized, JsonSerializer.Serialize(received, options));
        Assert.NotEqual(typeof(UserCreatedEvent).Assembly, typeof(ReceivedEvent).Assembly);
        Assert.Equal("urn:message:FiapCloudGames.Contracts.V1:UserCreatedEvent", MessageUrn.ForTypeString<UserCreatedEvent>());
        Assert.Equal(MessageUrn.ForTypeString<UserCreatedEvent>(), MessageUrn.ForTypeString<ReceivedEvent>());
        Assert.Equal(TimeSpan.Zero, received.OccurredAt.Offset);
        using var document = JsonDocument.Parse(serialized);
        Assert.Equal(new[] { "email", "eventId", "name", "occurredAt", "userId" }, document.RootElement.EnumerateObject().Select(p => p.Name).Order());
        var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        foreach (var key in fields.Keys)
        {
            var incomplete = JsonSerializer.Serialize(fields.Where(f => f.Key != key).ToDictionary(f => f.Key, f => f.Value));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UserCreatedEvent>(incomplete, options));
        }
    }
}
