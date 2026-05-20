using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Collections;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Windows.Input;

namespace SyncDeck.Views.Controls;

public partial class TransportControl : UserControl
{
    // ── Styled properties ─────────────────────────────────────────────────────

    public static readonly StyledProperty<bool>    IsPlayingProperty =
        AvaloniaProperty.Register<TransportControl, bool>(nameof(IsPlaying));
    public static readonly StyledProperty<double>  PositionProperty =
        AvaloniaProperty.Register<TransportControl, double>(nameof(Position));
    public static readonly StyledProperty<double>  DurationProperty =
        AvaloniaProperty.Register<TransportControl, double>(nameof(Duration), 1.0);
    public static readonly StyledProperty<string>  ElapsedTextProperty =
        AvaloniaProperty.Register<TransportControl, string>(nameof(ElapsedText), "0:00");
    public static readonly StyledProperty<string>  DurationTextProperty =
        AvaloniaProperty.Register<TransportControl, string>(nameof(DurationText), "0:00");
    public static readonly StyledProperty<ICommand?> PlayPauseCommandProperty =
        AvaloniaProperty.Register<TransportControl, ICommand?>(nameof(PlayPauseCommand));
    public static readonly StyledProperty<ICommand?> NextCommandProperty =
        AvaloniaProperty.Register<TransportControl, ICommand?>(nameof(NextCommand));
    public static readonly StyledProperty<ICommand?> PreviousCommandProperty =
        AvaloniaProperty.Register<TransportControl, ICommand?>(nameof(PreviousCommand));

    public bool     IsPlaying        { get => GetValue(IsPlayingProperty);        set => SetValue(IsPlayingProperty, value); }
    public double   Position         { get => GetValue(PositionProperty);         set => SetValue(PositionProperty, value); }
    public double   Duration         { get => GetValue(DurationProperty);         set => SetValue(DurationProperty, value); }
    public string   ElapsedText      { get => GetValue(ElapsedTextProperty);      set => SetValue(ElapsedTextProperty, value); }
    public string   DurationText     { get => GetValue(DurationTextProperty);     set => SetValue(DurationTextProperty, value); }
    public ICommand? PlayPauseCommand { get => GetValue(PlayPauseCommandProperty); set => SetValue(PlayPauseCommandProperty, value); }
    public ICommand? NextCommand      { get => GetValue(NextCommandProperty);      set => SetValue(NextCommandProperty, value); }
    public ICommand? PreviousCommand  { get => GetValue(PreviousCommandProperty);  set => SetValue(PreviousCommandProperty, value); }

    public event EventHandler<double>? SeekRequested;

    private bool   _dragging     = false;
    private double _dragFraction = 0;
    private ProgressBarRenderer? _progressRenderer;

    public TransportControl()
    {
        InitializeComponent();

        PlayPauseBtn.Click += (_, _) => PlayPauseCommand?.Execute(null);
        NextBtn.Click      += (_, _) => NextCommand?.Execute(null);
        PrevBtn.Click      += (_, _) => PreviousCommand?.Execute(null);

        this.Loaded += (_, _) =>
        {
            _progressRenderer = new ProgressBarRenderer(this);
            ProgressCanvas.Children.Add(_progressRenderer);

            ProgressCanvas.PointerPressed     += OnProgressPressed;
            ProgressCanvas.PointerMoved       += OnProgressMoved;
            ProgressCanvas.PointerReleased    += OnProgressReleased;
            ProgressCanvas.PointerCaptureLost += (_, _) => _dragging = false;
        };
    }

