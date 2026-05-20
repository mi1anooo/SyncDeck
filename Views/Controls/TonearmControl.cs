using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace SyncDeck.Views.Controls;

/// <summary>Procedural tonearm assembly with subtle play/progress animation.</summary>
public class TonearmControl : Control
{
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<TonearmControl, bool>(nameof(IsPlaying));
    public static readonly StyledProperty<bool> TrackChangingProperty =
        AvaloniaProperty.Register<TonearmControl, bool>(nameof(TrackChanging));
    public static readonly StyledProperty<double> PositionProperty =
        AvaloniaProperty.Register<TonearmControl, double>(nameof(Position));
    public static readonly StyledProperty<double> DurationProperty =
        AvaloniaProperty.Register<TonearmControl, double>(nameof(Duration), 1.0);

    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }
    public bool TrackChanging { get => GetValue(TrackChangingProperty); set => SetValue(TrackChangingProperty, value); }
    public double Position { get => GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public double Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    private readonly DispatcherTimer _timer;
    private double _angle;
    private double _lift;
    private double _resetPulse;

    public TonearmControl()
    {
        IsHitTestVisible = false;
        _angle = 79;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Animate();
        _timer.Start();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TrackChangingProperty && change.NewValue is true)
            _resetPulse = 1.0;
        InvalidateVisual();
    }

    private void Animate()
    {
        var progress = Math.Clamp(Duration <= 0 ? 0 : Position / Duration, 0, 1);
        var playTarget = IsPlaying ? 111.5 + progress * 7.5 : 79.0;
        if (_resetPulse > 0)
        {
            playTarget = 84.0;
            _resetPulse = Math.Max(0, _resetPulse - 0.045);
        }

        _angle += (playTarget - _angle) * 0.055;
        var targetLift = (!IsPlaying || _resetPulse > 0.001) ? 1.0 : 0.0;
        _lift += (targetLift - _lift) * 0.08;
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var pivot = new Point(w * 0.805, h * 0.21);
        var length = Math.Min(w, h) * 0.55;
        var angleRad = _angle * Math.PI / 180.0;
        var stylus = new Point(pivot.X + Math.Cos(angleRad) * length, pivot.Y + Math.Sin(angleRad) * length - _lift * 8.0);

        DrawBase(ctx, pivot, w, h);
        DrawArm(ctx, pivot, stylus, angleRad);
    }

    private static void DrawBase(DrawingContext ctx, Point pivot, double w, double h)
    {
        var shadow = new SolidColorBrush(new Color(100, 0, 0, 0));
        var graphite = new SolidColorBrush(Color.Parse("#181818"));
        var edge = new Pen(new SolidColorBrush(Color.Parse("#303030")), 1.2);

        var baseCenter = new Point(pivot.X + w * 0.075, pivot.Y + h * 0.102);
        ctx.DrawEllipse(shadow, null, new Point(baseCenter.X + 4, baseCenter.Y + 6), 45, 45);
        ctx.DrawEllipse(graphite, edge, baseCenter, 43, 43);
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#252525")),
            new Pen(new SolidColorBrush(Color.Parse("#414141")), 1), baseCenter, 25, 25);

        // Counterweight and pivot cap.
        ctx.DrawEllipse(shadow, null, new Point(pivot.X - 27, pivot.Y + 6), 27, 27);
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#1F1F1F")),
            new Pen(new SolidColorBrush(Color.Parse("#3A3A3A")), 1), new Point(pivot.X - 29, pivot.Y + 1), 26, 26);
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#242424")),
            new Pen(new SolidColorBrush(Color.Parse("#3D3D3D")), 1), pivot, 15, 15);
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#ECECEC")), null, pivot, 3.8, 3.8);
    }

    private static void DrawArm(DrawingContext ctx, Point pivot, Point stylus, double angleRad)
    {
        var dx = Math.Cos(angleRad);
        var dy = Math.Sin(angleRad);
        var normal = new Point(-dy, dx);
        var armStart = new Point(pivot.X + dx * 19, pivot.Y + dy * 19);
        var armEnd = new Point(stylus.X - dx * 29, stylus.Y - dy * 29);

        ctx.DrawLine(new Pen(new SolidColorBrush(new Color(130, 0, 0, 0)), 7.5),
            new Point(armStart.X + 3, armStart.Y + 5), new Point(armEnd.X + 3, armEnd.Y + 5));
        ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#111111")), 5.5), armStart, armEnd);
        ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#363636")), 1.2),
            new Point(armStart.X + normal.X * 1.8, armStart.Y + normal.Y * 1.8),
            new Point(armEnd.X + normal.X * 1.8, armEnd.Y + normal.Y * 1.8));

        DrawCartridge(ctx, stylus, dx, dy, normal);
    }

    private static void DrawCartridge(DrawingContext ctx, Point stylus, double dx, double dy, Point normal)
    {
        var p1 = new Point(stylus.X - dx * 34 + normal.X * 12, stylus.Y - dy * 34 + normal.Y * 12);
        var p2 = new Point(stylus.X - dx * 7 + normal.X * 7, stylus.Y - dy * 7 + normal.Y * 7);
        var p3 = new Point(stylus.X - dx * 2 - normal.X * 11, stylus.Y - dy * 2 - normal.Y * 11);
        var p4 = new Point(stylus.X - dx * 29 - normal.X * 14, stylus.Y - dy * 29 - normal.Y * 14);

        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = p1, IsClosed = true };
        fig.Segments!.Add(new LineSegment { Point = p2 });
        fig.Segments.Add(new LineSegment { Point = p3 });
        fig.Segments.Add(new LineSegment { Point = p4 });
        geo.Figures!.Add(fig);

        ctx.DrawGeometry(new SolidColorBrush(Color.Parse("#1D1D1D")),
            new Pen(new SolidColorBrush(Color.Parse("#444444")), 1), geo);
        ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#D6D6D0")), 1.1),
            new Point(stylus.X - dx * 5, stylus.Y - dy * 5), stylus);
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#D8D8D2")), null, stylus, 2.4, 2.4);
    }
}
