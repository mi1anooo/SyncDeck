using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;

namespace SyncDeck.Views;

public partial class MainWindow : Window
{
    private ViewModels.MainViewModel? _vm;
    private bool       _isDragging;
    private PixelPoint _dragStart;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _vm = DataContext as ViewModels.MainViewModel;
        if (_vm is null) return;

        _vm.Settings.AlwaysOnTopChanged  += (_, v) => Topmost = v;
        _vm.Settings.TransparencyChanged += (_, v) => ApplyTransparency(v);

        Topmost = _vm.Settings.AlwaysOnTop;
        ApplyTransparency(_vm.Settings.Transparent);

        if (TitleBar is not null)
        {
            TitleBar.PointerPressed  += OnTitleBarPressed;
            TitleBar.PointerMoved    += OnTitleBarMoved;
            TitleBar.PointerReleased += OnTitleBarReleased;

            TitleBar.MinimizeClicked += (_, _) => WindowState = WindowState.Minimized;
            TitleBar.MaximizeClicked += (_, _) =>
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal : WindowState.Maximized;
            TitleBar.CloseClicked    += (_, _) => Close();
            TitleBar.SettingsClicked += (_, _) => _vm.ToggleSettingsCommand.Execute(null);
        }

        // Wire transport seek from drag handle
        if (Transport is not null)
        {
            Transport.SeekRequested += async (_, secs) => await _vm.EndSeekAsync(secs);
        }
    }

    // ── Window dragging via title bar ─────────────────────────────────────────

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _isDragging = true;
        var pos = e.GetPosition(this);
        _dragStart = new PixelPoint((int)pos.X, (int)pos.Y);
    }

    private void OnTitleBarMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;
        var pos       = e.GetPosition(this);
        var screenPos = PointToScreen(new Point(pos.X - _dragStart.X, pos.Y - _dragStart.Y));
        Position = screenPos;
    }

    private void OnTitleBarReleased(object? sender, PointerReleasedEventArgs e)
        => _isDragging = false;

    // ── Transparency ──────────────────────────────────────────────────────────

    private void ApplyTransparency(bool enabled)
    {
        TransparencyLevelHint = enabled
            ? new[] { WindowTransparencyLevel.AcrylicBlur }
            : new[] { WindowTransparencyLevel.None };
    }
}
