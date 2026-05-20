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

        // Inject vector art placeholder (fixed 56×56 size)
        this.Loaded += (_, _) =>
        {
            var art = new AlbumArtPlaceholder { Width = 56, Height = 56 };
            ArtCanvas.Children.Add(art);
        };

        TrackTitleProperty.Changed.Subscribe(e =>
        {
            if (e.Sender != this) return;
            TitleLabel.Text = (string)e.NewValue.Value!;
            _scrollX = 0;
            TitleLabel.RenderTransform = new TranslateTransform(0, 0);
            Dispatcher.UIThread.Post(CheckMarquee, DispatcherPriority.Layout);
        });

        ArtistProperty.Changed.Subscribe(e =>
        {
            if (e.Sender != this) return;
            ArtistLabel.Text = (string)e.NewValue.Value!;
        });

        _marqueeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(38) };
        _marqueeTimer.Tick += (_, _) => TickMarquee();
        _marqueeTimer.Start();
    }

    private void CheckMarquee()
    {
        var clipWidth  = TitleLabel.Parent is Border b ? b.Bounds.Width : 160;
        _needsMarquee  = TitleLabel.Bounds.Width > clipWidth - 2;
        if (!_needsMarquee) { _scrollX = 0; TitleLabel.RenderTransform = null; }
    }

    private void TickMarquee()
    {
        if (!_needsMarquee) return;
        _scrollX -= 0.85;
        var textW  = TitleLabel.Bounds.Width;
        if (_scrollX < -(textW + 20)) _scrollX = 20;
        TitleLabel.RenderTransform = new TranslateTransform(_scrollX, 0);
    }
}

// ── Album art placeholder — spinning vinyl disc ───────────────────────────────

public class AlbumArtPlaceholder : Control
{
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
            RefreshColors();
            _angle = (_angle + 1.1) % 360;
            InvalidateVisual();
        };
        _timer.Start();
    }

    private void RefreshColors()
    {
        var res = Application.Current?.Resources;
        if (res is null) return;
        if (res.TryGetValue("ThemeVisPrimaryColor",   out var p) && p is Color cp) _primary   = cp;
        if (res.TryGetValue("ThemeVisSecondaryColor", out var s) && s is Color cs) _secondary = cs;
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width; var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;
        var cx = w / 2; var cy = h / 2;
        var r  = Math.Min(w, h) * 0.42;

        ctx.FillRectangle(new SolidColorBrush(_secondary with { A = 90 }), new Rect(0, 0, w, h));

        var rad = _angle * Math.PI / 180.0;
        using (ctx.PushTransform(
            Matrix.CreateTranslation(-cx, -cy) *
            Matrix.CreateRotation(rad) *
            Matrix.CreateTranslation(cx, cy)))
        {
            ctx.DrawEllipse(
                new SolidColorBrush(_secondary with { A = 200 }),
                new Pen(new SolidColorBrush(_primary with { A = 160 }), 1.2),
                new Point(cx, cy), r, r);

            for (int i = 1; i <= 3; i++)
                ctx.DrawEllipse(Brushes.Transparent,
                    new Pen(new SolidColorBrush(_primary with { A = (byte)(28 + i * 14) }), 0.5),
                    new Point(cx, cy), r * (0.28 + i * 0.22), r * (0.28 + i * 0.22));
        }

        ctx.DrawEllipse(new SolidColorBrush(_primary), null, new Point(cx, cy), 4.5, 4.5);
    }
}
