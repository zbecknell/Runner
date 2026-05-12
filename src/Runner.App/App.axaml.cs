using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Runner.App.Services;
using Runner.App.ViewModels;
using Runner.App.Views;
using Runner.Core.Config;
using Runner.Core.Runners;

namespace Runner.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            var viewModel = new MainWindowViewModel(
                new RunnerConfigStore(RunnerConfigStore.GetDefaultConfigPath()),
                new RunnerFactory(),
                new AvaloniaWorkingDirectoryPicker(mainWindow),
                new AvaloniaRunnerRemovalConfirmation(mainWindow));

            mainWindow.DataContext = viewModel;
            desktop.MainWindow = mainWindow;
            _ = viewModel.LoadAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
