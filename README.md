# Chisel
 
Chisel is a desktop daily planner for Windows, written in C# with .NET 10. It shows a fixed daily schedule alongside tasks, notes, and quick links in a small window meant to stay open on a second monitor.
 
It started as a single HTML page I used as a browser homepage. This repo is a rewrite of that idea as a proper desktop application with a tested domain layer and a local SQLite database.
 
## Current state

v0.1 is done and development has continued well past it. Chisel is a customizable dashboard: a live clock, a schedule grid with the current block highlighted, tasks, notes, quick links, and more, all stored in a local SQLite database.

- **Widgets:** clock (styles, timer, stopwatch, alarms), schedule, tasks, notes, quick links, weather, media controls, picture frames (slideshows), calculator, verse, and a notepad. A widget library lets you add more than one of some widgets.
- **Layout editor:** drag, resize, and rearrange widgets into columns, hide or show widget titles, and combine widgets into tabbed or split-view groups.
- **Settings:** schedule editing, palettes, themes with an accent color, 12/24-hour time, weather location and units, and per-widget options.

The domain logic, persistence layer, view models, and Avalonia UI are covered by unit tests and headless render tests.

## Built with
 
- C# / .NET 10
- Avalonia for the desktop UI
- Entity Framework Core with SQLite
- xUnit for testing
- GitHub Actions for CI
- Weather data by [Open-Meteo](https://open-meteo.com/), licensed under CC BY 4.0
- Verse text from the King James Version (1769 standard text), bundled with the app so it works offline. The KJV is public domain in the United States; in the United Kingdom it is subject to the Crown's letters patent. Source: [midvash/bible-data](https://github.com/midvash/bible-data) (public domain), with every bundled verse checked against two other KJV sources.
## Project structure
 
```
src/
  Chisel.Core/              Domain models and business rules
  Chisel.Infrastructure/    EF Core DbContext, SQLite persistence, seeding
  Chisel.App/               Avalonia desktop app: view models, widgets, main window, startup wiring
tests/
  Chisel.Core.Tests/
  Chisel.Infrastructure.Tests/
  Chisel.App.Tests/
```
 
`Chisel.Core` has no dependencies on the other projects. Infrastructure and App both reference Core, so the business logic can be tested without a UI or a database.
 
The database layer uses its own entity classes instead of reusing the Core models directly. The Core classes protect their state with private setters and methods like `Complete()`, while EF Core works best with plain classes it can populate freely, so the two are kept separate.
 
## Getting started
 
You'll need the [.NET 10 SDK](https://dotnet.microsoft.com/download).
 
```
git clone https://github.com/cjaruta/chisel.git
cd chisel
dotnet test
```
 
## Running the app
 
```
dotnet run --project src/Chisel.App
```
 
On first launch, Chisel creates its database at `%AppData%\Chisel\chisel.db`, migrates it, and seeds a default schedule, markers, and settings.
 
To build a standalone executable instead:
 
```
dotnet publish src/Chisel.App -c Release -r win-x64 --self-contained
```
 
The published app is written to `src/Chisel.App/bin/Release/net10.0/win-x64/publish/`.
 
## Development

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build
dotnet test
```

Entities live in `Chisel.Infrastructure/Data` and are mapped to and from the `Chisel.Core` domain
types via static `ToEntity`/`ToDomain` extension methods in `Chisel.Infrastructure/Mapping`. Any
change to an entity needs a corresponding EF Core migration:

```
dotnet ef migrations add <Name> --project src/Chisel.Infrastructure
```

The test suite fails if a pending model change is missing a migration. Pull requests run the full
build and test suite via GitHub Actions.

## Roadmap

Done since v0.1: layout editor, settings screen, weather, media controls, themes, widget groups, picture frames, and the clock, calculator, verse, and notepad widgets.

Possible future additions:

- Optional sync server with ASP.NET Core and SQL Server
- More widgets and settings pages

## License
MIT