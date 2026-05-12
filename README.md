# Runner

Runner is a small Avalonia desktop utility for starting, stopping, and restarting local project processes.

The first runner type is `DotNetProject`, which starts projects through `dotnet run`. The runner model is intentionally generic so additional runner types, such as npm scripts, shell commands, or Docker Compose, can be added later without replacing the UI.

## Run

```powershell
dotnet run --project src\Runner.App\Runner.App.csproj
```

Configuration is saved as editable JSON under the current user's application data folder:

```text
Runner/runner-settings.json
```

## Publish Local Builds

Windows:

```powershell
dotnet publish src\Runner.App\Runner.App.csproj -c Release -r win-x64 --self-contained true
```

macOS Apple Silicon:

```powershell
dotnet publish src\Runner.App\Runner.App.csproj -c Release -r osx-arm64 --self-contained true
```

macOS Intel:

```powershell
dotnet publish src\Runner.App\Runner.App.csproj -c Release -r osx-x64 --self-contained true
```

Installers, signing, notarization, and auto-update are intentionally out of scope for the first version.

## Test

```powershell
dotnet test Runner.slnx
```
