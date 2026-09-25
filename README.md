# RoR2 unofficial dedicated server

Experimental BepInEx 5 plugin for hosting a current Risk of Rain 2 client as a headless, direct-IP server. This repository contains source code only; it does not redistribute game files or DLC.

## Status

The plugin builds against the installed February 2026 `RoR2.dll`. It replaces desktop Steam client startup, skips Steam game-server initialization, and asks the game's existing server-only network path to listen on UDP port 7777. A scratch-copy boot **without Steam desktop running** stayed resident, reached the game's initialized server state, bound UDP `0.0.0.0:7777`, and had no signed-in local user. **Remote join, lobby flow, run start, and reset are not verified yet.** This is not ready for an exposed public server: skipping Steam game-server initialization also skips Steam ticket verification.

The original brainstorm named methods that are absent in this build. In particular, `RoR2.SteamManager.Awake()` and `ServerReturnToLobby()` are not present. The code uses the actual `PlatformSystems`, `NetworkManagerSystemSteam`, and `HostDescription` APIs found in the installed assemblies. The game's `VoteController` handles ready votes and calls its configured launch action; this flow still needs a remote-client check on the dedicated host.

## Build and local test on Windows

Requirements: a legally installed current Risk of Rain 2 client, .NET SDK, PowerShell, and network access for NuGet and the official BepInEx download.

```powershell
.\scripts\Prepare-LocalServer.ps1 -GamePath 'C:\Program Files (x86)\Steam\steamapps\common\Risk of Rain 2'
```

The script copies the game into ignored `.scratch/server`, downloads [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5), builds the plugin, and installs it **only in the scratch copy**. It never edits the original installation. Stop the scratch server before rerunning the script.

Launch from `.scratch/server`:

```powershell
& '.\Risk of Rain 2.exe' -batchmode -nographics -server --disableCrossplay -logFile '.\headless-player.log'
```

The plugin config is generated at `BepInEx/config/com.zdiemer.ror2.unofficialdedicatedserver.cfg`. Its `Port` default is 7777 and `MaxPlayers` default is 4. Check `BepInEx/LogOutput.log` for startup messages and `Get-NetUDPEndpoint -LocalPort 7777` for a bound socket.

Clients with the same game build and DLC setup can try the in-game console command:

```text
connect "SERVER_IP:7777"
```

Direct-IP access requires UDP port forwarding and a matching game version. The client should have crossplay disabled.

## Server mods

Put extra server mods and their dependencies in a BepInEx profile, then pass its path to the preparation script:

```powershell
.\scripts\Prepare-LocalServer.ps1 -GamePath 'C:\Program Files (x86)\Steam\steamapps\common\Risk of Rain 2' -ModsPath 'D:\ror2-server-mod-profile'
```

`-ModsPath` accepts a profile root containing `BepInEx`, or the `BepInEx` folder itself. It copies `plugins`, `patchers`, and `config` into the isolated server. The dedicated server plugin remains in `BepInEx/plugins`.

Only mods designed to run on the server while remaining compatible with vanilla clients meet the no-client-install goal. Mods that add characters, items, assets, UI, or custom network messages generally require clients to install matching mods. The [RoR2 modding wiki](https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/C%23-Programming/Networking/Server-side-and-client-side-mods/) explains the distinction and the `NetworkCompatibility` attribute. Test each server mod with an unmodded client before adding it to a hosted profile.

## Verification still required

- Connect a remote client and verify the handshake and player slot count.
- Add lobby readiness and automatic launch based on the current game's actual lobby APIs.
- Add a tested game-over and disconnect reset flow.
- Decide how a public deployment should authenticate clients, since the current direct-IP path does not validate Steam tickets.

## Docker follow-up

A separate image repository will use Proton, this plugin's build artifact, and a user-provided current game install. It will include a Helm chart whose values can list server mods. [avivace/ror2-server](https://github.com/avivace/ror2-server) is a reference for container layout, although it uses the old official server. The image will not embed or publish proprietary game files.
