# Guard: tests must not leak temp dirs, and a failed temp-delete must never be swallowed.
# Usage (repo root): .\scripts\guard-test-substrate.ps1
#                    .\scripts\guard-test-substrate.ps1 -UpdateBaseline
# Live in deploy-play.ps1, .github/workflows/ci.yml, and FusionRpg.Guard.Tests.
#
# Bans, per docs/contributing/testing-standard.md:
#   swallowed-delete : Directory.Delete(...) inside an empty / comment-only catch block, in tests/**
#   temp-store       : a tests/** file that both constructs new RpgStore( and uses Path.GetTempPath
# A file is exempt only by an explicit line in scripts/test-substrate-baseline.txt (the ratchet).
#
# The baseline only shrinks: a listed file with no remaining violation FAILS (remove its line when
# fixed); adding a line requires a reason and owner sign-off.
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$UpdateBaseline
)

$ErrorActionPreference = "Stop"
$TestsDir = Join-Path $Root "tests"
$BaselinePath = Join-Path $PSScriptRoot "test-substrate-baseline.txt"

if (-not (Test-Path $TestsDir)) {
    throw "tests/ missing: $TestsDir"
}

function Get-Baseline {
    if (-not (Test-Path $BaselinePath)) { return @{} }
    $map = @{}
    foreach ($line in Get-Content -LiteralPath $BaselinePath) {
        $t = $line.Trim()
        if ($t -eq "" -or $t.StartsWith("#")) { continue }
        $parts = $t -split "\s*:\s*", 2
        if ($parts.Count -eq 2) { $map[$parts[0].Trim()] = $parts[1].Trim() }
    }
    return $map
}

