namespace ContainerControl.Modules.Applications.Workloads;

public static class ApprovalPolicy
{
    public static bool Required(string environment, bool requested) =>
        string.Equals(environment, "prod", StringComparison.Ordinal) || requested;
}
