using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PocketFrame.App.ViewModels;
using PocketFrame.DeviceProfiles;

namespace PocketFrame.App.Views;

public partial class DeviceShellView : UserControl
{
    private static readonly IBrush BackdropBrush = Brush.Parse("#f8f8f8");
    private static readonly IBrush ShellShadowBrush = Brush.Parse("#505860");
    private static readonly IBrush ShellBrush = Brush.Parse("#d9dbdc");
    private static readonly IBrush ShellBorderBrush = Brush.Parse("#bcbfc2");
    private static readonly IBrush InsetBrush = Brush.Parse("#d2d4d6");
    private static readonly IBrush ScreenFrameBrush = Brush.Parse("#121418");
    private static readonly IBrush ScreenLipBrush = Brush.Parse("#202328");
    private static readonly IBrush LabelDarkBrush = Brush.Parse("#282b2f");
    private static readonly IBrush KeyBodyBrush = Brush.Parse("#1a1d21");
    private static readonly IBrush KeyShadowBrush = Brush.Parse("#090b0e");
    private static readonly IBrush PressFeedbackBrush = Brush.Parse("#667d93");
    private static readonly IBrush AccentPinkBrush = Brush.Parse("#dc5b8e");
    private static readonly IBrush AccentBlueBrush = Brush.Parse("#4a9bf0");
    private static readonly IBrush AccentOrangeBrush = Brush.Parse("#f3963f");
    private static readonly IBrush AccentYellowBrush = Brush.Parse("#f5d954");
    private static readonly IBrush StickerWhiteBrush = Brush.Parse("#f8f8f8");
    private static readonly IBrush PowerRedBrush = Brush.Parse("#dd453f");
    private static readonly IBrush ShellGrooveBrush = Brush.Parse("#b7babc");
    private static readonly IBrush PrintedGreyBrush = Brush.Parse("#c4c7c9");
    private const double CardputerDeviceWidthMm = 85.0;
    private const double CardputerDeviceHeightMm = 54.5;
    private const double CardputerScreenWidthMm = 42.6;
    private const double CardputerDipPerMm = 320.0 / CardputerScreenWidthMm;
    private static readonly double DeviceWidth = Mm(CardputerDeviceWidthMm);
    private static readonly double DeviceHeight = Mm(CardputerDeviceHeightMm);
    private static readonly double KeyboardDeckX = Mm(2.0);
    private static readonly double KeyboardDeckY = Mm(27.4);
    private static readonly double KeyboardDeckWidth = Mm(81.0);
    private static readonly double KeyboardDeckHeight = Mm(22.6);
    private static readonly double KeyUnitWidth = Mm(5.6);
    private static readonly double KeyGap = Mm(1.5);
    private static readonly double KeyAreaHeight = Mm(4.4);
    private static readonly double KeyboardRowStartX = Mm(3.0);
    private static readonly double KeyboardRow1Y = Mm(30.2);
    private static readonly double KeyboardRowGapY = Mm(5.0);

    public DeviceShellView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachViewModel();
        AddHandler(PointerPressedEvent, (_, _) => Focus(NavigationMethod.Pointer), RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void AttachViewModel()
    {
        if (DataContext is DeviceShellViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            RenderShell(viewModel);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is DeviceShellViewModel viewModel && (e.PropertyName is nameof(DeviceShellViewModel.Profile) or nameof(DeviceShellViewModel.DisplayScale)))
        {
            RenderShell(viewModel);
        }
    }

    private void RenderShell(DeviceShellViewModel viewModel)
    {
        if (viewModel.Profile is null)
        {
            ShellHost.Content = null;
            return;
        }

        var profile = viewModel.Profile;
        var scale = viewModel.DisplayScale;
        var effectiveScale = GetDevicePixelScale(scale);
        var canvas = new Canvas
        {
            Width = profile.ShellWidth,
            Height = profile.ShellHeight
        };

        if (profile.Id.Equals("uconsole", StringComparison.OrdinalIgnoreCase))
        {
            AddUConsoleShell(canvas, profile, viewModel);
        }
        else
        {
            canvas.Background = BackdropBrush;
            AddShellBody(canvas);
            AddKeyboardDeck(canvas);
            AddLeftPanel(canvas);
            AddRightPanel(canvas);
            AddShellAnnotations(canvas, profile);
            AddScreen(canvas, profile, viewModel);
            if (profile.Keyboard.Keys.Count > 0)
            {
                AddProfileKeyboard(canvas, profile, viewModel);
            }
            else
            {
                AddKeyboardVisuals(canvas);
                AddKeyboardHitAreas(canvas, viewModel);
            }
            AddButtons(canvas, profile, viewModel);
        }

        var scaledWidth = profile.ShellWidth * effectiveScale;
        var scaledHeight = profile.ShellHeight * effectiveScale;
        var viewbox = new Viewbox
        {
            Width = scaledWidth,
            Height = scaledHeight,
            Stretch = Stretch.Fill,
            Child = canvas
        };

        ShellHost.Width = scaledWidth;
        ShellHost.Height = scaledHeight;
        ShellHost.Content = viewbox;
    }

    private double GetDevicePixelScale(double requestedScale)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var renderScaling = topLevel?.RenderScaling ?? 1;
        return requestedScale / Math.Max(renderScaling, 0.01);
    }

    private static double Mm(double value) => value * CardputerDipPerMm;

