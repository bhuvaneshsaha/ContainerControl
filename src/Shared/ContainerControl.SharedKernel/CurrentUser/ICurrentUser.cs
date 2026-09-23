namespace ContainerControl.SharedKernel.CurrentUser;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }
}
