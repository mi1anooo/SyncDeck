using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Collections.Generic;

namespace SyncDeck.Themes;

/// <summary>
/// Central theme registry and applicator.
/// Calling ApplyTheme() atomically swaps all theme resource keys in the
/// Application's merged dictionaries — no restart required, no layout freeze.
/// </summary>
public class ThemeManager
{
    public static readonly IReadOnlyList<ThemeDefinition> All = new List<ThemeDefinition>
    {
        // ── 1. Turntable Noir ────────────────────────────────────────────────
        new()
        {
            Id              = "SonyChrome",
            DisplayName     = "Turntable Noir",
            Background      = Color.Parse("#0B0B0B"),
            SurfacePrimary  = Color.Parse("#121212"),
            SurfaceSecondary= Color.Parse("#181818"),
            FrameBorder     = Color.Parse("#2A2A2A"),
            AccentPrimary   = Color.Parse("#E5E2D8"),
            AccentSecondary = Color.Parse("#A9AAA4"),
            GlowColor       = Color.Parse("#000000"),
            GlowIntensity   = 0,
            TextPrimary     = Color.Parse("#ECECE6"),
            TextSecondary   = Color.Parse("#8E8E88"),
            TextLcd         = Color.Parse("#D8D8D2"),
            ButtonFill      = Color.Parse("#1B1B1B"),
            ButtonBorder    = Color.Parse("#363636"),
            ButtonHover     = Color.Parse("#262626"),
            ProgressTrack   = Color.Parse("#242424"),
            ProgressFill    = Color.Parse("#E5E2D8"),
            ProgressHandle  = Color.Parse("#F2F0E9"),
            VisualizerPrimary   = Color.Parse("#E5E2D8"),
            VisualizerSecondary = Color.Parse("#101010"),
            VisualizerStyle     = "turntable",
            CornerRadius        = 34,
            InnerCornerRadius   = 18,
        },

        // ── 2. Cyber Tribal ───────────────────────────────────────────────────
        new()
        {
            Id              = "CyberTribal",
            DisplayName     = "Cyber Tribal",
            Background      = Color.Parse("#0A0A0F"),
            SurfacePrimary  = Color.Parse("#14121E"),
            SurfaceSecondary= Color.Parse("#1C192A"),
            FrameBorder     = Color.Parse("#3A2860"),
            AccentPrimary   = Color.Parse("#8B5CF6"),
            AccentSecondary = Color.Parse("#C4B5FD"),
            GlowColor       = Color.Parse("#7C3AED"),
            GlowIntensity   = 10,
            TextPrimary     = Color.Parse("#EDE8FF"),
            TextSecondary   = Color.Parse("#7060A0"),
            TextLcd         = Color.Parse("#A78BFA"),
            ButtonFill      = Color.Parse("#1E1830"),
            ButtonBorder    = Color.Parse("#4C3880"),
            ButtonHover     = Color.Parse("#2C2244"),
            ProgressTrack   = Color.Parse("#1A1528"),
            ProgressFill    = Color.Parse("#8B5CF6"),
            ProgressHandle  = Color.Parse("#C4B5FD"),
            VisualizerPrimary   = Color.Parse("#8B5CF6"),
            VisualizerSecondary = Color.Parse("#2D1B6E"),
            VisualizerStyle     = "disc",
            CornerRadius        = 6,
            InnerCornerRadius   = 2,
        },

        // ── 3. Eurotrash Club ─────────────────────────────────────────────────
        new()
        {
            Id              = "EurotrashClub",
            DisplayName     = "Eurotrash Club",
            Background      = Color.Parse("#060806"),
            SurfacePrimary  = Color.Parse("#0C100C"),
            SurfaceSecondary= Color.Parse("#111811"),
            FrameBorder     = Color.Parse("#1A3A1A"),
            AccentPrimary   = Color.Parse("#39FF14"),
            AccentSecondary = Color.Parse("#9AFF72"),
            GlowColor       = Color.Parse("#39FF14"),
            GlowIntensity   = 12,
            TextPrimary     = Color.Parse("#D8FFD0"),
            TextSecondary   = Color.Parse("#407040"),
            TextLcd         = Color.Parse("#39FF14"),
            ButtonFill      = Color.Parse("#101810"),
            ButtonBorder    = Color.Parse("#204020"),
            ButtonHover     = Color.Parse("#1A281A"),
            ProgressTrack   = Color.Parse("#0C180C"),
            ProgressFill    = Color.Parse("#39FF14"),
            ProgressHandle  = Color.Parse("#9AFF72"),
            VisualizerPrimary   = Color.Parse("#39FF14"),
            VisualizerSecondary = Color.Parse("#0A280A"),
            VisualizerStyle     = "bars",
            CornerRadius        = 2,
            InnerCornerRadius   = 0,
        },

        // ── 4. Frosted Blue ───────────────────────────────────────────────────
        new()
        {
            Id              = "FrostedBlue",
            DisplayName     = "Frosted Blue",
            Background      = Color.Parse("#080C14"),
            SurfacePrimary  = Color.Parse("#101828"),
            SurfaceSecondary= Color.Parse("#162034"),
            FrameBorder     = Color.Parse("#2A4870"),
            AccentPrimary   = Color.Parse("#60A5FA"),
            AccentSecondary = Color.Parse("#BAD8FF"),
            GlowColor       = Color.Parse("#3B82F6"),
            GlowIntensity   = 6,
            TextPrimary     = Color.Parse("#E0EEFF"),
            TextSecondary   = Color.Parse("#6080B0"),
            TextLcd         = Color.Parse("#93C5FD"),
            ButtonFill      = Color.Parse("#162030"),
            ButtonBorder    = Color.Parse("#3060A0"),
            ButtonHover     = Color.Parse("#1E3050"),
            ProgressTrack   = Color.Parse("#101828"),
            ProgressFill    = Color.Parse("#60A5FA"),
            ProgressHandle  = Color.Parse("#BAD8FF"),
            VisualizerPrimary   = Color.Parse("#60A5FA"),
            VisualizerSecondary = Color.Parse("#0A1E40"),
            VisualizerStyle     = "waveform",
            CornerRadius        = 14,
            InnerCornerRadius   = 8,
        },
    };

