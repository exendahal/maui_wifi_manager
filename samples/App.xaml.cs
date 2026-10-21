namespace DemoApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());
            // Headless device testing: run the self-test requested at launch once the UI is up.
            SelfTest.ReadTriggerFile();
            window.Created += (_, _) => Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(2), () => _ = SelfTest.RunPendingAsync());
            return window;
        }
    }
}
