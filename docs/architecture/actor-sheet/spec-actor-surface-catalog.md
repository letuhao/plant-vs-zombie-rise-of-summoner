# Spec: `actor-surface-catalog`

**Module id:** `actor-surface-catalog` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Ideal:** [actor-sheet-ideal.md](../actor-sheet-ideal.md) § Runtime catalog SSOT ·
**Status:** Draft — pending owner review. **No build authorized until approved.**

**Depends on:** — · **Blocks:** `actor-sheet-shell` and every tab; Actor HUD token lookup (glyph path).

---

## Assumptions

1. **T7.2 holds:** Core never reads a file. Server (`Program.cs`) and Injector (`RpgHost`) parse JSON
   and `Configure(...)`. Tests construct catalog objects inline.
2. **Migration source today is C# + seed lexicon** (`AptitudeCatalog.All`, `StatusCatalogBootstrap`,
   `ElementRoster`, `DerivedStatChannels`, `lexicon.v1.json`). First publish of each `*-catalog.v1.json`
   is a byte-faithful extract; behaviour must not change until a deliberate copy edit.
3. **`ItemRole` stays on the item program** — this module only exposes role ids + display words the
   sheet lists; it does not own `core.v1.json` budget weights.
4. **Commander leftover v1** — Confirm uses existing commander allocate API on every sheet; honest
   scope chip. UniqueDemon allocate POST is later. Plate 13 is the visual SSOT for chrome.
5. **No hot-reload** — restart after `publish.py`.
6. **Eight closed tab kinds** — T5 rejects a ninth `kind` without a React renderer.
7. **`kitRoles` shape** — `{ roleId, labels: { humanoid, plant } }` (not flat humanoid/plant keys).
8. **FE resource roster** — iterate resource-catalog; **delete** the five-string `ResourceId` union
   as a roster; `channelLabel` / adapt reads catalog `displayName` (never `idWords`).

→ Correct these now or this spec proceeds as written.

---

## Objective

Make every player-visible identity for ActorSheet and Band B HUD tokens **load from versioned
runtime catalogs** under `data/tuning/`, inject into Core, and fan out on
`GET /api/catalogs/actor-surface` so the FE never hardcodes roster unions.

**Users:** Server and Injector hosts (load); FE ActorSheet / Phaser / Unity HUD (consume); designers
editing catalogs via `publish.py`.

**Success:** Hosts refuse to start if a catalog is missing or names an unknown kind; FE can render
tabs and StatRows from the fan-in DTO alone; a rename of Crit chance is a catalog bump that does not
move combat goldens.

---

## Tech Stack

| Layer | Choice |
|---|---|
| Config | `data/tuning/*-catalog.v{n}.json`, `actor-sheet.v1.json` — System.Text.Json parse |
| Core | `XxxCatalogHub.Configure` pattern (same as `AptitudeTuningHub` / `ActorHudTuningHub`) |
| Server | `Program.cs` load + new catalog endpoints |
| Injector | `RpgHost` load (plugin folder copy of tuning files) |
| FE | Typed DTO from `/api/catalogs/actor-surface`; presentation libs per map tech stack (lucide/recharts/…) |

---

## Commands

```powershell
# After implement: Core catalog parse/reject tests
dotnet test tests\FusionRpg.Core.Tests --filter ActorSurfaceCatalog

# Guard: Core still has no File.Read for these domains
# (existing guard-dal / review; no new File.* in FusionRpg.Core catalog hubs)

# Publish a copy-only bump (when tooling exists)
python tools\tuning\publish.py aptitude-catalog displayName.Might="Might"
```

---

## Project Structure

```text
data/tuning/aptitude-catalog.v1.json
data/tuning/derived-stat-catalog.v2.json
data/tuning/derived-stat-catalog.v1.json   # history only; boot uses v2
data/tuning/status-catalog.v1.json
data/tuning/resource-catalog.v1.json
data/tuning/element-catalog.v1.json
data/tuning/actor-sheet.v1.json          # tab order/labels/kinds + kit role display words
src/FusionRpg.Core/.../XxxCatalog*.cs    # parse + hub (no I/O)
src/FusionRpg.Server/ActorSurfaceCatalogEndpoints.cs
src/FusionRpg.Injector/Host/RpgHost.cs   # Configure calls
tests/FusionRpg.Core.Tests/.../ActorSurfaceCatalog*.cs
web/.../contract/actorSurfaceCatalog.ts # consumer types (later shell module wires fetch)
```

Seed `lexicon.v1.json` / `catalog.json` / `data/seed/*/roster.json` become check mirrors once inject
lands (`SeedCatalogMatchesCode` → “injected catalog matches live registry”).

---

## Design

### File ownership (numbers stay separate)

