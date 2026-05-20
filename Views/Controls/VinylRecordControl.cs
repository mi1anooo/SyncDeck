using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace SyncDeck.Views.Controls;

/// <summary>
/// Draws the premium vinyl platter and rotating center label. All visuals are
/// procedural so the control stays lightweight and scales cleanly.
/// </summary>
public class VinylRecordControl : Control, IDisposable
{
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<VinylRecordControl, bool>(nameof(IsPlaying));
    public static readonly StyledProperty<bool> TrackChangingProperty =
        AvaloniaProperty.Register<VinylRecordControl, bool>(nameof(TrackChanging));
    public static readonly StyledProperty<byte[]?> AlbumArtDataProperty =
        AvaloniaProperty.Register<VinylRecordControl, byte[]?>(nameof(AlbumArtData));
    public static readonly StyledProperty<string> TrackTitleProperty =
        AvaloniaProperty.Register<VinylRecordControl, string>(nameof(TrackTitle), "SYNCDECK");
    public static readonly StyledProperty<double> PlaybackRpmProperty =
        AvaloniaProperty.Register<VinylRecordControl, double>(nameof(PlaybackRpm), 33.0);

    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }
    public bool TrackChanging { get => GetValue(TrackChangingProperty); set => SetValue(TrackChangingProperty, value); }
    public byte[]? AlbumArtData { get => GetValue(AlbumArtDataProperty); set => SetValue(AlbumArtDataProperty, value); }
    public string TrackTitle { get => GetValue(TrackTitleProperty); set => SetValue(TrackTitleProperty, value); }
    public double PlaybackRpm { get => GetValue(PlaybackRpmProperty); set => SetValue(PlaybackRpmProperty, value); }

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastSeconds;
    private double _rotation;
    private double _speedBlend;
    private double _trackChangePulse;
    private Bitmap? _albumBitmap;

    private static Color A(Color c, byte alpha) => new(alpha, c.R, c.G, c.B);

    public VinylRecordControl()
    {
        IsHitTestVisible = false;
        _lastSeconds = _clock.Elapsed.TotalSeconds;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Animate();
        _timer.Start();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == AlbumArtDataProperty)
            UpdateAlbumBitmap(change.NewValue as byte[]);

        if (change.Property == TrackChangingProperty && change.NewValue is true)
            _trackChangePulse = 1.0;

        InvalidateVisual();
    }

    private void UpdateAlbumBitmap(byte[]? data)
    {
        _albumBitmap?.Dispose();
        _albumBitmap = null;

        if (data is null || data.Length == 0) return;

        try
        {
            using var stream = new MemoryStream(data);
            _albumBitmap = new Bitmap(stream);
        }
        catch
        {
            _albumBitmap = null;
        }
    }

    private void Animate()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Max(0.001, now - _lastSeconds);
        _lastSeconds = now;

        var target = IsPlaying ? 1.0 : 0.0;
        var easing = IsPlaying ? 0.055 : 0.065; // roughly 400-700ms ramp
        _speedBlend += (target - _speedBlend) * easing;

        var secondsPerRotation = PlaybackRpm >= 44 ? 1.33 : 1.80;
        _rotation = (_rotation + (360.0 / secondsPerRotation) * _speedBlend * dt) % 360.0;

        if (_trackChangePulse > 0)
            _trackChangePulse = Math.Max(0, _trackChangePulse - 0.055);

        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var cx = w * 0.415;
        var cy = h * 0.525;
        var radius = Math.Min(w * 0.43, h * 0.445);
        if (radius <= 20) return;

        DrawPlatterWell(ctx, cx, cy, radius);

        var fadeScale = 1.0 - _trackChangePulse * 0.025;
        using (ctx.PushTransform(Matrix.CreateTranslation(-cx, -cy) *
                                 Matrix.CreateScale(fadeScale, fadeScale) *
                                 Matrix.CreateRotation(_rotation * Math.PI / 180.0) *
                                 Matrix.CreateTranslation(cx, cy)))
        {
            DrawRecordSurface(ctx, cx, cy, radius);
            DrawCenterLabel(ctx, cx, cy, radius);
            DrawRotatingHighlight(ctx, cx, cy, radius);
        }

        DrawStaticGloss(ctx, cx, cy, radius);
        DrawSpindle(ctx, cx, cy, radius);
    }

    private static void DrawPlatterWell(DrawingContext ctx, double cx, double cy, double r)
    {
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#050505")),
            new Pen(new SolidColorBrush(Color.Parse("#252525")), 2.0),
            new Point(cx, cy), r + 14, r + 14);

        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#0A0A0A")),
            new Pen(new SolidColorBrush(Color.Parse("#111111")), 1.0),
            new Point(cx, cy), r + 7, r + 7);
    }

    private void DrawRecordSurface(DrawingContext ctx, double cx, double cy, double r)
    {
        var black = Color.Parse("#070707");
        ctx.DrawEllipse(new SolidColorBrush(black),
            new Pen(new SolidColorBrush(Color.Parse("#222222")), 1.2),
            new Point(cx, cy), r, r);

        // Concentric vinyl grooves. Subtle, dense, and uneven on purpose.
        for (var i = 0; i < 42; i++)
        {
            var rr = r * (0.17 + i * 0.019);
            var alpha = (byte)(i % 3 == 0 ? 32 : 18);
            ctx.DrawEllipse(Brushes.Transparent,
                new Pen(new SolidColorBrush(new Color(alpha, 255, 255, 255)), i % 5 == 0 ? 0.55 : 0.35),
                new Point(cx, cy), rr, rr);
        }

        // A few darker grooves to keep the record from looking flat.
        for (var i = 0; i < 13; i++)
        {
            var rr = r * (0.24 + i * 0.052);
            ctx.DrawEllipse(Brushes.Transparent,
                new Pen(new SolidColorBrush(new Color(65, 0, 0, 0)), 1.0),
                new Point(cx, cy), rr, rr);
        }
    }

    private void DrawCenterLabel(DrawingContext ctx, double cx, double cy, double r)
    {
        var labelR = r * 0.235;
        var rect = new Rect(cx - labelR, cy - labelR, labelR * 2, labelR * 2);

        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#202020")),
            new Pen(new SolidColorBrush(Color.Parse("#303030")), 1.0),
            new Point(cx, cy), labelR + 2.5, labelR + 2.5);

        using (ctx.PushGeometryClip(new EllipseGeometry(rect)))
        {
            if (_albumBitmap is not null)
            {
                ctx.DrawImage(_albumBitmap, rect);
                // Matte-black wash so real artwork sits inside the luxury palette.
                ctx.FillRectangle(new SolidColorBrush(new Color(84, 0, 0, 0)), rect);
            }
            else
            {
                ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#1A1D1D")), null, new Point(cx, cy), labelR, labelR);
                for (var i = 0; i < 8; i++)
                {
                    var rr = labelR * (0.22 + i * 0.095);
                    ctx.DrawEllipse(Brushes.Transparent,
                        new Pen(new SolidColorBrush(new Color(28, 255, 255, 255)), 0.45),
                        new Point(cx, cy), rr, rr);
                }
                DrawCenteredText(ctx, Initials(TrackTitle), new Point(cx, cy), labelR * 0.48, Color.Parse("#BFC1C1"), "Inter");
            }
        }

        ctx.DrawEllipse(Brushes.Transparent,
            new Pen(new SolidColorBrush(new Color(95, 255, 255, 255)), 0.75),
            new Point(cx, cy), labelR, labelR);
    }

    private static void DrawRotatingHighlight(DrawingContext ctx, double cx, double cy, double r)
    {
        var pen = new Pen(new SolidColorBrush(new Color(36, 255, 255, 255)), 9.0);
        var start = new Point(cx - r * 0.12, cy - r * 0.94);
        var end = new Point(cx + r * 0.56, cy + r * 0.72);
        ctx.DrawLine(pen, start, end);
    }

    private static void DrawStaticGloss(DrawingContext ctx, double cx, double cy, double r)
    {
        // Fine edge highlight and a short white progress-like glint at the top.
        ctx.DrawEllipse(Brushes.Transparent,
            new Pen(new SolidColorBrush(new Color(84, 255, 255, 255)), 1.0),
            new Point(cx, cy), r + 0.6, r + 0.6);
        DrawArc(ctx, new Point(cx, cy), r + 4, -82, 18,
            new Pen(new SolidColorBrush(new Color(210, 255, 255, 255)), 2.1));
    }

    private static void DrawSpindle(DrawingContext ctx, double cx, double cy, double r)
    {
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#DADADA")),
            new Pen(new SolidColorBrush(Color.Parse("#616161")), 1),
            new Point(cx, cy), r * 0.018, r * 0.018);
        ctx.DrawEllipse(new SolidColorBrush(new Color(80, 255, 255, 255)), null,
            new Point(cx - r * 0.006, cy - r * 0.006), r * 0.006, r * 0.006);
    }

    internal static void DrawArc(DrawingContext ctx, Point center, double radius, double startDeg, double sweepDeg, Pen pen)
    {
        if (Math.Abs(sweepDeg) < 0.01) return;

        var startRad = startDeg * Math.PI / 180.0;
        var endRad = (startDeg + sweepDeg) * Math.PI / 180.0;
        var start = new Point(center.X + Math.Cos(startRad) * radius, center.Y + Math.Sin(startRad) * radius);
        var end = new Point(center.X + Math.Cos(endRad) * radius, center.Y + Math.Sin(endRad) * radius);

        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(start, false);
            g.ArcTo(end, new Size(radius, radius), 0, Math.Abs(sweepDeg) > 180, sweepDeg >= 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
        }
        ctx.DrawGeometry(null, pen, geo);
    }

    private static string Initials(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "SD";
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1) return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
        return string.Concat(parts[0][0], parts[^1][0]).ToUpperInvariant();
    }

    private static void DrawCenteredText(DrawingContext ctx, string text, Point center, double size, Color color, string family)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(family), size, new SolidColorBrush(color));
        ctx.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
    }

    public void Dispose()
    {
        _timer.Stop();
        _albumBitmap?.Dispose();
    }
}
