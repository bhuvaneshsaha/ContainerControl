namespace ContainerControl.SharedKernel.Correlation;

public static class Correlation
{
    public const string HeaderName = "X-Correlation-ID";

    public const string ItemKey = "ContainerControl.CorrelationId";
}

public interface ICorrelationContext
{
    string CorrelationId { get; }
}
