using ContainerControl.Modules.Access.Domain.BreakGlass;

namespace ContainerControl.Host.IntegrationTests;

public class BreakGlassPolicyTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(61)]
    public void Duration_outside_five_to_sixty_minutes_is_rejected(int minutes)
    {
        Assert.NotNull(BreakGlassPolicy.DurationError(minutes));
    }

    [Fact]
    public void An_unexpired_grant_is_added_to_the_assigned_permissions()
    {
        var now = DateTimeOffset.Parse("2026-09-24T12:00:00Z");
        var grants = new[]
        {
            new BreakGlassGrant
            {
                PermissionCode = "deploy.approve",
                ExpiresAtUtc = now.AddMinutes(10)
            },
            new BreakGlassGrant
            {
                PermissionCode = "runtime.control",
                ExpiresAtUtc = now.AddMinutes(-1)
            }
        };

        var effective = BreakGlassPolicy.Effective(["apps.read"], grants, now);

        Assert.Equal(["apps.read", "deploy.approve"], effective);
    }
}
