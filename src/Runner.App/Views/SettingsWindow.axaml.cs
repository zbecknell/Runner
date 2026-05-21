using Avalonia.Controls;
using Runner.App.Services;
using Runner.App.ViewModels;

namespace Runner.App.Views;

public partial class SettingsWindow : Window
{
    private readonly ISettingsCloseConfirmation _settingsCloseConfirmation;
    private bool _closeConfirmed;
    private bool _closePromptStarted;

    public SettingsWindow()
        : this(null)
    {
    }

    public SettingsWindow(ISettingsCloseConfirmation? settingsCloseConfirmation)
    {
        InitializeComponent();
        _settingsCloseConfirmation = settingsCloseConfirmation ?? new AvaloniaSettingsCloseConfirmation(this);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_closeConfirmed)
        {
            base.OnClosing(e);
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel || !viewModel.IsSettingsDirty)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        base.OnClosing(e);
        ConfirmCloseAsync(viewModel);
    }

    private async void ConfirmCloseAsync(MainWindowViewModel viewModel)
    {
        if (_closePromptStarted)
        {
            return;
        }

        _closePromptStarted = true;

        try
        {
            var action = await _settingsCloseConfirmation.ConfirmCloseAsync();

            if (action == SettingsCloseAction.Cancel)
            {
                return;
            }

            if (action == SettingsCloseAction.Save)
            {
                await viewModel.SaveConfigCommand.ExecuteAsync(null);
            }
            else if (action == SettingsCloseAction.Discard)
            {
                viewModel.DiscardSettingsChanges();
            }

            _closeConfirmed = true;
            Close();
        }
        finally
        {
            _closePromptStarted = false;
        }
    }
}
