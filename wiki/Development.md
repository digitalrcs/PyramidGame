# Development

The web edition is an ASP.NET Core 10 application with a React 19/Vite frontend and SQLite storage. The separate Python edition is kept at the repository root.

## Build and run

Install the .NET 10 SDK and Node.js 22 LTS (22.12 or newer in that release line). Python 3 is needed for the classic edition and HTTP checks. From the repository root in PowerShell:

```powershell
.\BUILD-WEB.ps1
.\RUN-WEB.ps1
```

The build installs locked frontend dependencies, builds the client, runs the .NET checks and publishes to `artifacts/release`. The launcher serves Development at `http://localhost:5188/pyramid/`. Stop it with Ctrl+C. Build output and local saves are not source files.

For an isolated test database:

```powershell
$testData = Join-Path $PWD 'artifacts/test-data'
dotnet run --project Pyramid.Web -- --urls http://127.0.0.1:5190 --environment Development --Pyramid:PathBase /pyramid --Pyramid:DataDirectory "$testData"
```

Restart with the same data directory to check persistence. Use separate browser profiles for multiplayer accounts; normal tabs share cookies. Never point mutation tests at production or reuse production passwords.

## Source map

| Area | Files |
| --- | --- |
| Built-in campaign and changing scenery | `Pyramid.Web/Game/World.cs`, `Engine.cs`, `RoomNarrative.cs` |
| Object names and readable clues | `Pyramid.Web/Game/CommandAliases.cs`, `ReadableItems.cs` |
| Maps and exploration rewards | `Pyramid.Web/Game/Exploration.cs`, `Client/src/mapLayout.js` |
| Room sound events | `Pyramid.Web/Client/src/roomChimeEvents.js`, `roomChime.js` |
| Adventure library and designer | `Pyramid.Web/Client/src/AdventureHub.jsx`, `AdventureEditor.jsx` |
| Shared-world command rules | `Pyramid.Web/Game/Cooperation.cs` |
| Persistence and admission | `Pyramid.Web/Storage.cs` and related partial files |
| JSON adventures | `Pyramid.Web/Game/ContentPacks.cs`, `ModEngine.cs` |
| HTTP host and routes | `Pyramid.Web/Program.cs` and account endpoints |
| React client | `Pyramid.Web/Client/src` |
| Room illustrations and public assets | `Pyramid.Web/Client/public` |
| Sample content | `Pyramid.Web/ContentPacks` |
| Engine, storage, account and mail checks | `Pyramid.Tests` |

State changes, authorization, score calculations and map visibility belong on the server. Do not expose private puzzle solutions or unvisited room graphs to simplify the client. Preserve revision conflict handling and the distinction between personal and shared state.

## Verification

```powershell
dotnet run --project Pyramid.Tests -c Release
node Pyramid.Web/Client/src/mapLayout.test.mjs
node Pyramid.Web/Client/src/roomChimeEvents.test.mjs
python -m unittest test_pyramid2000.py
```

GitHub Actions runs the frontend build, both Node checks, the .NET suite, and the classic Python tests. `BUILD-WEB.ps1` builds and publishes the app and runs the .NET suite; run the Node and Python commands above separately when using that helper.

The Node checks cover known-room filtering, relative floors, one-way and portal routes, and room-event detection without playing audio. The .NET suite covers both adventures, changing room descriptions, shared state, persistence, account verification, permissions, backups, and settings.

HTTP integration scripts create accounts and change saves. They are manual fixture tools rather than part of the default CI job. The older `http_checks.py` and `http_coop_checks.py` registration flows predate mandatory email verification; update their setup before using them against the current server. Account recovery checks also need a disposable administrator and Capture-mode mail setup; inspect `http_account_checks.py` before running it.

`http_verification_checks.py` requires a fresh disposable Capture-mode server at `http://127.0.0.1:5198/pyramid/`, an empty database, and a `VERIFICATION-QA.marker` in its data directory. It writes local fixture credentials for subsequent role checks; keep that directory under ignored `artifacts/` and never commit it. `http_role_checks.py` consumes that fixture and also requires its owner binding to be provisioned. Read each script's prerequisites before running it with `--data <isolated-directory>`; do not point these scripts at production.

Browser verification should cover sign-in, guides, room art, expedition invitations, independent movement, consent-based item transfers, refresh/resume, visited-only maps and their cost, CLEAR, and audible chimes. Check that picked-up objects disappear from LOOK, dropped objects appear in their new room, and solved mechanisms describe their current state. Check relative-floor maps, one-way holes, and accessible route labels. Verify distinct room-change, puzzle, and arrival cues with sound enabled; also check muted mode and unchanged polling. Check both a desktop and a narrow mobile viewport. An error-free console does not establish audible output.

The [developer walkthrough](Developer-Walkthrough) supplies all campaign solutions and multiplayer checkpoints. The [deployment guide](Deployment) covers installation and recovery.
