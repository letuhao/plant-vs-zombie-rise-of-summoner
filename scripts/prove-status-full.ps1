# LIVE prove: StatusRuntime L2 catalog (status-l2-*) against a running lawn.
# Requires: lawn open, injector connected, SIM off. One scenario at a time.
# Usage:
#   .\scripts\prove-status-full.ps1
#   .\scripts\prove-status-full.ps1 -BaseUrl http://127.0.0.1:5088 -IncludeUnityBypass
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [switch]$IncludeUnityBypass,
    [switch]$SkipVisual,
    [string]$OutJson = ""
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')
if (-not $OutJson) {
    $OutJson = Join-Path $PSScriptRoot "..\docs\research\effect-runtime\_prove-status-full.json"
}

# -SkipVisual is the default track (F52+). -IncludeUnityBypass adds F5–F10.
if ($SkipVisual) { $IncludeUnityBypass = $false }

function Write-Step([string]$name, [bool]$ok, [string]$detail) {
    $mark = if ($ok) { "PASS" } else { "FAIL" }
    Write-Host ("[{0}] {1}: {2}" -f $mark, $name, $detail)
    return $ok
}

function Get-MaxEventId([string]$url) {
    function Has-After([long]$id) {
        $page = Invoke-RestMethod -Uri "$url/api/events?afterId=$id&limit=1" -Method GET
        return @($page.items).Count -gt 0
    }
    if (-not (Has-After 0)) { return 0L }
    $lo = 0L
    $hi = 1L
    while (Has-After $hi) {
        $lo = $hi
        if ($hi -gt [long]::MaxValue / 2) { break }
        $hi = $hi * 2L
    }
    while (($hi - $lo) -gt 1L) {
        $mid = $lo + (($hi - $lo) / 2L)
        if (Has-After $mid) { $lo = $mid } else { $hi = $mid }
    }
    $tail = Invoke-RestMethod -Uri "$url/api/events?afterId=$lo&limit=500" -Method GET
    $batch = @($tail.items)
    if ($batch.Count -eq 0) { return $lo }
    return [long]$batch[-1].id
}

function Get-Payload($ev) {
    $p = $ev.payload
    if ($null -eq $p) { return $null }
    if ($p -is [string]) {
        try { return $p | ConvertFrom-Json } catch { return $null }
    }
    return $p
}

function Test-EqPtr([string]$a, [string]$b) {
    if ([string]::IsNullOrWhiteSpace($a) -or [string]::IsNullOrWhiteSpace($b)) { return $false }
    $na = ($a.Trim() -replace '^0x', '').ToUpperInvariant()
    $nb = ($b.Trim() -replace '^0x', '').ToUpperInvariant()
    return $na -and $nb -and ($na -eq $nb)
}

function Get-InterestingKinds {
    return @(
        "debug.run-steps.done",
        "debug.status",
        "debug.status.resisted",
        "debug.status.applied",
        "debug.status.cleared",
        "debug.actor-derived",
        "debug.effect.synthetic",
        "debug.board-stats",
        "debug.combat.packet"
    )
}

function Add-MatchingEvents($bucket, $items) {
    $kinds = Get-InterestingKinds
    foreach ($ev in @($items)) {
        if ($kinds -contains [string]$ev.kind) {
            $bucket.Add($ev) | Out-Null
        }
    }
}

