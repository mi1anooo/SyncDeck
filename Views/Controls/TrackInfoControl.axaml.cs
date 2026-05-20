using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace SyncDeck.Views.Controls;

public partial class TrackInfoControl : UserControl
{
    public static readonly StyledProperty<string> TrackTitleProperty =
        AvaloniaProperty.Register<TrackInfoControl, string>(nameof(TrackTitle), "No Track");
    public static readonly StyledProperty<string> ArtistProperty =
        AvaloniaProperty.Register<TrackInfoControl, string>(nameof(Artist), "─ ─ ─");
    public static readonly StyledProperty<string> AlbumNameProperty =
        AvaloniaProperty.Register<TrackInfoControl, string>(nameof(AlbumName), "");
    public static readonly StyledProperty<byte[]?> AlbumArtDataProperty =
        AvaloniaProperty.Register<TrackInfoControl, byte[]?>(nameof(AlbumArtData));

    public string   TrackTitle   { get => GetValue(TrackTitleProperty);   set => SetValue(TrackTitleProperty, value); }
    public string   Artist       { get => GetValue(ArtistProperty);       set => SetValue(ArtistProperty, value); }
    public string   AlbumName    { get => GetValue(AlbumNameProperty);    set => SetValue(AlbumNameProperty, value); }
    public byte[]?  AlbumArtData { get => GetValue(AlbumArtDataProperty); set => SetValue(AlbumArtDataProperty, value); }

    private double _scrollX      = 0;
    private bool   _needsMarquee = false;
    private readonly DispatcherTimer _marqueeTimer;

    public TrackInfoControl()
    {
        InitializeComponent();

        this.Loaded += (_, _) =>
        {
            var art = new AlbumArtPlaceholder { Width = 56, Height = 56 };
            ArtCanvas.Children.Add(art);
        };

        _marqueeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(38) };
        _marqueeTimer.Tick += (_, _) => TickMarquee();
        _marqueeTimer.Start();
    }

    // Avalonia 11: use OnPropertyChanged override instead of .Changed.Subscribe()
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TrackTitleProperty)
        {
            TitleLabel.Text = change.NewValue as string ?? "";
            _scrollX = 0;
            TitleLabel.RenderTransform = new TranslateTransform(0, 0);
            Dispatcher.UIThread.Post(CheckMarquee, DispatcherPriority.Layout);
        }
        else if (change.Property == ArtistProperty)
        {
            ArtistLabel.Text = change.NewValue as string ?? "";
        }
    }

    private void CheckMarquee()
    {
        var clipWidth = TitleLabel.Parent is Border b ? b.Bounds.Width : 160;
        _needsMarquee = TitleLabel.Bounds.Width > clipWidth - 2;
        if (!_needsMarquee) { _scrollX = 0; TitleLabel.RenderTransform = null; }
    }

    private void TickMarquee()
    {
        if (!_needsMarquee) return;
        _scrollX -= 0.85;
        if (_scrollX < -(TitleLabel.Bounds.Width + 20)) _scrollX = 20;
        TitleLabel.RenderTransform = new TranslateTransform(_scrollX, 0);
    }
}

// ── Album art vinyl placeholder ───────────────────────────────────────────────

public class AlbumArtPlaceholder : Control
{
    private static Color A(Color c, byte alpha) => new Color(alpha, c.R, c.G, c.B);

    private Color  _primary   = Color.Parse("#3CA0FF");
    private Color  _secondary = Color.Parse("#0A3060");
    private double _angle     = 0;
    private readonly DispatcherTimer _timer;

    public AlbumArtPlaceholder()
    {
        IsHitTestVisible = false;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += (_, _) =>
        {
            var res = Application.Current?.Resources;
            if (res?.TryGetValue("ThemeVisPrimaryColor",   out var p) == true && p is Color cp) _primary   = cp;
            if (res?.TryGetValue("ThemeVisSecondaryColor", out var s) == true && s is Color cs) _secondary = cs;
            _angle = (_angle + 1.1) % 360;
            InvalidateVisual();
        };
        _timer.Start();
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width; var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;
        var cx = w / 2; var cy = h / 2;
        var r  = Math.Min(w, h) * 0.42;

        ctx.FillRectangle(new SolidColorBrush(A(_secondary, 90)), new Rect(0, 0, w, h));

        var rad = _angle * Math.PI / 180.0;
        using (ctx.PushTransform(
            Matrix.CreateTranslation(-cx, -cy) *
            Matrix.CreateRotation(rad) *
            Matrix.CreateTranslation(cx, cy)))
        {
            ctx.DrawEllipse(new SolidColorBrush(A(_secondary, 200)),
                new Pen(new SolidColorBrush(A(_primary, 160)), 1.2),
                new Point(cx, cy), r, r);
            for (int i = 1; i <= 3; i++)
                ctx.DrawEllipse(Brushes.Transparent,
                    new Pen(new SolidColorBrush(A(_primary, (byte)(28 + i * 14))), 0.5),
                    new Point(cx, cy), r * (0.28 + i * 0.22), r * (0.28 + i * 0.22));
        }
        ctx.DrawEllipse(new SolidColorBrush(_primary), null, new Point(cx, cy), 4.5, 4.5);
    }
}
