using Avalonia;
using Avalonia.Controls;

namespace SyncDeck.Views.Controls;

public partial class TurntableControl : UserControl
{
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<TurntableControl, bool>(nameof(IsPlaying));
    public static readonly StyledProperty<bool> TrackChangingProperty =
        AvaloniaProperty.Register<TurntableControl, bool>(nameof(TrackChanging));
    public static readonly StyledProperty<byte[]?> AlbumArtDataProperty =
        AvaloniaProperty.Register<TurntableControl, byte[]?>(nameof(AlbumArtData));
    public static readonly StyledProperty<string> TrackTitleProperty =
        AvaloniaProperty.Register<TurntableControl, string>(nameof(TrackTitle), "SYNCDECK");
    public static readonly StyledProperty<double> PositionProperty =
        AvaloniaProperty.Register<TurntableControl, double>(nameof(Position));
    public static readonly StyledProperty<double> DurationProperty =
        AvaloniaProperty.Register<TurntableControl, double>(nameof(Duration), 1.0);
    public static readonly StyledProperty<double> PlaybackRpmProperty =
        AvaloniaProperty.Register<TurntableControl, double>(nameof(PlaybackRpm), 33.0);

    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }
    public bool TrackChanging { get => GetValue(TrackChangingProperty); set => SetValue(TrackChangingProperty, value); }
    public byte[]? AlbumArtData { get => GetValue(AlbumArtDataProperty); set => SetValue(AlbumArtDataProperty, value); }
    public string TrackTitle { get => GetValue(TrackTitleProperty); set => SetValue(TrackTitleProperty, value); }
    public double Position { get => GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public double Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public double PlaybackRpm { get => GetValue(PlaybackRpmProperty); set => SetValue(PlaybackRpmProperty, value); }

    public TurntableControl()
    {
        InitializeComponent();
        SyncChildren();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        SyncChildren();
    }

    private void SyncChildren()
    {
        if (Record is null || Tonearm is null || ProgressArc is null) return;

        Record.IsPlaying = IsPlaying;
        Record.TrackChanging = TrackChanging;
        Record.AlbumArtData = AlbumArtData;
        Record.TrackTitle = TrackTitle;
        Record.PlaybackRpm = PlaybackRpm;

        Tonearm.IsPlaying = IsPlaying;
        Tonearm.TrackChanging = TrackChanging;
        Tonearm.Position = Position;
        Tonearm.Duration = Duration;

        ProgressArc.Position = Position;
        ProgressArc.Duration = Duration;
    }
}
