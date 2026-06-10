namespace Blogify.Web.Models;

public sealed class BlogInvitationEvent
{
    private BlogInvitationEvent() { }

    private BlogInvitationEvent(Guid invitationId, string eventType, string? actorUserId, string? details)
    {
        Id = Guid.NewGuid();
        InvitationId = invitationId;
        EventType = eventType;
        ActorUserId = actorUserId;
        Details = details;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private init; }
    public Guid InvitationId { get; private init; }
    public string EventType { get; private init; } = string.Empty;
    public string? ActorUserId { get; private init; }
    public string? Details { get; private init; }
    public DateTimeOffset CreatedAtUtc { get; private init; }

    public static BlogInvitationEvent Create(Guid invitationId, string eventType, string? actorUserId, string? details = null)
        => new(invitationId, eventType, actorUserId, details);
}
