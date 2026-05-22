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
/// Premium vinyl platter. When album art is available it fills the entire
/// disc surface; groove rings are painted on top as a semi-transparent overlay
/// so the vinyl texture remains visible. The centre spindle and label ring sit
/// above everything and never rotate.
/// </summary>
public class VinylRecordControl : Control, IDisposable
{
    public static readonly StyledProperty<bool>    IsPlayingProperty     = AvaloniaProperty.Register<VinylRecordControl, bool>(nameof(IsPlaying));
    public static readonly StyledProperty<bool>    TrackChangingProperty = AvaloniaProperty.Register<VinylRecordControl, bool>(nameof(TrackChanging));
    public static readonly StyledProperty<byte[]?> AlbumArtDataProperty  = AvaloniaProperty.Register<VinylRecordControl, byte[]?>(nameof(AlbumArtData));
    public static readonly StyledProperty<string>  TrackTitleProperty    = AvaloniaProperty.Register<VinylRecordControl, string>(nameof(TrackTitle), "SYNCDECK");
    public static readonly StyledProperty<double>  PlaybackRpmProperty   = AvaloniaProperty.Register<VinylRecordControl, double>(nameof(PlaybackRpm), 33.0);

    public bool    IsPlaying     { get => GetValue(IsPlayingProperty);     set => SetValue(IsPlayingProperty, value); }
    public bool    TrackChanging { get => GetValue(TrackChangingProperty); set => SetValue(TrackChangingProperty, value); }
    public byte[]? AlbumArtData  { get => GetValue(AlbumArtDataProperty);  set => SetValue(AlbumArtDataProperty, value); }
    public string  TrackTitle    { get => GetValue(TrackTitleProperty);    set => SetValue(TrackTitleProperty, value); }
    public double  PlaybackRpm   { get => GetValue(PlaybackRpmProperty);   set => SetValue(PlaybackRpmProperty, value); }

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastSeconds;
    private double _rotation;
    private double _speedBlend;
    private double _trackChangePulse;
    private Bitmap? _albumBitmap;

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
        try { using var s = new MemoryStream(data); _albumBitmap = new Bitmap(s); }
        catch { _albumBitmap = null; }
    }

    private void Animate()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt  = Math.Max(0.001, now - _lastSeconds);
        _lastSeconds = now;

        var target  = IsPlaying ? 1.0 : 0.0;
        _speedBlend += (target - _speedBlend) * (IsPlaying ? 0.055 : 0.065);

        var secPerRot = PlaybackRpm >= 44 ? 1.33 : 1.80;
        _rotation     = (_rotation + (360.0 / secPerRot) * _speedBlend * dt) % 360.0;

        if (_trackChangePulse > 0)
            _trackChangePulse = Math.Max(0, _trackChangePulse - 0.055);

        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width; var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var cx     = w * 0.415;
        var cy     = h * 0.525;
        var radius = Math.Min(w * 0.43, h * 0.445);
        if (radius <= 20) return;

        DrawPlatterWell(ctx, cx, cy, radius);

        // ── Rotating group ────────────────────────────────────────────────────
        var scale = 1.0 - _trackChangePulse * 0.025;
        using (ctx.PushTransform(
            Matrix.CreateTranslation(-cx, -cy) *
            Matrix.CreateScale(scale, scale) *
            Matrix.CreateRotation(_rotation * Math.PI / 180.0) *
            Matrix.CreateTranslation(cx, cy)))
        {
            DrawRecordBase(ctx, cx, cy, radius);

            if (_albumBitmap is not null)
                DrawAlbumArtFullDisc(ctx, cx, cy, radius);

            DrawGrooveOverlay(ctx, cx, cy, radius);
        }

        // ── Static elements (never rotate) ────────────────────────────────────
        DrawStaticGloss(ctx, cx, cy, radius);
        DrawCenterLabelRing(ctx, cx, cy, radius);
        DrawSpindle(ctx, cx, cy, radius);
    }

    // ── Platter well ──────────────────────────────────────────────────────────

    private static void DrawPlatterWell(DrawingContext ctx, double cx, double cy, double r)
    {
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#050505")),
            new Pen(new SolidColorBrush(Color.Parse("#252525")), 2.0),
            new Point(cx, cy), r + 14, r + 14);
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#0A0A0A")),
            new Pen(new SolidColorBrush(Color.Parse("#111111")), 1.0),
            new Point(cx, cy), r + 7, r + 7);
    }

    // ── Black vinyl base ──────────────────────────────────────────────────────

    private static void DrawRecordBase(DrawingContext ctx, double cx, double cy, double r)
    {
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#070707")),
            new Pen(new SolidColorBrush(Color.Parse("#1A1A1A")), 1.0),
            new Point(cx, cy), r, r);
    }

    // ── Album art fills the full disc ─────────────────────────────────────────

    private void DrawAlbumArtFullDisc(DrawingContext ctx, double cx, double cy, double r)
    {
        var rect = new Rect(cx - r, cy - r, r * 2, r * 2);
        var clip = new EllipseGeometry(rect);

        using (ctx.PushGeometryClip(clip))
        {
            // Draw art stretched to cover the full disc
            ctx.DrawImage(_albumBitmap!, rect);

            // Semi-transparent dark wash so it reads as vinyl, not album cover
            ctx.FillRectangle(new SolidColorBrush(new Color(140, 0, 0, 0)), rect);
        }
    }

    // ── Groove rings — always on top of the art ───────────────────────────────

    private static void DrawGrooveOverlay(DrawingContext ctx, double cx, double cy, double r)
    {
        // Dense concentric grooves — semi-transparent so art bleeds through
        for (var i = 0; i < 42; i++)
        {
            var rr    = r * (0.17 + i * 0.019);
            var alpha = (byte)(i % 3 == 0 ? 28 : 16);
            ctx.DrawEllipse(Brushes.Transparent,
                new Pen(new SolidColorBrush(new Color(alpha, 220, 220, 220)),
                        i % 5 == 0 ? 0.55 : 0.32),
                new Point(cx, cy), rr, rr);
        }
        // Slightly darker shadow grooves
        for (var i = 0; i < 10; i++)
        {
            var rr = r * (0.28 + i * 0.062);
            ctx.DrawEllipse(Brushes.Transparent,
                new Pen(new SolidColorBrush(new Color(45, 0, 0, 0)), 0.8),
                new Point(cx, cy), rr, rr);
        }
    }

    // ── Rotating glint ────────────────────────────────────────────────────────

    private static void DrawRotatingHighlight(DrawingContext ctx, double cx, double cy, double r)
    {
        var pen = new Pen(new SolidColorBrush(new Color(30, 255, 255, 255)), 8.0);
        ctx.DrawLine(pen,
            new Point(cx - r * 0.10, cy - r * 0.93),
            new Point(cx + r * 0.54, cy + r * 0.70));
    }

    // ── Static gloss arc ─────────────────────────────────────────────────────

    private static void DrawStaticGloss(DrawingContext ctx, double cx, double cy, double r)
    {
        ctx.DrawEllipse(Brushes.Transparent,
            new Pen(new SolidColorBrush(new Color(60, 255, 255, 255)), 1.0),
            new Point(cx, cy), r + 0.5, r + 0.5);
        DrawArc(ctx, new Point(cx, cy), r + 4, -82, 18,
            new Pen(new SolidColorBrush(new Color(200, 255, 255, 255)), 2.0));
    }

    // ── Centre label ring (static, never rotates) ────────────────────────────

    private void DrawCenterLabelRing(DrawingContext ctx, double cx, double cy, double r)
    {
        var labelR = r * 0.195;
        var rect   = new Rect(cx - labelR, cy - labelR, labelR * 2, labelR * 2);

        // Outer ring shadow
        ctx.DrawEllipse(new SolidColorBrush(new Color(180, 0, 0, 0)),
            new Pen(new SolidColorBrush(new Color(90, 255, 255, 255)), 0.8),
            new Point(cx, cy), labelR + 3, labelR + 3);

        using (ctx.PushGeometryClip(new EllipseGeometry(rect)))
        {
            if (_albumBitmap is not null)
            {
                // Tiny reflected version of the art in the label
                ctx.DrawImage(_albumBitmap!, rect);
                ctx.FillRectangle(new SolidColorBrush(new Color(160, 0, 0, 0)), rect);
            }
            else
            {
                ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#1C1F1F")),
                    null, new Point(cx, cy), labelR, labelR);
                // Initials
                DrawCenteredText(ctx, Initials(TrackTitle),
                    new Point(cx, cy), labelR * 0.46,
                    Color.Parse("#A0A8A8"), "Inter");
            }
        }

        // Thin highlight ring over label
        ctx.DrawEllipse(Brushes.Transparent,
            new Pen(new SolidColorBrush(new Color(80, 255, 255, 255)), 0.7),
            new Point(cx, cy), labelR, labelR);
    }

    // ── Spindle ───────────────────────────────────────────────────────────────

    private static void DrawSpindle(DrawingContext ctx, double cx, double cy, double r)
    {
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#D4D4D4")),
            new Pen(new SolidColorBrush(Color.Parse("#606060")), 1.0),
            new Point(cx, cy), r * 0.018, r * 0.018);
        ctx.DrawEllipse(new SolidColorBrush(new Color(80, 255, 255, 255)), null,
            new Point(cx - r * 0.005, cy - r * 0.005), r * 0.006, r * 0.006);
    }

    // ── Arc helper ────────────────────────────────────────────────────────────

    internal static void DrawArc(DrawingContext ctx, Point center, double radius,
        double startDeg, double sweepDeg, Pen pen)
    {
        if (Math.Abs(sweepDeg) < 0.01) return;
        var startRad = startDeg * Math.PI / 180.0;
        var endRad   = (startDeg + sweepDeg) * Math.PI / 180.0;
        var start    = new Point(center.X + Math.Cos(startRad) * radius, center.Y + Math.Sin(startRad) * radius);
        var end      = new Point(center.X + Math.Cos(endRad)   * radius, center.Y + Math.Sin(endRad)   * radius);
        var geo      = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(start, false);
            g.ArcTo(end, new Size(radius, radius), 0,
                Math.Abs(sweepDeg) > 180,
                sweepDeg >= 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
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
