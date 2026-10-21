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
        }

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
