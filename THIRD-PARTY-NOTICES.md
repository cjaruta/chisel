# Third-party notices

Chisel uses the following third-party software, fonts, data and services. Each remains under its own license.

## Libraries shipped with the app

| Component | Version | License | Source |
|---|---|---|---|
| Avalonia (Avalonia, Desktop, Themes.Fluent, Controls.ColorPicker, Skia, HarfBuzz) | 12.1.2 | MIT | https://github.com/AvaloniaUI/Avalonia |
| Avalonia.Labs.Gif | 12.1.0 | MIT | https://github.com/AvaloniaUI/Avalonia.Labs |
| Avalonia.Fonts.Inter (package) | 12.1.2 | MIT | https://github.com/AvaloniaUI/Avalonia |
| SkiaSharp | 3.119.x | MIT | https://github.com/mono/SkiaSharp |
| HarfBuzzSharp | 8.3.x | MIT | https://github.com/mono/SkiaSharp |
| MicroCom.Runtime | 0.11.x | MIT | https://github.com/AvaloniaUI/MicroCom |
| Tmds.DBus.Protocol | 0.94.x | MIT | https://github.com/tmds/Tmds.DBus |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | https://github.com/CommunityToolkit/dotnet |
| Microsoft.EntityFrameworkCore.Sqlite, Microsoft.Data.Sqlite | 10.0.12 | MIT | https://github.com/dotnet/efcore |
| SQLitePCLRaw (bundle_e_sqlite3, lib.e_sqlite3) | 2.1.12 | Apache-2.0 | https://github.com/ericsink/SQLitePCL.raw |
| SQLite | bundled by SQLitePCLRaw | Public domain | https://sqlite.org/copyright.html |
| NAudio | 2.2.1 | MIT, Copyright 2020 Mark Heath | https://github.com/naudio/NAudio |
| .NET runtime and libraries | 10.0 | MIT | https://github.com/dotnet/runtime |

Development and test only (not shipped in release builds): AvaloniaUI.DiagnosticsSupport (Debug builds only), xUnit (Apache-2.0),
Avalonia.Headless.XUnit (MIT), Microsoft.NET.Test.Sdk (MIT), Microsoft.Extensions.TimeProvider.Testing (MIT), coverlet.collector (MIT).

## Fonts

- **Inter**, by Rasmus Andersson, bundled through Avalonia.Fonts.Inter. SIL Open Font License 1.1. https://github.com/rsms/inter

Other text uses fonts already installed with Windows; none are bundled.

## Data

- **King James Version (KJV) Bible text**, 1769 Oxford standard text, used for the verse widget's bundled passages
  (`src/Chisel.Core/Scripture/kjv-passages.json`). Public domain in the United States. In the United Kingdom the text is
  subject to the Crown letters patent (the Crown holds the right to print it there). Text from
  https://github.com/midvash/bible-data, checked against https://github.com/aruljohn/Bible-kjv. "Read in context" links go to
  https://ebible.org/kjv/.

## Online services

- **Weather data by [Open-Meteo](https://open-meteo.com/)**, licensed under CC BY 4.0
  (https://creativecommons.org/licenses/by/4.0/). The forecast and geocoding APIs are called directly; the free tier is for
  non-commercial use. Place search uses Open-Meteo's geocoding API, which is based on GeoNames data (CC BY 4.0).

## Artwork

- The app icon (`src/Chisel.App/Assets/chiselIcon.ico`) was supplied by the project owner. Weather, clock and widget graphics are
  drawn in code; no other images, icons, sounds or media are bundled.
