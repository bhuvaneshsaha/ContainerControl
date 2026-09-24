using System.Text;
using ContainerControl.Modules.Platform.Engine;

namespace ContainerControl.Modules.Delivery.Runs;

public sealed record SecretFile(string Name, string Value);

public sealed record InjectedSecrets(
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<string>? Command,
    IReadOnlyList<SecretFile> Files);

/// <summary>
/// Places configured secrets onto a service. Environment mode becomes container
/// environment variables. File mode is packed for <c>/run/secrets</c>. Compose
/// <c>${NAME}</c> placeholders are filled from those values.
/// </summary>
public static class SecretInjection
{
    public static InjectedSecrets Apply(
        IReadOnlyDictionary<string, string> environment,
        IReadOnlyList<string>? command,
        IEnumerable<(string Name, string Value, string Mode)> secrets)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var files = new List<SecretFile>();
        foreach (var secret in secrets)
        {
            values[secret.Name] = secret.Value;
            if (secret.Mode == "file")
            {
                files.Add(new SecretFile(secret.Name, secret.Value));
            }
        }

        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in environment)
        {
            resolved[key] = Interpolate(value, values);
        }

        foreach (var secret in secrets)
        {
            if (secret.Mode != "file")
            {
                resolved[secret.Name] = secret.Value;
            }
        }

        IReadOnlyList<string>? resolvedCommand = command is null
            ? null
            : command.Select(argument => Interpolate(argument, values)).ToArray();
        return new InjectedSecrets(resolved, resolvedCommand, files);
    }

    internal static string Interpolate(string input, IReadOnlyDictionary<string, string> values)
    {
        var builder = new StringBuilder(input.Length);
        for (var index = 0; index < input.Length; index++)
        {
            if (input[index] != '$')
            {
                builder.Append(input[index]);
                continue;
            }

            if (index + 1 < input.Length && input[index + 1] == '$')
            {
                builder.Append('$');
                index++;
                continue;
            }

            if (index + 1 < input.Length && input[index + 1] == '{')
            {
                var end = input.IndexOf('}', index + 2);
                if (end > index + 2)
                {
                    var token = input[(index + 2)..end];
                    var name = token;
                    string? fallback = null;
                    var hasFallback = false;
                    var separator = token.IndexOf(':');
                    if (separator > 0 && separator + 1 < token.Length && token[separator + 1] == '-')
                    {
                        name = token[..separator];
                        hasFallback = true;
                        fallback = token[(separator + 2)..];
                    }

                    if (values.TryGetValue(name, out var value) && value.Length > 0)
                    {
                        builder.Append(value);
                    }
                    else if (hasFallback)
                    {
                        builder.Append(fallback);
                    }
                    else
                    {
                        builder.Append(input.AsSpan(index, end - index + 1));
                    }

                    index = end;
                    continue;
                }
            }

            var start = index + 1;
            var cursor = start;
            if (cursor < input.Length && (char.IsLetter(input[cursor]) || input[cursor] == '_'))
            {
                cursor++;
                while (cursor < input.Length && (char.IsLetterOrDigit(input[cursor]) || input[cursor] == '_'))
                {
                    cursor++;
                }

                var name = input[start..cursor];
                if (values.TryGetValue(name, out var value))
                {
                    builder.Append(value);
                    index = cursor - 1;
                    continue;
                }
            }

            builder.Append('$');
        }

        return builder.ToString();
    }
}

public static class SecretArchive
{
    public static byte[] Create(IReadOnlyList<SecretFile> files)
    {
        using var stream = new MemoryStream();
        WriteDirectory(stream, "run", string.Empty);
        WriteDirectory(stream, "secrets", "run");
        foreach (var file in files)
        {
            if (!IsSafeName(file.Name))
            {
                throw new DockerEngineException("A secret file name is not allowed.");
            }

            var content = Encoding.UTF8.GetBytes(file.Value);
            WriteEntry(stream, file.Name, "run/secrets", content, 0x124, '0');
        }

        stream.Write(new byte[1024]);
        return stream.ToArray();
    }

    private static bool IsSafeName(string name)
    {
        if (name.Length is 0 or > 100)
        {
            return false;
        }

        if (name is "." or "..")
        {
            return false;
        }

        foreach (var character in name)
        {
            if (character is '/' or '\\' or '\0' or ':' )
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteDirectory(Stream stream, string name, string prefix) =>
        WriteEntry(stream, name, prefix, [], 0x1ED, '5');

    private static void WriteEntry(Stream stream, string name, string prefix, byte[] content, int mode, char type)
    {
        var header = new byte[512];
        WriteString(header, 0, name, 100);
        WriteOctal(header, 100, mode, 8);
        WriteOctal(header, 108, 0, 8);
        WriteOctal(header, 116, 0, 8);
        WriteOctal(header, 124, content.Length, 12);
        WriteOctal(header, 136, 0, 12);
        for (var index = 148; index < 156; index++)
        {
            header[index] = 0x20;
        }

        header[156] = (byte)type;
        WriteString(header, 257, "ustar", 6);
        header[262] = 0;
        WriteString(header, 263, "00", 2);
        WriteString(header, 345, prefix, 155);
        var checksum = 0;
        foreach (var part in header)
        {
            checksum += part;
        }

        WriteOctal(header, 148, checksum, 8);
        stream.Write(header);
        stream.Write(content);
        var remainder = content.Length % 512;
        if (remainder != 0)
        {
            stream.Write(new byte[512 - remainder]);
        }
    }

    private static void WriteString(byte[] buffer, int offset, string value, int length)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, 0, buffer, offset, Math.Min(bytes.Length, length));
    }

    private static void WriteOctal(byte[] buffer, int offset, int value, int length)
    {
        var text = Convert.ToString(value, 8).PadLeft(length - 1, '0');
        WriteString(buffer, offset, text, length - 1);
        buffer[offset + length - 1] = 0;
    }
}