function Wait-AndCollect([string]$url, [long]$afterId, [double]$doneTimeoutSec, [double]$extraWaitSec, [bool]$refreshStatus, [string]$actorDerivedPtr) {
    $collected = New-Object System.Collections.Generic.List[object]
    $cursor = $afterId
    $deadline = (Get-Date).AddSeconds($doneTimeoutSec)
    $gotDone = $false
    $doneAt = $null
    while ((Get-Date) -lt $deadline) {
        if ($gotDone -and $null -ne $doneAt -and ((Get-Date) - $doneAt).TotalMilliseconds -gt 400) {
            break
        }
        $page = Invoke-RestMethod -Uri "$url/api/events?afterId=$cursor&limit=500" -Method GET
        $items = @($page.items)
        if ($items.Count -eq 0) {
            if ($gotDone) { break }
            Start-Sleep -Milliseconds 250
            continue
        }
        $cursor = [long]$items[-1].id
        Add-MatchingEvents $collected $items
        if (@($items | Where-Object { $_.kind -eq "debug.run-steps.done" }).Count -gt 0) {
            $gotDone = $true
            $doneAt = Get-Date
        }
        Start-Sleep -Milliseconds 80
    }

    if ($extraWaitSec -gt 0) {
        Start-Sleep -Seconds $extraWaitSec
    }

    if ($refreshStatus) {
        Invoke-RestMethod -Uri "$url/api/debug/board-stats" -Method POST -ContentType "application/json" -Body "{}" | Out-Null
        Invoke-RestMethod -Uri "$url/api/debug/status" -Method POST -ContentType "application/json" -Body "{}" | Out-Null
    }
    if (-not [string]::IsNullOrWhiteSpace($actorDerivedPtr)) {
        $enc = [uri]::EscapeDataString($actorDerivedPtr)
        Invoke-RestMethod -Uri "$url/api/debug/actor-derived?ptr=$enc" -Method GET | Out-Null
    }

    $tailDeadline = (Get-Date).AddSeconds(4)
    while ((Get-Date) -lt $tailDeadline) {
        $page = Invoke-RestMethod -Uri "$url/api/events?afterId=$cursor&limit=500" -Method GET
        $items = @($page.items)
        if ($items.Count -eq 0) {
            Start-Sleep -Milliseconds 250
            continue
        }
        $cursor = [long]$items[-1].id
        Add-MatchingEvents $collected $items
        Start-Sleep -Milliseconds 120
    }

    $out = [pscustomobject]@{
        Events  = @($collected.ToArray())
        GotDone = [bool]$gotDone
        Cursor  = [int64]$cursor
    }
    return $out
}

function Get-FirstPayload($events, [string]$kind) {
    $matches = @($events | Where-Object { $_.kind -eq $kind })
    if ($matches.Count -eq 0) { return $null }
    return Get-Payload $matches[0]
}

function Get-StatusAfterSynthetic($events) {
    $synths = @($events | Where-Object { $_.kind -eq "debug.effect.synthetic" })
    if ($synths.Count -eq 0) { return $null }
    $synId = [long]$synths[0].id
    foreach ($ev in @($events | Where-Object { $_.kind -eq "debug.status" })) {
        if ([long]$ev.id -gt $synId) { return Get-Payload $ev }
    }
    return $null
}

function Get-LastPayload($events, [string]$kind) {
    $matches = @($events | Where-Object { $_.kind -eq $kind })
    if ($matches.Count -eq 0) { return $null }
    return Get-Payload $matches[-1]
}

function Get-PlantFromBoard($board) {
    if ($null -eq $board -or $null -eq $board.plants) { return $null }
    foreach ($pl in @($board.plants)) {
        if ([int]$pl.col -eq 2 -and [int]$pl.row -eq 2) { return $pl }
    }
    $arr = @($board.plants)
    if ($arr.Count -gt 0) { return $arr[0] }
    return $null
}

function Get-ZombiePtrs($board) {
    if ($null -eq $board -or $null -eq $board.zombies) { return @() }
    return @($board.zombies | ForEach-Object { [string]$_.ptr })
}

function Get-Instances($statusSnap) {
    if ($null -eq $statusSnap) { return @() }
    return @($statusSnap.instances)
}

function Get-Resisted($events, $statusSnap) {
    $fromSnap = @()
    if ($null -ne $statusSnap -and $null -ne $statusSnap.resisted) {
        $fromSnap = @($statusSnap.resisted)
    }
    $fromEv = @($events | Where-Object { $_.kind -eq "debug.status.resisted" } | ForEach-Object { Get-Payload $_ })
    return @($fromSnap + $fromEv)
}

function Find-InstanceForBoard($instances, [string]$statusId, $zombiePtrs, [string]$preferHost) {
    $matches = @()
    foreach ($i in @($instances)) {
        if ("$($i.statusId)" -ne $statusId) { continue }
        $onBoard = $false
        foreach ($z in @($zombiePtrs)) {
            if (Test-EqPtr ([string]$i.hostPtr) $z) { $onBoard = $true; break }
        }
        if ($onBoard) { $matches += $i }
    }
    if ($matches.Count -eq 0) { return $null }
    if (-not [string]::IsNullOrWhiteSpace($preferHost)) {
        foreach ($i in $matches) {
            if (Test-EqPtr ([string]$i.hostPtr) $preferHost) { return $i }
        }
    }
    return $matches[-1]
}

