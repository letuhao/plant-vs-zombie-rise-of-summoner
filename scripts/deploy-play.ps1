# Fast local deploy: web UI, injector into the game folder, RPG server, then launch PVZRH.
# Usage (repo root):
#   .\scripts\deploy-play.ps1                    # MelonLoader 3.9, the default (2026-08-30 -- faster
#                                                 # startup than the older BepInEx 3.8.1 install below)
#   .\scripts\deploy-play.ps1 -LoaderHost BepInEx   # the older 3.8.1 install, kept for BepInEx-specific testing
#   .\scripts\deploy-play.ps1 -NoGame
#   .\scripts\deploy-play.ps1 -NoRebuildUi   # skip the web UI build (it's on by default -- 2026-08-30:
#                                            # a stale wwwroot silently served an old FE build for a
#                                            # whole session because this used to be opt-in and got
#                                            # forgotten; opt-out is the only safe default)
# FE landing (2026-09-09): vite writes src\FusionRpg.Server\wwwroot; the running server serves
# dist\FusionRpg.Server\wwwroot (ContentRoot = exe dir). A running server skips `dotnet publish`
# (DLL locks), which used to leave dist's FE stale even after a fresh vite build. This script always
# mirrors src wwwroot → dist wwwroot after the UI step so -NoServer / "server already up" still
# lets you hard-refresh and confirm FE fixes without -RestartServer.
# Server data (rpg-hot / rpg-media) lives next to the published exe: dist\FusionRpg.Server\data\
# Runs guard-single-writer.ps1 + guard-dal.ps1 + guard-test-substrate.ps1 + guard-secondary-no-unity.ps1 + guard-funnel-delta.ps1 + guard-actor-hub.ps1
# + guard-overflow.ps1 + guard-magic-numbers.ps1 + guard-power.ps1 + guard-stat-pairs.ps1
# + guard-class-system.ps1 before build.
param(
    [ValidateSet("BepInEx", "MelonLoader")]
    [string]$LoaderHost = "MelonLoader",
    [switch]$NoGame,
    [switch]$NoServer,
    [switch]$NoRebuildUi,
    [switch]$RestartServer
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

$ServerProj = Join-Path $Root "src\FusionRpg.Server\FusionRpg.Server.csproj"
$ServerOut = Join-Path $Root "dist\FusionRpg.Server"
$ServerExe = Join-Path $ServerOut "FusionRpg.Server.exe"
$DataDir = Join-Path $ServerOut "data"
$WwwrootSrc = Join-Path $Root "src\FusionRpg.Server\wwwroot"
$WwwrootDist = Join-Path $ServerOut "wwwroot"
$Health = "http://127.0.0.1:5088/health"

function Sync-ServerWwwroot {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )
    if (-not (Test-Path $Source)) {
        throw "wwwroot missing: $Source (run without -NoRebuildUi, or build web\fusion-rpg-web first)"
    }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    # Mirror so hashed vite chunks deleted from src do not linger in dist and get served stale.
    # robocopy uses bit-flag exits (1 = copied files). Do not let PS 7 native-command preference
    # treat a successful copy as a terminating error.
    $prevNative = $null
    if (Test-Path variable:PSNativeCommandUseErrorActionPreference) {
        $prevNative = $PSNativeCommandUseErrorActionPreference
        $PSNativeCommandUseErrorActionPreference = $false
    }
    try {
        $null = & robocopy $Source $Destination /MIR /NFL /NDL /NJH /NJS /nc /ns /np
        $rc = $LASTEXITCODE
    } finally {
        if ($null -ne $prevNative) { $PSNativeCommandUseErrorActionPreference = $prevNative }
    }
    # robocopy: 0-7 = success/partial copy; 8+ = failure
    if ($rc -ge 8) {
        throw "wwwroot sync failed (robocopy exit $rc): $Source -> $Destination"
    }
    $srcIndex = Join-Path $Source "index.html"
    $dstIndex = Join-Path $Destination "index.html"
    if (-not (Test-Path $dstIndex)) {
        throw "wwwroot sync left no index.html at $dstIndex"
    }
    $srcHash = (Get-FileHash $srcIndex -Algorithm SHA256).Hash
    $dstHash = (Get-FileHash $dstIndex -Algorithm SHA256).Hash
    if ($srcHash -ne $dstHash) {
        throw "wwwroot sync mismatch: src index.html hash $srcHash != dist $dstHash"
    }
    Write-Host "==> FE synced to $Destination (index.html SHA256 $($srcHash.Substring(0,12))…)"
}

