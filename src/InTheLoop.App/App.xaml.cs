using InTheLoop.App.Views;

namespace InTheLoop.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
        UserAppTheme = AppTheme.Dark;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_services.GetRequiredService<MainPage>())
        {
            Title = "In The Loop",
            TitleBar = new TitleBar
            {
                Title = "In The Loop",
                BackgroundColor = (Color)Resources["Canvas"],
                ForegroundColor = (Color)Resources["TextMuted"],
                HeightRequest = 36
            },
            Width = 1560,
            Height = 940,
            MinimumWidth = 1280,
            MinimumHeight = 700
        };
}
