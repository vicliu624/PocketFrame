namespace PocketFrame.App.Vnc;

public sealed class RfbConnectionException : Exception
{
    public RfbConnectionException(string code, string message, Exception? innerException = null) : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