function Test-PlantOnBoard($board, [string]$ptr) {
    if ($null -eq $board -or $null -eq $board.plants) { return $false }
    foreach ($pl in @($board.plants)) {
        if (Test-EqPtr ([string]$pl.ptr) $ptr) { return $true }
    }
    return $false
}

function Test-ApplyAssert($events, [string]$statusId, [bool]$requireSyntheticActions) {
    $board = Get-FirstPayload $events "debug.board-stats"
    if ($null -eq $board) { $board = Get-LastPayload $events "debug.board-stats" }
    $status = Get-StatusAfterSynthetic $events
    if ($null -eq $status) { $status = Get-LastPayload $events "debug.status" }
    $synths = @($events | Where-Object { $_.kind -eq "debug.effect.synthetic" })
    $synth = $null
    if ($synths.Count -gt 0) { $synth = Get-Payload $synths[0] }
    $plant = Get-PlantFromBoard $board
    if ($null -eq $plant) { return @{ pass = $false; note = "no plant in debug.board-stats" } }
    $plantPtr = [string]$plant.ptr
    $zPtrs = Get-ZombiePtrs $board
    if ($zPtrs.Count -eq 0) { return @{ pass = $false; note = "no zombies in debug.board-stats" } }

    if ($null -eq $synth) { return @{ pass = $false; note = "no debug.effect.synthetic" } }
    $actorPtr = [string]$synth.actorPtr
    $targetPtr = [string]$synth.targetPtr
    if (-not (Test-EqPtr $actorPtr $plantPtr)) {
        return @{ pass = $false; note = "synthetic.actorPtr=$actorPtr plant=$plantPtr" }
    }
    $targetIsZombie = $false
    foreach ($z in $zPtrs) {
        if (Test-EqPtr $targetPtr $z) { $targetIsZombie = $true; break }
    }
    if (-not $targetIsZombie) {
        return @{ pass = $false; note = "synthetic.targetPtr=$targetPtr not a board zombie" }
    }
    if (Test-EqPtr $actorPtr $targetPtr) {
        return @{ pass = $false; note = "actorPtr==targetPtr ($actorPtr)" }
    }
    if ($requireSyntheticActions -and [int]$synth.actions -le 0) {
        return @{ pass = $false; note = "synthetic.actions=$($synth.actions) expected >0" }
    }

    $inst = Find-InstanceForBoard (Get-Instances $status) $statusId $zPtrs $targetPtr
    if ($null -eq $inst) {
        $ids = @((Get-Instances $status) | ForEach-Object { "$($_.statusId):$($_.hostPtr)" }) -join ","
        return @{ pass = $false; note = "no instance statusId=$statusId on board zombies (have [$ids])" }
    }
    $atk = [string]$inst.attackerPtr
    $hostPtr = [string]$inst.hostPtr
    if (Test-EqPtr $atk $hostPtr) {
        return @{ pass = $false; note = "attackerPtr==hostPtr ($atk)" }
    }
    $atkOk = (Test-EqPtr $atk $actorPtr) -or (Test-PlantOnBoard $board $atk)
    if (-not $atkOk) {
        return @{ pass = $false; note = "instance.attackerPtr=$atk not plant actor $actorPtr or board plant" }
    }
    return @{
        pass = $true
        note = "statusId=$statusId attacker=$atk host=$hostPtr synth.actions=$($synth.actions)"
    }
}

function Test-ContagionAssert($events, [string]$statusId, [int]$minHosts, [int]$seedRow, [int]$controlRow) {
    $apply = Test-ApplyAssert $events $statusId $false
    if (-not $apply.pass) { return $apply }
    $board = Get-LastPayload $events "debug.board-stats"
    $status = Get-LastPayload $events "debug.status"
    $instances = @(Get-Instances $status | Where-Object { "$($_.statusId)" -eq $statusId })
    $hosts = @($instances | ForEach-Object { $_.hostPtr } | Select-Object -Unique)
    if ($hosts.Count -lt $minHosts) {
        return @{ pass = $false; note = "contagion hosts=$($hosts.Count) want>=$minHosts statusId=$statusId" }
    }
    if ($seedRow -lt 0) {
        return @{ pass = $true; note = "hosts=$($hosts.Count) statusId=$statusId" }
    }
    if ($null -eq $board -or $null -eq $board.zombies) {
        return @{ pass = $true; note = "hosts=$($hosts.Count) (no row check)" }
    }
    $seedCount = 0
    $controlCount = 0
    foreach ($z in @($board.zombies)) {
        $hit = $false
        foreach ($h in $hosts) {
            if (Test-EqPtr $h ([string]$z.ptr)) { $hit = $true; break }
        }
        if (-not $hit) { continue }
        $row = [int]$z.row
        if ($row -eq $seedRow) { $seedCount++ }
        if ($controlRow -ge 0 -and $row -eq $controlRow) { $controlCount++ }
    }
    if ($controlRow -ge 0 -and $controlCount -gt 0) {
        return @{ pass = $false; note = "control row $controlRow has $controlCount $statusId host(s)" }
    }
    if ($seedCount -lt $minHosts) {
        return @{ pass = $false; note = "seed row $seedRow hosts=$seedCount want>=$minHosts" }
    }
    return @{ pass = $true; note = "row$seedRow hosts=$seedCount control$row$controlRow=$controlCount" }
}

