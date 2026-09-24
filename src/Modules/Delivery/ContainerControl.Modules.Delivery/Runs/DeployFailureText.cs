using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.Modules.Platform.Engine;

namespace ContainerControl.Modules.Delivery.Runs;

/// <summary>
/// Text safe to store on a deployment and return from the API.
/// Engine and secret failures may echo values, so anything that looks like
/// an assignment, or is longer than a short sentence, is replaced.
/// </summary>
public static class DeployFailureText
{
    public const string Generic = "The Engine rejected the deployment.";

    public static string Describe(Exception exception)
    {
        if (exception is DockerEngineException or SecretStoreException)
        {
            var text = exception.Message.ReplaceLineEndings(" ").Trim();
            if (text.Length is > 0 and <= 300 && !text.Contains('='))
            {
                return text;
            }
        }

        return Generic;
    }
}
