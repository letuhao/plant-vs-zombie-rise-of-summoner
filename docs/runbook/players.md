# Player install (no SDK, no Node, no Desktop Runtime)

You do **not** install Node, Visual Studio, a .NET SDK, or the .NET Desktop Runtime.

## What you get (release zip)

```text
FusionRpg/
  FusionRpg.Launcher.exe     double-click this
  loader-manifest.json
  Server/
    FusionRpg.Server.exe     started by the launcher
    wwwroot/                 the website (already built)
    data/                    created on first run — kept when you update FusionRpg
  DropIntoGame/
    BepInEx/                 → BepInEx\plugins\FusionRpg\
      FusionRpg.Injector.dll
      …
    MelonLoader/             → Mods\ (when present in the zip)
      FusionRpg.Injector.MelonLoader.dll
      …
  PLAYERS.txt                this text
  LICENSE                    AGPL-3.0-or-later
  NOTICE
```

## Steps

1. Unzip anywhere.
2. Double-click **FusionRpg.Launcher.exe**.
3. Read the **Trust & security** prompt (first run). FusionRpg is an **unsigned hobby** AGPL project. Some antivirus products may false-positive `FusionRpg.Server.exe`. Click **Allow** if you accept that. Optionally run **Prepare Windows Security** (UAC) if you use Microsoft Defender.
4. **Browse** to your legal game folder (the folder that contains `PlantsVsZombiesRH.exe`). The launcher never assumes a fixed path on your PC.
5. Install **one** loader only: **Install BepInEx 6 (IL2CPP)** (pinned GitHub `v6.0.0-pre.2`) **or** **Install MelonLoader**. PVZ Fusion is Unity IL2CPP — **do not** install Mono BepInEx 5.4.x. Dual-load is refused.
6. If status says the FusionRpg plugin is missing, click **Install FusionRpg plugin** (or **Play**, which copies the matching `DropIntoGame` tree automatically).
7. Click **Play**. The launcher:
   - copies `DropIntoGame\BepInEx\` → `BepInEx\plugins\FusionRpg\` **or** `DropIntoGame\MelonLoader\` → `Mods\`,
   - starts the server (picks a free port if 5088 is busy),
   - starts the game,
   - opens the RPG UI in your browser.
8. Leave the launcher running (or minimize to tray) while you play.

## Trust & antivirus (unsigned hobby builds)

Builds are **not** Authenticode code-signed. That is normal for a free hobby project and can trigger cloud heuristics (e.g. Bitdefender `Cloud.Firestarter`).

| Your AV | What to do |
|---------|------------|
| **Microsoft Defender** | In the launcher: **Prepare Windows Security** → approve UAC. The launcher **stays open** (UAC elevates PowerShell only, not a second FusionRpg window). |
| **Bitdefender / other** | Restore `FusionRpg.Server.exe` from quarantine if removed, then add an exclusion for the FusionRpg folder yourself. The launcher cannot configure third-party AV. |
| Already deleted | Re-download `FusionRpg-win-x64.zip` from GitHub Releases after excluding the folder. |

Do **not** turn off antivirus entirely. Use Allow + exclusion only for this folder if you choose to run FusionRpg.

Open **Trust & security** in the launcher anytime for the same explanation.

## Updating FusionRpg

When the launcher shows an update, use **Download & install FusionRpg update**. That replaces FusionRpg files only and keeps `Server\data\` (your saves). It does **not** download or patch the game.

## You do not need

- Node.js / npm
- .NET SDK
- .NET 8 Desktop Runtime
- Manual copy of plugin DLLs
- Keeping a black console window open
- A paid code-signing certificate

Developers who change the UI still use Node. That is only for building a new zip. See [local-dev.md](local-dev.md) and [../contributing/dev-setup.md](../contributing/dev-setup.md).
