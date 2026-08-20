# ICD audit — current behavior vs common game patterns

Date: **2026-08-19**. Audit pass for StatusRuntime L2/L2b.

Scope:

- Current **status ICD** behavior in code
- How many buff / debuff / CC instances one actor can hold
- Whether statuses share a stack or track independently
- How to calculate the next legal re-apply time
- How this design compares to common patterns in WoW, League of Legends, and Genshin Impact

## Short answer

- **Status ICD is per `statusId` on one host ptr.**
- **There is no global buff/debuff/CC cap** in `StatusRuntime`.
- Statuses are **individual instances**, not one shared stack.
- The main cross-status rule is **elemental family mutex**: `freeze`, `cold`, `poison`, and `jala` evict each other.
- Next legal re-apply time is **`LastApplied + status_icd_ms`**.

## Current ICD flow

`StatusRuntime.Apply()` checks status ICD before resistance, family mutex, or instance upsert:

```113:117:src/FusionRpg.Core/Status/StatusRuntime.cs
        if (IsStatusIcdBlocked(input, now, out _))
        {
            return new StatusApplyOutcome(false, StatusResistReason.StatusIcd, null,
                new StatusApplyResult(false, StatusResistReason.StatusIcd, 0, 0, 0, 0, 0, 0, 0));
        }
```

The block rule is simple:

```225:234:src/FusionRpg.Core/Status/StatusRuntime.cs
    bool IsStatusIcdBlocked(StatusApplyInput input, DateTimeOffset now, out StatusInstance? existing)
    {
        existing = null;
        if (input.StatusIcdMs <= 0 || !_byHost.TryGetValue(input.HostPtr, out var list))
            return false;
        existing = list.FirstOrDefault(i =>
            string.Equals(i.StatusId, input.StatusId, StringComparison.OrdinalIgnoreCase));
        if (existing == null) return false;
        return (now - existing.LastApplied).TotalMilliseconds < input.StatusIcdMs;
    }
```

Meaning:

- If `status_icd_ms <= 0`, there is **no status ICD gate**.
- The lookup is by **host ptr** and **same `statusId`**.
- If an instance with that `statusId` already exists and `now - LastApplied < StatusIcdMs`, the apply is rejected with `StatusResistReason.StatusIcd`.
- This is a **binary time gate**. It does not reduce duration, potency, or chance. It only blocks or allows.

The overlay key comes from `StatusEffectBridge.BuildApplyInput()`:

```179:182:src/FusionRpg.Core/Status/StatusEffectBridge.cs
        var statusIcd = JsonOverlay.GetInt(overlay, "status_icd_ms", 0);
        if (statusIcd <= 0)
            statusIcd = JsonOverlay.GetInt(overlay, "statusIcdMs", 0);
```

The architecture doc also locks three separate clocks:

```49:55:docs/architecture/status-ssot.md
**Three ICD clocks (never merge):**

| Clock | Layer | Question |
|---|---|---|
| Grant `icd_ms` | L1 `EffectProcPolicy` | May this *listener* try Apply/Refresh again? |
| Status `icd_ms` | L2 instance / family | May this *status* be re-applied on this ptr? |
| `periodMs` | L2 | Pulse cadence — **not** ICD |
```

That separation matters. Grant ICD throttles the listener. Status ICD throttles re-apply on the host. `periodMs` only drives pulses after the status is already active.

## How to calculate next apply time

On successful apply, the runtime snapshots `LastApplied = now`:

```163:176:src/FusionRpg.Core/Status/StatusRuntime.cs
            AppliedAt = now,
            ExpiresAt = durationMs > 0 ? now.AddMilliseconds(durationMs) : DateTimeOffset.MaxValue,
            PeriodMs = input.PeriodMs,
            TickBudget = input.TickBudget > 0 ? input.TickBudget : 1,
            StatusIcdMs = input.StatusIcdMs,
            SpreadChance = input.SpreadChance,
            SpreadStatusId = input.SpreadStatusId,
            SpreadMaxHops = input.SpreadMaxHops,
            SpreadIcdMs = input.SpreadIcdMs,
            SpreadTarget = input.SpreadTarget,
            HopDepth = input.HopDepth,
            NextPulse = input.PeriodMs > 0 ? now.AddMilliseconds(input.PeriodMs) : DateTimeOffset.MaxValue,
            LastApplied = now,
            LastSpread = DateTimeOffset.MinValue
```

