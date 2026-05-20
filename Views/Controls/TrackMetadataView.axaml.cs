using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Globalization;
using System.IO;

namespace SyncDeck.Views.Controls;

public partial class TrackMetadataView : UserControl, IDisposable
{
    public static readonly StyledProperty<string> TrackTitleProperty =
        AvaloniaProperty.Register<TrackMetadataView, string>(nameof(TrackTitle), "No Track");
    public static readonly StyledProperty<string> ArtistProperty =
        AvaloniaProperty.Register<TrackMetadataView, string>(nameof(Artist), "─ ─ ─");
    public static readonly StyledProperty<string> AlbumNameProperty =
        AvaloniaProperty.Register<TrackMetadataView, string>(nameof(AlbumName), "");
    public static readonly StyledProperty<byte[]?> AlbumArtDataProperty =
        AvaloniaProperty.Register<TrackMetadataView, byte[]?>(nameof(AlbumArtData));
    public static readonly StyledProperty<string> ElapsedTextProperty =
        AvaloniaProperty.Register<TrackMetadataView, string>(nameof(ElapsedText), "0:00");
    public static readonly StyledProperty<string> DurationTextProperty =
        AvaloniaProperty.Register<TrackMetadataView, string>(nameof(DurationText), "0:00");

    public string TrackTitle { get => GetValue(TrackTitleProperty); set => SetValue(TrackTitleProperty, value); }
    public string Artist { get => GetValue(ArtistProperty); set => SetValue(ArtistProperty, value); }
    public string AlbumName { get => GetValue(AlbumNameProperty); set => SetValue(AlbumNameProperty, value); }
    public byte[]? AlbumArtData { get => GetValue(AlbumArtDataProperty); set => SetValue(AlbumArtDataProperty, value); }
    public string ElapsedText { get => GetValue(ElapsedTextProperty); set => SetValue(ElapsedTextProperty, value); }
    public string DurationText { get => GetValue(DurationTextProperty); set => SetValue(DurationTextProperty, value); }

    private Bitmap? _avatarBitmap;

    public TrackMetadataView()
    {
        InitializeComponent();
        UpdateText();
        UpdateAvatar();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == AlbumArtDataProperty)
        {
            UpdateAvatarBitmap(change.NewValue as byte[]);
            UpdateAvatar();
        }

        UpdateText();
    }

    private void UpdateText()
    {
        if (TitleLabel is null) return;
        TitleLabel.Text = string.IsNullOrWhiteSpace(TrackTitle) ? "No Track" : TrackTitle;
        ArtistLabel.Text = string.IsNullOrWhiteSpace(Artist) ? "─ ─ ─" : Artist;
        AlbumLabel.Text = string.IsNullOrWhiteSpace(AlbumName) ? "SYNCDECK" : AlbumName.ToUpperInvariant();
        ElapsedLabel.Text = ElapsedText;
        DurationLabel.Text = DurationText;
    }

    private void UpdateAvatarBitmap(byte[]? data)
    {
        _avatarBitmap?.Dispose();
        _avatarBitmap = null;

        if (data is null || data.Length == 0) return;

        try
        {
            using var stream = new MemoryStream(data);
            _avatarBitmap = new Bitmap(stream);
        }
        catch
        {
            _avatarBitmap = null;
        }
    }

    private void UpdateAvatar()
    {
        if (AvatarCanvas is null) return;
        AvatarCanvas.Children.Clear();
        AvatarCanvas.Children.Add(new AvatarRenderer(this));
    }

    internal Bitmap? AvatarBitmap => _avatarBitmap;

    internal string Initials
    {
        get
        {
            var value = string.IsNullOrWhiteSpace(Artist) ? TrackTitle : Artist;
            if (string.IsNullOrWhiteSpace(value)) return "SD";
            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 1) return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
            return string.Concat(parts[0][0], parts[^1][0]).ToUpperInvariant();
        }
    }

    public void Dispose() => _avatarBitmap?.Dispose();
}

internal class AvatarRenderer : Control
{
    private readonly TrackMetadataView _owner;

    public AvatarRenderer(TrackMetadataView owner)
    {
        _owner = owner;
        Width = 42;
        Height = 42;
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext ctx)
    {
        var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        var center = rect.Center;
        var radius = Math.Min(rect.Width, rect.Height) / 2.0;

        using (ctx.PushGeometryClip(new EllipseGeometry(rect)))
        {
            if (_owner.AvatarBitmap is not null)
            {
                ctx.DrawImage(_owner.AvatarBitmap, rect);
                ctx.FillRectangle(new SolidColorBrush(new Color(72, 0, 0, 0)), rect);
            }
            else
            {
                ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#181818")), null, center, radius, radius);
                ctx.DrawEllipse(Brushes.Transparent, new Pen(new SolidColorBrush(new Color(30, 255, 255, 255)), 0.6), center, radius * 0.72, radius * 0.72);
                var ft = new FormattedText(_owner.Initials, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Inter"), 12, new SolidColorBrush(Color.Parse("#D4D4D0")));
                ctx.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
            }
        }
    }
}
