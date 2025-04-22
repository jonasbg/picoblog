public class SanitizingLogger<T> : ILogger<T>
{
    private readonly ILogger<T> _innerLogger;

    public SanitizingLogger(ILogger<T> innerLogger)
    {
        _innerLogger = innerLogger;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => _innerLogger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => _innerLogger.IsEnabled(logLevel);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        var originalMessage = formatter(state, exception);
        var sanitizedMessage = SanitizeInput(originalMessage);
        _innerLogger.Log(logLevel, eventId, state, exception, (s, e) => sanitizedMessage);
    }

    private static string SanitizeInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sanitized = input
            .Replace("'", "")
            .Replace("\"", "")
            .Replace(";", "")
            .Replace("--", "")
            .Replace("/*", "")
            .Replace("*/", "")
            .Replace("xp_", "")
            .Replace("{", "")
            .Replace("}", "")
            .Replace("${", "")
            .Replace("#{", "")
            .Replace("\r", "")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Replace("\0", "")
            .Replace("<script", "")
            .Replace("</script>", "")
            .Trim()
            .Replace("  ", " ");

        return $"[Sanitized] {sanitized}";
    }
}
