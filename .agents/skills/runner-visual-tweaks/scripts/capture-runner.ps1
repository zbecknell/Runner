param(
    [string]$RepoRoot = "C:\git\Runner",
    [string]$ProjectPath = "src\Runner.App\Runner.App.csproj",
    [string]$OutputDir,
    [string]$WindowTitle = "Runner",
    [string]$ProcessName = "Runner",
    [int]$WaitSeconds = 30,
    [int]$SettleMilliseconds = 1000,
    [switch]$Attach,
    [switch]$KeepRunning,
    [switch]$FixtureConfig
)

$ErrorActionPreference = "Stop"

if ($FixtureConfig -and $KeepRunning) {
    throw "-FixtureConfig cannot be combined with -KeepRunning because Runner saves config on close."
}

$repoFullPath = [System.IO.Path]::GetFullPath($RepoRoot)
$projectFullPath = [System.IO.Path]::GetFullPath((Join-Path $repoFullPath $ProjectPath))

if (-not (Test-Path -LiteralPath $projectFullPath)) {
    throw "Runner project not found: $projectFullPath"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoFullPath "artifacts\visual-snapshots"
}

$outputFullPath = [System.IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Force -Path $outputFullPath | Out-Null

$nativeCode = @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class RunnerVisualNative
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@

Add-Type -TypeDefinition $nativeCode -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.Drawing

function Get-RunnerWindow {
    param(
        [string]$Title,
        [string]$ExpectedProcessName
    )

    $matches = New-Object System.Collections.Generic.List[object]
    $callback = [RunnerVisualNative+EnumWindowsProc]{
        param([IntPtr]$hWnd, [IntPtr]$lParam)

        if (-not [RunnerVisualNative]::IsWindowVisible($hWnd)) {
            return $true
        }

        $builder = New-Object System.Text.StringBuilder 512
        [void][RunnerVisualNative]::GetWindowText($hWnd, $builder, $builder.Capacity)
        $text = $builder.ToString()

        if ($text -like "*$Title*") {
            [uint32]$processId = 0
            [void][RunnerVisualNative]::GetWindowThreadProcessId($hWnd, [ref]$processId)

            $process = Get-Process -Id ([int]$processId) -ErrorAction SilentlyContinue
            if ($null -eq $process -or $process.ProcessName -ne $ExpectedProcessName) {
                return $true
            }

            $matches.Add([pscustomobject]@{
                Handle = $hWnd
                Title = $text
                ProcessId = [int]$processId
                ProcessName = $process.ProcessName
            })
        }

        return $true
    }

    [void][RunnerVisualNative]::EnumWindows($callback, [IntPtr]::Zero)
    return $matches | Select-Object -First 1
}

function Wait-RunnerWindow {
    param(
        [string]$Title,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    do {
        $window = Get-RunnerWindow -Title $Title -ExpectedProcessName $ProcessName
        if ($null -ne $window) {
            return $window
        }

        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for a visible '$ProcessName' process window containing title '$Title'."
}

function Write-FixtureConfig {
    param([string]$ConfigPath)

    $directory = Split-Path -Parent $ConfigPath
    New-Item -ItemType Directory -Force -Path $directory | Out-Null

    $escapedRepo = $repoFullPath.Replace("\", "\\")
    $fixture = @"
{
  "alwaysOnTop": false,
  "windowPlacement": {
    "x": 120,
    "y": 80,
    "width": 1120,
    "height": 720,
    "isMaximized": false
  },
  "runners": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "displayName": "Runner app",
      "type": "DotNetProject",
      "workingDirectory": "$escapedRepo",
      "command": "src\\Runner.App\\Runner.App.csproj",
      "arguments": "",
      "environmentVariables": {}
    },
    {
      "id": "22222222-2222-2222-2222-222222222222",
      "displayName": "Core tests",
      "type": "DotNetProject",
      "workingDirectory": "$escapedRepo",
      "command": "tests\\Runner.Core.Tests\\Runner.Core.Tests.csproj",
      "arguments": "",
      "environmentVariables": {}
    },
    {
      "id": "33333333-3333-3333-3333-333333333333",
      "displayName": "Release build check",
      "type": "DotNetProject",
      "workingDirectory": "$escapedRepo",
      "command": "src\\Runner.App\\Runner.App.csproj",
      "arguments": "-c Release",
      "environmentVariables": {
        "RUNNER_VISUAL_FIXTURE": "1"
      }
    }
  ]
}
"@

    Set-Content -LiteralPath $ConfigPath -Value $fixture -Encoding UTF8
}

$launchedProcess = $null
$window = $null
$configPath = Join-Path $env:APPDATA "Runner\runner-settings.json"
$backupPath = $null
$hadConfig = $false

try {
    if ($FixtureConfig) {
        $hadConfig = Test-Path -LiteralPath $configPath
        if ($hadConfig) {
            $backupPath = Join-Path $outputFullPath ("runner-settings.backup.{0}.json" -f (Get-Date -Format "yyyyMMdd-HHmmss"))
            Copy-Item -LiteralPath $configPath -Destination $backupPath -Force
        }

        Write-FixtureConfig -ConfigPath $configPath
    }

    if (-not $Attach) {
        $stdoutPath = Join-Path $outputFullPath "runner-launch.stdout.log"
        $stderrPath = Join-Path $outputFullPath "runner-launch.stderr.log"
        $arguments = @("run", "--project", $projectFullPath)
        $launchedProcess = Start-Process -FilePath "dotnet" -ArgumentList $arguments -WorkingDirectory $repoFullPath -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
    }

    $window = Wait-RunnerWindow -Title $WindowTitle -TimeoutSeconds $WaitSeconds

    [void][RunnerVisualNative]::ShowWindow($window.Handle, 9)
    [void][RunnerVisualNative]::SetForegroundWindow($window.Handle)
    Start-Sleep -Milliseconds $SettleMilliseconds

    $rect = New-Object RunnerVisualNative+RECT
    if (-not [RunnerVisualNative]::GetWindowRect($window.Handle, [ref]$rect)) {
        throw "Failed to read Runner window bounds."
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top

    if ($width -le 0 -or $height -le 0) {
        throw "Runner window bounds are invalid: $width x $height."
    }

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $screenshotPath = Join-Path $outputFullPath "runner-$timestamp.png"

    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $width, $height))
        $bitmap.Save($screenshotPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    Write-Output $screenshotPath
}
finally {
    if ($window -and -not $Attach -and -not $KeepRunning) {
        $runnerProcess = Get-Process -Id $window.ProcessId -ErrorAction SilentlyContinue
        if ($runnerProcess) {
            [void]$runnerProcess.CloseMainWindow()
            if (-not $runnerProcess.WaitForExit(5000)) {
                $runnerProcess.Kill()
            }
        }
    }

    if ($launchedProcess -and -not $launchedProcess.HasExited -and -not $KeepRunning) {
        $launchedProcess.Kill()
    }

    if ($FixtureConfig) {
        if ($hadConfig -and $backupPath -and (Test-Path -LiteralPath $backupPath)) {
            Copy-Item -LiteralPath $backupPath -Destination $configPath -Force
        }
        elseif (-not $hadConfig -and (Test-Path -LiteralPath $configPath)) {
            Remove-Item -LiteralPath $configPath -Force
        }
    }
}
