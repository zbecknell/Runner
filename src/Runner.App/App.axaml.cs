using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.ComponentModel;
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
            var configStore = new RunnerConfigStore(RunnerConfigStore.GetDefaultConfigPath());
            var mainWindow = new MainWindow();
            var viewModel = new MainWindowViewModel(
                configStore,
                new RunnerFactory(),
                new AvaloniaWorkingDirectoryPicker(mainWindow),
                new AvaloniaRunnerRemovalConfirmation(mainWindow),
                new VelopackAppUpdateService());

            viewModel.PropertyChanged += OnMainWindowViewModelPropertyChanged;
            mainWindow.DataContext = viewModel;
            desktop.MainWindow = mainWindow;
            _ = viewModel.LoadAsync();

            void OnMainWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(MainWindowViewModel.WindowPlacement))
                {
                    mainWindow.ApplyWindowPlacement(viewModel.WindowPlacement);
                    viewModel.PropertyChanged -= OnMainWindowViewModelPropertyChanged;
                }
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
