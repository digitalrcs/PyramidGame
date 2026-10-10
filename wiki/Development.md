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
| Built-in campaign | `Pyramid.Web/Game/World.cs`, `Engine.cs` |
| Maps, sound cues and exploration rewards | `Pyramid.Web/Game/Exploration.cs` |
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
```

With the isolated server running, use a second terminal:

```powershell
$env:PYRAMID_TEST_BASE = 'http://127.0.0.1:5190/pyramid/'
python Pyramid.Tests/http_checks.py
python Pyramid.Tests/http_coop_checks.py
```

The HTTP scripts create accounts and change saves. Account recovery checks additionally need their documented disposable administrator and Capture-mode mail setup; inspect `http_account_checks.py` before running it. Engine/storage account and mail checks run in the .NET test project.

Browser verification should cover sign-in, guides, room art, expedition invitations, independent movement, consent-based item transfers, refresh/resume, visited-only maps and their cost, CLEAR, and audible chimes. Check both a desktop and a narrow mobile viewport. An error-free console does not establish audible output.

The [developer walkthrough](Developer-Walkthrough) supplies all campaign solutions and multiplayer checkpoints. The [deployment guide](Deployment) covers installation and recovery.
