using ContainerControl.SharedKernel.Correlation;

namespace ContainerControl.Host.Hosting;

public sealed class HttpCorrelationContext : ICorrelationContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCorrelationContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string CorrelationId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.Items[Correlation.ItemKey] as string;
            return string.IsNullOrWhiteSpace(value) ? "startup" : value;
        }
    }
}
