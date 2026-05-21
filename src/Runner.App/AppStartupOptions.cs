namespace Runner.App;

public sealed class AppStartupOptions
{
    public bool OpenSettings { get; init; }

    public static AppStartupOptions Parse(IEnumerable<string>? args)
    {
        return new AppStartupOptions
        {
            OpenSettings = args?.Any(arg => string.Equals(arg, "--settings", StringComparison.OrdinalIgnoreCase)) == true
        };
    }
}