So the next legal re-apply time is:

```text
nextAllowedAt = existing.LastApplied + TimeSpan.FromMilliseconds(existing.StatusIcdMs)
remainingMs = max(0, existing.StatusIcdMs - (now - existing.LastApplied).TotalMilliseconds)
```

Practical notes:

- The clock resets on every **successful** apply or refresh that writes a new instance.
- A resisted apply does not update `LastApplied`.
- `ExpiresAt` is separate. A status can expire before the ICD window matters again, or the ICD can be shorter than the remaining duration.

## Stacking behavior

There are three stacking modes:

```18:23:src/FusionRpg.Core/Status/ResistanceEvaluator.cs
public enum StatusStacking
{
    Refresh,
    Replace,
    Coexist
}
```

The actual upsert logic is:

```183:212:src/FusionRpg.Core/Status/StatusRuntime.cs
    void UpsertInstance(string hostPtr, StatusInstance instance, StatusStacking stacking)
    {
        if (!_byHost.TryGetValue(hostPtr, out var list))
        {
            list = new List<StatusInstance>();
            _byHost[hostPtr] = list;
        }

        if (stacking == StatusStacking.Refresh)
        {
            var idx = list.FindIndex(i =>
                string.Equals(i.StatusId, instance.StatusId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(i.GrantId, instance.GrantId, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                list[idx] = instance;
            else
                list.Add(instance);
            return;
        }

        if (stacking == StatusStacking.Replace)
        {
            list.RemoveAll(i =>
                string.Equals(i.StatusId, instance.StatusId, StringComparison.OrdinalIgnoreCase));
            list.Add(instance);
            return;
        }

        list.Add(instance);
    }
```

Interpretation:

- **Refresh**: one instance per `(hostPtr, statusId, grantId)`. Same grant refreshes itself. Different grants can coexist.
- **Replace**: one instance per `(hostPtr, statusId)`. New apply wipes the old one of the same status id.
- **Coexist**: every successful apply appends another instance.

So statuses are not a shared pool. They are tracked as **individual instances in a per-host list**.

## How many buffs, debuffs, and CC can apply on one actor

`StatusRuntime` stores one `List<StatusInstance>` per host ptr and does not impose a count cap:

```79:80:src/FusionRpg.Core/Status/StatusRuntime.cs
    readonly Dictionary<string, List<StatusInstance>> _byHost = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, int> _counterHits = new(StringComparer.OrdinalIgnoreCase);
```

There is no policy constant for "max active statuses per actor". `StatusPolicy` only defines resist and spread defaults:

```4:20:src/FusionRpg.Core/Status/StatusPolicy.cs
public static class StatusPolicy
{
    public const double CategoryResistCap = 0.95;
    public const double ApplyScaleK = 100.0;
    public const double ApplyScaleFloor = 1.0;
    public const double ResistFromPowerRatio = 0.0;
    public const double MinNetFactor = 0.0;
    public const double MaxNetFactor = 10_000.0;
    public const double ProgressionPowerStubDefault = 1.0;
    public const int ProcDepthLimitDefault = 6;

    public const bool IncludeTierPowerInDelta = true;

    public static double ApplyScaleKForCategory(string category) => ApplyScaleK;
    public static double ApplySteepnessForCategory(string category) => 1.0;
}
```

That means the effective limits are only structural:

- **Replace** statuses cap themselves at one per `statusId`
- **Refresh** statuses cap themselves at one per `(statusId, grantId)`
- **Coexist** statuses have no per-id cap
- **Elemental family mutex** removes sibling elemental statuses

### Practical count by category

With the current 21-id catalog:

- **Buffs**: no global cap. Today only `rally` is `StatusKind.Buff`, and it uses `Refresh`, so one instance per grant.
- **Debuffs**: no global cap. `expose` and `shatter` use `Refresh`, so one instance per grant for each id.
- **CC**: no global cap across all CC ids. But most CC ids use `Replace`, so each CC id is at most one instance on the actor at a time.

An actor can therefore hold multiple CCs at once if they are different ids and not blocked by family mutex. Example: `butter`, `hypno`, `kelp`, and `ember` can coexist. `freeze` and `poison` cannot coexist because both are in `elemental`.

## Family sharing and mutex

The only built-in cross-status eviction rule is elemental family mutex:

