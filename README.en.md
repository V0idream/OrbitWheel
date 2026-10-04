# OrbitWheel

A radial shortcut launcher for Windows. Open a six-section wheel at the mouse position to launch apps, open folders, or run system actions.

[简体中文](README.md) · **English**

[![Release](https://img.shields.io/github/v/release/V0idream/OrbitWheel)](https://github.com/V0idream/OrbitWheel/releases/latest)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## Preview

The new WinUI 3 settings window uses native Fluent controls, with six pages for general settings, pages and actions, appearance, hotkeys, mouse gestures, and app information.

![OrbitWheel Fluent settings: pages and actions](assets/settings-fluent.jpg)

The screenshot shows a local build of the current 2.1 source with the default configuration. See [Releases](https://github.com/V0idream/OrbitWheel/releases) for publicly available versions. The settings interface shown is in Chinese.

## Features

- Six-section radial menu, numbered clockwise from the right-hand sector as `1–6`, with support for multiple pages.
- Execute actions by clicking, holding and releasing a hotkey, or pressing a number key.
- Launch desktop and Store / UWP apps, open folders, and run custom commands or common system actions.
- Prefer an existing window when the target app is already running, with compatibility logic for waking apps from the system tray.
- Choose liquid glass, Gaussian blur, or acrylic wheel effects. The wheel repositions near screen edges to remain visible.
- Optional mouse gestures: hold both mouse buttons, then swipe up for Start, down for the desktop, or horizontally to open and navigate `Alt+Tab`.
- Automatic settings saving, optional Windows startup, and system tray operation.

## Download and run

1. Download the self-contained package ending in `-self-contained.zip` from [Releases](https://github.com/V0idream/OrbitWheel/releases/latest).
2. Extract the entire archive, keep the `Settings` directory, and run `OrbitWheel.exe` from the root directory.
3. Double-click the system tray icon to open settings. Record a key combination on the hotkeys page and configure sectors on the pages and actions page.

The current source requires Windows 10 version 2004 or later (x64), .NET Framework, and the Visual C++ x64 runtime. The self-contained package includes the .NET and Windows App SDK runtimes. The framework-dependent package additionally requires .NET 10 x64 Runtime and a matching Windows App Runtime 1.8. System backdrop effects depend on the Windows version and system settings. See the [deployment notes](docs/winui-deployment.md) (Chinese) for details.

## Controls

| Action | Method |
| --- | --- |
| Default hotkey | `Ctrl + Space`; customizable in settings |
| Hold mode | Hold the hotkey, move toward a sector, then release to execute |
| Click mode | Press the hotkey to open the wheel, then click a sector |
| Execute a sector directly | Press `1–6` while the wheel is visible |
| Switch pages | Mouse wheel or left/right arrow keys |
| Cancel | Press `Esc`, or return to the center before releasing in hold mode |

Configuration is saved at:

```text
%APPDATA%\OrbitWheel\config.json
```

Closing settings leaves the main app running. Use the system tray menu to exit the app.

## Build from source

Install .NET SDK 10.0.400 on Windows, then run this command using Windows PowerShell 5.1:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release.ps1
```

The script builds the main app and WinUI settings, restores NuGet dependencies, and creates two deployment packages:

```text
dist\release\OrbitWheel-2.1-self-contained.zip
dist\release\OrbitWheel-2.1-framework-dependent.zip
```

The same directory contains `SHA256SUMS.txt`, `PACKAGE-SIZES.json`, and release notes. Running `build.ps1` alone builds only the main app; use the packaging script for a complete runnable distribution.

## Contributing and feedback

Report bugs and suggest features through [Issues](https://github.com/V0idream/OrbitWheel/issues). See the [contributing guide](CONTRIBUTING.md) (Chinese) for development and validation instructions.

## License

[MIT License](LICENSE)
