# Guard: classify every /api/debug/* route handler in src/FusionRpg.Server/DebugEndpoints.cs as
# Game-Injector-Debug-shaped or RPG-Server-Debug-shaped.
# Spec: docs/architecture/live-probe/spec-debug-scope-guard.md
# Standard: docs/contributing/live-probe-standard.md
#
# THE CORRECTED RULE (2026-09-13 audit): any relay call to the Injector -- Send(hub, inbox, "...",
# ...), by name, regardless of the command string's own prefix ("debug.*", "cheat.toggle",
# "effects.reload", "pvz.spawn.extra" all count) -- anywhere in a handler body makes that route
# Game-Injector-Debug-shaped, full stop. A real persisted-write call (store.*, ua.*, a *Service.*
# method) ALONGSIDE a relay is legitimate orchestration (e.g. entering a level requires telling the
# Injector to do it), never a violation. A route is RPG-Server-Debug-shaped only when it has NO
# relay at all, and does reach real persisted/domain logic. Neither present -> flagged for manual
# review, never auto-classified either way (e.g. a route that only reads static in-memory state).
#
# Usage (repo root): .\scripts\guard-debug-scope.ps1
#                     .\scripts\guard-debug-scope.ps1 -FilePath <fixture.cs>   # tests point elsewhere
#
# Once DebugEndpoints.cs carries the `// Game Injector Debug` / `// RPG Server Debug` scope-banner
# comments (spec Task 3), this guard ALSO checks that each route's nearest preceding banner agrees
# with its computed classification -- a mismatch fails the guard. Before those banners exist this
# check simply finds none and is a no-op, which is why the guard is green today with zero
# exemptions and only gains teeth once the banners land.
#
# Technique: text/regex-only, no dotnet build, mirroring guard-test-substrate.ps1's
# Find-SwallowedDelete -- character-by-character depth counting, not a flat single-line regex (a
# flat regex cannot isolate one handler's true multi-line extent; guard-single-writer.ps1's flat
# whole-file-regex approach was tried here first and found wrong against this file's real shape).
# Parens and braces nest independently in C#, so a route's full call span (both a block-bodied
# lambda `=> { ... }` AND an expression-bodied one like GET /session's `=> Results.Ok(...)` with no
# braces at all) is isolated correctly by counting '(' / ')' depth alone, from the call's own
# opening paren to its matching close -- embedded '{' '}' never affect that count. Brace-depth
# counting (the same technique Find-SwallowedDelete uses) is used separately below, where it is the
# right tool: isolating an actual method BODY (the MapPost helper's own definition, and
# AcceptDebugSpawnExtra's) for the two self-verification checks.

param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$FilePath = (Join-Path $Root "src\FusionRpg.Server\DebugEndpoints.cs")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $FilePath)) {
    throw "Target file missing: $FilePath"
}

