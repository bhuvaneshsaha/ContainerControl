using System.Reflection;
using ContainerControl.Modules.Access.Infrastructure.Auditing;

namespace ContainerControl.Host.IntegrationTests;

public class AuditRowTests
{
    [Fact]
    public void Audit_rows_name_the_action_and_not_a_secret_value()
    {
        var names = typeof(AuditRow).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).ToArray();

        Assert.Equal(["Id", "OccurredAtUtc", "ActorUserId", "Action", "SubjectType", "SubjectId"], names);
    }
}
