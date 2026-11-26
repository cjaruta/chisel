# Chisel

A daily planner dashboard for Windows that keeps your schedule, tasks, notes and handy tools in one window, stored on your own computer.

<!-- Screenshots: add the files listed in assets/screenshots/README.md, then remove this comment's markers.
![The Chisel dashboard with today's schedule, tasks, notes, clock and weather](assets/screenshots/dashboard.png)
![The widget library, where widgets are added to the dashboard](assets/screenshots/widget-library.png)
![Settings, showing the Appearance tab with theme and accent color](assets/screenshots/settings.png)
![The clock widget's analog stopwatch faces](assets/screenshots/stopwatch-faces.png)
![The dashboard in the dark theme](assets/screenshots/dark-theme.png)
-->

## Features

- **A dashboard you arrange yourself.** Drag, resize and reorder widgets in columns, show or hide their titles, and combine
  widgets into tabbed or side-by-side groups. Save layouts as profiles and switch between them.
- **Your day at a glance.** Today's schedule with the current block highlighted, a live clock in the top bar and quick
  links to the sites, files and folders you use most.
- **Looks how you like.** System, light and dark themes, an accent color, adjustable widget spacing and 12 or 24-hour time.
- **Stays out of the way.** Optionally starts with Windows, waits in the tray, and shows or hides with a keyboard shortcut.
- **Backup in one file.** Export everything to one file and import it later, replacing or merging, with an undo for the
  last import.
- **Keeps your data safe.** If saved data is damaged, Chisel keeps a copy and starts fresh instead of failing.

## Widgets

| Widget | What it does |
| --- | --- |
| Today | Your schedule for the day, with the current block highlighted. |
| Tasks | Things to do, with a quick box to add more, due dates and drag to reorder. |
| Notes | Quick notes you can jot down and keep, in separate pads. |
| Clock | Clock faces (including custom analog faces), a timer, a stopwatch and alarms. |
| Weather | Current conditions and the forecast for your place. |
| Media | Controls, artwork and volume for what is playing. |
| Picture frame | Shows your own pictures as a slideshow. Add as many frames as you like. |
| Calculator | Basic and advanced calculator. |
| Daily verse | A short Bible passage (KJV) that changes every day. |
| Calendar | A month view with today highlighted. Mark days with a dot. |
| System monitor | Live CPU, memory, disk, network and battery gauges. |
| Countdown | Days until the things you are looking forward to. |
| Focus timer | Pomodoro focus sessions and breaks with a draining ring. |
| Habits | Daily habits with check-ins, streaks and a short history. |
| Converter | Converts length, weight, temperature, volume, area, speed, data size and time. |
| Clipboard history | Text you copy, to copy again. Off until you press Start recording. |

## System requirements

- Windows 10 (version 2004 or later) or Windows 11, 64-bit (x64)
- An internet connection only for the weather widget and for pictures dropped from a web page

## Install

1. Download `Chisel-<version>-win-x64.zip` from the [Releases page](https://github.com/cjaruta/chisel/releases).
2. Optional: check the download against the SHA-256 checksum in the release notes:
   `Get-FileHash .\Chisel-<version>-win-x64.zip` in PowerShell.
3. Extract the zip to a folder you keep, such as `%LocalAppData%\Programs\Chisel`, and run `Chisel.App.exe`.

The app is not code-signed yet, so Windows SmartScreen may show "Windows protected your PC" the first time. Choose
**More info**, then **Run anyway**. Windows remembers the choice for that copy.

To uninstall, turn off **Start with Windows** in Settings > General (if you turned it on), then delete the folder.
Your data stays in `%AppData%\Chisel` until you delete that too.

## Your data

Everything is stored in `%AppData%\Chisel`:

- `chisel.db`: your schedule, tasks, notes, settings, layouts and widget data (SQLite)
- `pictures\`: copies of the pictures you add to picture frames
- `backups\`: the automatic copy taken before each import
- `logs\`: crash reports (no personal data)
- `clipboard-history.json`: only if you choose to save clipboard history

**Back up:** Settings > Backup > Export saves your settings, dashboard and the data of every widget to one file.
Picture files stay on your computer (the backup lists them), and clipboard history is never included.
**Restore:** Settings > Backup > Import, then choose Replace or Merge. Your current data is backed up first, so
**Undo last import** can put it back.

## Privacy

Chisel works offline and has no accounts, ads or telemetry. Nothing you type is sent anywhere.

Only these features use the network:

- **Weather** sends the place you search for, and then its coordinates, to [Open-Meteo](https://open-meteo.com/) to
  find it and fetch its forecast.
- **Picture frame** downloads a picture when you drag one in from a web page.

Quick links open in your own browser or apps when you click them. Clipboard history records nothing until you press
Start recording, skips password managers, and is kept in memory unless you choose to save it.

## Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| Ctrl+Alt+C | Show or hide Chisel from anywhere (change it or turn it off in Settings > General) |
| Space | Play or pause the current media (when you are not typing) |
| Esc | Close the open dialog, menu or panel |
| Enter | Confirm the box you are typing in (add a task, habit, alarm, and so on) |

The calculator also takes number, operator, Enter and Backspace keys while it has focus.

## Known limitations

- Windows only (Windows 10 version 2004 or later, x64).
- The app is not code-signed, so SmartScreen shows a warning on first run.
- No installer yet: the release is a zip you extract.
- The daily verse uses the King James Version only.
- The converter works offline, so it has no currency conversion.
- Clipboard history records text only, not images or files.
- Weather needs an internet connection and uses Open-Meteo's free service.
- Media controls only show apps that report to Windows' media controls (most music and video players and browsers do).
- No sync between computers; use export and import to move your data.

## Building from source

You need Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
git clone https://github.com/cjaruta/chisel.git
cd chisel
dotnet build
dotnet test
dotnet run --project src/Chisel.App
```

To build a self-contained release folder (the version comes from `Directory.Build.props`):

```
dotnet publish src/Chisel.App -c Release -r win-x64 --self-contained -p:DebugType=none -o artifacts/release/Chisel-win-x64
```

The project layout, test approach and how to report bugs are in [CONTRIBUTING.md](CONTRIBUTING.md).

## License and attributions

Chisel is released under the [MIT License](LICENSE). It uses open-source packages, the Inter font, the King James
Version text and weather data from Open-Meteo; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for their
licenses and attributions.
