using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;

namespace SyncDeck.Views;

public partial class MainWindow : Window
{
    private ViewModels.MainViewModel? _vm;
    private bool  _isDragging;
    private Point _dragStart;   // logical coords, not pixel

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

        if (Transport is not null)
            Transport.SeekRequested += async (_, secs) => await _vm.EndSeekAsync(secs);
    }

    // ── Window drag — use logical delta × RenderScaling ───────────────────────

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _isDragging = true;
        _dragStart  = e.GetPosition(this);  // logical px relative to window
    }

    private void OnTitleBarMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;
        var current = e.GetPosition(this);
        var delta   = current - _dragStart;
        var scale   = RenderScaling;
        Position = new PixelPoint(
            Position.X + (int)(delta.X * scale),
            Position.Y + (int)(delta.Y * scale));
        // NOTE: don't reset _dragStart — delta stays relative to the press point,
        // which gives correct absolute dragging behaviour.
    }

    private void OnTitleBarReleased(object? sender, PointerReleasedEventArgs e)
        => _isDragging = false;

    private void ApplyTransparency(bool enabled)
    {
        TransparencyLevelHint = enabled
            ? new[] { WindowTransparencyLevel.AcrylicBlur }
            : new[] { WindowTransparencyLevel.None };
    }
}