    // Avalonia 11: override OnPropertyChanged instead of .Changed.Subscribe()
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ElapsedTextProperty)
            ElapsedLabel.Text = change.NewValue as string ?? "";

        else if (change.Property == DurationTextProperty)
            DurationLabel.Text = change.NewValue as string ?? "";

        else if (change.Property == IsPlayingProperty)
            UpdatePlayIcon(change.NewValue is true);

        else if (change.Property == PositionProperty && !_dragging)
            _progressRenderer?.InvalidateVisual();
    }

    private void UpdatePlayIcon(bool isPlaying)
    {
        PlayIcon.Children.Clear();
        if (isPlaying)
        {
            var b1 = new Rectangle { Width = 4, Height = 14, Fill = new SolidColorBrush(Color.Parse("#0D0D12")) };
            var b2 = new Rectangle { Width = 4, Height = 14, Fill = new SolidColorBrush(Color.Parse("#0D0D12")) };
            Canvas.SetLeft(b1, 1); Canvas.SetTop(b1, 2);
            Canvas.SetLeft(b2, 9); Canvas.SetTop(b2, 2);
            PlayIcon.Children.Add(b1);
            PlayIcon.Children.Add(b2);
        }
        else
        {
            var tri = new Polygon
            {
                Points = new AvaloniaList<Point> { new(2, 0), new(16, 9), new(2, 18) },
                Fill   = new SolidColorBrush(Color.Parse("#0D0D12"))
            };
            PlayIcon.Children.Add(tri);
        }
    }

    private void OnProgressPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(ProgressCanvas).Properties.IsLeftButtonPressed) return;
        _dragging     = true;
        _dragFraction = GetFraction(e.GetPosition(ProgressCanvas));
        e.Pointer.Capture(ProgressCanvas);
        _progressRenderer?.InvalidateVisual();
    }

    private void OnProgressMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging) return;
        _dragFraction = GetFraction(e.GetPosition(ProgressCanvas));
        _progressRenderer?.InvalidateVisual();
    }

    private void OnProgressReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        SeekRequested?.Invoke(this, _dragFraction * Duration);
        _progressRenderer?.InvalidateVisual();
    }

    private double GetFraction(Point pt)
    {
        const double pad = 8.0;
        var w = ProgressCanvas.Bounds.Width;
        return Math.Clamp((pt.X - pad) / Math.Max(1, w - pad * 2), 0, 1);
    }

    internal double GetRenderFraction()
        => _dragging ? _dragFraction
                     : Math.Clamp(Duration <= 0 ? 0 : Position / Duration, 0, 1);

    internal double GetProgressWidth()  => ProgressCanvas.Bounds.Width;
    internal double GetProgressHeight() => ProgressCanvas.Bounds.Height;
}

// ── Progress bar renderer ─────────────────────────────────────────────────────

internal class ProgressBarRenderer : Control
{
    private readonly TransportControl _owner;
    private static Color A(Color c, byte alpha) => new Color(alpha, c.R, c.G, c.B);

    private Color _trackColor  = Color.Parse("#1E2030");
    private Color _fillColor   = Color.Parse("#3CA0FF");
    private Color _handleColor = Color.Parse("#7AD0FF");

    public ProgressBarRenderer(TransportControl owner)
    {
        _owner           = owner;
        IsHitTestVisible = false;

        // Track parent Canvas size changes
        _owner.ProgressCanvas.SizeChanged += (_, e) =>
        {
            Width  = e.NewSize.Width;
            Height = e.NewSize.Height;
            InvalidateVisual();
        };
    }

    public override void Render(DrawingContext ctx)
    {
        RefreshColors();
        var w = _owner.GetProgressWidth();
        var h = _owner.GetProgressHeight();
        if (w <= 0 || h <= 0) return;

        var cy       = h / 2;
        const double pad = 8.0;
        var tw       = w - pad * 2;
        var fraction = _owner.GetRenderFraction();

        // Track groove
        ctx.FillRectangle(new SolidColorBrush(_trackColor), new Rect(pad, cy - 2, tw, 4), 2);

        // Fill
        if (fraction > 0.001)
            ctx.FillRectangle(new SolidColorBrush(_fillColor), new Rect(pad, cy - 2, tw * fraction, 4), 2);

        // Glow at tip
        ctx.FillRectangle(new SolidColorBrush(A(_fillColor, 60)),
            new Rect(pad + tw * fraction - 4, cy - 4, 8, 8), 4);

        // Diamond handle
        DrawDiamond(ctx, pad + tw * fraction, cy);
    }

    private void DrawDiamond(DrawingContext ctx, double cx, double cy)
    {
        const double s = 6.5;
        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(cx, cy - s), IsClosed = true };
        fig.Segments!.Add(new LineSegment { Point = new Point(cx + s, cy) });
        fig.Segments.Add(new LineSegment  { Point = new Point(cx,     cy + s) });
        fig.Segments.Add(new LineSegment  { Point = new Point(cx - s, cy) });
        geo.Figures!.Add(fig);
        ctx.DrawGeometry(new SolidColorBrush(_handleColor),
            new Pen(new SolidColorBrush(_fillColor), 1.2), geo);
        ctx.DrawEllipse(new SolidColorBrush(A(_handleColor, 140)), null, new Point(cx, cy), 2.2, 2.2);
    }

    private void RefreshColors()
    {
        var res = Application.Current?.Resources;
        if (res is null) return;
        if (res.TryGetValue("ThemeProgressTrack",  out var t) && t is SolidColorBrush tb) _trackColor  = tb.Color;
        if (res.TryGetValue("ThemeProgressFill",   out var f) && f is SolidColorBrush fb) _fillColor   = fb.Color;
        if (res.TryGetValue("ThemeProgressHandle", out var h) && h is SolidColorBrush hb) _handleColor = hb.Color;
    }
}
