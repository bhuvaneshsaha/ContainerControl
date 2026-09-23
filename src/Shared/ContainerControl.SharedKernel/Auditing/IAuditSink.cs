namespace ContainerControl.SharedKernel.Auditing;

public interface IAuditSink
{
    Task WriteAsync(AuditRecord record, CancellationToken cancellationToken);
}