function Test-ResistAssert($events, [string]$statusId, [string]$reason) {
    $board = Get-LastPayload $events "debug.board-stats"
    $status = Get-LastPayload $events "debug.status"
    $zPtrs = Get-ZombiePtrs $board
    $inst = Find-InstanceForBoard (Get-Instances $status) $statusId $zPtrs ""
    $resisted = @()
    foreach ($ev in @(Get-Resisted $events $status)) {
        if ("$($ev.statusId)" -ne $statusId) { continue }
        if ("$($ev.reason)" -ne $reason) { continue }
        if ($zPtrs.Count -eq 0) { $resisted += $ev; continue }
        foreach ($z in $zPtrs) {
            if (Test-EqPtr ([string]$ev.hostPtr) $z) { $resisted += $ev; break }
        }
    }
    if ($resisted.Count -eq 0) {
        $reasons = @(Get-Resisted $events $status | ForEach-Object { "$($_.statusId):$($_.reason)" }) -join ","
        return @{ pass = $false; note = "no resisted $statusId/$reason on board (have [$reasons])" }
    }
    if ($null -ne $inst) {
        return @{ pass = $false; note = "instance present for resisted $statusId host=$($inst.hostPtr)" }
    }
    return @{ pass = $true; note = "resisted $statusId reason=$reason n=$($resisted.Count)" }
}

function Test-ResistContagionAssert($events) {
    $board = Get-LastPayload $events "debug.board-stats"
    $status = Get-LastPayload $events "debug.status"
    $zPtrs = Get-ZombiePtrs $board
    $instances = @()
    foreach ($i in @(Get-Instances $status)) {
        if ("$($i.statusId)" -ne "blight") { continue }
        foreach ($z in $zPtrs) {
            if (Test-EqPtr ([string]$i.hostPtr) $z) { $instances += $i; break }
        }
    }
    $resisted = @()
    foreach ($ev in @(Get-Resisted $events $status)) {
        if ("$($ev.statusId)" -ne "blight") { continue }
        if ("$($ev.reason)" -ne "PotencyFloor") { continue }
        $resisted += $ev
    }
    if ($instances.Count -lt 1) {
        return @{ pass = $false; note = "seed blight missing after pulse (instances=0 resisted=$($resisted.Count))" }
    }
    if ($resisted.Count -lt 1) {
        return @{ pass = $false; note = "iron-contagion neighbor did not resist (blight hosts=$($instances.Count))" }
    }
    return @{ pass = $true; note = "seed blight=$($instances.Count) resisted=$($resisted.Count)" }
}

function Test-BondAssert($events) {
    $apply = Test-ApplyAssert $events "bond" $false
    if (-not $apply.pass) { return $apply }
    $synths = @($events | Where-Object { $_.kind -eq "debug.effect.synthetic" })
    $packets = @($events | Where-Object { $_.kind -eq "debug.combat.packet" })
    $burst = $false
    foreach ($ev in $packets) {
        $p = Get-Payload $ev
        if ($null -eq $p) { continue }
        if ([int]$p.fa10 -gt 0) { $burst = $true; break }
    }
    if ($synths.Count -lt 5) {
        return @{ pass = $false; note = "bond instance ok but synthetic hits=$($synths.Count) want>=5" }
    }
    $burstNote = if ($burst) { "fa10 packet" } else { "5 synthetics (burst flushes via Funnel)" }
    return @{ pass = $true; note = "bond instance + synthetics=$($synths.Count) $burstNote" }
}

