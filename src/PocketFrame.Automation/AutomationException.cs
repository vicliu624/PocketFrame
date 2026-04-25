namespace PocketFrame.Automation;

public sealed class AutomationException : Exception
{
    public AutomationException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
