namespace ContainerControl.Modules.Access.Application.Teams;

public interface ITeamDirectory
{
    Task<bool> IsMemberAsync(Guid userId, Guid teamId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> TeamIdsForAsync(Guid userId, CancellationToken cancellationToken);
}