    private static void AddShellBody(Canvas canvas)
    {
        AddRoundedRect(canvas, Mm(1.6), Mm(1.8), DeviceWidth - Mm(1.0), DeviceHeight - Mm(1.0), Mm(3.2), Brush.Parse("#9ca2a4"));
        AddRoundedRect(canvas, 0, 0, DeviceWidth - Mm(1.6), DeviceHeight - Mm(1.8), Mm(3.0), ShellBrush);
        AddRect(canvas, Mm(1.6), Mm(0.9), DeviceWidth - Mm(8.0), Mm(0.18), Brush.Parse("#fbfbf8"));
        AddRect(canvas, Mm(1.2), Mm(1.2), DeviceWidth - Mm(4.5), DeviceHeight - Mm(3.8), Brushes.Transparent, ShellBorderBrush, 1);
        AddRect(canvas, Mm(39.0), 0, Mm(14.0), Mm(0.25), Brush.Parse("#151515"));
        AddRect(canvas, 0, 0, Mm(2.0), Mm(2.0), BackdropBrush);
        AddRect(canvas, Mm(18.2), 0, Mm(1.2), Mm(2.4), BackdropBrush);
        AddRect(canvas, DeviceWidth - Mm(2.4), 0, Mm(2.4), Mm(2.0), BackdropBrush);
        AddRect(canvas, 0, DeviceHeight - Mm(4.8), Mm(1.8), Mm(4.8), BackdropBrush);
        AddRect(canvas, DeviceWidth - Mm(3.6), DeviceHeight - Mm(5.2), Mm(3.6), Mm(5.2), BackdropBrush);
    }


    private static void AddLeftPanel(Canvas canvas)
    {
        AddRoundedRect(canvas, Mm(1.6), Mm(2.2), Mm(17.0), Mm(24.2), Mm(2.0), InsetBrush);
        AddText(canvas, "M5", Mm(3.0), Mm(3.2), Mm(7.6), PrintedGreyBrush, FontWeight.Light);
        AddRect(canvas, Mm(18.5), Mm(2.5), Mm(1.2), Mm(23.5), ShellGrooveBrush);
        AddRect(canvas, Mm(18.1), Mm(2.5), Mm(0.3), Mm(23.5), Brush.Parse("#f3f3ef"));
        AddRoundedRect(canvas, Mm(3.0), Mm(11.9), Mm(13.5), Mm(9.5), Mm(1.0), StickerWhiteBrush);
        AddCornerMarkers(canvas, Mm(3.4), Mm(12.3), Mm(12.7), Mm(8.7));
        AddText(canvas, "CARDPUTER", Mm(4.0), Mm(12.8), Mm(1.55), Brush.Parse("#55585a"), FontWeight.Medium);
        AddText(canvas, "ZERO", Mm(4.0), Mm(14.7), Mm(3.8), Brush.Parse("#202020"), FontWeight.Bold);
        AddRoundedRect(canvas, Mm(3.2), Mm(21.2), Mm(8.5), Mm(3.4), Mm(0.2), LabelDarkBrush);
        AddText(canvas, "Hold to", Mm(3.55), Mm(21.15), Mm(1.45), Brushes.White, FontWeight.Bold);
        AddText(canvas, "TALK", Mm(3.55), Mm(22.55), Mm(1.55), Brushes.White, FontWeight.Bold);
        AddText(canvas, "MIC", Mm(12.2), Mm(21.8), Mm(1.3), AccentOrangeBrush, FontWeight.Bold);
        AddCircle(canvas, Mm(11.8), Mm(20.6), Mm(1.0), AccentOrangeBrush);
        AddRect(canvas, Mm(12.0), Mm(21.4), Mm(0.5), Mm(1.4), AccentOrangeBrush);
        AddRoundedRect(canvas, Mm(4.5), Mm(25.2), Mm(4.5), Mm(2.3), Mm(1.1), KeyBodyBrush);
        AddRect(canvas, Mm(10.3), Mm(24.7), Mm(0.8), Mm(3.0), Brush.Parse("#202020"));
        AddCircle(canvas, Mm(13.1), Mm(24.2), Mm(3.2), Brush.Parse("#cfd1d2"), ShellBorderBrush);
        AddText(canvas, "/", Mm(14.1), Mm(24.55), Mm(1.8), ShellGrooveBrush, FontWeight.Bold);
    }


    private static void AddRightPanel(Canvas canvas)
    {
        AddRoundedRect(canvas, Mm(69.0), Mm(3.0), Mm(13.5), Mm(25.0), Mm(1.5), InsetBrush);
        AddRect(canvas, Mm(65.2), Mm(2.5), Mm(1.2), Mm(23.5), ShellGrooveBrush);
        AddRect(canvas, Mm(67.0), Mm(3.0), Mm(0.25), Mm(23.0), Brush.Parse("#f3f3ef"));
        AddRect(canvas, Mm(82.0), Mm(3.1), Mm(0.25), Mm(21.0), ShellGrooveBrush);

        AddRoundedRect(canvas, Mm(70.5), Mm(5.0), Mm(5.5), Mm(3.5), Mm(0.8), PowerRedBrush);
        AddRoundedRect(canvas, Mm(70.85), Mm(5.25), Mm(4.3), Mm(0.45), Mm(0.2), Brush.Parse("#f06a64"));
        AddRoundedRect(canvas, Mm(75.1), Mm(5.55), Mm(1.4), Mm(2.5), Mm(0.4), Brush.Parse("#b53030"));
        AddRoundedRect(canvas, Mm(78.0), Mm(5.8), Mm(3.9), Mm(2.8), Mm(1.4), AccentOrangeBrush);
        AddText(canvas, ">", Mm(78.7), Mm(6.05), Mm(1.15), Brushes.White, FontWeight.Bold);
        AddText(canvas, "ON", Mm(80.2), Mm(6.1), Mm(1.15), Brushes.White, FontWeight.Bold);
        AddText(canvas, "POWER", Mm(70.5), Mm(9.7), Mm(2.55), LabelDarkBrush, FontWeight.Bold);
        AddText(canvas, "SWITCH", Mm(70.7), Mm(12.25), Mm(1.25), LabelDarkBrush, FontWeight.Bold);

        AddCircle(canvas, Mm(70.2), Mm(15.2), Mm(3.3), Brush.Parse("#d1d3d4"), Brush.Parse("#7e8285"));
        AddText(canvas, "USB-C", Mm(76.0), Mm(13.9), Mm(1.15), LabelDarkBrush, FontWeight.Bold);
        AddText(canvas, "PORT", Mm(76.2), Mm(15.15), Mm(1.0), LabelDarkBrush, FontWeight.Bold);
        AddText(canvas, "RIGHT", Mm(76.0), Mm(16.2), Mm(1.35), Brush.Parse("#31a56a"), FontWeight.Bold);
        AddRect(canvas, Mm(80.4), Mm(15.0), Mm(1.8), Mm(3.2), Brushes.White, Brush.Parse("#5b5e60"), 2);
        AddCircle(canvas, Mm(81.15), Mm(15.7), Mm(0.32), AccentOrangeBrush);
        AddCircle(canvas, Mm(81.15), Mm(17.1), Mm(0.32), AccentOrangeBrush);
        AddText(canvas, "5V", Mm(72.1), Mm(18.3), Mm(1.9), Brush.Parse("#ee4b28"), FontWeight.Bold);
        AddText(canvas, "ONLY", Mm(73.0), Mm(20.3), Mm(0.85), LabelDarkBrush, FontWeight.Bold);
        AddText(canvas, "CHG", Mm(77.3), Mm(18.55), Mm(1.45), LabelDarkBrush, FontWeight.Bold);
        AddText(canvas, "<", Mm(81.2), Mm(18.55), Mm(1.65), AccentOrangeBrush, FontWeight.Bold);

        AddRoundedRect(canvas, Mm(69.5), Mm(22.4), Mm(12.7), Mm(4.4), Mm(0.15), Brushes.Black);
        AddRoundedRect(canvas, Mm(71.5), Mm(23.15), Mm(5.8), Mm(0.9), Mm(0.45), Brushes.White);
        AddCircle(canvas, Mm(78.1), Mm(23.1), Mm(0.7), Brushes.White);
        AddText(canvas, "HOME", Mm(70.4), Mm(24.6), Mm(1.55), Brushes.White, FontWeight.Bold);
        AddText(canvas, "NEXT", Mm(76.7), Mm(24.6), Mm(1.55), Brushes.White, FontWeight.Bold);
        AddRoundedRect(canvas, Mm(75.8), Mm(27.5), Mm(4.7), Mm(2.3), Mm(1.1), KeyBodyBrush);
    }


