using System.Security.Claims;
using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Runtime.Inspection;
using ContainerControl.SharedKernel.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ContainerControl.Modules.Runtime.Http;

[Authorize]
public sealed class LogHub : Hub
{
    private readonly RuntimeInspector _runtime;
    private readonly ILogger<LogHub> _logger;

    public LogHub(RuntimeInspector runtime, ILogger<LogHub> logger)
    {
        _runtime = runtime;
        _logger = logger;
    }

    public static bool CanRead(ClaimsPrincipal? user) =>
        user?.HasClaim(PermissionPolicy.ClaimType, PermissionCatalog.RuntimeLogsRead) == true;

    public async Task Tail(Guid appId)
    {
        if (!CanRead(Context.User))
        {
            throw new HubException("Logs are not available.");
        }

        try
        {
            await _runtime.FollowAsync(appId, line => Clients.Caller.SendAsync("log", line, Context.ConnectionAborted), Context.ConnectionAborted);
        }
        catch (LogStreamException exception)
        {
            throw new HubException(exception.Message);
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not HubException)
        {
            _logger.LogError("Log tail stopped. {ExceptionType} {ApplicationId}", exception.GetType().Name, appId);
            throw new HubException("The log stream stopped.");
        }
    }
}
