namespace FiapCloudGames.Contracts.V1;

public sealed record UserCreatedEvent
{
    public required Guid EventId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
    public required string Name { get; init; }
}