```214:223:src/FusionRpg.Core/Status/StatusRuntime.cs
    void ApplyFamilyMutex(string hostPtr, StatusDef def, DateTimeOffset now)
    {
        if (!string.Equals(def.Family, "elemental", StringComparison.OrdinalIgnoreCase))
            return;
        if (!_byHost.TryGetValue(hostPtr, out var list))
            return;
        list.RemoveAll(i =>
            string.Equals(i.Family, "elemental", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(i.StatusId, def.StatusId, StringComparison.OrdinalIgnoreCase));
    }
```

The locked catalog families are:

```202:210:docs/architecture/status-ssot.md
| Family | Members | Rule |
|---|---|---|
| `elemental` | `freeze`, `cold`, `poison`, `jala` | **Replace** within family (Fusion Cryo / Enflamed / Poison mutex) |
| `mixer` | `ember` | **Coexists** with all Unity CC |
| `slow` | `kelp` | Coexists with Cryo; **Replace** on same `kelp` id |
| `overlay` | custom overlay ids below | May coexist with Unity CC; overlay DoT never calls `SetPoison` unless def is `UnityCc` poison |
| `cc` | `butter`, `hypno` | Unity CC; hypno stays zombie bucket ([match-runtime.md](match-runtime.md)) |
```

So the answer to "did they share stack?" is:

- **Mostly no**. Each status is tracked individually.
- **Elemental statuses share a mutex family**, not a pooled stack counter.
- Other families do not currently impose family-wide replacement or diminishing returns.

## Catalog breakdown

The current 21 locked status ids are:

```15:40:src/FusionRpg.Core/Status/StatusCatalogBootstrap.cs
        Register(catalog, "butter", StatusKind.UnityCc, "cc", StatusL2bCategory.Cc, StatusStacking.Replace, StatusPayloadKind.UnityCc);
        Register(catalog, "freeze", StatusKind.UnityCc, "elemental", StatusL2bCategory.Cc, StatusStacking.Replace, StatusPayloadKind.UnityCc);
        Register(catalog, "cold", StatusKind.UnityCc, "elemental", StatusL2bCategory.Cc, StatusStacking.Replace, StatusPayloadKind.UnityCc);
        Register(catalog, "poison", StatusKind.UnityCc, "elemental", StatusL2bCategory.Dot, StatusStacking.Replace, StatusPayloadKind.UnityCc);
        Register(catalog, "hypno", StatusKind.UnityCc, "cc", StatusL2bCategory.Cc, StatusStacking.Replace, StatusPayloadKind.UnityCc);
        Register(catalog, "ember", StatusKind.UnityCc, "mixer", StatusL2bCategory.Cc, StatusStacking.Coexist, StatusPayloadKind.UnityCc);
        Register(catalog, "jala", StatusKind.UnityCc, "elemental", StatusL2bCategory.Cc, StatusStacking.Replace, StatusPayloadKind.UnityCc);
        Register(catalog, "kelp", StatusKind.UnityCc, "slow", StatusL2bCategory.Cc, StatusStacking.Replace, StatusPayloadKind.UnityCc);

        Register(catalog, "wither", StatusKind.OverTime, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.PulseHp);
        Register(catalog, "bond", StatusKind.Counter, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.PulseHp);
        Register(catalog, "rally", StatusKind.Buff, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.ModifyStat);
        Register(catalog, "leech", StatusKind.OverTime, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.PulseHp);
        Register(catalog, "expose", StatusKind.Debuff, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.ModifyStat);
        Register(catalog, "command", StatusKind.Meter, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.ModifyStat);
        Register(catalog, "shatter", StatusKind.Debuff, "overlay", StatusL2bCategory.Dot, StatusStacking.Refresh, StatusPayloadKind.ModifyStat);
        Register(catalog, "charm_pulse", StatusKind.CrowdControl, "overlay", StatusL2bCategory.Cc, StatusStacking.Replace, StatusPayloadKind.UnityCc);

        Register(catalog, "blight", StatusKind.Contagion, "overlay", StatusL2bCategory.Contagion, StatusStacking.Refresh, StatusPayloadKind.Spread, StatusPayloadKind.PulseHp);
        Register(catalog, "rot", StatusKind.Contagion, "overlay", StatusL2bCategory.Contagion, StatusStacking.Refresh, StatusPayloadKind.Spread, StatusPayloadKind.PulseHp);
        Register(catalog, "spark", StatusKind.Contagion, "overlay", StatusL2bCategory.Contagion, StatusStacking.Refresh, StatusPayloadKind.Spread, StatusPayloadKind.PulseHp);
        Register(catalog, "pact_mark", StatusKind.Contagion, "overlay", StatusL2bCategory.Contagion, StatusStacking.Refresh, StatusPayloadKind.Spread, StatusPayloadKind.PulseHp);
        Register(catalog, "spore", StatusKind.Contagion, "overlay", StatusL2bCategory.Contagion, StatusStacking.Refresh, StatusPayloadKind.Spread, StatusPayloadKind.PulseHp);
```

