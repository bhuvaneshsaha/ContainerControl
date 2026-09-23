namespace ContainerControl.Modules.Access.Domain.Teams;

public sealed class Team
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public List<TeamMembership> Memberships { get; set; } = [];
}

public sealed class TeamMembership
{
    public Guid TeamId { get; set; }

    public Guid UserId { get; set; }

    public Team? Team { get; set; }
}
