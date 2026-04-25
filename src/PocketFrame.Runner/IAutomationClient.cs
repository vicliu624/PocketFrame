namespace PocketFrame.Runner;

public interface IAutomationClient
{
    Task<T> SendAsync<T>(string method, object? parameters = null, int timeoutMs = 10000, CancellationToken cancellationToken = default);
}
