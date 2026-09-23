namespace ContainerControl.Modules.Access.Application.Tokens;

public interface IApiTokenAuthenticator
{
    Task<Guid?> AuthenticateAsync(string plaintext, CancellationToken cancellationToken);
}
