namespace ContainerControl.Modules.Applications.Templates;

public sealed class AppTemplate
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NameKey { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ComposeYaml { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
