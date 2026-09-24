using ContainerControl.Modules.Applications.Workloads;

namespace ContainerControl.Host.IntegrationTests;

public class ApprovalPolicyTests
{
    [Theory]
    [InlineData("prod", false, true)]
    [InlineData("prod", true, true)]
    [InlineData("dev", true, true)]
    [InlineData("staging", true, true)]
    [InlineData("dev", false, false)]
    [InlineData("staging", false, false)]
    public void Prod_and_opt_in_apps_require_approval(string environment, bool requested, bool required)
    {
        Assert.Equal(required, ApprovalPolicy.Required(environment, requested));
    }
}
