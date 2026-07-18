using Newtonsoft.Json;
using Playnite.SDK;

namespace Playnite.SDK.V7.Host;

internal sealed class HostLogProvider : ILogProvider
{
    private readonly Func<string, string, string> hostCall;

    public HostLogProvider(Func<string, string, string> hostCall) =>
        this.hostCall = hostCall ?? throw new ArgumentNullException(nameof(hostCall));

    public ILogger GetLogger(string loggerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loggerName);
        return new HostLogger(hostCall, loggerName);
    }
}

internal sealed class HostLogger : ILogger
{
    private readonly Func<string, string, string> hostCall;
    private readonly string loggerName;

    public HostLogger(Func<string, string, string> hostCall, string loggerName)
    {
        this.hostCall = hostCall ?? throw new ArgumentNullException(nameof(hostCall));
        this.loggerName = string.IsNullOrWhiteSpace(loggerName)
            ? throw new ArgumentException("A logger name is required.", nameof(loggerName))
            : loggerName;
    }

    public void Debug(string message) => Write("Debug", null, message);
    public void Debug(Exception exception, string message) => Write("Debug", exception, message);
    public void Error(string message) => Write("Error", null, message);
    public void Error(Exception exception, string message) => Write("Error", exception, message);
    public void Info(string message) => Write("Info", null, message);
    public void Info(Exception exception, string message) => Write("Info", exception, message);
    public void Warn(string message) => Write("Warn", null, message);
    public void Warn(Exception exception, string message) => Write("Warn", exception, message);
    public void Trace(string message) => Write("Trace", null, message);
    public void Trace(Exception exception, string message) => Write("Trace", exception, message);

    private void Write(string level, Exception exception, string message) =>
        hostCall("Log", JsonConvert.SerializeObject(new
        {
            Level = level,
            LoggerName = loggerName,
            Message = message ?? string.Empty,
            Exception = exception?.ToString()
        }));
}
