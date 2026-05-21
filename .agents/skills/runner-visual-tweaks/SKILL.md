---
name: runner-visual-tweaks
description: Run the local Runner Avalonia desktop app, capture screenshots, and iterate on visual UI changes. Use when working in the C:\git\Runner repository on visual styling, layout, spacing, typography, icon, or interaction-state tweaks that should be verified by launching Runner and inspecting screenshots.
---

# Runner Visual Tweaks

## Workflow

Use this skill for visual work in `C:\git\Runner`.

1. Inspect the relevant Avalonia files before editing, usually `src\Runner.App\Views\MainWindow.axaml`, nearby code-behind, and affected view models.
2. Make narrowly scoped UI changes that fit the existing Avalonia XAML style.
3. Run tests when the change touches bindings, view models, or behavior:

```powershell
dotnet test C:\git\Runner\Runner.slnx
```

4. Capture a screenshot after each meaningful visual revision:

```powershell
powershell -ExecutionPolicy Bypass -File C:\git\Runner\.agents\skills\runner-visual-tweaks\scripts\capture-runner.ps1 -RepoRoot C:\git\Runner
```

5. Inspect the generated PNG in `C:\git\Runner\artifacts\visual-snapshots`, compare it against the request, then repeat edits and captures until the UI is acceptable.

## Screenshot Script

Use `scripts\capture-runner.ps1` instead of rewriting launch and capture code.

Common commands:

```powershell
# Launch Runner, capture the current/default user config, close the launched app.
powershell -ExecutionPolicy Bypass -File C:\git\Runner\.agents\skills\runner-visual-tweaks\scripts\capture-runner.ps1 -RepoRoot C:\git\Runner

# Use a temporary populated fixture config for a more useful dashboard screenshot.
powershell -ExecutionPolicy Bypass -File C:\git\Runner\.agents\skills\runner-visual-tweaks\scripts\capture-runner.ps1 -RepoRoot C:\git\Runner -FixtureConfig

# Launch Runner directly into Settings by passing --settings to the app.
# Use this when verifying Settings window layout or interactions such as project editing and reordering.
dotnet run --project C:\git\Runner\src\Runner.App\Runner.App.csproj -- --settings

# Keep the launched app open for manual inspection; do not combine with -FixtureConfig.
powershell -ExecutionPolicy Bypass -File C:\git\Runner\.agents\skills\runner-visual-tweaks\scripts\capture-runner.ps1 -RepoRoot C:\git\Runner -KeepRunning

# Capture an already-open Runner window without launching or closing it.
powershell -ExecutionPolicy Bypass -File C:\git\Runner\.agents\skills\runner-visual-tweaks\scripts\capture-runner.ps1 -RepoRoot C:\git\Runner -Attach
```

The script prints the screenshot path. Use that path in `view_image` or Markdown image output when visual inspection is needed.

## Fixture Config

Use `-FixtureConfig` when the user's real config is empty or unknown and the visual change needs populated runner rows. The script temporarily replaces `%APPDATA%\Runner\runner-settings.json`, launches Runner, captures the window, closes Runner, and restores the original config.

Do not use `-FixtureConfig` with `-KeepRunning`; the script rejects that combination to avoid later app shutdown overwriting the user's restored config.

## Visual Checks

Check for:

- Text clipping, wrapping, and ellipsis behavior at the 1120x720 default window.
- Overlaps between toolbar buttons, status badges, runner titles, and paths.
- Clear contrast for status colors, disabled regions, and failure panels.
- Stable button and badge dimensions when labels or status text change.
- Consistency with the existing quiet desktop-utility style.

Prefer another screenshot after any follow-up edit. For Avalonia binding or layout errors, inspect shell output from the launch and run `dotnet test`.
