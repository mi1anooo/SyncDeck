using Avalonia.Media;

namespace SyncDeck.Themes;

/// <summary>
/// A complete theme definition. ThemeManager converts this into live
/// Avalonia ResourceDictionary entries that the entire view tree reacts to.
/// </summary>
public class ThemeDefinition
{
    public string Id          { get; init; } = "";
    public string DisplayName { get; init; } = "";

    // ── Surfaces ──────────────────────────────────────────────────────────────
    public Color Background      { get; init; }
    public Color SurfacePrimary  { get; init; }
    public Color SurfaceSecondary{ get; init; }
    public Color FrameBorder     { get; init; }

    // ── Accents ───────────────────────────────────────────────────────────────
    public Color AccentPrimary   { get; init; }
    public Color AccentSecondary { get; init; }
    public Color GlowColor       { get; init; }
    public double GlowIntensity  { get; init; } = 6.0;

    // ── Text ──────────────────────────────────────────────────────────────────
    public Color TextPrimary     { get; init; }
    public Color TextSecondary   { get; init; }
    public Color TextLcd         { get; init; }   // LCD/display label color

    // ── Controls ──────────────────────────────────────────────────────────────
    public Color ButtonFill      { get; init; }
    public Color ButtonBorder    { get; init; }
    public Color ButtonHover     { get; init; }
    public Color ProgressTrack   { get; init; }
    public Color ProgressFill    { get; init; }
    public Color ProgressHandle  { get; init; }

    // ── Visualizer ────────────────────────────────────────────────────────────
    public Color VisualizerPrimary   { get; init; }
    public Color VisualizerSecondary { get; init; }
    public string VisualizerStyle    { get; init; } = "disc"; // disc | waveform | bars | minidisc

    // ── Corner / border radii ─────────────────────────────────────────────────
    public double CornerRadius       { get; init; } = 8;
    public double InnerCornerRadius  { get; init; } = 4;
}
