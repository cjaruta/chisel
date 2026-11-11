# Chisel

Windows desktop daily planner. C# / .NET 10, Avalonia (MVVM), EF Core + SQLite, xUnit, GitHub Actions CI.
Repo: cjaruta/chisel. Owner: the owner. 
## Architecture
- src/Chisel.Core: domain types and repository interfaces. No dependencies.
- src/Chisel.Infrastructure: EF Core DbContext, entities, mappers (static extension methods ToEntity/ToDomain), repositories, migrations.
- src/Chisel.App: Avalonia UI. View models use CommunityToolkit.Mvvm.
- tests/Chisel.Core.Tests, tests/Chisel.Infrastructure.Tests (xUnit, SqliteConnectionFixture for in-memory SQLite).
- Repository interfaces live in Core, implementations in Chisel.Infrastructure.Repositories.
- Id-less types (markers, schedule, settings, layout) use replace-all semantics.
- Time goes through TimeProvider (FakeTimeProvider in tests).
- Any entity change needs a migration: `dotnet ef migrations add <Name> --project src/Chisel.Infrastructure`. MigrationTests fails if one is missing.

## Workflow (follow exactly)
1. One branch per chunk, named feature/<chunk>. Branch from main, or from the previous chunk's branch if it isn't merged yet.
2. Inside a chunk, work test-first: write the failing test, run `dotnet test` and confirm red, write the code, confirm green. One commit per cycle, conventional messages ("feat: ...", "test: ...", "chore: ...").
3. Never commit with failing tests. Run `dotnet build` and `dotnet test` before every commit.
4. Push the branch with `git push -u origin <branch>`.
5. Pull requests:
   - B) Run `gh pr create --fill`, then `gh pr merge --auto --squash`, then wait with `gh pr checks --watch` until merged, then `git switch main && git pull` before the next chunk.
6. Never push to main. Never force push. Never add or change a license.
7. After each chunk, tick it in docs/ROADMAP.md and commit that.
8. If a test is still failing after 3 honest attempts, stop and tell the owner what's wrong instead of guessing.

## Style
Keep code simple. No new packages unless needed. No UI tests; view models get light unit tests only.
Preference toward: short, plain-language messages. Report briefly when a chunk is done.

## v0.1 scope ("done")
A window with a live clock, today's schedule with the current block highlighted, task list, notes, quick links, all stored in local SQLite.
Cut from v0.1: layout editor, settings screen, weather, media controls, sync server.

## Privacy
- Never put real personal information in code, tests, seed data, docs, comments, or commit messages: no real names, emails, birthdates, addresses, phone numbers, employer, or school.
- Use fake data: "Test User", "user@example.com", 2000-01-01.
- The app must not hard-code any person's name. Names come from user settings at runtime.
- Never copy personal details from prompts or memory into the repo.
- If a task seems to need real personal data, ask first.