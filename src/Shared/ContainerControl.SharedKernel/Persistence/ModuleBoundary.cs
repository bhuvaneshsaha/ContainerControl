namespace ContainerControl.SharedKernel.Persistence;

/// <summary>
/// Row that keeps an empty module schema present until that module owns real tables.
/// </summary>
public sealed class ModuleBoundary
{
    public int Id { get; set; }

    public string ModuleName { get; set; } = string.Empty;
}
