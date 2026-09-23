using ContainerControl.Modules.Access.Application.Teams;
using ContainerControl.Modules.Access.Domain.Teams;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Access.Infrastructure.Teams;

public sealed class TeamDirectory : ITeamDirectory
{
    private readonly AccessDbContext _db;
    private readonly IClock _clock;

    public TeamDirectory(AccessDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task<bool> IsMemberAsync(Guid userId, Guid teamId, CancellationToken cancellationToken) =>
        _db.TeamMemberships.AnyAsync(membership => membership.UserId == userId && membership.TeamId == teamId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> TeamIdsForAsync(Guid userId, CancellationToken cancellationToken) =>
        await _db.TeamMemberships
            .Where(membership => membership.UserId == userId)
            .Select(membership => membership.TeamId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Team>> ListForUserAsync(Guid userId, bool allTeams, CancellationToken cancellationToken)
    {
        if (allTeams)
        {
            return await _db.Teams.AsNoTracking().OrderBy(team => team.Name).ToListAsync(cancellationToken);
        }

        var ids = await TeamIdsForAsync(userId, cancellationToken);
        return await _db.Teams.AsNoTracking().Where(team => ids.Contains(team.Id)).OrderBy(team => team.Name).ToListAsync(cancellationToken);
    }

    public async Task<(bool Ok, Guid? Id, string? Error)> CreateAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, null, "Enter a team name.");
        }

        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            CreatedAtUtc = _clock.UtcNow
        };
        _db.Teams.Add(team);
        await _db.SaveChangesAsync(cancellationToken);
        return (true, team.Id, null);
    }

    public async Task<bool> AddMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken)
    {
        var teamExists = await _db.Teams.AnyAsync(team => team.Id == teamId, cancellationToken);
        if (!teamExists)
        {
            return false;
        }

        var exists = await IsMemberAsync(userId, teamId, cancellationToken);
        if (!exists)
        {
            _db.TeamMemberships.Add(new TeamMembership { TeamId = teamId, UserId = userId });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
