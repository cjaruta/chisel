# Changelog

## 1.0.0 - unreleased

The first public release of Chisel, a daily planner dashboard for Windows.

### Features

- **Dashboard:** arrange widgets in columns by dragging and resizing, show or hide their titles, group widgets as tabs
  or side by side, and save layouts as profiles. A starter dashboard and short welcome tips on first run.
- **Widgets:**
  - **Today:** today's schedule, with the current block highlighted.
  - **Tasks:** due dates and drag to reorder.
  - **Notes:** separate pads.
  - **Clock:** clock faces and custom analog faces, a timer, a stopwatch with laps, and alarms.
  - **Weather:** current conditions with an hourly forecast.
  - **Media:** controls with artwork and volume.
  - **Picture frame:** slideshows; add as many frames as you like.
  - **Calculator:** basic and advanced modes.
  - **Daily verse:** a KJV passage that changes each day.
  - **Calendar:** marked days.
  - **System monitor:** live CPU, memory, disk, network and battery gauges.
  - **Countdown:** days until the things you are looking forward to.
  - **Focus timer:** Pomodoro sessions and breaks.
  - **Habits:** check-ins and streaks.
  - **Converter:** unit conversions.
  - **Clipboard history:** off until you start recording.
- **Quick links** in the top bar to the sites, files and folders you use most.
- **Themes:** system, light and dark, with an accent color, widget spacing and 12 or 24-hour time.
- **Windows integration:** start with Windows, close to the tray, and a show/hide shortcut (Ctrl+Alt+C by default).
- **Backup:** export everything to one file; import it to replace or merge, with an undo for the last import.

### Improvements

- Every widget stays usable from its smallest size to full window, and the top bar adapts to narrow windows.
- A short weather card shows the next few hours beside the temperature.
- Uses less power while the window is hidden or minimized.
- Saves are written safely, so a crash or power cut can't leave a half-written file.
- If saved data is damaged, Chisel keeps a copy, starts fresh and tells you, instead of failing to open.
- If something goes wrong, Chisel shows a friendly message and writes a crash report with no personal data.
  Settings > Help > About can open the logs folder and copy diagnostic info for bug reports.

### Known issues

- The app is not code-signed, so Windows SmartScreen warns on first run.
- No installer yet: the release is a zip you extract.
- Windows 10 (version 2004 or later) and Windows 11, x64 only.
- The daily verse uses the King James Version only; the converter has no currency; clipboard history is text only.
