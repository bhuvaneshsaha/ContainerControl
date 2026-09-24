using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.SharedKernel.Authorization;
using ContainerControl.SharedKernel.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ContainerControl.Modules.Access.Infrastructure.BreakGlass;

public sealed class BreakGlassAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly AccessDbContext _db;
    private readonly IClock _clock;

    public BreakGlassAuthorizationHandler(AccessDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return;
        }

        var now = _clock.UtcNow;
        var active = await _db.BreakGlassGrants.AsNoTracking().AnyAsync(
            grant => grant.UserId == userId
                && grant.PermissionCode == requirement.Permission
                && grant.ExpiresAtUtc > now);
        if (active)
        {
            context.Succeed(requirement);
        }
    }
}
