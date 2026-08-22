# Spec: status-payload-completion (E17)

Module **E17** in the [atom effect map](../effect-atom-map.md). Depends on **E11**. Off the critical path.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

> **Cross-stream.** This is status-system and injector work inside an effect-layer program, and `StatusCatalog` is ADR-locked code-first. It needs the **status stream's agreement**, not just ours.

## Objective

Make the status catalog tell the truth. Eight of twenty-one statuses are declared and do nothing; this module builds their payloads so the catalog's 21 becomes 21 functional, and the family library can author all of them.

## Design (locked on approval)

### What is broken, precisely

| Status | Declares | Reality |
|---|---|---|
| `ember`, `jala`, `kelp` | `UnityCc` | **No Unity branch in *our* code** — `ApplyStatusToZombie` handles only butter, freeze, cold, poison, hypno. The game methods **do** exist (verified below); we simply never wired them |
| `charm_pulse` | `UnityCc` | **No vanilla method exists at all** — a def error, not missing wiring |
| `rally`, `expose`, `command`, `shatter` | `ModifyStat` | **`StatusPayloadKind.ModifyStat` has zero consumers repo-wide.** They create instances and play VFX and change no stat |
| `leech` | dual pulse | only the **damage half** is implemented; the "heal the attacker" half was never built |

`status-ssot.md` claims `SetEmbered` / `SetJalaed` / `SetKelped` exist. **They do** — assembly metadata confirms all three. The doc was right; the code never caught up.

### The three pieces of work

**1. Three Unity CC branches — the methods exist (verified 2026-08-22).**

Assembly metadata inspection of `BepInEx/interop/Assembly-CSharp.dll` found `SetEmbered`, `SetJalaed`, and `SetKelped` present, alongside the already-wired `SetFreeze`. So `status-ssot.md` was **right** and the earlier "no such method" reading was about *our* code never wiring them, not about the game lacking them. This is straightforward wiring, not research.

Add cases to `ApplyStatusToZombie` for `ember`, `jala`, `kelp`. Signatures still need confirming against the interop types, and one LIVE pass should confirm each does something visible — a method that exists but no-ops on a day lawn is still a dead status.

**`charm_pulse` is different: no vanilla method exists.** The same search found no `SetCharm*`; only `SetZombieWithMindControl` / `SetZombieMindControlledNode`. So `charm_pulse` declaring a `UnityCc` payload is a **def error**, not missing wiring. Fix the def — it is an overlay-authored status and should carry an overlay payload — rather than inventing a Unity path for it. Do **not** fake it with a float write; that is the `applyFloatSlow` path and it is documented as weak and VFX-less.

**2. A `ModifyStat` payload consumer.** A status instance carrying a `stat` overlay contributes timed modifiers while active, and withdraws them on expiry. Two constraints:

- The `stat` overlay key **has no parser today** and is **not in the FA10 allowlist** — yet it is documented in `status-ssot.md` and used in a shipped example (`examples/status/blight-row.overlay.json`) that would fail validation. This module adds the parser and the allowlist entry, and fixes the example.
- Timed modifiers are a **source-tagged bag entry**, withdrawn on expiry — never a direct write. Same law as everything else.

**3. `leech`'s heal half.** The attacker gains what the host loses. It must route **Funnel → FA10**, never a direct heal, so the shield gate and the merge semantics apply to both halves.

### The consistency fix that comes with it

`poison` is incoherent across three subsystems: category `dot`, family `elemental`, kind `UnityCc`. It resists on the **DoT** channel, CC-locks in battle (the check tests `Kind`), and **never pulses**. Whatever the status stream decides, decide it here rather than carrying a status that three subsystems disagree about into authored content.

Two other facts the catalog should stop implying: only the **`elemental`** family mutex is implemented — every other "family" is a label with no runtime behaviour — and `StatusDef.Tags` is **unconditionally empty**, so immunity tags arrive per-grant, not from the def.

### Battle stays out of scope

Once applied in battle, `rally`/`expose`/`command`/`shatter` would still be inert, because battle consumes one opcode and never calls `OnEvent`. E17 fixes the **lawn**. Battle reachability is the action/enrichment program's work.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Status"
# LIVE, operator-run: the three CC scenarios
# status-l2-ember | status-l2-jala | status-l2-kelp
```

## Structure

```
src/FusionRpg.Injector/DebugActions.cs                    (3 Unity CC branches)
src/FusionRpg.Core/Status/StatusEffectBridge.cs           (`stat` overlay parser; leech heal half)
src/FusionRpg.Core/Effects/EffectProcAndOwner.cs          (`stat` joins the FA10 allowlist)
src/FusionRpg.Core/Status/StatusRuntime.cs                (ModifyStat payload lifecycle)
docs/architecture/examples/status/blight-row.overlay.json (fix the invalid example)
tests/FusionRpg.Core.Tests/Status/ModifyStatPayloadTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| Apply `ember` / `jala` / `kelp` | `SetEmbered` / `SetJalaed` / `SetKelped` called; LIVE pass confirms a visible effect |
| `charm_pulse` | payload kind corrected off `UnityCc`; no Unity path invented |
| `rally` with a `stat` overlay | modifier present while active, **withdrawn on expiry** |
| `expose` expiring early via `ClearStatus` | modifier withdrawn |
| Two `rally` instances on one actor | stacking follows the def's policy, not accidental double-add |
| `leech` tick | host loses X, attacker gains X, **both through the Funnel** |
| `leech` heal at full HP | clamps, no overheal |
| `blight-row.overlay.json` | validates |
| `stat` key on FA10 | accepted; unknown keys still rejected |
| The 25 status fixtures | unchanged |
| `poison` | one coherent answer across category, family, and kind |

## Boundaries

**Always:** confirm interop signatures before wiring; route heals through the Funnel; withdraw timed modifiers by source tag on expiry; probe LIVE before claiming a Unity method exists.

**Ask first:** changing any status def's category, family, or kind — including `poison`.

**Never:** fake a missing Unity CC with a float write; write a timed modifier directly instead of through the bag; author a family for a status whose payload is still absent; claim battle reachability this module does not deliver.
