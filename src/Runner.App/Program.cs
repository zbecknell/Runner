using Avalonia;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;
using Runner.App.Services;
using System;
using Velopack;

namespace Runner.App;

sealed class Program
{
    private const string SingleInstanceName = "Runner.RunnerApp.SingleInstance";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        using var singleInstanceService = SingleInstanceService.Acquire(SingleInstanceName);

        if (!singleInstanceService.IsPrimaryInstance)
        {
            _ = singleInstanceService.SendLaunchRequestAsync(args).GetAwaiter().GetResult();
            return;
        }

        singleInstanceService.StartListening();
        App.SingleInstanceService = singleInstanceService;

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            App.SingleInstanceService = null;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
    }
}
