using Avalonia.Input;

namespace PocketFrame.App.Services;

public interface IInputMappingService
{
    uint? ToKeysym(Key key);
    uint? ToKeysym(string keyCode);
}
