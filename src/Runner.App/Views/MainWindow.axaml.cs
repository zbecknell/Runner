using Avalonia.Controls;
using Runner.App.ViewModels;

namespace Runner.App.Views;

public partial class MainWindow : Window
{
    private bool _closeAfterCleanup;
    private bool _cleanupStarted;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_closeAfterCleanup)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        base.OnClosing(e);
        BeginCleanupAndClose();
    }

    private async void BeginCleanupAndClose()
    {
        if (_cleanupStarted)
        {
            return;
        }

        _cleanupStarted = true;
        IsEnabled = false;

        try
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                await viewModel.DisposeAsync();
            }
        }
        finally
        {
            _closeAfterCleanup = true;
            Close();
        }
    }
}
