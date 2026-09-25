namespace ContainerControl.Modules.Applications.Templates;

public sealed record ComposeGateResult(bool Accepted, IReadOnlyList<string> Errors);

/// <summary>
/// Accepts a compose document that already passes compose policy and contains no secret values.
/// </summary>
public interface IComposeDocumentGate
{
    ComposeGateResult Accept(string yaml);
}
