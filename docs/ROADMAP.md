# Roadmap to v0.1

- [x] Core domain types, entities, mappers, TaskRepository
- [x] Chunk 1 feature/repositories: QuickLink, Note, Marker, Schedule, UserSettings, WidgetLayout repositories (+ tests)
- [ ] Chunk 2 feature/app-startup: DB in %AppData%\Chisel, migrate on startup, first-run seed, DI wiring in Chisel.App
      Seed: wake 6:30; focus 8-11, 12-2, 4-6; workout 2-3; dinner 6:30; read 10-11; sleep 11-6:30.
      Markers: last coffee 1 PM, all work done 6 PM.
- [ ] Chunk 3 feature/view-models: clock, schedule (current block), tasks, notes, quick links
- [ ] Chunk 4 feature/main-window: Avalonia main window bound to the view models
- [ ] Chunk 5 feature/release: README roadmap/scope update, publishable launchable build