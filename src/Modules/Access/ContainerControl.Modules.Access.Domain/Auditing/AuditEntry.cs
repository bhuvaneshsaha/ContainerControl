namespace ContainerControl.Modules.Access.Domain.Auditing;

public sealed class AuditEntry
{
    public Guid Id { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public Guid? ActorUserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string SubjectType { get; set; } = string.Empty;

    public string? SubjectId { get; set; }

    public string CorrelationId { get; set; } = string.Empty;
}
