using ContainerControl.Modules.Access.Domain.Users;
using ContainerControl.SharedKernel.Auditing;
using ContainerControl.SharedKernel.CurrentUser;
using ContainerControl.SharedKernel.Time;
using Microsoft.AspNetCore.Identity;

namespace ContainerControl.Modules.Access.Application.Users;

public sealed class UserAdminService
{
    private readonly UserManager<User> _users;
    private readonly IAuditSink _audit;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public UserAdminService(
        UserManager<User> users,
        IAuditSink audit,
        IClock clock,
        ICurrentUser currentUser)
    {
        _users = users;
        _audit = audit;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<UserMutationResult> CreateAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName.Trim(),
            LockoutEnabled = true,
            CreatedAtUtc = _clock.UtcNow
        };

        var result = await _users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return UserMutationResult.Failed(result.Errors.Select(error => error.Description).ToArray());
        }

        await _audit.WriteAsync(
            new AuditRecord("access.user.created", "user", user.Id.ToString(), _currentUser.UserId),
            cancellationToken);
        return UserMutationResult.Success(user.Id);
    }

    public async Task<UserMutationResult> DisableAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return UserMutationResult.Missing();
        }

        user.IsDisabled = true;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded)
        {
            return UserMutationResult.Failed(update.Errors.Select(error => error.Description).ToArray());
        }

        await _users.UpdateSecurityStampAsync(user);
        await _audit.WriteAsync(
            new AuditRecord("access.user.disabled", "user", user.Id.ToString(), _currentUser.UserId),
            cancellationToken);
        return UserMutationResult.Success(user.Id);
    }
}
