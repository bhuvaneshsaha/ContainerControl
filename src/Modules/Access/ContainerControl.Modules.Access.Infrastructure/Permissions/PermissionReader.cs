using ContainerControl.Modules.Access.Application.Permissions;
using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Access.Infrastructure.Permissions;

public sealed class PermissionReader : IPermissionReader
{
    private readonly AccessDbContext _db;

    public PermissionReader(AccessDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<string>> GetEffectivePermissionCodesAsync(
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

    public Task<IReadOnlyList<PermissionDefinition>> GetCatalogAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(PermissionCatalog.All);
    }
}
