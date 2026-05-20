using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace SyncDeck.Views.Controls;

// ── VisualizerControl — thin host, passes property changes to renderer ─────────

public partial class VisualizerControl : UserControl
{
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<VisualizerControl, bool>(nameof(IsPlaying));
    public static readonly StyledProperty<bool> TrackChangingProperty =
        AvaloniaProperty.Register<VisualizerControl, bool>(nameof(TrackChanging));
    public static readonly StyledProperty<byte[]?> AlbumArtDataProperty =
        AvaloniaProperty.Register<VisualizerControl, byte[]?>(nameof(AlbumArtData));

    public bool    IsPlaying     { get => GetValue(IsPlayingProperty);     set => SetValue(IsPlayingProperty, value); }
    public bool    TrackChanging { get => GetValue(TrackChangingProperty); set => SetValue(TrackChangingProperty, value); }
    public byte[]? AlbumArtData  { get => GetValue(AlbumArtDataProperty);  set => SetValue(AlbumArtDataProperty, value); }

    private VisualizerRenderer? _renderer;

    public VisualizerControl()
    {
        InitializeComponent();
        this.Loaded += (_, _) =>
        {
            _renderer = new VisualizerRenderer(VisCanvas) { IsPlaying = IsPlaying };
            VisCanvas.Children.Add(_renderer);
        };
    }

    // Avalonia 11: override OnPropertyChanged instead of .Changed.Subscribe()
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_renderer is null) return;

        if (change.Property == IsPlayingProperty)
            _renderer.IsPlaying = (bool)change.NewValue!;

        if (change.Property == TrackChangingProperty && change.NewValue is true)
            _renderer.TriggerFlash();
    }
}

// ── VisualizerRenderer — self-drawing control ─────────────────────────────────

public class VisualizerRenderer : Control
{
    private readonly Canvas _canvas;

    private double   _angle      = 0;
    private double   _waveOffset = 0;
    private double   _flash      = 0;
    private readonly double[] _barHeights = new double[16];
    private readonly double[] _barTargets = new double[16];
    private readonly Random   _rng        = new();
    private readonly DispatcherTimer _timer;

    public bool IsPlaying { get; set; }

    private Color  _primary   = Color.Parse("#3CA0FF");
    private Color  _secondary = Color.Parse("#0A3060");
    private string _style     = "minidisc";

    // Helper — Avalonia Color is an immutable struct, NOT a C# record.
    // Use the 4-arg constructor: Color(a, r, g, b)
    private static Color A(Color c, byte alpha) => new Color(alpha, c.R, c.G, c.B);

    public VisualizerRenderer(Canvas canvas)
    {
        _canvas          = canvas;
        IsHitTestVisible = false;

        // Size ourselves to match the parent Canvas
        _canvas.SizeChanged += (_, e) =>
        {
            Width  = e.NewSize.Width;
            Height = e.NewSize.Height;
            InvalidateVisual();
        };

        for (int i = 0; i < _barTargets.Length; i++) _barTargets[i] = 0.1;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => { RefreshThemeColors(); Animate(); InvalidateVisual(); };
        _timer.Start();
    }

    public void TriggerFlash() => _flash = 1.0;

    private void RefreshThemeColors()
    {
        var res = Application.Current?.Resources;
        if (res is null) return;
        if (res.TryGetValue("ThemeVisPrimaryColor",   out var p)  && p is Color cp)  _primary   = cp;
        if (res.TryGetValue("ThemeVisSecondaryColor", out var s)  && s is Color cs)  _secondary = cs;
        if (res.TryGetValue("ThemeVisStyle",          out var st) && st is string ss) _style    = ss;
    }