function Test-ActorDerivedAssert($events) {
    $apply = Test-ApplyAssert $events "wither" $false
    if (-not $apply.pass) { return $apply }
    $derived = @($events | Where-Object { $_.kind -eq "debug.actor-derived" } | ForEach-Object { Get-Payload $_ })
    if ($derived.Count -eq 0) {
        return @{ pass = $false; note = "no debug.actor-derived" }
    }
    $plant = Get-PlantFromBoard (Get-LastPayload $events "debug.board-stats")
    $plantPtr = [string]$plant.ptr
    $matched = $false
    $power = $null
    foreach ($d in $derived) {
        if (-not (Test-EqPtr ([string]$d.ptr) $plantPtr)) { continue }
        $ch = $d.channels
        if ($null -eq $ch) { continue }
        if ($ch.'status.power.omni') { $power = [double]$ch.'status.power.omni' }
        elseif ($ch.status -and $ch.status.power -and $ch.status.power.omni) {
            $power = [double]$ch.status.power.omni
        }
        if ($null -ne $power -and $power -ge 100) { $matched = $true; break }
    }
    if (-not $matched) {
        return @{ pass = $false; note = "plant ptr=$plantPtr caster pin not seen (derived n=$($derived.Count) power=$power)" }
    }
    return @{ pass = $true; note = "actor-derived plant=$plantPtr status.power.omni=$power" }
}

function Test-SnapshotAssert($events) {
    $apply = Test-ApplyAssert $events "wither" $false
    if (-not $apply.pass) { return $apply }
    $status = Get-LastPayload $events "debug.status"
    if ($null -eq $status) { return @{ pass = $false; note = "no debug.status snapshot" } }
    $hasResisted = $null -ne $status.resisted
    if (-not $hasResisted) {
        return @{ pass = $false; note = "debug.status missing resisted[]" }
    }
    return @{ pass = $true; note = "snapshot instances=$($status.count) resistedCount=$($status.resistedCount)" }
}

function Test-UnityBypassAssert($events, [string]$statusName, [bool]$method, [bool]$clear) {
    if ($clear) {
        $ev = Get-LastPayload $events "debug.status.cleared"
        if ($null -eq $ev) { return @{ pass = $false; note = "no debug.status.cleared" } }
        return @{ pass = $true; note = "cleared count=$($ev.count)" }
    }
    $ev = Get-LastPayload $events "debug.status.applied"
    if ($null -eq $ev) { return @{ pass = $false; note = "no debug.status.applied" } }
    $gotMethod = [bool]$ev.method
    $gotStatus = [string]$ev.status
    if ($gotStatus -ne $statusName) {
        return @{ pass = $false; note = "applied status=$gotStatus want=$statusName" }
    }
    if ($gotMethod -ne $method) {
        return @{ pass = $false; note = "applied method=$gotMethod want=$method" }
    }
    return @{ pass = $true; note = "applied status=$gotStatus method=$gotMethod count=$($ev.count)" }
}

