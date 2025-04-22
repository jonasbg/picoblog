public class SanitizingLoggerProvider : ILoggerProvider
{
    private readonly ILoggerProvider _innerProvider;

    public SanitizingLoggerProvider(ILoggerProvider innerProvider)
    {
        _innerProvider = innerProvider;
    }

    public ILogger CreateLogger(string categoryName)
    {
        // Get the actual type from the category name
        Type categoryType = Type.GetType(categoryName) ?? typeof(object);

        // Create the correct generic ILogger<T> type
        Type genericLoggerType = typeof(ILogger<>).MakeGenericType(categoryType);

        // Get the inner logger and cast it to ILogger<T>
        ILogger innerLogger = _innerProvider.CreateLogger(categoryName);
        var genericInnerLogger = innerLogger
            .GetType()
            .GetMethod("AsLogger")
            ?.MakeGenericMethod(categoryType)
            .Invoke(innerLogger, null);

        if (genericInnerLogger == null)
        {
            // Fallback to using the non-generic logger
            return innerLogger;
        }

        // Create our sanitizing logger with the properly typed inner logger
        var sanitizingLoggerType = typeof(SanitizingLogger<>).MakeGenericType(categoryType);
        return (ILogger)Activator.CreateInstance(sanitizingLoggerType, genericInnerLogger)!;
    }

    public void Dispose() => _innerProvider.Dispose();
}
