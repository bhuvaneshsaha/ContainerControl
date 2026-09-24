using ContainerControl.Modules.Access.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Access.Infrastructure.Auditing;

public sealed record AuditRow(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    Guid? ActorUserId,
    string Action,
    string SubjectType,
    string? SubjectId);

public sealed class AuditQuery
{
    private readonly AccessDbContext _db;

    public AuditQuery(AccessDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AuditRow>> LatestAsync(CancellationToken cancellationToken)
    {
        return await _db.AuditEntries
            .AsNoTracking()
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => new AuditRow(
                entry.Id,
                entry.OccurredAtUtc,
                entry.ActorUserId,
                entry.Action,
                entry.SubjectType,
                entry.SubjectId))
            .ToListAsync(cancellationToken);
    }
}
