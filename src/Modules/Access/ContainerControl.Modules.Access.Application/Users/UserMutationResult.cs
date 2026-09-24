namespace ContainerControl.Modules.Access.Application.Users;

public sealed class UserMutationResult
{
    private UserMutationResult(bool succeeded, Guid? userId, bool notFound, IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        UserId = userId;
        NotFound = notFound;
        Errors = errors;
    }

    public bool Succeeded { get; }

    public Guid? UserId { get; }

    public bool NotFound { get; }

    public IReadOnlyList<string> Errors { get; }

    public static UserMutationResult Success(Guid userId) => new(true, userId, false, []);

    public static UserMutationResult Missing() => new(false, null, true, []);

    public static UserMutationResult Failed(IReadOnlyList<string> errors) => new(false, null, false, errors);
}
