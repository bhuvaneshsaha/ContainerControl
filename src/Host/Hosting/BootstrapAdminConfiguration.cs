namespace ContainerControl.Host.Hosting;

public static class BootstrapAdminConfiguration
{
    public static void MapEnvironmentVariables(ConfigurationManager configuration)
    {
        Map(configuration, "CONTAINERCONTROL_ADMIN_EMAIL", "BootstrapAdmin:Email");
        Map(configuration, "CONTAINERCONTROL_ADMIN_PASSWORD", "BootstrapAdmin:Password");
        Map(configuration, "CONTAINERCONTROL_ADMIN_DISPLAY_NAME", "BootstrapAdmin:DisplayName");
    }

    private static void Map(ConfigurationManager configuration, string variable, string key)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(value))
        {
            configuration[key] = value;
        }
    }
}
