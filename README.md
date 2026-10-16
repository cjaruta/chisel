# Chisel
 
Chisel is a desktop daily planner for Windows, written in C# with .NET 10. It shows a fixed daily schedule alongside tasks, notes, and quick links in a small window meant to stay open on a second monitor.
 
It started as a single HTML page I used as a browser homepage. This repo is a rewrite of that idea as a proper desktop application with a tested domain layer and a local SQLite database.
 
## Current state
 
The core domain logic (schedule blocks, markers, tasks, notes, user settings, and widget layout) is implemented and covered by unit tests. The persistence layer using Entity Framework Core and SQLite is being built out now. The desktop UI has not been started yet, so there is nothing to launch at the moment.
 
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
  Chisel.Infrastructure/    EF Core DbContext and SQLite persistence
  Chisel.App/               Avalonia desktop app
tests/
  Chisel.Core.Tests/
  Chisel.Infrastructure.Tests/
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
 
## Roadmap
 
- Finish the remaining database entities and add the first EF Core migration
- Build the main window and widget views in Avalonia
- Media controls using the Windows media session API
- Weather widget using Open-Meteo
- Optional sync server with ASP.NET Core and SQL Server

## License
MIT