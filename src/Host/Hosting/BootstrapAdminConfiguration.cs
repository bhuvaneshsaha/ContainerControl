namespace ContainerControl.Host.Hosting;

public static class BootstrapAdminConfiguration
{
    public static void MapEnvironmentVariables(ConfigurationManager configuration)
    {
        Map(configuration, "CONTAINERCONTROL_ADMIN_EMAIL", "BootstrapAdmin:Email");
        Map(configuration, "CONTAINERCONTROL_ADMIN_PASSWORD", "BootstrapAdmin:Password");
        Map(configuration, "CONTAINERCONTROL_ADMIN_DISPLAY_NAME", "BootstrapAdmin:DisplayName");
        Map(configuration, "INFISICAL_SITE_URL", "Infisical:SiteUrl");
        Map(configuration, "INFISICAL_DEV_CLIENT_ID", "Infisical:Environments:dev:ClientId");
        Map(configuration, "INFISICAL_DEV_CLIENT_SECRET", "Infisical:Environments:dev:ClientSecret");
        Map(configuration, "INFISICAL_DEV_PROJECT_ID", "Infisical:Environments:dev:ProjectId");
        Map(configuration, "INFISICAL_STAGING_CLIENT_ID", "Infisical:Environments:staging:ClientId");
        Map(configuration, "INFISICAL_STAGING_CLIENT_SECRET", "Infisical:Environments:staging:ClientSecret");
        Map(configuration, "INFISICAL_STAGING_PROJECT_ID", "Infisical:Environments:staging:ProjectId");
        Map(configuration, "INFISICAL_PROD_CLIENT_ID", "Infisical:Environments:prod:ClientId");
        Map(configuration, "INFISICAL_PROD_CLIENT_SECRET", "Infisical:Environments:prod:ClientSecret");
        Map(configuration, "INFISICAL_PROD_PROJECT_ID", "Infisical:Environments:prod:ProjectId");
        Map(configuration, "EDGE_ACME_EMAIL", "Edge:AcmeEmail");
        Map(configuration, "EDGE_HTTP_PORT", "Edge:HttpPort");
        Map(configuration, "EDGE_HTTPS_PORT", "Edge:HttpsPort");
        Map(configuration, "ALERTS_SMTP_HOST", "Alerts:Smtp:Host");
        Map(configuration, "ALERTS_SMTP_PORT", "Alerts:Smtp:Port");
        Map(configuration, "ALERTS_SMTP_FROM", "Alerts:Smtp:From");
        Map(configuration, "ALERTS_SMTP_USERNAME", "Alerts:Smtp:Username");
        Map(configuration, "ALERTS_SMTP_PASSWORD", "Alerts:Smtp:Password");
        Map(configuration, "ALERTS_SMTP_ENABLE_SSL", "Alerts:Smtp:EnableSsl");
        Map(configuration, "LOGS_RETENTION_DAYS", "Logs:RetentionDays");
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
