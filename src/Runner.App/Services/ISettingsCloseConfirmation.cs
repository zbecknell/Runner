namespace Runner.App.Services;

public interface ISettingsCloseConfirmation
{
    Task<SettingsCloseAction> ConfirmCloseAsync(CancellationToken cancellationToken = default);
}
