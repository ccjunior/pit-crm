using System.Text.RegularExpressions;

internal static class TestLogSanitizer
{
    private static readonly Regex DisposableSecretPattern = new("^[a-f0-9]{64}$", RegexOptions.CultureInvariant);
    private static readonly Regex HexTokenPattern = new("[a-f0-9]{64,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ConnectionStringPattern = new(
        "connection[_\\s-]?strings?|connection string",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ConnectionFieldPattern = new(
        "(?:^|[;,\\s])(?:host|server|port|database|data\\s+source|initial\\s+catalog|username|user\\s+id|uid|password|passwd|pwd)\\s*=",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CredentialPattern = new(
        "(?:^|[^a-z0-9_])(?:password|passwd|pwd|secret(?:[_-]?key)?|(?:access|refresh)[_-]?token|token|jwt|api[_-]?key|client[_-]?secret|access[_-]?key|account[_-]?key|shared[_-]?access[_-]?key|authorization)[^a-z0-9_]*[:=]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex UrlUserInfoPattern = new(
        "://[^/@\\s]+:[^/@\\s]+@",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex BearerTokenPattern = new(
        "(?:^|[\\s:])bearer\\s+\\S+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static int Run(string logPath, string secretPath, TextWriter output, TextWriter error)
    {
        try
        {
            EnsureRegularFile(logPath);
            EnsureRegularFile(secretPath);
            var secret = File.ReadAllText(secretPath).TrimEnd('\r', '\n');
            if (!DisposableSecretPattern.IsMatch(secret))
            {
                error.WriteLine("Diagnostic sanitizer rejected an unexpected disposable secret format.");
                return 2;
            }

            foreach (var line in File.ReadLines(logPath))
            {
                output.WriteLine(SanitizeLine(line, secret));
            }

            return 0;
        }
        catch
        {
            error.WriteLine("Diagnostic sanitizer failed; test details must remain withheld.");
            return 2;
        }
    }

    private static string SanitizeLine(string line, string secret)
    {
        if (ConnectionStringPattern.IsMatch(line)
            || ConnectionFieldPattern.IsMatch(line)
            || CredentialPattern.IsMatch(line)
            || BearerTokenPattern.IsMatch(line)
            || UrlUserInfoPattern.IsMatch(line))
        {
            return "[REDACTED CREDENTIAL OR CONNECTION STRING LINE]";
        }

        return HexTokenPattern.Replace(line.Replace(secret, "[REDACTED]", StringComparison.Ordinal), "[REDACTED]");
    }

    private static void EnsureRegularFile(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new IOException("Expected a regular file.");
        }
    }
}