    private void Animate()
    {
        if (IsPlaying)
        {
            _angle      = (_angle + 1.4) % 360;
            _waveOffset = (_waveOffset + 2.5) % (Math.PI * 200);
            for (int i = 0; i < _barHeights.Length; i++)
            {
                if (_rng.NextDouble() < 0.15)
                    _barTargets[i] = 0.2 + _rng.NextDouble() * 0.75;
                _barHeights[i] += (_barTargets[i] - _barHeights[i]) * 0.22;
            }
        }
        else
        {
            _angle += 0.18;
            for (int i = 0; i < _barHeights.Length; i++)
                _barHeights[i] *= 0.93;
        }
        if (_flash > 0) _flash = Math.Max(0, _flash - 0.035);
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width; var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        ctx.FillRectangle(new SolidColorBrush(A(_secondary, 25)), new Rect(0, 0, w, h));

        switch (_style)
        {
            case "bars":     DrawBars(ctx, w, h);     break;
            case "waveform": DrawWaveform(ctx, w, h); break;
            case "minidisc": DrawMiniDisc(ctx, w, h); break;
            default:         DrawDisc(ctx, w, h);     break;
        }

        if (_flash > 0)
            ctx.FillRectangle(new SolidColorBrush(A(_primary, (byte)(_flash * 55))),
                new Rect(0, 0, w, h));

        DrawScanLines(ctx, w, h);
    }

    // ── Visualizer modes ──────────────────────────────────────────────────────

    private void DrawDisc(DrawingContext ctx, double w, double h)
    {
        var cx = w / 2; var cy = h / 2;
        var r  = Math.Min(w, h) * 0.40;

        ctx.DrawEllipse(new SolidColorBrush(A(_primary, 15)),
            new Pen(new SolidColorBrush(A(_primary, 55)), 1.5),
            new Point(cx, cy), r + 10, r + 10);

        var rad = _angle * Math.PI / 180.0;
        using (ctx.PushTransform(
            Matrix.CreateTranslation(-cx, -cy) *
            Matrix.CreateRotation(rad) *
            Matrix.CreateTranslation(cx, cy)))
        {
            ctx.DrawEllipse(new SolidColorBrush(A(_secondary, 220)),
                new Pen(new SolidColorBrush(_primary), 1.5),
                new Point(cx, cy), r, r);

            for (int i = 1; i <= 4; i++)
            {
                var rr = r * (0.25 + i * 0.16);
                ctx.DrawEllipse(Brushes.Transparent,
                    new Pen(new SolidColorBrush(A(_primary, (byte)(25 + i * 12))), 0.5),
                    new Point(cx, cy), rr, rr);
            }
            ctx.DrawLine(new Pen(new SolidColorBrush(A(_primary, 100)), 1),
                new Point(cx, cy), new Point(cx + r * 0.85, cy));
        }

        ctx.DrawEllipse(new SolidColorBrush(_primary), null, new Point(cx, cy), 5, 5);

        if (IsPlaying)
            ctx.DrawEllipse(new SolidColorBrush(A(_primary, 200)), null,
                new Point(cx + r + 12, cy), 3, 3);
    }

