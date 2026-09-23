namespace ContainerControl.SharedKernel.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
