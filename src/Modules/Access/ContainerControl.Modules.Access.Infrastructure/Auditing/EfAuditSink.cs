using ContainerControl.Modules.Access.Domain.Auditing;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.SharedKernel.Auditing;
using ContainerControl.SharedKernel.Correlation;
using ContainerControl.SharedKernel.Time;

namespace ContainerControl.Modules.Access.Infrastructure.Auditing;

public sealed class EfAuditSink : IAuditSink
{
    private readonly AccessDbContext _db;
    private readonly IClock _clock;
    private readonly ICorrelationContext _correlation;

    public EfAuditSink(AccessDbContext db, IClock clock, ICorrelationContext correlation)
    {
        _db = db;
        _clock = clock;
        _correlation = correlation;
    }

    public async Task WriteAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        _db.AuditEntries.Add(new AuditEntry
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = _clock.UtcNow,
            ActorUserId = record.ActorUserId,
            Action = record.Action,
            SubjectType = record.SubjectType,
            SubjectId = record.SubjectId,
            CorrelationId = _correlation.CorrelationId
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