$l2Scenarios = @(
    @{ id = "status-l2-wither"; waitSec = 1; kind = "apply"; statusId = "wither"; cc = $false }
    @{ id = "status-l2-snapshot"; waitSec = 0.5; kind = "snapshot"; statusId = "wither"; cc = $false }
    @{ id = "status-l2-resist"; waitSec = 1; kind = "resist"; statusId = "wither"; reason = "PotencyFloor" }
    @{ id = "status-l2-leech"; waitSec = 1; kind = "apply"; statusId = "leech"; cc = $false }
    @{ id = "status-l2-rally"; waitSec = 1; kind = "apply"; statusId = "rally"; cc = $false }
    @{ id = "status-l2-expose"; waitSec = 1; kind = "apply"; statusId = "expose"; cc = $false }
    @{ id = "status-l2-command"; waitSec = 1; kind = "apply"; statusId = "command"; cc = $false }
    @{ id = "status-l2-shatter"; waitSec = 1; kind = "apply"; statusId = "shatter"; cc = $false }
    @{ id = "status-l2-bond"; waitSec = 1.5; kind = "bond"; statusId = "bond"; cc = $false }
    @{ id = "status-l2-blight-row"; waitSec = 2; kind = "contagion-row"; statusId = "blight"; minHosts = 2; seedRow = 2; controlRow = 3 }
    @{ id = "status-l2-rot"; waitSec = 2; kind = "contagion"; statusId = "rot"; minHosts = 2 }
    @{ id = "status-l2-spark"; waitSec = 2; kind = "contagion"; statusId = "spark"; minHosts = 2 }
    @{ id = "status-l2-pact-mark"; waitSec = 2; kind = "contagion"; statusId = "pact_mark"; minHosts = 1 }
    @{ id = "status-l2-spore"; waitSec = 2; kind = "contagion"; statusId = "spore"; minHosts = 2 }
    @{ id = "status-l2-butter"; waitSec = 1; kind = "apply"; statusId = "butter"; cc = $true }
    @{ id = "status-l2-freeze"; waitSec = 1; kind = "apply"; statusId = "freeze"; cc = $true }
    @{ id = "status-l2-cold"; waitSec = 1; kind = "apply"; statusId = "cold"; cc = $true }
    @{ id = "status-l2-poison"; waitSec = 1; kind = "apply"; statusId = "poison"; cc = $true }
    @{ id = "status-l2-hypno"; waitSec = 1; kind = "apply"; statusId = "hypno"; cc = $true }
    @{ id = "status-l2-ember"; waitSec = 1; kind = "apply"; statusId = "ember"; cc = $true }
    @{ id = "status-l2-jala"; waitSec = 1; kind = "apply"; statusId = "jala"; cc = $true }
    @{ id = "status-l2-kelp"; waitSec = 1; kind = "apply"; statusId = "kelp"; cc = $true }
    @{ id = "status-l2-charm-pulse"; waitSec = 1; kind = "apply"; statusId = "charm_pulse"; cc = $true }
    @{ id = "status-l2-resist-cc"; waitSec = 1; kind = "resist"; statusId = "butter"; reason = "PotencyFloor" }
    @{ id = "status-l2-resist-contagion"; waitSec = 2; kind = "resist-contagion"; statusId = "blight" }
    @{ id = "status-l2-poison-immune"; waitSec = 1; kind = "resist"; statusId = "poison"; reason = "Immunity" }
    @{ id = "status-l2-actor-derived"; waitSec = 1; kind = "actor-derived"; statusId = "wither" }
)

$unityScenarios = @(
    @{ id = "status-butter"; waitSec = 1; kind = "unity"; statusName = "butter"; method = $true; clear = $false }
    @{ id = "status-freeze"; waitSec = 1; kind = "unity"; statusName = "freeze"; method = $true; clear = $false }
    @{ id = "status-cold"; waitSec = 1; kind = "unity"; statusName = "cold"; method = $true; clear = $false }
    @{ id = "status-poison"; waitSec = 1; kind = "unity"; statusName = "poison"; method = $true; clear = $false }
    @{ id = "status-float-butter"; waitSec = 1; kind = "unity"; statusName = "butter"; method = $false; clear = $false }
    @{ id = "status-clear"; waitSec = 1; kind = "unity"; statusName = ""; method = $false; clear = $true }
)

function Invoke-ScenarioRow($row, [string]$url) {
    $id = [string]$row.id
    $afterId = Get-MaxEventId $url
    try {
        $queued = Invoke-RestMethod -Uri "$url/api/debug/scenario/$id" -Method POST -ContentType "application/json" -Body "{}"
        if (-not [bool]$queued.ok) {
            return @{ id = $id; pass = $false; note = "scenario queue failed" }
        }
    }
    catch {
        return @{ id = $id; pass = $false; note = $_.Exception.Message }
    }

    $needDerived = $row.kind -eq "actor-derived"
    $plantPtrHint = ""
    $bundle = Wait-AndCollect $url $afterId 20 ([double]$row.waitSec) $true $plantPtrHint
    if (-not $bundle.gotDone) {
        return @{ id = $id; pass = $false; note = "no debug.run-steps.done afterId=$afterId" }
    }

    $events = @($bundle.Events)
    if ($needDerived) {
        $board = Get-LastPayload $events "debug.board-stats"
        $plant = Get-PlantFromBoard $board
        if ($null -ne $plant) {
            $enc = [uri]::EscapeDataString([string]$plant.ptr)
            Invoke-RestMethod -Uri "$url/api/debug/actor-derived?ptr=$enc" -Method GET | Out-Null
            $cursor = [long]$bundle.cursor
            $tailDeadline = (Get-Date).AddSeconds(3)
            $extra = New-Object System.Collections.Generic.List[object]
            while ((Get-Date) -lt $tailDeadline) {
                $page = Invoke-RestMethod -Uri "$url/api/events?afterId=$cursor&limit=500" -Method GET
                $items = @($page.items)
                if ($items.Count -eq 0) {
                    Start-Sleep -Milliseconds 250
                    continue
                }
                $cursor = [long]$items[-1].id
                Add-MatchingEvents $extra $items
                Start-Sleep -Milliseconds 120
            }
            $events = @($events + @($extra.ToArray()))
        }
    }
    switch ([string]$row.kind) {
        "apply" { $r = Test-ApplyAssert $events ([string]$row.statusId) ([bool]$row.cc) }
        "snapshot" { $r = Test-SnapshotAssert $events }
        "resist" { $r = Test-ResistAssert $events ([string]$row.statusId) ([string]$row.reason) }
        "resist-contagion" { $r = Test-ResistContagionAssert $events }
        "contagion-row" { $r = Test-ContagionAssert $events ([string]$row.statusId) ([int]$row.minHosts) ([int]$row.seedRow) ([int]$row.controlRow) }
        "contagion" { $r = Test-ContagionAssert $events ([string]$row.statusId) ([int]$row.minHosts) ([int]-1) ([int]-1) }
        "bond" { $r = Test-BondAssert $events }
        "actor-derived" { $r = Test-ActorDerivedAssert $events }
        "unity" { $r = Test-UnityBypassAssert $events ([string]$row.statusName) ([bool]$row.method) ([bool]$row.clear) }
        default { $r = @{ pass = $false; note = "unknown kind $($row.kind)" } }
    }
    return @{ id = $id; pass = [bool]$r.pass; note = [string]$r.note }
}

