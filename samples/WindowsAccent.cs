namespace DemoApp;

/// <summary>
/// The user's Windows accent color, in the shades WinUI uses for accent fills
/// (AccentDark1 on light theme, AccentLight2 on dark theme). Other platforms never read these.
/// </summary>
public static class WindowsAccent
{
#if WINDOWS
    private static readonly Windows.UI.ViewManagement.UISettings Settings = new();

    public static Color Light { get; } = Read(Windows.UI.ViewManagement.UIColorType.AccentDark1);
    public static Color Dark { get; } = Read(Windows.UI.ViewManagement.UIColorType.AccentLight2);

    private static Color Read(Windows.UI.ViewManagement.UIColorType type)
    {
        var c = Settings.GetColorValue(type);
        return Color.FromRgba(c.R, c.G, c.B, c.A);
    }
#else
    public static Color Light => Colors.Transparent;
    public static Color Dark => Colors.Transparent;
#endif
}
