# PROVE: live-probe-tool (spec-live-probe-tool.md) — the real 6-step live-probe recipe against a
# running FusionRpg.Server (+ live game/Injector for -Mode B), end to end, real HTTP throughout. This
# is the one prove tool in the repo that is NOT RpgStore.InMemory() in-process (ProveHubCombat/
# ProveAptitude's shape): it drives tools/ProveLiveProbe, a small console tool that opens a real
# HttpClient against -BaseUrl (default http://127.0.0.1:5088).
#
# Mode A (persisted-state only, steps 1-5): Server up, no game/Injector needed.
# Mode B (full 6-step proof): Injector connected + a live match/board already running.
#
# Usage (repo root) — every named flag just forwards through to `dotnet run --` as-is:
#   .\scripts\prove-live-probe.ps1 -Mode A -PlayerId 1 -Side plant -TypeId <id> `
#       -AptitudeId Might -AptitudePoints 30 -Role <slot> -ItemInstanceId <owned-item-id>
#   .\scripts\prove-live-probe.ps1 -Mode B -PlayerId 1 -Side plant -BannerId <banner-id> `
#       -AptitudeId Might -AptitudePoints 30 -Role <slot> -ItemInstanceId <owned-item-id> -TimeoutSec 30
# exits 0 on full pass, non-zero + a labeled failure line naming which half (persisted vs live) failed.
#
# Deliberately NOT scripts/prove-hub-combat.ps1's own "--no-build, retry once with a build on any
# nonzero exit" shape: that tool's exit code is a pure in-memory assertion result, so re-running it
# costs nothing. This tool's real run has real HTTP side effects (mints a specimen, spends allocation
# points, equips an item) — retrying on ANY nonzero exit would silently re-run a genuine step refusal
# a second time against the live server. The build check here is keyed on the build OUTPUT existing,
# never on the recipe's own exit code, so a real refusal is reported once and only once.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RestArgs
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$toolDir = Join-Path $repoRoot "tools\ProveLiveProbe"
$builtDll = Join-Path $toolDir "bin\Debug\net8.0\ProveLiveProbe.dll"

if (-not (Test-Path $builtDll)) {
    & dotnet build $toolDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Push-Location $toolDir
try {
    & dotnet run --no-build -- @RestArgs
    $exitCode = $LASTEXITCODE
} finally {
    Pop-Location
}

exit $exitCode
