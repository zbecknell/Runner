namespace Runner.App.Services;

public sealed class NullSettingsCloseConfirmation : ISettingsCloseConfirmation
{
    public static NullSettingsCloseConfirmation Instance { get; } = new();

    private NullSettingsCloseConfirmation()
    {
    }

    public Task<SettingsCloseAction> ConfirmCloseAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SettingsCloseAction.Cancel);
    }
}