    private static void AddScreen(Canvas canvas, DeviceProfile profile, DeviceShellViewModel viewModel)
    {
        AddRect(canvas, Mm(18.5), Mm(2.5), Mm(1.2), Mm(23.5), ShellGrooveBrush);
        AddRect(canvas, Mm(65.2), Mm(2.5), Mm(1.2), Mm(23.5), ShellGrooveBrush);
        AddRoundedRect(canvas, profile.ScreenX - Mm(0.8), profile.ScreenY - Mm(0.8), profile.ScreenWidth + Mm(1.6), profile.ScreenHeight + Mm(1.6), Mm(0.8), Brush.Parse("#0a0b0d"));
        AddRoundedRect(canvas, profile.ScreenX - Mm(0.25), profile.ScreenY - Mm(0.25), profile.ScreenWidth + Mm(0.5), profile.ScreenHeight + Mm(0.5), Mm(0.45), ScreenLipBrush);

        var screen = new ScreenViewport
        {
            Width = profile.ScreenWidth,
            Height = profile.ScreenHeight,
            DataContext = viewModel
        };
        Canvas.SetLeft(screen, profile.ScreenX);
        Canvas.SetTop(screen, profile.ScreenY);
        canvas.Children.Add(screen);
    }


    private static void AddKeyboardDeck(Canvas canvas)
    {
        AddRoundedRect(canvas, KeyboardDeckX, KeyboardDeckY, KeyboardDeckWidth, KeyboardDeckHeight, Mm(1.8), Brush.Parse("#e6e8ea"));
        AddRect(canvas, Mm(3.0), Mm(29.6), Mm(79.0), Mm(3.6), Brush.Parse("#eeeeed"));
        AddRect(canvas, Mm(3.0), Mm(34.6), Mm(79.0), Mm(3.6), Brush.Parse("#e2e2e0"));
        AddRect(canvas, Mm(3.0), Mm(39.6), Mm(79.0), Mm(3.6), Brush.Parse("#eeeeed"));
        AddRect(canvas, Mm(3.0), Mm(44.6), Mm(79.0), Mm(3.6), Brush.Parse("#e2e2e0"));
        AddRect(canvas, Mm(3.0), Mm(33.2), Mm(79.0), 1, Brush.Parse("#c9cbcc"));
        AddRect(canvas, Mm(3.0), Mm(38.2), Mm(79.0), 1, Brush.Parse("#c9cbcc"));
        AddRect(canvas, Mm(3.0), Mm(43.2), Mm(79.0), 1, Brush.Parse("#c9cbcc"));
    }