Summary by stacking mode:

- **Replace**: `butter`, `freeze`, `cold`, `poison`, `hypno`, `jala`, `kelp`, `charm_pulse`
- **Refresh**: `wither`, `bond`, `rally`, `leech`, `expose`, `command`, `shatter`, `blight`, `rot`, `spark`, `pact_mark`, `spore`
- **Coexist**: `ember`

## Comparison with common game patterns

### World of Warcraft

Common pattern:

- Hard buff/debuff slot caps historically existed and still influence UI and encounter design
- CCs usually participate in **diminishing return groups**
- Repeated CC in the same DR group lands for reduced duration, then becomes temporarily immune

Compared with this repo:

- Rise of Summoner has **no global slot cap**
- It has **no DR group system**
- Repeat prevention is mostly **same-status time gate** plus normal resist
- Result: repeated CC pressure is easier to sustain if the caller rotates different status ids or waits for per-status ICD

### League of Legends

Common pattern:

- Most control effects are not blocked by a separate per-target ICD
- Counterplay comes from **cooldowns on abilities**, **tenacity**, cleanse, immunity windows, and spell-specific rules
- Repeated hard CC can chain, but usually through different abilities from different sources

Compared with this repo:

- Rise of Summoner is closer to LoL than WoW in that it lacks DR groups
- But it adds an explicit **status re-apply gate on the host** when `status_icd_ms` is set
- Tenacity-style duration reduction is not the main mechanism here; resistance is an apply-time power/resist calculation

### Genshin Impact

Common pattern:

- Elemental application commonly uses a hidden **ICD of about 2.5s or 3 hits**, whichever comes first
- The key question is not only "can I apply again?" but also "does this hit consume the shared elemental application counter?"
- Elemental auras also interact and are consumed by reactions

Compared with this repo:

- Rise of Summoner has **time-based status ICD only**
- There is **no hit-count rule**
- Elemental statuses use **family mutex**, not aura gauge consumption
- There is no reaction math equivalent to "consume aura, trigger new elemental result"

## Audit verdict

### What the current design does well

1. The gate is simple and predictable. Operators can reason about one host, one status id, one timer.
2. The stack model is explicit in code. `Refresh`, `Replace`, and `Coexist` are easy to audit.
3. Elemental mutex prevents conflicting elemental CC overlays from piling up into a confusing state.
4. The split between grant ICD, status ICD, and pulse cadence is clean in the architecture doc and in the implementation.

### Gaps vs common game patterns

1. **No CC diminishing returns**
   Repeated hard control does not shorten or decay over successive applications. WoW-style DR is absent.

2. **No global per-actor status cap**
   There is no backstop against too many concurrent distinct statuses or too many coexist instances.

3. **No hit-count ICD**
   Genshin-style "3 hits or 2.5 seconds" logic is absent. Only elapsed time matters.

4. **No family-wide cooldown buckets beyond elemental mutex**
   Two different CC ids can still chain freely if they are not in the elemental family.

5. **No reaction-style consumption**
   Elemental statuses replace siblings, but they do not combine into a new outcome or consume an aura budget.

## Bottom line

Today the runtime answers the user-facing questions like this:

- **How many buff/debuff can apply on an actor?** No hard cap. Real count depends on stacking mode and family mutex.
- **How many CC can apply on an actor?** No hard cap across distinct ids. Most single CC ids are one-at-a-time because they use `Replace`.
- **Do they share stack?** Usually no. Statuses are individual instances. Elemental ids share a mutex family, not a common stack counter.
- **How to calculate cooldown for next apply time?** `nextAllowedAt = LastApplied + status_icd_ms`.

For current project scope this is coherent and easy to debug. Compared to larger live-service games, it is a leaner model: simple per-status host ICD instead of DR groups, hit-count ICD, aura gauges, or slot caps.
