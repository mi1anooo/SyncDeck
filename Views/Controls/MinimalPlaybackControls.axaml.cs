using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Collections;
using Avalonia.Media;
using System;
using System.Windows.Input;

namespace SyncDeck.Views.Controls;

public partial class MinimalPlaybackControls : UserControl
{
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<MinimalPlaybackControls, bool>(nameof(IsPlaying));
    public static readonly StyledProperty<double> PositionProperty =
        AvaloniaProperty.Register<MinimalPlaybackControls, double>(nameof(Position));
    public static readonly StyledProperty<double> DurationProperty =
        AvaloniaProperty.Register<MinimalPlaybackControls, double>(nameof(Duration), 1.0);
    public static readonly StyledProperty<string> ElapsedTextProperty =
        AvaloniaProperty.Register<MinimalPlaybackControls, string>(nameof(ElapsedText), "0:00");
    public static readonly StyledProperty<string> DurationTextProperty =
        AvaloniaProperty.Register<MinimalPlaybackControls, string>(nameof(DurationText), "0:00");
    public static readonly StyledProperty<ICommand?> PlayPauseCommandProperty =
        AvaloniaProperty.Register<MinimalPlaybackControls, ICommand?>(nameof(PlayPauseCommand));
    public static readonly StyledProperty<ICommand?> NextCommandProperty =
        AvaloniaProperty.Register<MinimalPlaybackControls, ICommand?>(nameof(NextCommand));
    public static readonly StyledProperty<ICommand?> PreviousCommandProperty =
        AvaloniaProperty.Register<MinimalPlaybackControls, ICommand?>(nameof(PreviousCommand));

    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }
    public double Position { get => GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public double Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public string ElapsedText { get => GetValue(ElapsedTextProperty); set => SetValue(ElapsedTextProperty, value); }
    public string DurationText { get => GetValue(DurationTextProperty); set => SetValue(DurationTextProperty, value); }
    public ICommand? PlayPauseCommand { get => GetValue(PlayPauseCommandProperty); set => SetValue(PlayPauseCommandProperty, value); }
    public ICommand? NextCommand { get => GetValue(NextCommandProperty); set => SetValue(NextCommandProperty, value); }
    public ICommand? PreviousCommand { get => GetValue(PreviousCommandProperty); set => SetValue(PreviousCommandProperty, value); }

    public event EventHandler<double>? SeekRequested;

    public MinimalPlaybackControls()
    {
        InitializeComponent();
        PlayPauseBtn.Click += (_, _) => PlayPauseCommand?.Execute(null);
        NextBtn.Click += (_, _) => NextCommand?.Execute(null);
        PrevBtn.Click += (_, _) => PreviousCommand?.Execute(null);
        UpdatePlayIcon();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPlayingProperty)
            UpdatePlayIcon();
    }

    private void UpdatePlayIcon()
    {
        if (PlayIcon is null) return;
        PlayIcon.Children.Clear();

        if (IsPlaying)
        {
            var left = new Rectangle { Width = 4.2, Height = 16, RadiusX = 1, RadiusY = 1, Fill = new SolidColorBrush(Color.Parse("#F3F3EF")) };
            var right = new Rectangle { Width = 4.2, Height = 16, RadiusX = 1, RadiusY = 1, Fill = new SolidColorBrush(Color.Parse("#F3F3EF")) };
            Canvas.SetLeft(left, 4); Canvas.SetTop(left, 1);
            Canvas.SetLeft(right, 10); Canvas.SetTop(right, 1);
            PlayIcon.Children.Add(left);
            PlayIcon.Children.Add(right);
        }
        else
        {
            var tri = new Polygon
            {
                Points = new AvaloniaList<Point> { new(5, 2), new(16, 9), new(5, 16) },
                Fill = new SolidColorBrush(Color.Parse("#F3F3EF"))
            };
            PlayIcon.Children.Add(tri);
        }
    }

    // Kept for compatibility with MainWindow. The turntable design uses the platter arc,
    // but this event is still available for future click/drag seek implementation.
    public void RequestSeek(double seconds) => SeekRequested?.Invoke(this, seconds);
}