# Strip // line comments and /* */ block comments to blank spaces IN PLACE (same length, newlines
# preserved), so a keyword mentioned only in prose never triggers a false classification, and
# character indices line up 1:1 with the raw text for the banner-proximity check below. String/char
# literals are scanned over (so an embedded "//" or "/*" — e.g. a URL — is never mistaken for a
# comment start) but their own text is left untouched: route paths and command-name strings are
# meaningful and must survive for Shape A/B's regex captures.
function Strip-CommentsPreservingLayout {
    param([string]$Text)
    $n = $Text.Length
    $chars = $Text.ToCharArray()
    $i = 0
    while ($i -lt $n) {
        $c = $chars[$i]

        if ($c -eq '/' -and $i + 1 -lt $n -and $chars[$i + 1] -eq '/') {
            while ($i -lt $n -and $chars[$i] -ne "`n") { $chars[$i] = ' '; $i++ }
            continue
        }

        if ($c -eq '/' -and $i + 1 -lt $n -and $chars[$i + 1] -eq '*') {
            $chars[$i] = ' '; $chars[$i + 1] = ' '
            $i += 2
            while ($i + 1 -lt $n -and -not ($chars[$i] -eq '*' -and $chars[$i + 1] -eq '/')) {
                if ($chars[$i] -ne "`n") { $chars[$i] = ' ' }
                $i++
            }
            if ($i + 1 -lt $n) { $chars[$i] = ' '; $chars[$i + 1] = ' '; $i += 2 }
            continue
        }

        if ($c -eq '"' -or $c -eq "'") {
            $quote = $c
            $i++
            while ($i -lt $n) {
                if ($chars[$i] -eq '\' -and $i + 1 -lt $n) { $i += 2; continue }
                if ($chars[$i] -eq $quote) { $i++; break }
                $i++
            }
            continue
        }

        $i++
    }
    return -join $chars
}

# Balanced-depth scan from the index of an opening bracket to its matching close. One routine for
# either bracket pair: paren-matching a route's full call span (Shape B), or brace-matching an
# actual method body (the two self-verification checks).
function Find-MatchingClose {
    param([string]$Code, [int]$OpenIndex, [char]$Open, [char]$Close)
    $depth = 0
    $i = $OpenIndex
    $n = $Code.Length
    while ($i -lt $n) {
        $c = $Code[$i]
        if ($c -eq $Open) { $depth++ }
        elseif ($c -eq $Close) { $depth--; if ($depth -eq 0) { return $i } }
        $i++
    }
    return -1
}

function Get-Line {
    param([string]$Text, [int]$Index)
    return ($Text.Substring(0, $Index) -split "`n").Count
}

$raw = Get-Content -LiteralPath $FilePath -Raw
$code = Strip-CommentsPreservingLayout $raw
if ($code.Length -ne $raw.Length) { throw "internal error: stripped text length drifted from raw text" }

$routes = New-Object System.Collections.Generic.List[object]

# ---- Shape A: the shared single-line helper -- MapPost(g, "path", "cmd") -- Game Injector Debug
# by construction, no body scan. `(?<!\.)` excludes `g.MapPost(` (a dotted call) so this never
# collides with Shape B. ----
$shapeA = [regex]::Matches($code, '(?<!\.)\bMapPost\s*\(\s*g\s*,\s*"(?<path>[^"]*)"\s*,\s*"(?<cmd>[^"]*)"\s*\)\s*;')
foreach ($m in $shapeA) {
    $routes.Add([pscustomobject]@{
        Method         = "POST"
        Path           = $m.Groups['path'].Value
        Shape          = "A"
        Index          = $m.Index
        Classification = "GameInjectorDebug"
        Reason         = "shared MapPost(g, path, cmd) helper -- Game Injector Debug by construction"
    })
}

# Self-verify the MapPost(g, path, cmd) helper itself still relays -- Shape A's whole premise. If a
# future edit strips its Send(...) call, Shape A's classification would silently go stale; fail loud
# instead.
$mapPostDef = [regex]::Match($code, '\bstatic\s+void\s+MapPost\s*\(\s*RouteGroupBuilder\s+g\s*,\s*string\s+path\s*,\s*string\s+cmdName\s*\)')
if ($mapPostDef.Success) {
    $mpBraceOpen = $code.IndexOf('{', $mapPostDef.Index + $mapPostDef.Length)
    $mpBraceClose = Find-MatchingClose -Code $code -OpenIndex $mpBraceOpen -Open '{' -Close '}'
    if ($mpBraceOpen -lt 0 -or $mpBraceClose -lt 0) {
        throw "guard-debug-scope: could not brace-match the MapPost(g, path, cmdName) helper body -- fix the guard."
    }
    $mpBody = $code.Substring($mpBraceOpen, $mpBraceClose - $mpBraceOpen + 1)
    if ($mpBody -notmatch '\bSend\s*\(\s*hub\s*,\s*inbox\s*,') {
        throw "guard-debug-scope: MapPost(g, path, cmdName) no longer relays via Send(hub, inbox, ...) -- Shape A's `Game Injector Debug by construction' claim is stale. Fix the guard or the helper."
    }
} elseif ($shapeA.Count -gt 0) {
    throw "guard-debug-scope: found MapPost(g, path, cmd) call sites but no matching helper definition to self-verify against -- fix the guard."
}

# Self-verify AcceptDebugSpawnExtra still relays -- the delegate-call special case below (for
# /spawn-extra and /fire-spawn-extra) depends on it.
$acceptDef = [regex]::Match($code, '\bAcceptDebugSpawnExtra\s*\(\s*JsonElement\s+body')
if ($acceptDef.Success) {
    $abBraceOpen = $code.IndexOf('{', $acceptDef.Index + $acceptDef.Length)
    $abBraceClose = Find-MatchingClose -Code $code -OpenIndex $abBraceOpen -Open '{' -Close '}'
    if ($abBraceOpen -lt 0 -or $abBraceClose -lt 0) {
        throw "guard-debug-scope: could not brace-match AcceptDebugSpawnExtra's body -- fix the guard."
    }
    $abBody = $code.Substring($abBraceOpen, $abBraceClose - $abBraceOpen + 1)
    if ($abBody -notmatch '\bSend\s*\(\s*hub\s*,\s*inbox\s*,') {
        throw "guard-debug-scope: AcceptDebugSpawnExtra no longer relays via Send(hub, inbox, ...) -- its callers' Game-Injector-Debug classification is stale. Fix the guard or the helper."
    }
}

# ---- Shape B: an inline g.MapPost("/path", ...) / g.MapGet("/path", ...) lambda registration.
# Isolate the FULL call span via paren-depth counting from the call's own '(' to its matching ')' --
# this correctly spans a block-bodied lambda (embedded '{' '}' never affect paren depth) AND an
# expression-bodied one (no braces at all) with one technique. ----
$shapeB = [regex]::Matches($code, 'g\.(?<verb>MapPost|MapGet)\s*\(\s*"(?<path>[^"]*)"')
foreach ($m in $shapeB) {
    $openParen = $code.IndexOf('(', $m.Index)
    if ($openParen -lt 0) {
        throw "guard-debug-scope: could not find the opening paren for $($m.Groups['verb'].Value) '$($m.Groups['path'].Value)' at line $(Get-Line $raw $m.Index)"
    }
    $closeParen = Find-MatchingClose -Code $code -OpenIndex $openParen -Open '(' -Close ')'
    if ($closeParen -lt 0) {
        throw "guard-debug-scope: unbalanced parens scanning $($m.Groups['verb'].Value) '$($m.Groups['path'].Value)' at line $(Get-Line $raw $m.Index)"
    }
    $span = $code.Substring($openParen + 1, $closeParen - $openParen - 1)

    $hasRelay = $span -match '\bSend\s*\(\s*hub\s*,\s*inbox\s*,'
    $hasDelegateRelay = $span -match '\bAcceptDebugSpawnExtra\s*\('
    $hasPersisted = ($span -match '\bRpgStore\b') -or ($span -match '\bua\.\w+\s*\(') -or ($span -match '\b\w+Service\.\w+\s*\(')

    if ($hasRelay -or $hasDelegateRelay) {
        $classification = "GameInjectorDebug"
        $reason = if ($hasRelay) { "relay call Send(hub, inbox, ...) in body" } else { "delegates to AcceptDebugSpawnExtra, which itself relays" }
    } elseif ($hasPersisted) {
        $classification = "RpgServerDebug"
        $reason = "no relay; real persisted-domain call/param (RpgStore / ua.* / *Service.*)"
    } else {
        $classification = "ManualReview"
        $reason = "no relay and no persisted-domain call found -- needs manual review"
    }

    $routes.Add([pscustomobject]@{
        Method         = $m.Groups['verb'].Value.Replace("Map", "").ToUpperInvariant()
        Path           = $m.Groups['path'].Value
        Shape          = "B"
        Index          = $m.Index
        Classification = $classification
        Reason         = $reason
    })
}

$routes = @($routes | Sort-Object Index)

# ---- Banner-vs-classification check. Before Task 3 lands the scope-banner comments this finds
# none and is a no-op (which is why the guard is green today); afterwards a route whose nearest
# preceding banner disagrees with its computed classification fails the guard. ManualReview routes
# are never checked against a banner -- the corrected rule deliberately does not auto-classify them
# either way, so no banner text on them can be "wrong". ----
$bannerMatches = [regex]::Matches($raw, '(?m)^\s*//\s*(?<label>Game Injector Debug|RPG Server Debug)\s*$')
$banners = @($bannerMatches)

function Get-DeclaredBanner {
    param([array]$Banners, [int]$Index)
    $best = $null
    foreach ($b in $Banners) {
        if ($b.Index -le $Index) { $best = $b } else { break }
    }
    if ($null -eq $best) { return $null }
    if ($best.Groups['label'].Value -eq 'Game Injector Debug') { return "GameInjectorDebug" }
    return "RpgServerDebug"
}

$failures = @()
foreach ($r in $routes) {
    $declared = Get-DeclaredBanner -Banners $banners -Index $r.Index
    if ($null -ne $declared -and $r.Classification -ne "ManualReview" -and $declared -ne $r.Classification) {
        $failures += "$($r.Method) $($r.Path) (line $(Get-Line $raw $r.Index)): banner says '$declared' but computed classification is '$($r.Classification)' -- $($r.Reason)"
    }
}

Write-Host "DEBUG SCOPE GUARD -- $($routes.Count) route(s) classified in $FilePath"
foreach ($r in $routes) {
    Write-Host ("  [{0,-17}] {1,-5} {2}  -- {3}" -f $r.Classification, $r.Method, $r.Path, $r.Reason)
}

if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "DEBUG SCOPE GUARD FAILED -- banner/classification mismatch:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  $_" }
    Write-Host ""
    Write-Host "Standard: docs/contributing/live-probe-standard.md; spec: docs/architecture/live-probe/spec-debug-scope-guard.md" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "DEBUG SCOPE GUARD OK -- $($routes.Count) route(s), 0 banner mismatches"
exit 0