if ($LoaderHost -eq "MelonLoader") {
    # Default install for this machine (2026-08-30) -- override with $env:FUSIONRPG_ML_GAMEDIR for a
    # different MelonLoader pack, same fallback shape the BepInEx branch below already uses.
    $GameDir = if ($env:FUSIONRPG_ML_GAMEDIR) { Resolve-Path $env:FUSIONRPG_ML_GAMEDIR } else { Resolve-Path "H:\Games\PVZ-Fusion-3.9_MelonLoader" }
    $GameExe = Join-Path $GameDir "PlantsVsZombiesRH.exe"
    $PluginDir = Join-Path $GameDir "Mods"
    $GameProfile = if ($env:FUSIONRPG_GAME_PROFILE) { $env:FUSIONRPG_GAME_PROFILE } else { "pvzrh-3.8.1" }
    $ga = Join-Path $GameDir "GameAssembly.dll"
    if (-not $env:FUSIONRPG_GAME_PROFILE -and (Test-Path $ga) -and ((Get-Item $ga).Length -eq 57717248)) {
        $GameProfile = "pvzrh-3.9"
    }
    if ($GameProfile -eq "pvzrh-3.9") {
        $InjectorProj = Join-Path $Root "src\FusionRpg.Injector.MelonLoader.39\FusionRpg.Injector.MelonLoader.39.csproj"
        $InjectorDll = "FusionRpg.Injector.MelonLoader.39.dll"
    }
    else {
        $InjectorProj = Join-Path $Root "src\FusionRpg.Injector.MelonLoader\FusionRpg.Injector.MelonLoader.csproj"
        $InjectorDll = "FusionRpg.Injector.MelonLoader.dll"
    }
}
else {
    $GameDir = if ($env:FUSIONRPG_GAME_DIR) { Resolve-Path $env:FUSIONRPG_GAME_DIR } else { Resolve-Path (Join-Path $Root "..") }
    $GameExe = Join-Path $GameDir "PlantsVsZombiesRH.exe"
    $PluginDir = Join-Path $GameDir "BepInEx\plugins\FusionRpg"
    $InjectorProj = Join-Path $Root "src\FusionRpg.Injector.BepInEx\FusionRpg.Injector.BepInEx.csproj"
    $InjectorDll = "FusionRpg.Injector.dll"
    $GameProfile = if ($env:FUSIONRPG_GAME_PROFILE) { $env:FUSIONRPG_GAME_PROFILE } else { "pvzrh-3.8.1" }
}

if (-not (Test-Path $GameExe)) {
    throw "Game exe missing: $GameExe"
}

if (-not $NoRebuildUi) {
    Write-Host "==> Building web UI (writes straight into src\FusionRpg.Server\wwwroot)"
    Push-Location (Join-Path $Root "web\fusion-rpg-web")
    if (-not (Test-Path "node_modules")) { npm install }
    npm run build
    if ($LASTEXITCODE -ne 0) { Pop-Location; throw "web UI build failed" }
    Pop-Location
} else {
    Write-Host "==> Skipping web UI build (-NoRebuildUi) -- will sync whatever is already in src wwwroot"
}

# Always push FE into dist, even when `dotnet publish` is skipped because :5088 is locked.
# The live server ContentRoot is dist\FusionRpg.Server — src wwwroot alone is not enough.
if (Test-Path $WwwrootSrc) {
    Write-Host "==> Syncing FE wwwroot into published server tree"
    Sync-ServerWwwroot -Source $WwwrootSrc -Destination $WwwrootDist
} else {
    Write-Host "==> No src wwwroot yet — FE sync deferred until after publish (or build the UI)"
}

Write-Host "==> Single-writer guard"
& (Join-Path $Root "scripts\guard-single-writer.ps1")
if ($LASTEXITCODE -ne 0) { throw "single-writer guard failed" }

Write-Host "==> DAL guard"
& (Join-Path $Root "scripts\guard-dal.ps1")
if ($LASTEXITCODE -ne 0) { throw "DAL guard failed" }

Write-Host "==> Test-substrate guard"
& (Join-Path $Root "scripts\guard-test-substrate.ps1")
if ($LASTEXITCODE -ne 0) { throw "test-substrate guard failed" }

