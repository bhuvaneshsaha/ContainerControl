using ContainerControl.Modules.Access.Domain.Permissions;

namespace ContainerControl.Modules.Access.Application.Permissions;

public interface IPermissionReader
{
    Task<IReadOnlyList<string>> GetEffectivePermissionCodesAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PermissionDefinition>> GetCatalogAsync(CancellationToken cancellationToken);
}
