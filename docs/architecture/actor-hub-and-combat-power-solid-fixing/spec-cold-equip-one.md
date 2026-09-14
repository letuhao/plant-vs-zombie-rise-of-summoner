# Spec: `cold-equip-one`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** HF-cold-equip-one · Q7 **stub is debt** · item / effect-atom rolled bindings  
**Wave:** 1 (before / with `battle-hub-fuse`)  
**Code anchors:** `UniqueEquipmentCatalog` (stub `Items`) · `RpgStore` `ReconcileUniqueEquipmentAtomBindingsUnlocked` · `EquippedBoundAtoms` · `EquipAtomSource.DerivedAtomsFor` / `ModsFor` · `UniqueActorService.PutEquipment`

---

## Objective

There must be **one** Cold UniqueActor equip materialize path: **rolled / atom bindings** that Hub and (pre-fuse) battle already share via `EquippedBoundAtoms` / `EquipAtomSource`. The hardcoded stub catalog (`stub.atk_ring`, `butter_bead`, `hp_charm`) is **architectural debt**, not SSOT.

Success: player equip/unequip resolves combat grants from durable atom bindings (effect instances / containers), not from stub grant templates as the end-state. Stub may remain as a **migration shim** only while fixtures convert — then deleted.

---

## Tech stack

- Data/Core: `RpgStore` UniqueActor equip reconcile · `OwnerKind.UniqueActor` bindings · E7 compiled grants
- Core: `EquippedBoundAtoms.SourceFromStore` · `AtomDerivedSubsystem`
- Server: `UniqueActorService` / equipment endpoints — stop documenting stub as intentional
- Align with item / effect-pipeline “retire stub” direction (`item-ideal`, `decision-d1`, mods-absorption) — **reconcile, do not fork a third equip pipeline**

---

## Commands

```powershell
dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~UniqueEquipment|Equipped|AtomBinding"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EquipAtom|EquippedBound"
.\scripts\guard-actor-hub.ps1
```

---

## Project structure

| Path | Duty |
|---|---|
| `UniqueEquipmentCatalog.cs` | Mark stub `Items` DEBT; remove as SSOT; keep slot normalize only if still needed |
| `RpgStore` UniqueActors equip | Sole write path → reconcile atom bindings |
| `EquippedBoundAtoms.cs` | Shared reader for Hub + battle migration |
| `UniqueActorService.cs` | Comments/API: rolled path; refuse stub-only product claims |
| Tests / fixtures | Convert stub item ids to rolled fixtures; delete stub-only goldens |

### Serious implement gaps (must be in plan, not “later maybe”)

1. **Rolled grant materialize** — equip of `ref_kind = rolled` (or successor) produces Hub-visible `stat.derived` atoms with ops honored on Hub path.
2. **Stub retirement** — no production default that equips stub catalog rows; tests that need combat use rolled fixtures.
3. **Single rebuild** — `UpsertUniqueEquipment` / clear continues one reconcile; no dual `mods_json` SSOT beside atoms.
4. **SourceIds** — `equip:{role}:{itemRef}` grammar (GG-49) on sheet and post-fuse battle.
5. **No silent coerce** — unknown ops skip/visible error (Hub path already); battle ops honesty owned by `battle-ops-parity`.

---

## Code style

- Magnitudes `long`.
- Prefer deleting stub tables/paths over wrapping them forever.
- Comments: `// DEBT stub — cold-equip-one` until removed.

---

## Testing strategy

| Level | Cases |
|---|---|
| Data | Equip rolled item → bindings exist; unequip withdraws; no orphan sweep deleting worn wrong |
| Core | `EquippedBoundAtoms` / Hub Derived rises on equip for a combat channel |
| Regression | Stub catalog rows not required for green suite after migration |
| Guard | No new stub item ids in production catalogs |

---

## Boundaries

- **Always:** One Cold path = atom bindings; stub is debt; Hub SourceIds; long.
- **Ask first:** Dropping `rpg_unique_equipment` table vs keeping as projection key; inventing a second inventory SSOT.
- **Never:** End-state that “stub catalog is fine forever”; fork a battle-only equip parse; deepen ChannelMods for equip.

---

## Success criteria

- [ ] Documented sole Cold materialize path is rolled/atom bindings.
- [ ] Stub `UniqueEquipmentCatalog.Items` deleted or test-only with DEBT comment and no production caller.
- [ ] Equip → Hub Derived combat channel change proven without BattleStatComposer.
- [ ] Shared `EquippedBoundAtoms` remains the reader fuse uses (no third equip fold).