Write-Host "==> Secondary no-Unity guard"
& (Join-Path $Root "scripts\guard-secondary-no-unity.ps1")
if ($LASTEXITCODE -ne 0) { throw "secondary no-Unity guard failed" }

Write-Host "==> Funnel delta guard"
& (Join-Path $Root "scripts\guard-funnel-delta.ps1")
if ($LASTEXITCODE -ne 0) { throw "funnel delta guard failed" }

Write-Host "==> ActorHub gate guard"
& (Join-Path $Root "scripts\guard-actor-hub.ps1")
if ($LASTEXITCODE -ne 0) { throw "actor-hub guard failed" }

Write-Host "==> Overflow guard"
& (Join-Path $Root "scripts\guard-overflow.ps1")
if ($LASTEXITCODE -ne 0) { throw "overflow guard failed" }

Write-Host "==> Magic-number guard"
& (Join-Path $Root "scripts\guard-magic-numbers.ps1")
if ($LASTEXITCODE -ne 0) { throw "magic-number guard failed" }

Write-Host "==> POWER guard"
& (Join-Path $Root "scripts\guard-power.ps1")
if ($LASTEXITCODE -ne 0) { throw "POWER guard failed" }

Write-Host "==> STAT-PAIRS guard"
& (Join-Path $Root "scripts\guard-stat-pairs.ps1")
if ($LASTEXITCODE -ne 0) { throw "STAT-PAIRS guard failed" }

Write-Host "==> CLASS-SYSTEM guard"
$ClassSystemOutput = & (Join-Path $Root "scripts\guard-class-system.ps1") *>&1
$ClassSystemExit = $LASTEXITCODE
$ClassSystemOutput | ForEach-Object { Write-Host $_ }
if ($ClassSystemExit -ne 0) {
    # class-system-plan.md decision 12 (2026-08-27): G3 (Might/Ferocity feed both combat.power.* and
    # progression.bonus.atk) is a deliberate, PERMANENT forward-looking safeguard for battle-adoption's
    # own transition, not a same-day defect -- the shipped tuning file is never edited to silence it,
    # so the guard's real-tree exit is 1 by design (ClassSystemGuardTests.cs's own
    # "exitsOneOnTheRealTree_onlyG3_permanentlyByDesign" test proves exactly this). Tolerate ONLY this
    # named, documented exception; any other or additional finding still hard-fails the deploy.
    $ClassSystemText = $ClassSystemOutput -join "`n"
    $OnlyG3 = ($ClassSystemText -match "G3 Might:") -and ($ClassSystemText -match "G3 Ferocity:")
    foreach ($OtherRule in @("G1 ", "G2 ", "G4 ", "G5 ", "G6 ", "G7 ")) {
        if ($ClassSystemText -match [regex]::Escape($OtherRule)) { $OnlyG3 = $false }
    }
    if ($OnlyG3) {
        Write-Host "==> CLASS-SYSTEM guard: only the known, permanent-by-design G3 finding (decision 12) -- tolerated, deploy continues"
    } else {
        throw "CLASS-SYSTEM guard failed"
    }
}

Write-Host "==> Building $LoaderHost injector ($GameProfile) into $PluginDir"
& (Join-Path $Root "scripts\guard-game-profile.ps1") -GameDir $GameDir -ExpectedProfile $GameProfile
if ($LASTEXITCODE -ne 0) { throw "game-profile guard failed" }
if ($LoaderHost -eq "MelonLoader") {
    dotnet build $InjectorProj -c Release -p:MlGameDir=$GameDir -p:GameProfile=$GameProfile -p:OutputPath=$PluginDir\
}
else {
    dotnet build $InjectorProj -c Release -p:GameDir=$GameDir -p:GameProfile=$GameProfile
}
if ($LASTEXITCODE -ne 0) {
    throw "injector build failed — the deployed DLLs were not refreshed"
}
if (-not (Test-Path (Join-Path $PluginDir $InjectorDll))) {
    throw "Injector DLL missing after build: $PluginDir\$InjectorDll"
}
if (-not (Test-Path (Join-Path $PluginDir "FusionRpg.Core.dll"))) {
    throw "FusionRpg.Core.dll missing after build"
}

