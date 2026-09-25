using ContainerControl.Modules.Edge.Domains;

namespace ContainerControl.Modules.Delivery.Runs;

/// <summary>
/// Blue/green slot names and Traefik weights. One replica stays the default.
/// A slot deploy is the explicit second copy.
/// </summary>
public static class TrafficPlan
{
    public const string Classic = "classic";

    public const string Blue = "blue";

    public const string Green = "green";

    public const string SlotMode = "slot";

    public const string DirectorImage = "nginx:1.28-alpine";

    public static string Other(string slot) =>
        string.Equals(slot, Blue, StringComparison.Ordinal) ? Green : Blue;

    public static string CandidateFor(string? liveSlot) =>
        string.Equals(liveSlot, Blue, StringComparison.Ordinal) ? Green : Blue;

    public static string BackendService(string routerName, string slot) =>
        string.Equals(slot, Classic, StringComparison.Ordinal) ? routerName : routerName + "-" + slot;

    public static string Network(Guid applicationId, string slot) =>
        "cc-app-" + applicationId.ToString("N") + "-" + slot;

    public static IReadOnlyList<SlotWeight> Weights(string routerName, string liveSlot, string? candidateSlot, int candidatePercent)
    {
        var percent = Math.Clamp(candidatePercent, 0, 100);
        var weights = new List<SlotWeight>();
        var liveWeight = candidateSlot is null ? 100 : 100 - percent;
        if (liveWeight > 0)
        {
            weights.Add(new SlotWeight(liveSlot, BackendService(routerName, liveSlot), liveWeight));
        }

        if (candidateSlot is not null && percent > 0)
        {
            weights.Add(new SlotWeight(candidateSlot, BackendService(routerName, candidateSlot), percent));
        }

        if (weights.Count == 0)
        {
            weights.Add(new SlotWeight(liveSlot, BackendService(routerName, liveSlot), 100));
        }

        return weights;
    }
}

public sealed class TrafficSlot
{
    public Guid ApplicationId { get; set; }

    public string LiveSlot { get; set; } = TrafficPlan.Classic;

    public string? CandidateSlot { get; set; }

    public int CandidatePercent { get; set; }

    public string? PreviousSlot { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed record TrafficSlotView(string LiveSlot, string? CandidateSlot, int CandidatePercent, string? PreviousSlot);
