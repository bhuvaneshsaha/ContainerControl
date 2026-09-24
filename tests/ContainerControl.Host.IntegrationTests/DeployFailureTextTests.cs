using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.Modules.Delivery.Runs;
using ContainerControl.Modules.Platform.Engine;

namespace ContainerControl.Host.IntegrationTests;

public class DeployFailureTextTests
{
    [Fact]
    public void Short_engine_message_without_an_assignment_is_kept()
    {
        var text = DeployFailureText.Describe(new DockerEngineException("A secret could not be read."));

        Assert.Equal("A secret could not be read.", text);
    }

    [Fact]
    public void Engine_text_that_looks_like_an_assignment_is_replaced()
    {
        var text = DeployFailureText.Describe(new DockerEngineException("TOKEN=value"));

        Assert.Equal(DeployFailureText.Generic, text);
    }

    [Fact]
    public void Blank_engine_text_is_replaced()
    {
        var text = DeployFailureText.Describe(new DockerEngineException(" "));

        Assert.Equal(DeployFailureText.Generic, text);
    }

    [Fact]
    public void Secret_store_text_with_an_assignment_is_replaced()
    {
        var text = DeployFailureText.Describe(new SecretStoreException("path=/prod/db"));

        Assert.Equal(DeployFailureText.Generic, text);
    }

    [Fact]
    public void Unexpected_exceptions_are_replaced_and_still_described()
    {
        var text = DeployFailureText.Describe(new InvalidOperationException("PASSWORD=secret"));

        Assert.Equal(DeployFailureText.Generic, text);
    }

    [Fact]
    public void Engine_text_longer_than_a_short_sentence_is_replaced()
    {
        var text = DeployFailureText.Describe(new DockerEngineException(new string('x', 301)));

        Assert.Equal(DeployFailureText.Generic, text);
    }
}
