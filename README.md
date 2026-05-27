# Runner

Runner is a small Avalonia desktop utility for running local project workflows.

Runner supports .NET projects through `dotnet restore`, `dotnet build`, and `dotnet run`, plus custom command workflows with optional clean, restore, build, and run shell commands.

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

## Release

Installable builds and update packages are produced by GitHub Actions when a tag like `v1.2.3` is pushed:

```powershell
git tag v1.2.3
git push origin v1.2.3
```

The release workflow uses Velopack and publishes Windows x64, macOS Apple Silicon, and macOS Intel assets to GitHub Releases. Installed apps check the public GitHub Releases feed and show an update button when a newer release is available.

Production releases require these repository secrets:

Windows Azure Trusted Signing:

```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_SUBSCRIPTION_ID
AZURE_TRUSTED_SIGNING_ENDPOINT
AZURE_TRUSTED_SIGNING_ACCOUNT
AZURE_TRUSTED_SIGNING_CERT_PROFILE
```

macOS Developer ID signing and notarization:

```text
MACOS_CERT_APP_BASE64
MACOS_CERT_INSTALLER_BASE64
MACOS_CERT_PASSWORD
MACOS_SIGN_APP_IDENTITY
MACOS_SIGN_INSTALL_IDENTITY
MACOS_NOTARY_APPLE_ID
MACOS_NOTARY_TEAM_ID
MACOS_NOTARY_PASSWORD
```

The macOS certificate secrets should be base64-encoded `.p12` exports for Developer ID Application and Developer ID Installer certificates.

See [Release Signing Setup](docs/release-signing-setup.md) for the Azure and Apple setup process.

## Test

```powershell
dotnet test Runner.slnx
```
