# Chisel
 
Chisel is a desktop daily planner for Windows, written in C# with .NET 10. It shows a fixed daily schedule alongside tasks, notes, and quick links in a small window meant to stay open on a second monitor.
 
It started as a single HTML page I used as a browser homepage. This repo is a rewrite of that idea as a proper desktop application with a tested domain layer and a local SQLite database.
 
## Current state
 
v0.1 is feature-complete: a window with a live clock, today's schedule with the current block highlighted, a task list, notes, and quick links, all stored in a local SQLite database. The domain logic, persistence layer, view models, and Avalonia main window are all implemented and covered by unit tests.
 
## Built with
 
- C# / .NET 10
- Avalonia for the desktop UI
- Entity Framework Core with SQLite
- xUnit for testing
- GitHub Actions for CI
## Project structure
 
```
src/
  Chisel.Core/              Domain models and business rules
  Chisel.Infrastructure/    EF Core DbContext, SQLite persistence, seeding
  Chisel.App/               Avalonia desktop app: view models, main window, startup wiring
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
 
v0.1 scope is done. Possible future additions, cut from v0.1:
 
- A layout editor for rearranging widgets
- A settings screen for editing the schedule, wake/bed times, and markers
- Media controls using the Windows media session API
- Weather widget using Open-Meteo
- Optional sync server with ASP.NET Core and SQL Server

## License
MIT