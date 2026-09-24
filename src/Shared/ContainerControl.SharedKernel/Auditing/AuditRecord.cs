namespace ContainerControl.SharedKernel.Auditing;

public sealed record AuditRecord(
    string Action,
    string SubjectType,
    string? SubjectId,
    Guid? ActorUserId);
