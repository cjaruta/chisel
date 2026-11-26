# Contributing

Thanks for your interest in Chisel!

## Build and test

You need Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build
dotnet test
dotnet run --project src/Chisel.App
```

- `src/Chisel.Core`: domain types and rules, with no dependencies.
- `src/Chisel.Infrastructure`: SQLite storage with EF Core. Any entity change needs a migration:
  `dotnet ef migrations add <Name> --project src/Chisel.Infrastructure` (a test fails if one is missing).
- `src/Chisel.App`: the Avalonia app. View models have unit tests; views have headless render tests.

Please add or update tests with each change, and make sure `dotnet test` passes before opening a pull request.
Keep pull requests small and focused on one change.

## Reporting bugs

Open an [issue](https://github.com/cjaruta/chisel/issues/new/choose) using the bug report form. Include the version
from Settings > Help > About, your Windows version and display scaling, and steps to reproduce.

Please don't include personal data: use made-up examples, and read any log file before sharing it.

Security problems: see [SECURITY.md](SECURITY.md).
