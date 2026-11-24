using InTheLoop.App.Handlers;
using InTheLoop.App.Services;
using InTheLoop.App.ViewModels;
using InTheLoop.App.Views;
using InTheLoop.Core;
using InTheLoop.Core.Services;
using Microsoft.Extensions.Logging;

namespace InTheLoop.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseChromelessInputs();

        builder.Services.AddInTheLoopCore();

        builder.Services.AddSingleton<IUiThread, UiThread>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<IShellService, ShellService>();
        builder.Services.AddSingleton<IAttentionService, AttentionService>();
        builder.Services.AddSingleton<IMcpServerLocator, McpServerLocator>();

        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<ReviewDetailViewModel>();
        builder.Services.AddSingleton<ConnectViewModel>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // One window is enough: a second launch (for example by an MCP server) just exits.
        if (!app.Services.GetRequiredService<IAppPresence>().Announce(Environment.ProcessPath ?? ""))
        {
            Environment.Exit(0);
        }

        return app;
    }
}
