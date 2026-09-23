using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Access.Domain.Roles;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.SharedKernel.Auditing;
using ContainerControl.SharedKernel.CurrentUser;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Access.Infrastructure.Roles;

public sealed record RoleDetails(Guid Id, string Name, string? Description, IReadOnlyList<string> PermissionCodes);

public sealed class RoleWriteResult
{
    private RoleWriteResult(bool succeeded, RoleDetails? role, bool notFound, IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        Role = role;
        NotFound = notFound;
        Errors = errors;
    }

    public bool Succeeded { get; }

    public RoleDetails? Role { get; }

    public bool NotFound { get; }

    public IReadOnlyList<string> Errors { get; }

    public static RoleWriteResult Success(RoleDetails role) => new(true, role, false, []);

    public static RoleWriteResult Missing() => new(false, null, true, []);

    public static RoleWriteResult Failed(params string[] errors) => new(false, null, false, errors);
}

public sealed class RoleAdminService
{
    private readonly AccessDbContext _db;
    private readonly IAuditSink _audit;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public RoleAdminService(AccessDbContext db, IAuditSink audit, IClock clock, ICurrentUser currentUser)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<RoleDetails>> ListAsync(CancellationToken cancellationToken)
    {
        var roles = await _db.PermissionRoles
            .AsNoTracking()
            .Include(role => role.Permissions)
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);
        return roles.Select(ToDetails).ToArray();
    }

    public async Task<RoleWriteResult> CreateAsync(
        string name,
        string? description,
        IReadOnlyList<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(name, permissionCodes, excludeRoleId: null, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var role = new PermissionRole
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = NormalizeDescription(description),
            CreatedAtUtc = _clock.UtcNow
        };
        ReplacePermissions(role, permissionCodes);
        _db.PermissionRoles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(
            new AuditRecord("access.role.created", "permission-role", role.Id.ToString(), _currentUser.UserId),
            cancellationToken);
        return RoleWriteResult.Success(ToDetails(role));
    }

    public async Task<RoleWriteResult> UpdateAsync(
        Guid roleId,
        string name,
        string? description,
        IReadOnlyList<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        var role = await _db.PermissionRoles
            .Include(item => item.Permissions)
            .SingleOrDefaultAsync(item => item.Id == roleId, cancellationToken);
        if (role is null)
        {
            return RoleWriteResult.Missing();
        }

        var validation = await ValidateAsync(name, permissionCodes, role.Id, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        role.Name = name.Trim();
        role.Description = NormalizeDescription(description);
        _db.PermissionRolePermissions.RemoveRange(role.Permissions);
        ReplacePermissions(role, permissionCodes);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(
            new AuditRecord("access.role.updated", "permission-role", role.Id.ToString(), _currentUser.UserId),
            cancellationToken);
        return RoleWriteResult.Success(ToDetails(role));
    }

    public async Task<bool> DeleteAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await _db.PermissionRoles.SingleOrDefaultAsync(item => item.Id == roleId, cancellationToken);
        if (role is null)
        {
            return false;
        }

        _db.PermissionRoles.Remove(role);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(
            new AuditRecord("access.role.deleted", "permission-role", roleId.ToString(), _currentUser.UserId),
            cancellationToken);
        return true;
    }

    public async Task<RoleWriteResult> AssignUserRolesAsync(
        Guid userId,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        var userExists = await _db.Users.AnyAsync(user => user.Id == userId, cancellationToken);
        if (!userExists)
        {
            return RoleWriteResult.Missing();
        }

        var distinctRoleIds = roleIds.Distinct().ToArray();
        var known = await _db.PermissionRoles
            .Where(role => distinctRoleIds.Contains(role.Id))
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);
        if (known.Count != distinctRoleIds.Length)
        {
            return RoleWriteResult.Failed("One or more roles do not exist.");
        }

        var current = await _db.UserPermissionRoles
            .Where(link => link.UserId == userId)
            .ToListAsync(cancellationToken);
        _db.UserPermissionRoles.RemoveRange(current);
        foreach (var roleId in distinctRoleIds)
        {
            _db.UserPermissionRoles.Add(new UserPermissionRole { UserId = userId, RoleId = roleId });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(
            new AuditRecord("access.user.roles-assigned", "user", userId.ToString(), _currentUser.UserId),
            cancellationToken);
        return RoleWriteResult.Success(new RoleDetails(userId, string.Empty, null, []));
    }

    private async Task<RoleWriteResult?> ValidateAsync(
        string name,
        IReadOnlyList<string> permissionCodes,
        Guid? excludeRoleId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            return RoleWriteResult.Failed("Enter a role name up to 200 characters.");
        }

        var codes = permissionCodes.Distinct(StringComparer.Ordinal).ToArray();
        if (codes.Any(code => !PermissionCatalog.Contains(code)))
        {
            return RoleWriteResult.Failed("One or more permission codes are not in the catalog.");
        }

        var trimmed = name.Trim();
        var duplicate = await _db.PermissionRoles.AnyAsync(
            role => role.Name == trimmed && role.Id != excludeRoleId,
            cancellationToken);
        if (duplicate)
        {
            return RoleWriteResult.Failed("A role with that name already exists.");
        }

        return null;
    }

    private static void ReplacePermissions(PermissionRole role, IReadOnlyList<string> permissionCodes)
    {
        role.Permissions.Clear();
        foreach (var code in permissionCodes.Distinct(StringComparer.Ordinal))
        {
            role.Permissions.Add(new PermissionRolePermission
            {
                RoleId = role.Id,
                PermissionCode = code
            });
        }
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();
        return trimmed.Length > 400 ? trimmed[..400] : trimmed;
    }

    private static RoleDetails ToDetails(PermissionRole role) =>
        new(
            role.Id,
            role.Name,
            role.Description,
            role.Permissions.Select(link => link.PermissionCode).OrderBy(code => code).ToArray());
}
