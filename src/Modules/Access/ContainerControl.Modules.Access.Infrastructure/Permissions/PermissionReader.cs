using ContainerControl.Modules.Access.Application.Permissions;
using ContainerControl.Modules.Access.Domain.BreakGlass;
using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Access.Infrastructure.Permissions;

public sealed class PermissionReader : IPermissionReader
{
    private readonly AccessDbContext _db;
    private readonly IClock _clock;

    public PermissionReader(AccessDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<string>> GetAssignedPermissionCodesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _db.UserPermissionRoles
            .Where(assignment => assignment.UserId == userId)
            .SelectMany(assignment => assignment.Role!.Permissions.Select(link => link.PermissionCode))
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetEffectivePermissionCodesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var assigned = await GetAssignedPermissionCodesAsync(userId, cancellationToken);
        var grants = await _db.BreakGlassGrants.AsNoTracking()
            .Where(grant => grant.UserId == userId)
            .ToListAsync(cancellationToken);
        return BreakGlassPolicy.Effective(assigned, grants, _clock.UtcNow);
    }

    public Task<IReadOnlyList<PermissionDefinition>> GetCatalogAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(PermissionCatalog.All);
    }
}
