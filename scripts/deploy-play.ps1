# Fast local deploy: injector into the game folder, RPG server, then launch PVZRH.
# Usage (repo root):
#   .\scripts\deploy-play.ps1
#   .\scripts\deploy-play.ps1 -LoaderHost MelonLoader   # requires FUSIONRPG_ML_GAMEDIR Melon pack
#   .\scripts\deploy-play.ps1 -NoGame
#   .\scripts\deploy-play.ps1 -RebuildUi
# Server data (rpg-hot / rpg-media) lives next to the published exe: dist\FusionRpg.Server\data\
# Runs guard-single-writer.ps1 + guard-dal.ps1 + guard-secondary-no-unity.ps1 + guard-funnel-delta.ps1 before build.
param(
    [ValidateSet("BepInEx", "MelonLoader")]
    [string]$LoaderHost = "BepInEx",
    [switch]$NoGame,
    [switch]$NoServer,
    [switch]$RebuildUi,
    [switch]$RestartServer
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

$ServerProj = Join-Path $Root "src\FusionRpg.Server\FusionRpg.Server.csproj"
$ServerOut = Join-Path $Root "dist\FusionRpg.Server"
$ServerExe = Join-Path $ServerOut "FusionRpg.Server.exe"
$DataDir = Join-Path $ServerOut "data"
$Health = "http://127.0.0.1:5088/health"

if ($LoaderHost -eq "MelonLoader") {
    if (-not $env:FUSIONRPG_ML_GAMEDIR) {
        throw "MelonLoader host requires FUSIONRPG_ML_GAMEDIR (MelonLoader game pack). Do not write into BepInEx\plugins."
    }
    $GameDir = Resolve-Path $env:FUSIONRPG_ML_GAMEDIR
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

if ($RebuildUi) {
    Write-Host "==> Building web UI"
    Push-Location (Join-Path $Root "web\fusion-rpg-web")
    if (-not (Test-Path "node_modules")) { npm install }
    npm run build
    Pop-Location
}

Write-Host "==> Single-writer guard"
& (Join-Path $Root "scripts\guard-single-writer.ps1")
if ($LASTEXITCODE -ne 0) { throw "single-writer guard failed" }

Write-Host "==> DAL guard"
& (Join-Path $Root "scripts\guard-dal.ps1")
if ($LASTEXITCODE -ne 0) { throw "DAL guard failed" }

Write-Host "==> Secondary no-Unity guard"
& (Join-Path $Root "scripts\guard-secondary-no-unity.ps1")
if ($LASTEXITCODE -ne 0) { throw "secondary no-Unity guard failed" }

Write-Host "==> Funnel delta guard"
& (Join-Path $Root "scripts\guard-funnel-delta.ps1")
if ($LASTEXITCODE -ne 0) { throw "funnel delta guard failed" }

Write-Host "==> Building $LoaderHost injector ($GameProfile) into $PluginDir"
& (Join-Path $Root "scripts\guard-game-profile.ps1") -GameDir $GameDir -ExpectedProfile $GameProfile
if ($LASTEXITCODE -ne 0) { throw "game-profile guard failed" }
if ($LoaderHost -eq "MelonLoader") {
    dotnet build $InjectorProj -c Release -p:MlGameDir=$GameDir -p:GameProfile=$GameProfile -p:OutputPath=$PluginDir\
}
else {
    dotnet build $InjectorProj -c Release -p:GameDir=$GameDir -p:GameProfile=$GameProfile
}
if (-not (Test-Path (Join-Path $PluginDir $InjectorDll))) {
    throw "Injector DLL missing after build: $PluginDir\$InjectorDll"
}
if (-not (Test-Path (Join-Path $PluginDir "FusionRpg.Core.dll"))) {
    throw "FusionRpg.Core.dll missing after build"
}

function Test-ServerUp {
    try {
        $r = Invoke-WebRequest -Uri $Health -UseBasicParsing -TimeoutSec 2
        return $r.StatusCode -eq 200
    } catch {
        return $false
    }
}

if (-not $NoServer) {
    if ((Test-ServerUp) -and $RestartServer) {
        Write-Host "==> Stopping old RPG server on :5088"
        try {
            Get-NetTCPConnection -LocalPort 5088 -ErrorAction SilentlyContinue |
                Where-Object { $_.State -eq 'Listen' } |
                ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
        } catch { }
        Start-Sleep -Seconds 2
    }

    Write-Host "==> Publishing server to $ServerOut"
    dotnet publish $ServerProj -c Release -o $ServerOut --nologo -v q
    if (-not (Test-Path $ServerExe)) {
        throw "Server exe missing after publish: $ServerExe"
    }

    if (Test-ServerUp) {
        Write-Host "==> Server already running at $Health (pass -RestartServer after server code changes)"
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
