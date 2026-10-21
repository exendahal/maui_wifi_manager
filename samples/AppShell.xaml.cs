namespace DemoApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Windows navigation uses the system icon font so the pane matches Windows 11 apps.
            if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
            {
                SetIcon(networksItem, "\uE701");   // Wifi
                SetIcon(connectionItem, "\uE774"); // Globe
                SetIcon(connectItem, "\uE8D7");    // Permissions (key)
                SetIcon(analyzerItem, "\uE9D9");   // Diagnostic (chart)
                SetIcon(toolsItem, "\uE90F");      // Repair (wrench)
            }
            else if (DeviceInfo.Current.Idiom != DeviceIdiom.Phone)
            {
                // Tablets use the Windows layout: a side pane instead of bottom tabs. Without Mica behind it, the pane
                // gets the page background, and each item shows its tab icon.
                this.SetAppThemeColor(FlyoutBackgroundColorProperty, PaletteColor("BackgroundLight"), PaletteColor("BackgroundDark"));
                foreach (var content in new[] { networksItem, connectionItem, connectItem, analyzerItem, toolsItem })
                {
                    content.FlyoutIcon = content.Icon;
                    if (content.Parent is BaseShellItem section)
                        section.FlyoutIcon = content.Icon;
                }
            }
        }

        private static Color PaletteColor(string key) =>
            Application.Current!.Resources[key] is OnPlatform<Color> perPlatform ? (Color)perPlatform : (Color)Application.Current.Resources[key];

        // The pane reads FlyoutIcon from the implicit section Shell wraps around each ShellContent.
        private static void SetIcon(ShellContent content, string glyph)
        {
            var icon = FluentIcon(glyph);
            content.FlyoutIcon = icon;
            if (content.Parent is BaseShellItem section)
                section.FlyoutIcon = icon;
        }

        private static FontImageSource FluentIcon(string glyph)
        {
            // Segoe Fluent Icons ships with Windows 11 (build 22000+); Windows 10 has the same glyphs in Segoe MDL2 Assets.
            var family = Environment.OSVersion.Version.Build >= 22000 ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
            var icon = new FontImageSource { Glyph = glyph, FontFamily = family, Size = 16 };
            icon.SetAppThemeColor(FontImageSource.ColorProperty, Colors.Black, Colors.White);
            return icon;
        }
    }
}
