using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PocketFrame.App.Models;
using PocketFrame.App.ViewModels;

namespace PocketFrame.App.Views;

public partial class DeviceShellView : UserControl
{
    private static readonly IBrush BackdropBrush = Brush.Parse("#06223a");
    private static readonly IBrush ShellShadowBrush = Brush.Parse("#505860");
    private static readonly IBrush ShellBrush = Brush.Parse("#dfe1e3");
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
    private const double DeviceWidth = 800;
    private const double DeviceHeight = 520;
    private const double ScreenFrameX = 180;
    private const double ScreenFrameY = 26;
    private const double ScreenFrameWidth = 374;
    private const double ScreenFrameHeight = 202;
    private const double RightPanelX = 592;
    private const double RightPanelY = 24;
    private const double RightPanelWidth = 166;
    private const double RightPanelHeight = 226;
    private const double KeyboardDeckX = 18;
    private const double KeyboardDeckY = 240;
    private const double KeyboardDeckWidth = 740;
    private const double KeyboardDeckHeight = 242;
    private const double KeyUnitWidth = 54;
    private const double KeyGap = 8;
    private const double KeyAreaHeight = 42;
    private const double KeyboardRowStartX = 36;
    private const double KeyboardRow1Y = 254;
    private const double KeyboardRowGapY = 54;

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
            AddLeftPanel(canvas);
            AddRightPanel(canvas);
            AddScreen(canvas, profile, viewModel);
            AddKeyboardDeck(canvas);
            AddKeyboardVisuals(canvas);
            AddKeyboardHitAreas(canvas, viewModel);
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

    private static void AddShellBody(Canvas canvas)
    {
        AddRoundedRect(canvas, 12, 16, DeviceWidth - 20, DeviceHeight - 28, 34, ShellShadowBrush);
        AddRoundedRect(canvas, 0, 0, DeviceWidth - 20, DeviceHeight - 28, 34, ShellBrush);
        AddRoundedRect(canvas, 6, 6, DeviceWidth - 32, 14, 7, Brushes.White);
        AddRoundedRect(canvas, 8, 8, DeviceWidth - 36, DeviceHeight - 44, 30, ShellBrush);
        AddRect(canvas, 12, 12, DeviceWidth - 44, DeviceHeight - 52, Brushes.Transparent, ShellBorderBrush, 1);
    }

    private static void AddLeftPanel(Canvas canvas)
    {
        AddRoundedRect(canvas, 24, 24, 144, 226, 20, InsetBrush);
        AddText(canvas, "M5", 38, 40, 76, Brush.Parse("#c6c9cc"), FontWeight.Light);
        AddRoundedRect(canvas, 24, 110, 132, 88, 10, StickerWhiteBrush);
        AddCornerMarkers(canvas, 34, 120, 132, 76);
        AddText(canvas, "CARDPUTER", 34, 120, 18, Brush.Parse("#4a4a4c"), FontWeight.Medium);
        AddText(canvas, "ZERO", 36, 141, 42, Brush.Parse("#222224"), FontWeight.Bold);
        AddRoundedRect(canvas, 34, 173, 62, 25, 2, LabelDarkBrush);
        AddText(canvas, "Hold to", 38, 172, 15, Brushes.White, FontWeight.Bold);
        AddText(canvas, "TALK", 38, 187, 15, Brushes.White, FontWeight.Bold);
        AddText(canvas, "♩", 106, 178, 24, AccentOrangeBrush, FontWeight.Bold);
        AddRoundedRect(canvas, 40, 202, 38, 16, 8, LabelDarkBrush);
        AddRoundedRect(canvas, 106, 202, 16, 16, 8, Brush.Parse("#cacccd"));
        AddCircle(canvas, 130, 196, 36, Brush.Parse("#d5d7d9"), ShellBorderBrush);
    }

    private static void AddRightPanel(Canvas canvas)
    {
        AddRoundedRect(canvas, RightPanelX, RightPanelY, RightPanelWidth, RightPanelHeight, 18, InsetBrush);
        AddRoundedRect(canvas, 606, 52, 58, 34, 10, PowerRedBrush);
        AddRoundedRect(canvas, 668, 58, 52, 26, 13, AccentOrangeBrush);
        AddText(canvas, "ON", 696, 61, 14, Brushes.White, FontWeight.Bold);
        AddText(canvas, "POWER", 606, 100, 25, LabelDarkBrush, FontWeight.Bold);
        AddText(canvas, "SWITCH", 612, 125, 13, LabelDarkBrush, FontWeight.Bold);
        AddCircle(canvas, 610, 150, 34, Brush.Parse("#c8cacc"), ShellBorderBrush);
        AddText(canvas, "USB-C", 656, 131, 12, LabelDarkBrush, FontWeight.Bold);
        AddText(canvas, "LINUX", 656, 145, 13, AccentBlueBrush, FontWeight.Bold);
        AddText(canvas, "CARDPUTER ZERO", 628, 160, 10, AccentPinkBrush, FontWeight.Bold);
        AddRoundedRect(canvas, 614, 185, 62, 22, 10, KeyBodyBrush);
        AddRoundedRect(canvas, 684, 185, 62, 22, 10, KeyBodyBrush);
        AddText(canvas, "HOME", 622, 213, 14, LabelDarkBrush, FontWeight.Bold);
        AddText(canvas, "NEXT", 693, 213, 14, LabelDarkBrush, FontWeight.Bold);
    }

    private static void AddScreen(Canvas canvas, DeviceProfile profile, DeviceShellViewModel viewModel)
    {
        AddRoundedRect(canvas, ScreenFrameX, ScreenFrameY, ScreenFrameWidth, ScreenFrameHeight, 18, ScreenFrameBrush);
        AddRoundedRect(canvas, ScreenFrameX + 9, ScreenFrameY + 11, ScreenFrameWidth - 18, ScreenFrameHeight - 20, 12, ScreenLipBrush);
        AddRect(canvas, profile.ScreenX - 6, profile.ScreenY - 6, profile.ScreenWidth + 12, profile.ScreenHeight + 12, Brushes.Black);

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
        AddRoundedRect(canvas, KeyboardDeckX, KeyboardDeckY, KeyboardDeckWidth, KeyboardDeckHeight, 18, Brush.Parse("#e6e8ea"));
        AddRect(canvas, 36, 306, KeyboardDeckWidth - 36, 2, Brush.Parse("#cdcfd1"));
        AddRect(canvas, 36, 360, KeyboardDeckWidth - 36, 2, Brush.Parse("#cdcfd1"));
        AddRect(canvas, 36, 414, KeyboardDeckWidth - 36, 2, Brush.Parse("#cdcfd1"));
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
            await viewModel.SendButtonAsync(button);
            _ = RemoveFeedbackLater(canvas, feedback, TimeSpan.FromMilliseconds(140));
        };

        hitArea.PointerReleased += (_, args) =>
        {
            args.Handled = true;
            if (!isStickyLongPress)
            {
                RemoveFeedback(canvas, feedback);
                feedback = null;
            }
        };

        hitArea.PointerCaptureLost += (_, _) =>
        {
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
