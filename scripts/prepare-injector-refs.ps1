# Prepare BepInEx core refs + optional game interop for Injector builds (CI / path-free publish).
# Does not install into any game folder.
$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")

& (Join-Path $PSScriptRoot "fetch-bepinex-refs.ps1")

$Refs = Join-Path $Root "artifacts\bepinex-refs"
$InteropOut = Join-Path $Refs "BepInEx\interop"

function Copy-InteropFrom([string]$GameDir) {
    $src = Join-Path $GameDir "BepInEx\interop"
    if (-not (Test-Path (Join-Path $src "Assembly-CSharp.dll"))) {
        throw "FUSIONRPG_GAME_DIR has no BepInEx\interop\Assembly-CSharp.dll: $GameDir"
    }
    New-Item -ItemType Directory -Force -Path $InteropOut | Out-Null
    Copy-Item (Join-Path $src "*") $InteropOut -Force
    Write-Host "==> Copied interop from $GameDir"
}

if ($env:FUSIONRPG_GAME_DIR -and (Test-Path $env:FUSIONRPG_GAME_DIR)) {
    Copy-InteropFrom $env:FUSIONRPG_GAME_DIR
}
elseif ($env:FUSIONRPG_INTEROP_ZIP_URL) {
    $zip = Join-Path $Root "artifacts\interop-cache.zip"
    Write-Host "==> Downloading interop zip from FUSIONRPG_INTEROP_ZIP_URL"
    Invoke-WebRequest -Uri $env:FUSIONRPG_INTEROP_ZIP_URL -OutFile $zip -UseBasicParsing
    $tmp = Join-Path $Root "artifacts\interop-extract"
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $tmp -Force
    Remove-Item $zip -Force
    $src = if (Test-Path (Join-Path $tmp "Assembly-CSharp.dll")) { $tmp }
        elseif (Test-Path (Join-Path $tmp "BepInEx\interop\Assembly-CSharp.dll")) { Join-Path $tmp "BepInEx\interop" }
        else {
            $hit = Get-ChildItem $tmp -Recurse -Filter "Assembly-CSharp.dll" | Select-Object -First 1
            if (-not $hit) { throw "Interop zip missing Assembly-CSharp.dll" }
            $hit.Directory.FullName
        }
    New-Item -ItemType Directory -Force -Path $InteropOut | Out-Null
    Copy-Item (Join-Path $src "*") $InteropOut -Force
    Remove-Item $tmp -Recurse -Force
    Write-Host "==> Interop ready from zip URL"
}
elseif (Test-Path (Join-Path $InteropOut "Assembly-CSharp.dll")) {
    Write-Host "==> Using existing interop under $InteropOut"
}
else {
    $parent = Join-Path $Root ".."
    if (Test-Path (Join-Path $parent "BepInEx\interop\Assembly-CSharp.dll")) {
        Copy-InteropFrom (Resolve-Path $parent).Path
    }
}

if (-not (Test-Path (Join-Path $InteropOut "Assembly-CSharp.dll"))) {
    throw @"
Injector interop missing under $InteropOut.
Set FUSIONRPG_GAME_DIR to a legal game folder with BepInEx\interop,
or set FUSIONRPG_INTEROP_ZIP_URL to a private zip of those DLLs (do not commit interop to git).
"@
}

$env:FUSIONRPG_GAME_DIR = $Refs
Write-Host "FUSIONRPG_GAME_DIR=$Refs"