$results = @()
$failed = $false

Write-Host "==> StatusRuntime L2 prove against $BaseUrl"

try {
    $health = Invoke-RestMethod -Uri "$BaseUrl/health" -Method GET
}
catch {
    Write-Step "health" $false $_.Exception.Message | Out-Null
    $payload = [ordered]@{
        at      = (Get-Date).ToString("o")
        baseUrl = $BaseUrl
        passed  = 0
        total   = 0
        results = @()
        status  = "FAIL"
        note    = "health request failed: $($_.Exception.Message)"
    }
    $dir = Split-Path -Parent $OutJson
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    ($payload | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $OutJson -Encoding UTF8
    exit 1
}

$connected = [bool]$health.injectorConnected
$simOff = -not [bool]$health.simEnabled
$source = [string]$health.source
$okHealth = $connected -and $simOff
if (-not (Write-Step "health" $okHealth "injectorConnected=$connected simEnabled=$($health.simEnabled) source=$source")) {
    $payload = [ordered]@{
        at      = (Get-Date).ToString("o")
        baseUrl = $BaseUrl
        passed  = 0
        total   = 0
        results = @()
        status  = "FAIL"
        note    = "need injectorConnected=true simEnabled=false (got connected=$connected sim=$($health.simEnabled) source=$source)"
    }
    $dir = Split-Path -Parent $OutJson
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    ($payload | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $OutJson -Encoding UTF8
    exit 1
}

try { Invoke-RestMethod -Uri "$BaseUrl/api/debug/session/end" -Method POST -ContentType "application/json" -Body "{}" | Out-Null } catch { }
Invoke-RestMethod -Uri "$BaseUrl/api/debug/session/start" -Method POST -ContentType "application/json" -Body "{}" | Out-Null

$rows = @($l2Scenarios)
if ($IncludeUnityBypass) {
    $rows = @($unityScenarios) + $rows
}

foreach ($row in $rows) {
    $r = Invoke-ScenarioRow $row $BaseUrl
    Write-Step $r.id ([bool]$r.pass) $r.note | Out-Null
    $results += $r
    if (-not $r.pass) { $failed = $true }
}

$passed = @($results | Where-Object { $_.pass }).Count
$payload = [ordered]@{
    at      = (Get-Date).ToString("o")
    baseUrl = $BaseUrl
    passed  = $passed
    total   = $results.Count
    results = $results
    status  = if ($failed) { "FAIL" } else { "PASS" }
    note    = "StatusRuntime L2 prove. Poll via Get-MaxEventId + debug.run-steps.done. IncludeUnityBypass=$IncludeUnityBypass"
}
$dir = Split-Path -Parent $OutJson
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
($payload | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $OutJson -Encoding UTF8
Write-Host ("Wrote {0} ({1}/{2})" -f $OutJson, $passed, $results.Count)

if ($failed) { exit 1 }
exit 0