# FRESHNESS GUARD (2026-08-30). "Build succeeded" is not evidence the DLL was rebuilt:
#   * the MelonLoader.39 project prints success and compiles NOTHING when MlGameDir is unset
#     (its own WarnSkipMelon39 target) -- a real compile error hid behind that for an hour today;
#   * a running game holds a lock on the DLL, and a failed copy can leave the old file in place.
# Either way the deployed injector silently disagrees with FusionRpg.Core.dll beside it, which
# surfaces as a runtime `MethodNotFound` mid-match rather than a build error. Compare against the
# newest source we actually compiled, so both cases fail HERE, loudly.
# Check each deployed artifact against the source trees that can produce it. Comparing every DLL
# with every `src/**/*.cs` file is a false-positive: the server/Data tree can change after the
# injector was built, even though it is not a dependency of either deployed game assembly. The old
# global check blocked a valid deploy immediately after a server-only edit (2026-09-08).
$hostSourceRoot = if ($LoaderHost -eq "MelonLoader") {
    if ($GameProfile -eq "pvzrh-3.9") { Join-Path $Root "src\FusionRpg.Injector.MelonLoader.39" }
    else { Join-Path $Root "src\FusionRpg.Injector.MelonLoader" }
} else { Join-Path $Root "src\FusionRpg.Injector.BepInEx" }
$injectorSourceRoots = @(
    (Join-Path $Root "src\FusionRpg.Injector"),
    $hostSourceRoot,
    (Join-Path $Root "src\FusionRpg.Contracts"),
    (Join-Path $Root "src\FusionRpg.Core"),
    (Join-Path $Root "src\FusionRpg.CheatCore"))
$coreSourceRoots = @(
    (Join-Path $Root "src\FusionRpg.Core"),
    (Join-Path $Root "src\FusionRpg.Contracts"))
function Get-NewestSource([string[]]$roots) {
    Get-ChildItem -Path $roots -Recurse -Filter *.cs -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
}
$freshnessChecks = @(
    @{ Dll = $InjectorDll; Source = Get-NewestSource $injectorSourceRoots },
    @{ Dll = "FusionRpg.Core.dll"; Source = Get-NewestSource $coreSourceRoots })
foreach ($check in $freshnessChecks) {
    if ($null -eq $check.Source) { continue }
    $built = Get-Item (Join-Path $PluginDir $check.Dll)
    if ($built.LastWriteTimeUtc -lt $check.Source.LastWriteTimeUtc) {
        throw @"
STALE DEPLOY: $($check.Dll) is older than the newest source in its dependency set.
  $($check.Dll)  $($built.LastWriteTimeUtc.ToString('u'))
  newest .cs    $($check.Source.LastWriteTimeUtc.ToString('u'))  ($($check.Source.Name))
The build did not actually produce this DLL. Usual causes: the game is still running and holds a
lock on it, or the injector project skipped compiling. Close the game and re-run.
"@
    }
}
Write-Host "==> Freshness OK — deployed injector artifacts match their source trees"

function Test-ServerUp {
    try {
        $r = Invoke-WebRequest -Uri $Health -UseBasicParsing -TimeoutSec 2
        return $r.StatusCode -eq 200
    } catch {
        return $false
    }
}

