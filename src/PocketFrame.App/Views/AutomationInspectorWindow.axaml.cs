using Avalonia.Controls;
using PocketFrame.App.Automation;
using PocketFrame.App.Services;
using PocketFrame.App.ViewModels;
using PocketFrame.Automation;

namespace PocketFrame.App.Views;

public partial class AutomationInspectorWindow : Window
{
    private readonly MainWindowViewModel viewModel;
    private readonly AutomationPipeServer pipeServer;
    private readonly IVncClientService vncClientService;

    public AutomationInspectorWindow()
    {
        InitializeComponent();
        viewModel = null!;
        pipeServer = null!;
        vncClientService = null!;
    }

    public AutomationInspectorWindow(MainWindowViewModel viewModel, AutomationPipeServer pipeServer, IVncClientService vncClientService)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        this.pipeServer = pipeServer;
        this.vncClientService = vncClientService;
        Refresh();
    }

    private void OnRefreshClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Refresh();

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void Refresh()
    {
        if (viewModel is null || pipeServer is null)
        {
            return;
        }

        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PocketFrame.Mcp", "PocketFrame.Mcp.csproj"));
        PipeStatusText.Text = pipeServer.IsRunning ? "Ready" : "Stopped";
        PipeNameText.Text = AutomationPipeNames.DefaultPipeName;
        DeviceText.Text = $"{viewModel.SelectedProfile?.Name ?? "No device"} ({viewModel.SelectedProfile?.Id ?? "none"})";
        VncStatusText.Text = viewModel.Vnc.Status;
        FrameIndexText.Text = vncClientService.FrameIndex.ToString();
        LastFrameText.Text = vncClientService.LastFrameUpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "None";
        LastCommandText.Text = pipeServer.LastCommand;
        LastResultText.Text = pipeServer.LastResult;
        LastErrorText.Text = string.IsNullOrWhiteSpace(pipeServer.LastError) ? "None" : pipeServer.LastError;
        LogPathText.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PocketFrame", "input.log");
        McpCommandBox.Text = $"dotnet run --project \"{projectPath}\"";
    }
}
