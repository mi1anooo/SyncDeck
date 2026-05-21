using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;

namespace SyncDeck.Views;

public partial class MainWindow : Window
{
    private ViewModels.MainViewModel? _vm;
    private bool  _isDragging;
    private Point _dragStart;

    // Breakpoint: below this width switch to compact layout
    private const double CompactBreakpoint = 580;

    public MainWindow()
    {
        InitializeComponent();
        Opened      += OnOpened;
        SizeChanged += OnSizeChanged;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _vm = DataContext as ViewModels.MainViewModel;
        if (_vm is null) return;

        _vm.Settings.AlwaysOnTopChanged  += (_, v) => Topmost = v;
        _vm.Settings.TransparencyChanged += (_, v) => ApplyTransparency(v);

        Topmost = _vm.Settings.AlwaysOnTop;
        ApplyTransparency(_vm.Settings.Transparent);

        TitleBar.PointerPressed  += OnTitleBarPressed;
        TitleBar.PointerMoved    += OnTitleBarMoved;
        TitleBar.PointerReleased += OnTitleBarReleased;

        TitleBar.MinimizeClicked += (_, _) => WindowState = WindowState.Minimized;
        TitleBar.MaximizeClicked += (_, _) =>
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal : WindowState.Maximized;
        TitleBar.CloseClicked    += (_, _) => Close();
        TitleBar.SettingsClicked += (_, _) => _vm.ToggleSettingsCommand.Execute(null);

        if (Transport is not null)
            Transport.SeekRequested += async (_, secs) => await _vm.EndSeekAsync(secs);

        // Apply initial layout
        ApplyLayout(Bounds.Width);
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        => ApplyLayout(e.NewSize.Width);

    private void ApplyLayout(double width)
    {
        var compact = width < CompactBreakpoint;
        LandscapeLayout.IsVisible = !compact;
        CompactLayout.IsVisible   =  compact;

        // Adjust min height for compact mode
        MinHeight = compact ? 560 : 410;
    }

    // ── Drag ─────────────────────────────────────────────────────────────────

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _isDragging = true;
        _dragStart  = e.GetPosition(this);
    }

    private void OnTitleBarMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;
        var delta = e.GetPosition(this) - _dragStart;
        var scale = RenderScaling;
        Position = new PixelPoint(
            Position.X + (int)(delta.X * scale),
            Position.Y + (int)(delta.Y * scale));
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
