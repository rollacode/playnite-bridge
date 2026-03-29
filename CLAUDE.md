# Playnite Bridge

Playnite plugin (`PlayniteBridge.dll`) exposing full HTTP API on `localhost:19821` with Bearer token auth.

## Build & Deploy
```bash
cd src && dotnet build -c Release
```
Output: `src/bin/Release/net462/PlayniteBridge.dll`

Copy `PlayniteBridge.dll` + `extension.yaml` to Playnite extensions folder. Playnite must be restarted (locks the DLL).

## Tests
```bash
cd tests && dotnet test
```
- Framework: NUnit 3 + NSubstitute
- 77 tests, ~3 seconds
- Test project: `tests/PlayniteBridge.Tests.csproj`
- `InternalsVisibleTo` in `src/Properties/AssemblyInfo.cs` — tests can access internal types
- Playnite SDK Game.Source/Genres/Developers are read-only (DB-backed) — can't set in unit tests, test via serialized JSON dict instead of `dynamic`

## Architecture
- .NET Framework 4.6.2, Playnite SDK 6.15.0
- Plugin ID: `PlayniteBridge_f47ac10b`
- Auth token auto-generated on first run, stored in plugin data folder
- Main plugin: `src/PlayniteBridgePlugin.cs` (monolith, being refactored)
- Extracted services: `src/Services/` (GameQueryService, EvalService, PluginIntegrationService, GameSerializationService)
- Extracted helpers: `src/Helpers/` (DictExtensions, CollectionResolver, JsonHelper, NetworkHelper)
- Server abstraction: `src/Server/RequestContext.cs`

## Deploy
```bash
taskkill //F //IM Playnite.DesktopApp.exe
cp src/bin/Release/net462/PlayniteBridge.dll "C:/Games/Playnite/Extensions/PlayniteBridge/"
"C:/Games/Playnite/Playnite.DesktopApp.exe"
```

## Key Gotchas
- **Encoding:** `ReadBody` must use `Encoding.UTF8` explicitly — Windows default codepage mangles non-ASCII
- **Deploy:** Must kill Playnite before copying DLL (file lock)
- **dynamic keyword:** Needs `Microsoft.CSharp` reference — avoid in plugin code, use in tests only
- **Game readonly props:** Source, Genres, Categories, Tags, etc. are read-only (loaded from DB via DatabaseReference). Use SourceId, GenreIds etc. or test via JSON serialization
