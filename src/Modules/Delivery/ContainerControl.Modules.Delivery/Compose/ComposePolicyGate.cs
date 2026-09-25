using ContainerControl.Modules.Applications.Templates;

namespace ContainerControl.Modules.Delivery.Compose;

public sealed class ComposePolicyGate : IComposeDocumentGate
{
    public const int MaxComposeLength = 100_000;

    public ComposeGateResult Accept(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return new ComposeGateResult(false, ["Enter a compose file."]);
        }

        if (yaml.Length > MaxComposeLength)
        {
            return new ComposeGateResult(false, ["The compose file is too long."]);
        }

        var secrets = TemplateComposeSecrets.FindInCompose(yaml);
        if (secrets.Count > 0)
        {
            return new ComposeGateResult(false, secrets);
        }

        var plan = ComposePolicy.Parse(yaml);
        return new ComposeGateResult(plan.Accepted, plan.Errors);
    }
}