    private static void AddProfileKeyboard(Canvas canvas, DeviceProfile profile, DeviceShellViewModel viewModel)
    {
        foreach (var key in profile.Keyboard.Keys)
        {
            var fill = string.IsNullOrWhiteSpace(key.Fill) ? KeyBodyBrush : Brush.Parse(key.Fill);
            if (key.Role.Equals("layer", StringComparison.OrdinalIgnoreCase))
            {
                AddRoundedRect(canvas, key.X, key.Y + Mm(0.15), key.Width, Mm(2.2), 0, fill);
                var layerTextBrush = key.Label.Equals("ctrl", StringComparison.OrdinalIgnoreCase) || key.Label.Equals("alt", StringComparison.OrdinalIgnoreCase)
                    ? LabelDarkBrush
                    : Brushes.White;
                AddText(canvas, key.Label, key.X + Mm(0.45), key.Y + Mm(0.15), Mm(1.85), layerTextBrush, FontWeight.Bold);
            }
            else
            {
                var labelBrush = key.Id.Equals("key-ok", StringComparison.OrdinalIgnoreCase)
                    ? Brush.Parse("#159253")
                    : LabelDarkBrush;
                var labelSize = key.Id.Equals("key-del", StringComparison.OrdinalIgnoreCase)
                    ? Mm(3.2)
                    : key.Label.Length > 1 ? Mm(1.45) : Mm(2.55);
                var labelX = key.Id.Equals("key-del", StringComparison.OrdinalIgnoreCase)
                    ? key.X + key.Width * 0.26
                    : key.X + key.Width * 0.36;
                AddText(canvas, key.Label, labelX, key.Y - Mm(0.08), labelSize, labelBrush, FontWeight.Bold);
                if (key.Id.Equals("key-del", StringComparison.OrdinalIgnoreCase))
                {
                    AddText(canvas, key.Label, labelX - Mm(0.08), key.Y - Mm(0.08), labelSize, labelBrush, FontWeight.Bold);
                }
            }

            foreach (var legend in key.Legends)
            {
                AddKeyLegend(canvas, key, legend);
            }

            var buttonX = key.X + key.Width * 0.08;
            var buttonY = key.Y + key.Height * 0.55;
            var buttonWidth = key.Width * 0.84;
            var buttonHeight = key.Height * 0.38;
            AddRoundedRect(canvas, buttonX + Mm(0.18), buttonY + Mm(0.25), buttonWidth, buttonHeight, buttonHeight / 2, KeyShadowBrush);
            AddRoundedRect(canvas, buttonX, buttonY, buttonWidth, buttonHeight, buttonHeight / 2, KeyBodyBrush);
            AddRoundedRect(canvas, buttonX + Mm(0.35), buttonY + Mm(0.25), buttonWidth - Mm(0.7), Mm(0.22), Mm(0.1), Brush.Parse("#3a3d40"));

            var button = new ButtonProfile
            {
                Id = key.Id,
                Label = key.Label,
                KeyCode = key.KeyCode,
                FnKeyCode = key.FnKeyCode,
                OrangeKeyCode = key.FnKeyCode,
                SymKeyCode = key.SymKeyCode,
                BlueKeyCode = key.SymKeyCode,
                ShiftKeyCode = key.ShiftKeyCode,
                Role = key.Role,
                X = key.X,
                Y = key.Y,
                Width = key.Width,
                Height = key.Height
            };
            var hitArea = new Border
            {
                Width = key.Width,
                Height = key.Height,
                Background = Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            AttachButtonPressHandlers(canvas, hitArea, button, viewModel, 10);
            ToolTip.SetTip(hitArea, $"{key.Id}: {key.KeyCode} fn:{key.FnKeyCode} sym:{key.SymKeyCode} shift:{key.ShiftKeyCode}");
            Canvas.SetLeft(hitArea, key.X);
            Canvas.SetTop(hitArea, key.Y);
            canvas.Children.Add(hitArea);
        }
    }

    private static void AddKeyLegend(Canvas canvas, KeyProfile key, KeyLegendProfile legend)
    {
        var (x, y) = legend.Position switch
        {
            "topLeft" => (key.X + Mm(0.15), key.Y + Mm(0.12)),
            "topCenter" => (key.X + key.Width / 2 - legend.Text.Length * legend.FontSize * 0.22, key.Y + Mm(0.05)),
            "bottomLeft" => (key.X + Mm(0.15), key.Y + key.Height - legend.FontSize - Mm(0.25)),
            "bottomRight" => (key.X + key.Width - Math.Min(key.Width - Mm(0.3), legend.Text.Length * legend.FontSize * 0.52 + Mm(0.4)), key.Y + key.Height - legend.FontSize - Mm(0.25)),
            "center" => (key.X + key.Width / 2 - legend.Text.Length * legend.FontSize * 0.24, key.Y + key.Height / 2 - legend.FontSize / 2),
            _ => (key.X + key.Width - Math.Min(key.Width - Mm(0.3), legend.Text.Length * legend.FontSize * 0.52 + Mm(0.4)), key.Y + Mm(0.12))
        };
        AddText(canvas, legend.Text, x, y, legend.FontSize, Brush.Parse(legend.Color), ParseWeight(legend.Weight));
    }

    private static void AddShellAnnotations(Canvas canvas, DeviceProfile profile)
    {
        foreach (var annotation in profile.ShellAnnotations)
        {
            var fill = Brush.Parse(annotation.Fill);
            var foreground = Brush.Parse(annotation.Foreground);
            var stroke = string.IsNullOrWhiteSpace(annotation.Stroke) ? null : Brush.Parse(annotation.Stroke);
            if (annotation.Kind.Equals("badge", StringComparison.OrdinalIgnoreCase))
            {
                AddRoundedRect(canvas, annotation.X, annotation.Y, annotation.Width, annotation.Height, annotation.Radius, fill);
            }
            else if (annotation.Kind.Equals("outline", StringComparison.OrdinalIgnoreCase))
            {
                AddRect(canvas, annotation.X, annotation.Y, annotation.Width, annotation.Height, Brushes.Transparent, stroke ?? fill, 2);
            }

            AddText(canvas, annotation.Text, annotation.X + 5, annotation.Y + 4, annotation.FontSize, foreground, ParseWeight(annotation.Weight));
        }
    }

    private static void AddKeyboardVisuals(Canvas canvas)
    {
        var row1 = new (string Primary, string Secondary, IBrush SecondaryBrush)[] { ("1", "!", AccentBlueBrush), ("2", "@", AccentBlueBrush), ("3", "#", AccentBlueBrush), ("4", "$", AccentBlueBrush), ("5", "%", AccentBlueBrush), ("6", "^", AccentBlueBrush), ("7", "&", AccentBlueBrush), ("8", "*", AccentBlueBrush), ("9", "(", AccentBlueBrush), ("0", ")", AccentBlueBrush), ("◀", "del", AccentOrangeBrush) };
        var row2 = new (string Primary, string Secondary, IBrush SecondaryBrush)[] { ("tab", "alt", AccentOrangeBrush), ("Q", "~", AccentBlueBrush), ("W", "`", AccentBlueBrush), ("E", "+", AccentBlueBrush), ("R", "-", AccentBlueBrush), ("T", "/", AccentBlueBrush), ("Y", "\\", AccentBlueBrush), ("U", "{", AccentBlueBrush), ("I", "}", AccentBlueBrush), ("O", "[", AccentBlueBrush), ("P", "]", AccentBlueBrush) };
        var row3 = new (string Primary, string Secondary, IBrush SecondaryBrush)[] { ("Aa", "", AccentBlueBrush), ("A", "", AccentBlueBrush), ("S", "", AccentBlueBrush), ("D", "▲", AccentOrangeBrush), ("F", "|", AccentBlueBrush), ("G", "=", AccentBlueBrush), ("H", ":", AccentBlueBrush), ("J", ";", AccentBlueBrush), ("K", "_", AccentBlueBrush), ("L", "?", AccentBlueBrush), ("◀", "ok", LabelDarkBrush) };
        var row4 = new (string Primary, string Secondary, IBrush SecondaryBrush)[] { ("fn", "", AccentOrangeBrush), ("ctrl", "", AccentYellowBrush), ("Z", "◄", AccentOrangeBrush), ("X", "▼", AccentOrangeBrush), ("C", "►", AccentOrangeBrush), ("V", "<", AccentBlueBrush), ("B", ">", AccentBlueBrush), ("N", "'", AccentBlueBrush), ("M", "\"", AccentBlueBrush), (",", ".", AccentBlueBrush), ("▔", "", AccentBlueBrush) };
        AddKeyboardRow(canvas, row1, KeyboardRow1Y);
        AddKeyboardRow(canvas, row2, KeyboardRow1Y + KeyboardRowGapY);
        AddKeyboardRow(canvas, row3, KeyboardRow1Y + KeyboardRowGapY * 2);
        AddKeyboardRow(canvas, row4, KeyboardRow1Y + KeyboardRowGapY * 3);
    }

    private static void AddKeyboardRow(Canvas canvas, IReadOnlyList<(string Primary, string Secondary, IBrush SecondaryBrush)> keys, double y)
    {
        for (var index = 0; index < keys.Count; index++)
        {
            var x = KeyboardRowStartX + index * (KeyUnitWidth + KeyGap);
            var key = keys[index];
            if (key.Primary is "fn" or "Aa")
            {
                AddRoundedRect(canvas, x + 4, y + 1, 42, 17, 0, key.SecondaryBrush);
                AddText(canvas, key.Primary, x + 9, y + 1, 15, Brushes.White, FontWeight.Bold);
            }
            else if (key.Primary == "ctrl")
            {
                AddRoundedRect(canvas, x + 2, y + 1, 48, 17, 0, AccentYellowBrush);
                AddText(canvas, key.Primary, x + 8, y + 2, 13, LabelDarkBrush, FontWeight.Bold);
            }
            else
            {
                AddText(canvas, key.Primary, x + 20 - (key.Primary.Length > 1 ? 10 : 0), y + 1, key.Primary.Length > 1 ? 13 : 22, LabelDarkBrush, FontWeight.Bold);
            }

            if (!string.IsNullOrWhiteSpace(key.Secondary))
            {
                AddText(canvas, key.Secondary, x + 35, y + 5, key.Secondary.Length > 1 ? 11 : 14, key.SecondaryBrush, FontWeight.Bold);
            }

            AddRoundedRect(canvas, x + 2, y + 23, KeyUnitWidth - 4, KeyAreaHeight - 23, 10, KeyShadowBrush);
            AddRoundedRect(canvas, x, y + 20, KeyUnitWidth - 2, KeyAreaHeight - 24, 10, KeyBodyBrush);
        }
    }

    private static void AddKeyboardHitAreas(Canvas canvas, DeviceShellViewModel viewModel)
    {
        var rows = new (string Key, string Blue, string Orange)[][]
        {
            [("1", "!", ""), ("2", "@", ""), ("3", "#", ""), ("4", "$", ""), ("5", "%", ""), ("6", "^", ""), ("7", "&", ""), ("8", "*", ""), ("9", "(", ""), ("0", ")", ""), ("Backspace", "", "Delete")],
            [("Tab", "", "Alt"), ("Q", "~", ""), ("W", "`", ""), ("E", "+", ""), ("R", "-", ""), ("T", "/", ""), ("Y", "\\", ""), ("U", "{", ""), ("I", "}", ""), ("O", "[", ""), ("P", "]", "")],
            [("Blue", "", ""), ("A", "", ""), ("S", "", ""), ("D", "", "Up"), ("F", "|", ""), ("G", "=", ""), ("H", ":", ""), ("J", ";", ""), ("K", "_", ""), ("L", "?", ""), ("Enter", "", "")],
            [("Orange", "", ""), ("Ctrl", "", ""), ("Z", "", "Left"), ("X", "", "Down"), ("C", "", "Right"), ("V", "<", ""), ("B", ">", ""), ("N", "'", ""), ("M", "\"", ""), (",", ".", ""), ("Space", "", "")]
        };

        for (var row = 0; row < rows.Length; row++)
        {
            var y = KeyboardRow1Y + row * KeyboardRowGapY;
            for (var column = 0; column < rows[row].Length; column++)
            {
                var key = rows[row][column];
                var hitArea = new Border
                {
                    Width = KeyUnitWidth,
                    Height = KeyAreaHeight,
                    Background = Brushes.Transparent,
                    Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
                };
                var button = new ButtonProfile
                {
                    Id = $"keyboard-{row}-{column}",
                    Label = key.Key,
                    KeyCode = key.Key,
                    BlueKeyCode = key.Blue,
                    OrangeKeyCode = key.Orange,
                    X = KeyboardRowStartX + column * (KeyUnitWidth + KeyGap),
                    Y = y,
                    Width = KeyUnitWidth,
                    Height = KeyAreaHeight
                };
                AttachButtonPressHandlers(canvas, hitArea, button, viewModel, 10);
                ToolTip.SetTip(hitArea, $"{key.Key} blue:{key.Blue} orange:{key.Orange}");
                Canvas.SetLeft(hitArea, button.X);
                Canvas.SetTop(hitArea, button.Y);
                canvas.Children.Add(hitArea);
            }
        }
    }

    private static void AddUConsoleShell(Canvas canvas, DeviceProfile profile, DeviceShellViewModel viewModel)
    {
        canvas.Background = Brush.Parse("#edf8fa");
        var body = Brush.Parse("#aeb9b8");
        var bodyDark = Brush.Parse("#758484");
        var bodyLight = Brush.Parse("#c8d0cf");
        var key = Brush.Parse("#172122");
        var keyStroke = Brush.Parse("#4d5d5d");
        var label = Brush.Parse("#101616");

        AddRoundedRect(canvas, 90, 92, profile.ShellWidth - 145, profile.ShellHeight - 155, 42, Brush.Parse("#7f8d8d"));
        AddRoundedRect(canvas, 70, 70, profile.ShellWidth - 145, profile.ShellHeight - 165, 42, body);
        AddRoundedRect(canvas, 86, 86, profile.ShellWidth - 177, profile.ShellHeight - 197, 34, bodyLight);
        AddRect(canvas, 122, 142, profile.ShellWidth - 244, 2, Brush.Parse("#dbe2e1"));
        AddRoundedRect(canvas, 260, 118, 184, 42, 18, Brush.Parse("#435050"));
        AddText(canvas, "clockwork", 276, 124, 28, Brushes.White, FontWeight.Bold);
        AddText(canvas, "TF", profile.ShellWidth - 520, 126, 17, label, FontWeight.Normal);
        AddText(canvas, "ON/OFF", profile.ShellWidth - 390, 126, 17, label, FontWeight.Normal);

        AddUConsoleScrew(canvas, 124, 112);
        AddUConsoleScrew(canvas, profile.ShellWidth - 178, 112);
        AddUConsoleScrew(canvas, 124, 1160);
        AddUConsoleScrew(canvas, profile.ShellWidth - 178, 1160);
        AddUConsoleScrew(canvas, 124, profile.ShellHeight - 178);
        AddUConsoleScrew(canvas, profile.ShellWidth - 178, profile.ShellHeight - 178);

        AddRoundedRect(canvas, profile.ScreenX - 38, profile.ScreenY - 38, profile.ScreenWidth + 76, profile.ScreenHeight + 76, 12, bodyDark);
        AddRoundedRect(canvas, profile.ScreenX - 28, profile.ScreenY - 28, profile.ScreenWidth + 56, profile.ScreenHeight + 56, 8, bodyLight);
        AddRect(canvas, profile.ScreenX - 6, profile.ScreenY - 6, profile.ScreenWidth + 12, profile.ScreenHeight + 12, Brushes.Black, Brush.Parse("#0b1111"), 2);
        var screen = new ScreenViewport
        {
            Width = profile.ScreenWidth,
            Height = profile.ScreenHeight,
            DataContext = viewModel
        };
        Canvas.SetLeft(screen, profile.ScreenX);
        Canvas.SetTop(screen, profile.ScreenY);
        canvas.Children.Add(screen);

        AddUConsoleControls(canvas, viewModel, key, keyStroke, bodyLight);
        AddUConsoleKeyboard(canvas, viewModel, key, keyStroke);
    }

    private static void AddUConsoleScrew(Canvas canvas, double x, double y)
    {
        AddCircle(canvas, x, y, 54, Brush.Parse("#718080"), Brush.Parse("#2f3b3b"));
        AddCircle(canvas, x + 14, y + 14, 26, Brush.Parse("#839090"), Brush.Parse("#344141"));
    }

    private static void AddUConsoleControls(Canvas canvas, DeviceShellViewModel viewModel, IBrush key, IBrush keyStroke, IBrush bodyLight)
    {
        const double controlsY = 1188;
        AddRoundedRect(canvas, 300, controlsY + 2, 150, 145, 14, bodyLight);
        AddUConsoleKey(canvas, viewModel, 348, controlsY, 72, 48, "▲", "Up", key, keyStroke);
        AddUConsoleKey(canvas, viewModel, 300, controlsY + 52, 72, 48, "◀", "Left", key, keyStroke);
        AddUConsoleKey(canvas, viewModel, 396, controlsY + 52, 72, 48, "▶", "Right", key, keyStroke);
        AddUConsoleKey(canvas, viewModel, 348, controlsY + 104, 72, 48, "▼", "Down", key, keyStroke);
        AddUConsoleKey(canvas, viewModel, 530, controlsY + 20, 58, 58, "L", "F1", key, keyStroke);
        AddUConsoleKey(canvas, viewModel, 578, controlsY + 102, 58, 58, "R", "F2", key, keyStroke);
        AddRoundedRect(canvas, 845, controlsY + 50, 90, 90, 12, bodyLight);
        AddCircle(canvas, 862, controlsY + 66, 58, key, keyStroke);
        AddCircle(canvas, 878, controlsY + 82, 26, Brush.Parse("#5c6b6b"), Brush.Parse("#0d1515"));
        AddUConsoleKey(canvas, viewModel, 1110, controlsY + 20, 58, 58, "Y", "Y", key, keyStroke);
        AddUConsoleKey(canvas, viewModel, 1180, controlsY + 20, 58, 58, "X", "X", key, keyStroke);
        AddUConsoleKey(canvas, viewModel, 1142, controlsY + 90, 58, 58, "B", "B", key, keyStroke);
        AddUConsoleKey(canvas, viewModel, 1214, controlsY + 90, 58, 58, "A", "A", key, keyStroke);
    }

    private static void AddUConsoleKeyboard(Canvas canvas, DeviceShellViewModel viewModel, IBrush key, IBrush keyStroke)
    {
        const double startX = 220;
        const double startY = 1388;
        const double unit = 94;
        const double gap = 10;
        const double rowGap = 82;
        AddRoundedRect(canvas, startX - 22, startY - 22, 1335, 510, 14, Brush.Parse("#c8d0cf"));

        AddUConsoleKeyboardRow(canvas, viewModel, startX, startY, unit, gap, key, keyStroke,
            [("Esc", "Escape", 124, "lock"), ("Print\nSelect", "F1", 124, ""), ("Pause\nStart", "F2", 124, ""), ("Vol", "F3", 112, "mute"), ("[ {", "[", 96, ""), ("] }", "]", 96, ""), ("/ ?", "/", 108, ""), ("F11\n−", "F11", 104, ""), ("F12\n+", "F12", 104, ""), ("\\ |", "\\", 108, "")]);
        AddUConsoleKeyboardRow(canvas, viewModel, startX + 20, startY + rowGap, unit, gap, key, keyStroke,
            [("` ~", "`", 94, ""), ("1", "1", 94, "F1"), ("2 @", "2", 94, "F2"), ("3 #", "3", 94, "F3"), ("4 $", "4", 94, "F4"), ("5 %", "5", 94, "F5"), ("6 ^", "6", 94, "F6"), ("7 &", "7", 94, "F7"), ("8 *", "8", 94, "F8"), ("9 (", "9", 94, "F9"), ("0 )", "0", 94, "F10"), ("Enter", "Enter", 124, "return")]);
        AddUConsoleKeyboardRow(canvas, viewModel, startX, startY + rowGap * 2, unit, gap, key, keyStroke,
            [("Tab", "Tab", 112, "Caps"), ("Q", "Q", 94, ""), ("W", "W", 94, ""), ("E", "E", 94, ""), ("R", "R", 94, ""), ("T", "T", 94, ""), ("Y", "Y", 94, ""), ("U", "U", 94, "PgUp"), ("I", "I", 94, "Ins"), ("O", "O", 94, ""), ("P", "P", 94, "")]);
        AddUConsoleKeyboardRow(canvas, viewModel, startX, startY + rowGap * 3, unit, gap, key, keyStroke,
            [("\"", "\"", 94, ""), ("A", "A", 94, ""), ("S", "S", 94, ""), ("D", "D", 94, ""), ("F", "F", 94, ""), ("G", "G", 94, ""), ("H", "H", 94, "Home"), ("J", "J", 94, "End"), ("K", "K", 94, "PgDn"), ("L", "L", 94, ""), ("; :", ";", 94, ""), ("Delete\n+Backspace", "Backspace", 142, "Delete")]);
        AddUConsoleKeyboardRow(canvas, viewModel, startX, startY + rowGap * 4, unit, gap, key, keyStroke,
            [("Shift", "Shift", 136, "shift"), ("Z", "Z", 94, ""), ("X", "X", 94, ""), ("C", "C", 94, ""), ("V", "V", 94, ""), ("B", "B", 94, ""), ("N", "N", 94, ""), ("M", "M", 94, ""), (",", ",", 94, "bright−"), (".", ".", 94, "bright+"), ("Shift", "Shift", 136, "shift")]);
        AddUConsoleKeyboardRow(canvas, viewModel, startX + 30, startY + rowGap * 5, unit, gap, key, keyStroke,
            [("Fn", "Fn", 128, "Fn"), ("Ctrl", "Ctrl", 128, ""), ("Alt", "Alt", 128, "Cmd"), ("Space", "Space", 348, "sun"), ("Alt", "Alt", 128, ""), ("Ctrl", "Ctrl", 128, ""), ("Fn", "Fn", 128, "Fn")]);
    }

    private static void AddUConsoleKeyboardRow(
        Canvas canvas,
        DeviceShellViewModel viewModel,
        double x,
        double y,
        double unit,
        double gap,
        IBrush key,
        IBrush keyStroke,
        IReadOnlyList<(string Label, string KeyCode, double Width, string Secondary)> keys)
    {
        var currentX = x;
        foreach (var item in keys)
        {
            AddUConsoleKey(canvas, viewModel, currentX, y, item.Width, 68, item.Label, item.KeyCode, key, keyStroke, item.Secondary);
            currentX += item.Width + gap;
        }
    }

    private static void AddUConsoleKey(Canvas canvas, DeviceShellViewModel viewModel, double x, double y, double width, double height, string label, string keyCode, IBrush fill, IBrush stroke, string secondary = "")
    {
        AddRoundedRect(canvas, x + 5, y + 6, width, height, 9, Brush.Parse("#071010"));
        AddRect(canvas, x, y, width, height, fill, stroke, 2);
        AddText(canvas, label, x + 11, y + 10, label.Length > 8 ? 13 : label.Contains('\n') ? 14 : 24, Brushes.White, FontWeight.Medium);
        if (!string.IsNullOrWhiteSpace(secondary))
        {
            AddText(canvas, secondary, x + width - Math.Min(secondary.Length * 8 + 16, width - 14), y + height - 24, secondary.Length > 4 ? 10 : 14, AccentYellowBrush, FontWeight.Bold);
        }
        var button = new ButtonProfile { Id = $"uconsole-{keyCode}-{x}-{y}", Label = label, KeyCode = keyCode, X = x, Y = y, Width = width, Height = height };
        var hitArea = new Border
        {
            Width = width,
            Height = height,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        AttachButtonPressHandlers(canvas, hitArea, button, viewModel, 8);
        ToolTip.SetTip(hitArea, keyCode);
        Canvas.SetLeft(hitArea, x);
        Canvas.SetTop(hitArea, y);
        canvas.Children.Add(hitArea);
    }

    private static void AddButtons(Canvas canvas, DeviceProfile profile, DeviceShellViewModel viewModel)
    {
        foreach (var button in profile.Buttons)
        {
            var hitArea = new Border
            {
                Width = button.Width,
                Height = button.Height,
                Background = Brushes.Transparent,
                Tag = button,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };
            AttachButtonPressHandlers(canvas, hitArea, button, viewModel, 10);
            ToolTip.SetTip(hitArea, $"{button.Id}: {button.KeyCode}");
            Canvas.SetLeft(hitArea, button.X);
            Canvas.SetTop(hitArea, button.Y);
            canvas.Children.Add(hitArea);
        }
    }

    private static void AttachButtonPressHandlers(Canvas canvas, Border hitArea, ButtonProfile button, DeviceShellViewModel viewModel, double radius)
    {
        Rectangle? feedback = null;
        var isStickyLongPress = false;
        var isPressed = false;
        var isHoldLongPress = false;
        System.Threading.CancellationTokenSource? holdCancellation = null;
        var supportsHoldLongPress = !string.IsNullOrWhiteSpace(button.LongPressKeyCode);

        hitArea.PointerPressed += async (_, args) =>
        {
            args.Handled = true;
            var properties = args.GetCurrentPoint(hitArea).Properties;
            if (properties.IsRightButtonPressed)
            {
                if (isStickyLongPress)
                {
                    await viewModel.EndButtonPressAsync(button);
                    isStickyLongPress = false;
                    RemoveFeedback(canvas, feedback);
                    feedback = null;
                    return;
                }

                feedback = ShowPressFeedback(canvas, button, radius);
                isStickyLongPress = true;
                await viewModel.BeginButtonPressAsync(button);
                return;
            }

            feedback = ShowPressFeedback(canvas, button, radius);
            isPressed = true;
            isHoldLongPress = false;
            if (!supportsHoldLongPress)
            {
                await viewModel.SendButtonAsync(button);
                _ = RemoveFeedbackLater(canvas, feedback, TimeSpan.FromMilliseconds(140));
                return;
            }

            holdCancellation?.Cancel();
            holdCancellation = new System.Threading.CancellationTokenSource();
            var token = holdCancellation.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(420), token);
                    if (token.IsCancellationRequested || !isPressed)
                    {
                        return;
                    }

                    isHoldLongPress = true;
                    await viewModel.BeginButtonPressAsync(button);
                }
                catch (TaskCanceledException)
                {
                }
            }, token);
        };

        hitArea.PointerReleased += async (_, args) =>
        {
            args.Handled = true;
            isPressed = false;
            holdCancellation?.Cancel();
            holdCancellation?.Dispose();
            holdCancellation = null;

            if (!isStickyLongPress)
            {
                if (supportsHoldLongPress)
                {
                    if (isHoldLongPress)
                    {
                        await viewModel.EndButtonPressAsync(button);
                    }
                    else
                    {
                        await viewModel.SendButtonAsync(button);
                        _ = RemoveFeedbackLater(canvas, feedback, TimeSpan.FromMilliseconds(140));
                        return;
                    }
                }

                RemoveFeedback(canvas, feedback);
                feedback = null;
                isHoldLongPress = false;
            }
        };

        hitArea.PointerCaptureLost += async (_, _) =>
        {
            isPressed = false;
            holdCancellation?.Cancel();
            holdCancellation?.Dispose();
            holdCancellation = null;
            if (isHoldLongPress)
            {
                await viewModel.EndButtonPressAsync(button);
                isHoldLongPress = false;
            }

            if (!isStickyLongPress)
            {
                RemoveFeedback(canvas, feedback);
                feedback = null;
            }
        };
    }

    private static Rectangle ShowPressFeedback(Canvas canvas, ButtonProfile button, double radius)
    {
        var feedback = new Rectangle
        {
            Width = button.Width,
            Height = button.Height,
            RadiusX = radius,
            RadiusY = radius,
            Fill = PressFeedbackBrush,
            Opacity = 0.55,
            IsHitTestVisible = false,
            ZIndex = 20
        };
        Canvas.SetLeft(feedback, button.X);
        Canvas.SetTop(feedback, button.Y);
        canvas.Children.Add(feedback);
        return feedback;
    }

    private static async Task RemoveFeedbackLater(Canvas canvas, Rectangle? feedback, TimeSpan delay)
    {
        await Task.Delay(delay);
        RemoveFeedback(canvas, feedback);
    }

    private static void RemoveFeedback(Canvas canvas, Rectangle? feedback)
    {
        if (feedback is not null)
        {
            canvas.Children.Remove(feedback);
        }
    }

    private static void AddRoundedRect(Canvas canvas, double x, double y, double width, double height, double radius, IBrush fill)
    {
        var rect = new Rectangle { Width = width, Height = height, RadiusX = radius, RadiusY = radius, Fill = fill };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        canvas.Children.Add(rect);
    }

    private static void AddRect(Canvas canvas, double x, double y, double width, double height, IBrush fill, IBrush? stroke = null, double strokeThickness = 0)
    {
        var rect = new Rectangle { Width = width, Height = height, Fill = fill, Stroke = stroke, StrokeThickness = strokeThickness };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        canvas.Children.Add(rect);
    }

    private static void AddCircle(Canvas canvas, double x, double y, double size, IBrush fill, IBrush? stroke = null)
    {
        var ellipse = new Ellipse { Width = size, Height = size, Fill = fill, Stroke = stroke, StrokeThickness = stroke is null ? 0 : 2 };
        Canvas.SetLeft(ellipse, x);
        Canvas.SetTop(ellipse, y);
        canvas.Children.Add(ellipse);
    }

    private static void AddText(Canvas canvas, string text, double x, double y, double size, IBrush fill, FontWeight weight)
    {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight, Foreground = fill, FontFamily = FontFamily.Default, LineHeight = size };
        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, y);
        canvas.Children.Add(block);
    }

    private static FontWeight ParseWeight(string weight) =>
        weight.Equals("Normal", StringComparison.OrdinalIgnoreCase) ? FontWeight.Normal :
        weight.Equals("Medium", StringComparison.OrdinalIgnoreCase) ? FontWeight.Medium :
        weight.Equals("Light", StringComparison.OrdinalIgnoreCase) ? FontWeight.Light :
        FontWeight.Bold;

    private static void AddCornerMarkers(Canvas canvas, double x, double y, double width, double height)
    {
        AddRect(canvas, x, y, 10, 3, AccentPinkBrush);
        AddRect(canvas, x, y, 3, 10, AccentPinkBrush);
        AddRect(canvas, x + width - 14, y, 10, 3, AccentPinkBrush);
        AddRect(canvas, x + width - 7, y, 3, 10, AccentPinkBrush);
        AddRect(canvas, x, y + height - 8, 3, 10, AccentPinkBrush);
        AddRect(canvas, x, y + height - 1, 10, 3, AccentPinkBrush);
        AddRect(canvas, x + width - 14, y + height - 1, 10, 3, AccentPinkBrush);
        AddRect(canvas, x + width - 7, y + height - 8, 3, 10, AccentPinkBrush);
    }
}
