The first public release of Chisel: a daily planner dashboard for Windows that keeps your schedule, tasks, notes and
handy tools in one window, stored on your own computer. No accounts, no ads, no telemetry.

## Highlights

- 16 widgets: Today (schedule), Tasks, Notes, Clock (faces, timer, stopwatch, alarms), Weather, Media, Picture frame,
  Calculator, Daily verse, Calendar, System monitor, Countdown, Focus timer, Habits, Converter and Clipboard history.
- Arrange them your way: drag, resize, group as tabs or side by side, and save layouts as profiles.
- System, light and dark themes with an accent color.
- Starts with Windows, waits in the tray, and shows or hides with Ctrl+Alt+C.
- Export and import all your data in one file, with an undo for the last import.

See the [changelog](https://github.com/cjaruta/chisel/blob/main/CHANGELOG.md) for the full list.

## Install

1. Download `Chisel-1.0.0-win-x64.zip` below.
2. Check it: run `Get-FileHash .\Chisel-1.0.0-win-x64.zip` in PowerShell and compare with the checksum below.
3. Extract it to a folder you keep, such as `%LocalAppData%\Programs\Chisel`, and run `Chisel.App.exe`.

This build is not code-signed, so Windows SmartScreen may say "Windows protected your PC". Choose **More info**, then
**Run anyway**.

Requires Windows 10 (version 2004 or later) or Windows 11, 64-bit. Your data is stored in `%AppData%\Chisel`.

**SHA-256** `Chisel-1.0.0-win-x64.zip`: `<paste the hash from Chisel-1.0.0-win-x64.zip.sha256>`

## Known issues

- Not code-signed (SmartScreen warning on first run), and no installer yet.
- The daily verse is KJV only, the converter has no currency, and clipboard history is text only.

Found a bug? [Open an issue](https://github.com/cjaruta/chisel/issues/new/choose). Please don't attach crash logs or
screenshots that show personal content.