# Strip // line comments, /* */ block comments, and string literals in ONE pass, so a comment that
# mentions a banned pattern is not a violation (same discipline as guard-dal.ps1's comment skip).
#
# ONE PASS IS LOAD-BEARING, and this is a fixed defect: the first version stripped block comments
# before line comments with a regex, so a `/**` inside prose (e.g. the path literal
# `data/seed/items/charms/**` in a doc comment) opened a phantom block comment whose match ran
# forward to the next real `*/` -- often 150+ lines below -- deleting the ctor and Dispose from the
# scanned text and making a real swallowed delete INVISIBLE. 15 test files contain such prose, so the
# gate silently passed on them; two (CharmCarryStoreTests, ItemSetStoreTests) really were violating.
# A single left-to-right scan cannot do that: it consumes a `//` line to EOL and a `/* */` block to
# its own closing marker, whichever comes first, and never lets one comment type eat the other.
function Strip-Comments {
    param([string]$Text)
    $sb = [System.Text.StringBuilder]::new($Text.Length)
    $i = 0
    $n = $Text.Length
    while ($i -lt $n) {
        $c = $Text[$i]

        # // line comment -> to end of line
        if ($c -eq '/' -and $i + 1 -lt $n -and $Text[$i + 1] -eq '/') {
            while ($i -lt $n -and $Text[$i] -ne "`n") { $i++ }
            continue
        }

        # /* block comment -> to its own closing marker
        if ($c -eq '/' -and $i + 1 -lt $n -and $Text[$i + 1] -eq '*') {
            $i += 2
            while ($i + 1 -lt $n -and -not ($Text[$i] -eq '*' -and $Text[$i + 1] -eq '/')) { $i++ }
            $i = [Math]::Min($n, $i + 2)
            [void]$sb.Append(' ')
            continue
        }

        # "..." / '...' string or char literal -> skip it, so a banned token inside a literal (or a
        # `*/` inside a string, which previously mis-paired with a later `/*`) is not scanned either.
        if ($c -eq '"' -or $c -eq "'") {
            $quote = $c
            $i++
            # @"..." verbatim and """" raw strings: treat a doubled quote as an escape.
            while ($i -lt $n) {
                if ($Text[$i] -eq '\' -and $quote -eq '"') { $i += 2; continue }
                if ($Text[$i] -eq $quote) {
                    if ($i + 1 -lt $n -and $Text[$i + 1] -eq $quote) { $i += 2; continue }
                    $i++
                    break
                }
                $i++
            }
            [void]$sb.Append('""')
            continue
        }

        [void]$sb.Append($c)
        $i++
    }
    return $sb.ToString()
}

# Find every Directory.Delete whose immediately-enclosing catch block is empty / comment-only.
# Walks char-by-char for brace matching so multi-line and nested blocks are handled.
function Find-SwallowedDelete {
    param([string]$Path)
    $raw = Get-Content -LiteralPath $Path -Raw
    $code = Strip-Comments $raw
    $violations = @()

    foreach ($m in [regex]::Matches($code, 'Directory\.Delete\s*\(')) {
        # find the nearest 'catch' at or after the delete, before the next '}'
        $tail = $code.Substring($m.Index)
        $c = [regex]::Match($tail, 'catch\b')
        if (-not $c.Success) { continue }
        # the catch body must open within a short window (same statement region)
        $after = $tail.Substring($c.Index)
        $open = $after.IndexOf('{')
        if ($open -lt 0) { continue }
        # skip if a ']' or another statement starts far before the brace
        if ($open -gt 200) { continue }
        # brace-match the catch body
        $depth = 0; $i = $open; $end = -1
        while ($i -lt $after.Length) {
            $ch = $after[$i]
            if ($ch -eq '{') { $depth++ }
            elseif ($ch -eq '}') { $depth--; if ($depth -eq 0) { $end = $i; break } }
            $i++
        }
        if ($end -lt 0) { continue }
        $body = $after.Substring($open + 1, $end - $open - 1)
        # comment-stripped body: whitespace or ';' or empty => swallowed
        if ($body.Trim() -match '^[;\s]*$') {
            $violations += @{ code = 'swallowed-delete'; line = $raw.Substring(0, $m.Index).Split("`n").Count }
        }
    }
    return $violations
}

function Find-TempStore {
    param([string]$Path)
    $raw = Get-Content -LiteralPath $Path -Raw
    $code = Strip-Comments $raw
    if ($code -match 'new\s+RpgStore\s*\(' -and $code -match 'Path\.GetTempPath') {
        return @{ code = 'temp-store'; line = 0 }
    }
    return $null
}

$baseline = Get-Baseline
$found = @{}          # relpath -> sorted unique codes
$violationsByFile = @{}

# The gate's own tests must contain the banned patterns as fixtures (they prove the gate fails).
# Excluding one named file is narrower than allowing it everywhere, and it is asserted by
# TestSubstrateGuardTests, so it cannot silently widen.
$selfExempt = @(
    "tests/FusionRpg.Guard.Tests/TestSubstrateGuardTests.cs"
)

Get-ChildItem -Path $TestsDir -Recurse -Filter "*.cs" | ForEach-Object {
    $full = $_.FullName
    if ($full -match '[\\/](obj|bin)[\\/]') { return }
    $rel = $full.Substring($Root.Length).TrimStart('\', '/').Replace('\', '/')
    if ($selfExempt -contains $rel) { return }

    $codes = @()
    foreach ($v in (Find-SwallowedDelete -Path $full)) { $codes += $v.code }
    $ts = Find-TempStore -Path $full
    if ($ts) { $codes += $ts.code }
    $codes = $codes | Sort-Object -Unique

    if ($codes.Count -gt 0) {
        $found[$rel] = $codes
        $violationsByFile[$rel] = ($codes -join ", ")
    }
}

if ($UpdateBaseline) {
    $lines = @(
        "# Test-substrate baseline (ratchet). One line per exempt file: path : code[,code]",
        "# A listed file with no remaining violation fails the gate — remove its line when fixed.",
        "# Adding a line is a review event: state why, get owner sign-off. See docs/contributing/testing-standard.md.",
        "# Generated 2026-09-12 by scripts/guard-test-substrate.ps1 -UpdateBaseline."
    )
    foreach ($rel in ($found.Keys | Sort-Object)) {
        $lines += "$rel : $($violationsByFile[$rel])"
    }
    Set-Content -LiteralPath $BaselinePath -Value $lines -Encoding utf8
    Write-Host "test-substrate baseline written: $($found.Count) file(s) -> $BaselinePath"
    exit 0
}

$failures = @()

# 1) New violations not in the baseline.
foreach ($rel in ($found.Keys | Sort-Object)) {
    if (-not $baseline.ContainsKey($rel)) {
        foreach ($c in $found[$rel]) { $failures += "${rel}: $c (new — not in baseline)" }
    } else {
        # newly appeared code within an already-exempt file is still a new violation
        $old = $baseline[$rel].Split(',') | ForEach-Object { $_.Trim() }
        foreach ($c in $found[$rel]) {
            if ($old -notcontains $c) { $failures += "${rel}: $c (new code — not in baseline line)" }
        }
    }
}

# 2) Stale baseline entries: fixed files must be removed so the ratchet only shrinks.
foreach ($rel in ($baseline.Keys | Sort-Object)) {
    if (-not $found.ContainsKey($rel)) {
        $failures += "${rel}: baseline entry no longer violated — remove the line (the ratchet only shrinks)"
    }
}

if ($failures.Count -gt 0) {
    Write-Host "TEST SUBSTRATE GUARD FAILED — test temp-dir / swallowed-delete violations:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  $_" }
    Write-Host ""
    Write-Host "Standard: docs/contributing/testing-standard.md" -ForegroundColor Yellow
    exit 1
}

Write-Host "TEST SUBSTRATE GUARD OK — no new swallowed deletes or temp-backed stores in tests/"
exit 0
