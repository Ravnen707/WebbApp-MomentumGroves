namespace MomentumGroves.Helper;

/// <summary>
/// Loads local environment settings before ASP.NET Core configuration is created.
/// Values in the .env file take precedence over existing process environment variables.
/// </summary>
public static class DotEnvLoader
{
    /// <summary>
    /// Loads environment variables from the supplied dotenv file.
    /// </summary>
    /// <param name="path">The path to the file to process.</param>
    public static void Load(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line[7..].TrimStart();
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
