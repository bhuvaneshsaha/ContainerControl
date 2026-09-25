using ContainerControl.Modules.Access.Application.Teams;
using ContainerControl.Modules.Applications.Workloads;
using ContainerControl.Modules.Runtime.Persistence;
using ContainerControl.SharedKernel.CurrentUser;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Runtime.Inspection;

public sealed record StoredLogEntry(string Service, DateTimeOffset At, string Text);

public sealed class StoredLogReader
{
    private readonly RuntimeDbContext _db;
    private readonly IWorkloadStore _apps;
    private readonly ITeamDirectory _teams;
    private readonly ICurrentUser _currentUser;

    public StoredLogReader(RuntimeDbContext db, IWorkloadStore apps, ITeamDirectory teams, ICurrentUser currentUser)
    {
        _db = db;
        _apps = apps;
        _teams = teams;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<StoredLogEntry>?> ReadAsync(Guid applicationId, int take, CancellationToken cancellationToken)
    {
        var app = await _apps.FindAsync(applicationId, cancellationToken);
        if (app is null || _currentUser.UserId is null || !await _teams.IsMemberAsync(_currentUser.UserId.Value, app.TeamId, cancellationToken))
        {
            return null;
        }

        var limit = Math.Clamp(take, 1, 1000);
        var rows = await _db.LogLines
            .AsNoTracking()
            .Where(line => line.ApplicationId == applicationId)
            .OrderByDescending(line => line.RecordedAtUtc)
            .ThenByDescending(line => line.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
        rows.Reverse();
        return rows.Select(line => new StoredLogEntry(line.Service, line.RecordedAtUtc, line.Text)).ToArray();
    }
}