# NOTE (2026-08-30): `-NoServer` means "do not LAUNCH a server", never "do not BUILD one".
# It used to wrap this whole block, so `-NoServer` silently skipped `dotnet publish` too and left
# dist\FusionRpg.Server\ on whatever binary happened to be there. That cost a full hour of chasing a
# "fix that did not work" -- the fix was correct and unit-tested; the deployed server was 80 minutes
# stale and still carried the old code. A flag whose name describes one action must not quietly skip
# a different one. Publishing is ~10s and keeps dist\ honest; only the Start-Process below is gated.
$serverWasUp = Test-ServerUp
    if ($serverWasUp -and $RestartServer) {
        Write-Host "==> Stopping old RPG server on :5088"
        try {
            Get-NetTCPConnection -LocalPort 5088 -ErrorAction SilentlyContinue |
                Where-Object { $_.State -eq 'Listen' } |
                ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
        } catch { }
        Start-Sleep -Seconds 2
        $serverWasUp = $false
    }

    if ($serverWasUp) {
        # A running server locks its own published DLLs -- `dotnet publish` below would fail with a
        # confusing MSBuild file-lock retry spam, not a clear message. Skip DLL publish, but FE was
        # already mirrored into dist\wwwroot above (2026-09-09). Pass -RestartServer when you need
        # fresh server/injector managed DLLs too.
        Write-Host "==> Server already running at $Health -- skipping DLL publish (exe locks its own DLLs)."
        Write-Host "    FE wwwroot was synced into dist; hard-refresh the browser to confirm UI fixes."
        Write-Host "    Pass -RestartServer to stop it and publish a fresh server binary, or stop it yourself first."
        if (Test-Path $WwwrootSrc) {
            Sync-ServerWwwroot -Source $WwwrootSrc -Destination $WwwrootDist
        }
    } else {
        Write-Host "==> Publishing server to $ServerOut"
        dotnet publish $ServerProj -c Release -o $ServerOut --nologo -v q
        if ($LASTEXITCODE -ne 0) { throw "server publish failed -- see output above" }
        if (-not (Test-Path $ServerExe)) {
            throw "Server exe missing after publish: $ServerExe"
        }
        # Publish copies wwwroot, but vite may have finished after MSBuild's content snapshot in some
        # edge timings — re-mirror so dist always matches the UI build from this same script run.
        if (Test-Path $WwwrootSrc) {
            Sync-ServerWwwroot -Source $WwwrootSrc -Destination $WwwrootDist
        }
    }

    Write-Host "==> Importing data\seed into $DataDir (E20 — the server boots on this, not code literals)"
    $ImporterProj = Join-Path $Root "tools\AtomImporter\AtomImporter.csproj"
    dotnet run --project $ImporterProj -c Release -- --db $DataDir
    if ($LASTEXITCODE -ne 0) { throw "AtomImporter refused the import — see output above" }

    if ($NoServer) {
        Write-Host "==> -NoServer: built and published, NOT started. Start it yourself with:"
        Write-Host "    Start-Process -FilePath `"$ServerExe`" -WorkingDirectory `"$ServerOut`""
    } elseif ($serverWasUp) {
        Write-Host "==> Server still running at $Health (DLLs unchanged; FE wwwroot synced — hard-refresh UI)"
        Write-Host "    Pass -RestartServer to redeploy the server binary too"
    } else {
        Write-Host "==> Starting RPG server (data beside exe: $DataDir)"
        # No FUSIONRPG_DATA — Program.cs defaults to {exeDir}/data/{rpg-hot,rpg-media}.sqlite
        Start-Process -FilePath $ServerExe -WorkingDirectory $ServerOut | Out-Null
        $ok = $false
        foreach ($i in 1..30) {
            Start-Sleep -Seconds 1
            if (Test-ServerUp) { $ok = $true; break }
        }
        if (-not $ok) {
            Write-Warning "Server did not answer $Health yet. Check the server window."
        } else {
            Write-Host "==> Server up: $Health"
        }
    }

if (-not $NoGame) {
    $ServerUrl = "http://127.0.0.1:5088"
    if ($LoaderHost -eq "MelonLoader") {
        New-Item -ItemType Directory -Force -Path $PluginDir | Out-Null
        $cfg = Join-Path $PluginDir "fusionrpg.cfg"
        @"
# FusionRpg MelonLoader host config (written by deploy-play.ps1)
ServerUrl=$ServerUrl
PersistCheats=false
EnableUnsafeHitPatches=false
"@ | Set-Content -Path $cfg -Encoding UTF8
        Write-Host "==> Wrote $cfg"
    }

    $running = Get-Process -Name "PlantsVsZombiesRH" -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "==> Game already running (pid $($running.Id -join ', '))"
    } else {
        Write-Host "==> Launching $GameExe (FUSIONRPG_SERVER_URL=$ServerUrl)"
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $GameExe
        $psi.WorkingDirectory = "$GameDir"
        $psi.UseShellExecute = $false
        $psi.Environment["FUSIONRPG_SERVER_URL"] = $ServerUrl
        [System.Diagnostics.Process]::Start($psi) | Out-Null
    }
}

Write-Host ""
Write-Host "Host:     $LoaderHost"
Write-Host "Injector: $PluginDir"
Write-Host "Server:   $ServerExe"
Write-Host "SQLite:   $(Join-Path $DataDir 'rpg-hot.sqlite') + rpg-media.sqlite"
Write-Host "UI:       http://127.0.0.1:5088"
Write-Host "Share dist\FusionRpg.Server\data\ for analysis if needed."
