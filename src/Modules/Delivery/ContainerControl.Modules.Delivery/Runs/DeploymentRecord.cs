namespace ContainerControl.Modules.Delivery.Runs;

public sealed class DeploymentRecord
{
    public Guid Id { get; set; }

    public Guid ApplicationId { get; set; }

    public string? Image { get; set; }

    public string? CommandJson { get; set; }

    public string? ComposeYaml { get; set; }

    public int? InternalPort { get; set; }

    public string? Hostname { get; set; }

    public bool Exposed { get; set; }

    public string Status { get; set; } = "succeeded";

    public string? Error { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class WorkerLease
{
    public int Id { get; set; }

    public Guid? OwnerId { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }
}
