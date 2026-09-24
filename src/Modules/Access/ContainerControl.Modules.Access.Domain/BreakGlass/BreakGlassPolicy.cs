namespace ContainerControl.Modules.Access.Domain.BreakGlass;

public static class BreakGlassPolicy
{
    public const int MinimumMinutes = 5;

    public const int MaximumMinutes = 60;

    public static string? DurationError(int minutes) =>
        minutes is >= MinimumMinutes and <= MaximumMinutes
            ? null
            : "A break-glass grant lasts between 5 and 60 minutes.";

    public static IReadOnlyList<string> Effective(
        IEnumerable<string> assigned,
        IEnumerable<BreakGlassGrant> grants,
        DateTimeOffset now)
    {
        return assigned
            .Concat(grants.Where(grant => grant.ExpiresAtUtc > now).Select(grant => grant.PermissionCode))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
    }
}