    private void DrawMiniDisc(DrawingContext ctx, double w, double h)
    {
        var cx = w / 2; var cy = h / 2;
        var cw = Math.Min(w, h) * 0.78;
        var ch = cw * 0.90;
        var x0 = cx - cw / 2; var y0 = cy - ch / 2;

        ctx.FillRectangle(new SolidColorBrush(A(_secondary, 210)), new Rect(x0, y0, cw, ch), 5);
        ctx.DrawRectangle(Brushes.Transparent,
            new Pen(new SolidColorBrush(A(_primary, 160)), 1.5),
            new Rect(x0, y0, cw, ch), 5, 5);

        var sw = cw * 0.74; var sh = ch * 0.10;
        var sx = cx - sw / 2; var sy = y0 + ch * 0.07;
        ctx.FillRectangle(new SolidColorBrush(A(_primary, 30)), new Rect(sx, sy, sw, sh), 2);
        ctx.DrawRectangle(Brushes.Transparent,
            new Pen(new SolidColorBrush(A(_primary, 80)), 1),
            new Rect(sx, sy, sw, sh), 2, 2);

        var dcx = cx; var dcy = cy + ch * 0.06; var dr = cw * 0.27;
        var rad = _angle * Math.PI / 180.0;
        using (ctx.PushTransform(
            Matrix.CreateTranslation(-dcx, -dcy) *
            Matrix.CreateRotation(rad) *
            Matrix.CreateTranslation(dcx, dcy)))
        {
            ctx.DrawEllipse(new SolidColorBrush(A(_secondary, 240)),
                new Pen(new SolidColorBrush(_primary), 1.5),
                new Point(dcx, dcy), dr, dr);
            for (int i = 1; i <= 3; i++)
                ctx.DrawEllipse(Brushes.Transparent,
                    new Pen(new SolidColorBrush(A(_primary, 35)), 0.5),
                    new Point(dcx, dcy), dr * (0.3 + i * 0.22), dr * (0.3 + i * 0.22));
        }

        DrawText(ctx, "SYNCDECK  MZ-1", new Point(cx, y0 + ch * 0.80), 8, _primary);
        ctx.DrawEllipse(new SolidColorBrush(A(_primary, IsPlaying ? (byte)220 : (byte)50)),
            null, new Point(x0 + cw - 11, y0 + 11), 3.5, 3.5);
    }

    private void DrawWaveform(DrawingContext ctx, double w, double h)
    {
        var pen  = new Pen(new SolidColorBrush(A(_primary, 210)), 1.5);
        var amp  = IsPlaying ? 1.0 : 0.12;
        var px   = 0.0; var py = h / 2;
        for (int i = 1; i <= 80; i++)
        {
            var t    = (double)i / 80;
            var x    = t * w;
            var wave = (Math.Sin(t * Math.PI * 8 + _waveOffset * 0.05) * 0.65
                      + Math.Sin(t * Math.PI * 3 + _waveOffset * 0.03) * 0.35) * h * 0.26 * amp;
            var y    = h / 2 + wave;
            ctx.DrawLine(pen, new Point(px, py), new Point(x, y));
            px = x; py = y;
        }
        ctx.DrawLine(new Pen(new SolidColorBrush(A(_primary, 25)), 0.5),
            new Point(0, h / 2), new Point(w, h / 2));
    }

    private void DrawBars(DrawingContext ctx, double w, double h)
    {
        var count = _barHeights.Length;
        var gap   = 3.0;
        var barW  = (w - gap * (count + 1)) / count;
        for (int i = 0; i < count; i++)
        {
            var bh = Math.Max(2, _barHeights[i] * h * 0.84);
            var x  = gap + i * (barW + gap);
            var y  = h - bh;
            ctx.FillRectangle(new SolidColorBrush(A(_primary, 200)), new Rect(x, y, barW, bh));
            ctx.FillRectangle(new SolidColorBrush(_primary), new Rect(x, y, barW, 2));
            ctx.FillRectangle(new SolidColorBrush(A(_primary, 22)),
                new Rect(x, h * 0.92, barW, h * 0.08 * _barHeights[i]));
        }
        ctx.DrawLine(new Pen(new SolidColorBrush(A(_primary, 55)), 1),
            new Point(0, h - 1), new Point(w, h - 1));
    }

    private static void DrawScanLines(DrawingContext ctx, double w, double h)
    {
        var pen = new Pen(new SolidColorBrush(new Color(9, 0, 0, 0)), 1);
        for (double y = 0; y < h; y += 3)
            ctx.DrawLine(pen, new Point(0, y), new Point(w, y));
    }

    private static void DrawText(DrawingContext ctx, string text, Point center, double size, Color color)
    {
        var ft = new FormattedText(text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface("Consolas"), size,
            new SolidColorBrush(color));
        ctx.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
    }
}