| Catalog file | Fields | Sibling number file (unchanged role) |
|---|---|---|
| `aptitude-catalog` | id, posture, ordinal, displayName, role, reading | `aptitudes.v7.json` edges/economy |
| `derived-stat-catalog` | family, axis, compose, unitClass, displayName, reading, icon, gauge, sheetGroup, capRef, **expand axis / policy** (element or resource construction — same shape Core uses) | `derived-stats.v2.json` cap *values*. **Not** one JSON row per registered channel: families expand to the live id set (registry **268** today). Mirror tests assert expand(catalog) ⊆ / aligns with `AllRegistered`, not `entries.length === 268` |
| `status-catalog` | id, kind, categories, stacking, payloadKinds, displayName, reading, hudToken, color | `status.v1.json` policy |
| `resource-catalog` | id, class, exhaustion, actionCost, plant/zombie labels, icon, color, meterKind | — |
| `element-catalog` | id, displayName, ordinal, color; omni presentation row | element/combat matrix values |
| `actor-sheet` | tabs[{kind,label,order,hidden}], defaultOpen, kitRoles[{ roleId, labels: { humanoid, plant } }] | — |

### Load reject (T5 / T8)

- Missing file / missing required key
- Unknown `StatusKind`, compose kind, `UnitClass`, or tab `kind` not in the closed C# / FE renderer set
- Element matrix incomplete after roster widen
- Aptitude id colliding with a derived channel id
- Aptitude listed in catalog but absent from `aptitudes.v{n}` edges (when edges required)

### API

```text
GET /api/catalogs/actor-surface
→ { tabs, aptitudes, families, resources, elements, statuses, kitRoles, versionStamp }
```

Cacheable per process; FE loads once per session. Unknown id on a player band → designed placeholder,
never `idWords` (GG-62).

### HUD

Injector injects the same `status-catalog` / `resource-catalog`. Renderers resolve
`id → { hudToken, color, displayName }`. **Forbid** `StatusInitials(id)` and hashed RGB
([actor-hud-ideal.md](../actor-hud-ideal.md) §4.1).

### Magnitudes

Catalogs carry presentation and identity only. Any magnitude remains `long` where composition
already uses it; this module does not introduce float magnitudes.

---

## Tunables

This module **is** the runtime-catalog surface (tunables-ssot §1). It introduces **no new balance
numbers**. Cap *values* stay in existing number files; catalogs may only **reference** them.

---

## Code Style

```csharp
// Host:
StatusCatalogHub.Configure(StatusCatalogLoader.Parse(File.ReadAllText(path)));

// Core — no File I/O:
public static class StatusCatalogHub
{
    public static void Configure(StatusCatalogDocument doc) { /* register defs */ }
}
```

JSON keys camelCase on the wire; C# records PascalCase. Ordinals append-only.

---

## Testing Strategy

| Level | What |
|---|---|
| Core unit | Parse happy path; reject unknown kind; reject missing key; aptitude–channel collision |
| Server | Endpoint returns all sections; 503/500 if hub unconfigured |
| Mirror | Injected catalog **expand** aligns with live registry (replaces seed-only `SeedCatalogMatchesCode`). Family entry count alone must **not** be compared to 268 |
| FE (later shell) | Guard: no `ResourceId` five-string union; iterates catalog; derived expand/join covered under `derived-tab` |

---

## Boundaries

- **Always:** Host inject; T5 reject; copy not in number files; GG-62 no `idWords`.
- **Ask first:** Widening aptitude/resource/element counts (DESIGN-GATE rows must move); new tab
  `kind`; adding a *second* presentation library that overlaps §3.3; changing `ItemRole` /
  `core.v1.json`.
- **Never:** Core `File.Read`; silent default for missing catalog; YAML loader; putting displayName
  into `aptitudes.v7.json` / `derived-stats.v2.json`; third channel classification; five-string
  `ResourceId` union as the FE roster once catalog ships.

---

## Success Criteria

- [ ] Six catalog files (+ actor-sheet chrome) parse and inject on Server and Injector
- [ ] `GET /api/catalogs/actor-surface` returns tabs + all rosters with displayNames
- [ ] Unknown kind / missing file fails host startup naming the key
- [ ] Copy-only publish does not move combat goldens
- [ ] Expand mirror: catalog families + axis produce the registered channel id set (268 today); no
      “entries must equal AllRegistered.Count” false invariant
- [ ] FE guard: no `ResourceId` five-string roster; `channelLabel` uses catalog `displayName`
- [ ] HUD path can resolve `hudToken`/`color` without id-slicing (wiring may land with shell/HUD amend)

---

## Open Questions

None for this module. Leftover scope is **commander v1** (locked).
