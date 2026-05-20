using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace SyncDeck.Views.Controls;

/// <summary>Thin, monochrome progress arc that sits around the platter edge.</summary>
public class ProgressArcControl : Control
{
    public static readonly StyledProperty<double> PositionProperty =
        AvaloniaProperty.Register<ProgressArcControl, double>(nameof(Position));
    public static readonly StyledProperty<double> DurationProperty =
        AvaloniaProperty.Register<ProgressArcControl, double>(nameof(Duration), 1.0);

    public double Position { get => GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public double Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    private readonly DispatcherTimer _timer;

    public ProgressArcControl()
    {
        IsHitTestVisible = false;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => InvalidateVisual();
        _timer.Start();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PositionProperty || change.Property == DurationProperty)
            InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var cx = w * 0.415;
        var cy = h * 0.525;
        var r = Math.Min(w * 0.43, h * 0.445) + 4;
        var progress = Math.Clamp(Duration <= 0 ? 0 : Position / Duration, 0, 1);
        const double start = -82;
        const double sweep = 318;

        var trackPen = new Pen(new SolidColorBrush(new Color(28, 255, 255, 255)), 1.0);
        var fillPen = new Pen(new SolidColorBrush(new Color(190, 245, 245, 240)), 1.8);

        VinylRecordControl.DrawArc(ctx, new Point(cx, cy), r, start, sweep, trackPen);
        VinylRecordControl.DrawArc(ctx, new Point(cx, cy), r, start, sweep * progress, fillPen);

        if (progress > 0.001)
        {
            var endRad = (start + sweep * progress) * Math.PI / 180.0;
            var pt = new Point(cx + Math.Cos(endRad) * r, cy + Math.Sin(endRad) * r);
            ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#F4F4EF")), null, pt, 2.2, 2.2);
        }
    }
}
