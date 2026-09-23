using System.Diagnostics;
using ContainerControl.Modules.Access.Domain.Users;
using ContainerControl.SharedKernel.Auditing;
using Microsoft.AspNetCore.Identity;

namespace ContainerControl.Modules.Access.Application.SignIn;

public static class AccessTelemetry
{
    public const string SourceName = "ContainerControl.Access";

    public static readonly ActivitySource Source = new(SourceName);
}

public sealed class SignInService
{
    private readonly UserManager<User> _users;
    private readonly SignInManager<User> _signIn;
    private readonly IAuditSink _audit;

    public SignInService(UserManager<User> users, SignInManager<User> signIn, IAuditSink audit)
    {
        _users = users;
        _signIn = signIn;
        _audit = audit;
    }

    public async Task<SignInStatus> SignInAsync(string email, string password, CancellationToken cancellationToken)
    {
        using var activity = AccessTelemetry.Source.StartActivity("Access.SignIn");
        var user = await _users.FindByEmailAsync(email);
        if (user is null || user.IsDisabled)
        {
            await RejectAsync(cancellationToken);
            return SignInStatus.Rejected;
        }

        var result = await _signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            await RejectAsync(cancellationToken);
            return SignInStatus.Rejected;
        }

        await _signIn.SignInAsync(user, isPersistent: false);
        await _audit.WriteAsync(
            new AuditRecord("access.sign-in.succeeded", "user", user.Id.ToString(), user.Id),
            cancellationToken);
        return SignInStatus.Succeeded;
    }

    public async Task SignOutAsync(Guid? actorUserId, CancellationToken cancellationToken)
    {
        await _signIn.SignOutAsync();
        await _audit.WriteAsync(
            new AuditRecord("access.sign-out", "user", actorUserId?.ToString(), actorUserId),
            cancellationToken);
    }

    private Task RejectAsync(CancellationToken cancellationToken) =>
        _audit.WriteAsync(
            new AuditRecord("access.sign-in.rejected", "user", null, null),
            cancellationToken);
}
