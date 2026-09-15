# Guard: the MelonLoader injector host still compiles.
# Usage (repo root): .\scripts\guard-injector-compile.ps1
#
# Builds src/FusionRpg.Injector.MelonLoader.39 into a temp OutputPath, never the game's Mods folder, so
# it works while the game is running and holds the deployed DLLs. Needs the game's MelonLoader interop
# assemblies: FUSIONRPG_ML_GAMEDIR, else FUSIONRPG_ML_GAMEDIR_DEFAULT from the repo .env. With neither
# (CI), it prints SKIPPED and exits 0 — a skip is reported, never passed off as a compile.
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$gameDir = $env:FUSIONRPG_ML_GAMEDIR
if (-not $gameDir) {
    $envFile = Join-Path $Root ".env"
    if (Test-Path -LiteralPath $envFile) {
        $line = Get-Content -LiteralPath $envFile | Where-Object { $_ -match '^\s*FUSIONRPG_ML_GAMEDIR_DEFAULT\s*=' } | Select-Object -First 1
        if ($line) { $gameDir = ($line -split '=', 2)[1].Trim() }
    }
}

if (-not $gameDir -or -not (Test-Path -LiteralPath (Join-Path $gameDir "MelonLoader"))) {
    Write-Host "INJECTOR COMPILE GUARD SKIPPED — no MelonLoader game dir (set FUSIONRPG_ML_GAMEDIR); injector NOT compiled" -ForegroundColor Yellow
    exit 0
}

$project = Join-Path $Root "src\FusionRpg.Injector.MelonLoader.39\FusionRpg.Injector.MelonLoader.39.csproj"
$out = Join-Path ([System.IO.Path]::GetTempPath()) "fusionrpg-injector-compile\"
$log = & dotnet build $project -c Release "-p:OutputPath=$out" "-p:MlGameDir=$gameDir" -nologo 2>&1
if ($LASTEXITCODE -ne 0) {
    $log | Where-Object { $_ -match ' error ' } | Select-Object -First 20 | ForEach-Object { Write-Host $_ }
    Write-Host "INJECTOR COMPILE GUARD FAILED" -ForegroundColor Red
    exit 1
}
if ($log -match 'Skipping FusionRpg\.Injector\.MelonLoader\.39') {
    Write-Host "INJECTOR COMPILE GUARD FAILED — project skipped itself (interop refs not found under $gameDir)" -ForegroundColor Red
    exit 1
}
Write-Host "INJECTOR COMPILE GUARD OK — MelonLoader host compiled to $out"
exit 0
