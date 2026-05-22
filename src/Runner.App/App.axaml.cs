using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.ComponentModel;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Runner.App.Services;
using Runner.App.ViewModels;
using Runner.App.Views;
using Runner.Core.Config;
using Runner.Core.Runners;

namespace Runner.App;

public partial class App : Application
{
    private TrayIconController? _trayIconController;

    internal static SingleInstanceService? SingleInstanceService { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var startupOptions = AppStartupOptions.Parse(desktop.Args);
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
            _trayIconController = new TrayIconController(desktop, mainWindow);
            DataContext = _trayIconController;
            _ = LoadAndApplyStartupOptionsAsync();

            void OnMainWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(MainWindowViewModel.WindowPlacement))
                {
                    mainWindow.ApplyWindowPlacement(viewModel.WindowPlacement);
                    viewModel.PropertyChanged -= OnMainWindowViewModelPropertyChanged;
                }
            }

            async Task LoadAndApplyStartupOptionsAsync()
            {
                await viewModel.LoadAsync();
                HandleStartupRequest(startupOptions, isSecondaryLaunch: false);
                SingleInstanceService?.RegisterLaunchRequestHandler(DispatchSecondaryLaunchAsync);
            }

            Task DispatchSecondaryLaunchAsync(string[] args)
            {
                var completionSource = new TaskCompletionSource();

                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        HandleStartupRequest(
                            AppStartupOptions.Parse(args),
                            isSecondaryLaunch: true);
                        completionSource.SetResult();
                    }
                    catch (Exception ex)
                    {
                        completionSource.SetException(ex);
                    }
                });

                return completionSource.Task;
            }

            void HandleStartupRequest(AppStartupOptions options, bool isSecondaryLaunch)
            {
                var action = AppStartupRequestBehavior.DetermineAction(options, isSecondaryLaunch);

                if (action == AppStartupRequestAction.ToggleDashboard)
                {
                    mainWindow.ToggleTrayVisibility();
                }
                else if (action == AppStartupRequestAction.OpenSettings)
                {
                    if (isSecondaryLaunch)
                    {
                        mainWindow.ShowFromTray();
                    }

                    mainWindow.OpenSettingsWindow();
                }
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
