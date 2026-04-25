using System.Collections.ObjectModel;
using Avalonia.Controls;
using PocketFrame.App.Models;
using PocketFrame.DeviceProfiles;
using PocketFrame.App.Services;

namespace PocketFrame.App.Views;

public partial class VncConnectionDialog : Window
{
    private readonly IConnectionProfileService connectionProfileService;
    private readonly IReadOnlyList<DeviceProfile> devices;
    private readonly ObservableCollection<ConnectionProfile> profiles;

    public VncConnectionDialog()
        : this(new ConnectionProfileService(), [], [])
    {
    }

    public VncConnectionDialog(
        IConnectionProfileService connectionProfileService,
        IReadOnlyList<DeviceProfile> devices,
        IEnumerable<ConnectionProfile> profiles)
    {
        InitializeComponent();
        this.connectionProfileService = connectionProfileService;
        this.devices = devices;
        this.profiles = new ObservableCollection<ConnectionProfile>(profiles);

        ProfilesList.ItemsSource = this.profiles;
        DeviceBox.ItemsSource = devices;
        DeviceBox.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(DeviceProfile.Name));
        ScaleBox.ItemsSource = new[] { 1d, 2d, 3d, 4d };

        ProfilesList.SelectedIndex = this.profiles.Count > 0 ? 0 : -1;
        if (ProfilesList.SelectedItem is not ConnectionProfile)
        {
            LoadProfile(new ConnectionProfile());
        }
    }

    public ConnectionProfile? SelectedConnection { get; private set; }
    public IReadOnlyList<ConnectionProfile> Profiles => profiles;

    private void OnProfileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProfilesList.SelectedItem is ConnectionProfile profile)
        {
            LoadProfile(profile);
        }
    }

    private async void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var profile = ProfilesList.SelectedItem as ConnectionProfile ?? new ConnectionProfile();
        ApplyForm(profile);
        if (!profiles.Contains(profile))
        {
            profiles.Add(profile);
            ProfilesList.SelectedItem = profile;
        }

        await connectionProfileService.SaveAsync(profiles);
    }

    private void OnNewClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var profile = new ConnectionProfile { Name = "New Connection" };
        profiles.Add(profile);
        ProfilesList.SelectedItem = profile;
    }

    private async void OnDeleteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ProfilesList.SelectedItem is not ConnectionProfile profile)
        {
            return;
        }

        profiles.Remove(profile);
        ProfilesList.SelectedIndex = profiles.Count > 0 ? 0 : -1;
        await connectionProfileService.SaveAsync(profiles);
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(null);

    private async void OnConnectClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var profile = ProfilesList.SelectedItem as ConnectionProfile ?? new ConnectionProfile();
        ApplyForm(profile);
        if (!profiles.Contains(profile))
        {
            profiles.Add(profile);
        }

        await connectionProfileService.SaveAsync(profiles);
        SelectedConnection = profile;
        Close(profile);
    }

    private void LoadProfile(ConnectionProfile profile)
    {
        NameBox.Text = profile.Name;
        HostBox.Text = profile.Host;
        PortBox.Value = profile.Port;
        PasswordBox.Text = profile.Password;
        ScaleBox.SelectedItem = profile.Scale <= 0 ? 1d : profile.Scale;
        DeviceBox.SelectedItem = devices.FirstOrDefault(device => device.Id == profile.DeviceId) ?? devices.FirstOrDefault();
    }

    private void ApplyForm(ConnectionProfile profile)
    {
        profile.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Untitled Connection" : NameBox.Text.Trim();
        profile.Host = string.IsNullOrWhiteSpace(HostBox.Text) ? "127.0.0.1" : HostBox.Text.Trim();
        profile.Port = (int)(PortBox.Value ?? 5910);
        profile.Password = PasswordBox.Text ?? string.Empty;
        profile.DeviceId = (DeviceBox.SelectedItem as DeviceProfile)?.Id ?? "cardputer-zero";
        profile.Scale = ScaleBox.SelectedItem is double scale ? scale : 1;
    }
}
