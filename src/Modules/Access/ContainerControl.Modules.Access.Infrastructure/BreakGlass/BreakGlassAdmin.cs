using ContainerControl.Modules.Access.Domain.BreakGlass;
using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Access.Domain.Users;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.SharedKernel.Auditing;
using ContainerControl.SharedKernel.CurrentUser;
using ContainerControl.SharedKernel.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Access.Infrastructure.BreakGlass;

public sealed record BreakGlassView(Guid Id, Guid UserId, string PermissionCode, DateTimeOffset ExpiresAtUtc);

public sealed class BreakGlassAdmin
{
    private readonly AccessDbContext _db;
    private readonly UserManager<User> _users;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public BreakGlassAdmin(AccessDbContext db, UserManager<User> users, IAuditSink audit, ICurrentUser currentUser, IClock clock)
    {
        _db = db;
        _users = users;
        _audit = audit;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<BreakGlassView>> ListActiveAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        return await _db.BreakGlassGrants.AsNoTracking()
            .Where(grant => grant.ExpiresAtUtc > now)
            .OrderBy(grant => grant.ExpiresAtUtc)
            .Select(grant => new BreakGlassView(grant.Id, grant.UserId, grant.PermissionCode, grant.ExpiresAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool Ok, Guid? Id, string? Error)> GrantAsync(
        Guid userId,
        string? permissionCode,
        int minutes,
        CancellationToken cancellationToken)
    {
        var durationError = BreakGlassPolicy.DurationError(minutes);
        if (durationError is not null)
        {
            return (false, null, durationError);
        }

        var code = permissionCode?.Trim() ?? string.Empty;
        if (!PermissionCatalog.Contains(code))
        {
            return (false, null, "Choose a permission from the catalog.");
        }

        if (await _users.FindByIdAsync(userId.ToString()) is null)
        {
            return (false, null, "That user was not found.");
        }

        var grant = new BreakGlassGrant
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PermissionCode = code,
            GrantedByUserId = _currentUser.UserId ?? Guid.Empty,
            ExpiresAtUtc = _clock.UtcNow.AddMinutes(minutes),
            CreatedAtUtc = _clock.UtcNow
        };
        _db.BreakGlassGrants.Add(grant);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditRecord("access.breakglass.granted", "break-glass", grant.Id.ToString(), _currentUser.UserId), cancellationToken);
        return (true, grant.Id, null);
    }
}
