using ContainerControl.Modules.Applications.Workloads;

namespace ContainerControl.Host.IntegrationTests;

public class DatabaseImageOverrideTests
{
    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, true)]
    public void Enabling_database_images_requires_platform_settings_manage(
        bool requested,
        bool currentlyAllowed,
        bool callerMayManageSettings,
        bool allowed)
    {
        Assert.Equal(
            allowed,
            DatabaseImageOverride.MayEnable(requested, currentlyAllowed, callerMayManageSettings));
    }
}
