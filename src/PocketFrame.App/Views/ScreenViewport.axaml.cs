using Avalonia.Controls;
using Avalonia.Input;
using PocketFrame.App.ViewModels;

namespace PocketFrame.App.Views;

public partial class ScreenViewport : UserControl
{
    private bool pointerDown;

    public ScreenViewport()
    {
        InitializeComponent();
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
    }

    private async void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus(NavigationMethod.Pointer);

        if (DataContext is not DeviceShellViewModel viewModel || viewModel.Profile is null)
        {
            return;
        }

        pointerDown = true;
        var position = e.GetPosition(this);
        await viewModel.VncClientService.SendPointerAsync((int)position.X, (int)position.Y, 1);
        e.Handled = true;
    }

    private async void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not DeviceShellViewModel viewModel)
        {
            return;
        }

        pointerDown = false;
        var position = e.GetPosition(this);
        await viewModel.VncClientService.SendPointerAsync((int)position.X, (int)position.Y, 0);
        e.Handled = true;
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!pointerDown || DataContext is not DeviceShellViewModel viewModel)
        {
            return;
        }

        var position = e.GetPosition(this);
        await viewModel.VncClientService.SendPointerAsync((int)position.X, (int)position.Y, 1);
    }
}