    private static readonly Dictionary<string, ThemeDefinition> _map = new();

    public ThemeDefinition Current { get; private set; } = All[0];

    public event EventHandler<ThemeDefinition> ThemeChanged = delegate { };

    static ThemeManager()
    {
        foreach (var t in All)
            _map[t.Id] = t;
    }

    public void ApplyTheme(string themeId)
    {
        if (!_map.TryGetValue(themeId, out var def)) return;
        Current = def;
        PushToResources(def);
        ThemeChanged.Invoke(this, def);
    }

    // ── Push all theme keys into Application.Current.Resources ────────────────
    private static void PushToResources(ThemeDefinition t)
    {
        var res = Application.Current!.Resources;

        // Brushes
        Set(res, "ThemeBackground",       new SolidColorBrush(t.Background));
        Set(res, "ThemeSurfacePrimary",   new SolidColorBrush(t.SurfacePrimary));
        Set(res, "ThemeSurfaceSecondary", new SolidColorBrush(t.SurfaceSecondary));
        Set(res, "ThemeFrameBorder",      new SolidColorBrush(t.FrameBorder));
        Set(res, "ThemeAccentPrimary",    new SolidColorBrush(t.AccentPrimary));
        Set(res, "ThemeAccentSecondary",  new SolidColorBrush(t.AccentSecondary));
        Set(res, "ThemeGlow",             new SolidColorBrush(t.GlowColor));
        Set(res, "ThemeTextPrimary",      new SolidColorBrush(t.TextPrimary));
        Set(res, "ThemeTextSecondary",    new SolidColorBrush(t.TextSecondary));
        Set(res, "ThemeTextLcd",          new SolidColorBrush(t.TextLcd));
        Set(res, "ThemeButtonFill",       new SolidColorBrush(t.ButtonFill));
        Set(res, "ThemeButtonBorder",     new SolidColorBrush(t.ButtonBorder));
        Set(res, "ThemeButtonHover",      new SolidColorBrush(t.ButtonHover));
        Set(res, "ThemeProgressTrack",    new SolidColorBrush(t.ProgressTrack));
        Set(res, "ThemeProgressFill",     new SolidColorBrush(t.ProgressFill));
        Set(res, "ThemeProgressHandle",   new SolidColorBrush(t.ProgressHandle));
        Set(res, "ThemeVisPrimary",       new SolidColorBrush(t.VisualizerPrimary));
        Set(res, "ThemeVisSecondary",     new SolidColorBrush(t.VisualizerSecondary));

        // Raw colors (for custom-drawn controls)
        Set(res, "ThemeAccentPrimaryColor",  t.AccentPrimary);
        Set(res, "ThemeGlowColor",           t.GlowColor);
        Set(res, "ThemeVisPrimaryColor",     t.VisualizerPrimary);
        Set(res, "ThemeVisSecondaryColor",   t.VisualizerSecondary);

        // Scalars
        Set(res, "ThemeGlowIntensity",   t.GlowIntensity);
        Set(res, "ThemeCornerRadius",    new CornerRadius(t.CornerRadius));
        Set(res, "ThemeInnerCorner",     new CornerRadius(t.InnerCornerRadius));
        Set(res, "ThemeVisStyle",        t.VisualizerStyle);
    }

    private static void Set(IResourceDictionary res, object key, object value)
    {
        res[key] = value;
    }
}
