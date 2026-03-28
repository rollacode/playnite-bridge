# Playnite Bridge

Playnite plugin (`PlayniteBridge.dll`) exposing full HTTP API on `localhost:19821` with Bearer token auth.

## Build & Deploy
```bash
cd src && dotnet build -c Release
```
Output: `src/bin/Release/net462/PlayniteBridge.dll`

Copy `PlayniteBridge.dll` + `extension.yaml` to Playnite extensions folder. Playnite must be restarted (locks the DLL).

## Architecture
- Single file: `src/PlayniteBridgePlugin.cs`
- .NET Framework 4.6.2, Playnite SDK 6.15.0
- Plugin ID: `PlayniteBridge_f47ac10b`
- Auth token auto-generated on first run, stored in plugin data folder

## Key Gotchas
- **Encoding:** `ReadBody` must use `Encoding.UTF8` explicitly — Windows default codepage mangles non-ASCII
- **Deploy:** Must kill Playnite before copying DLL (file lock)
