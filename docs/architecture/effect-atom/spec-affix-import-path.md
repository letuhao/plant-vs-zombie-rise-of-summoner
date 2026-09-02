# Spec: affix-import-path (E32)

**Status: DRAFTED 2026-09-03**, from [effect-atom-ideal.md](../effect-atom-ideal.md) §W7.7.4 and the
capability map's [§12](../effect-atom-map.md). Module **E32**, Wave 7. Depends on **E30**.

**What it owns: making an authored affix loadable.** `AffixLibraryGenerator` is written and tested,
`effect_affix` tables exist, `Instantiator` and `Resolver` consume affixes — and **nothing can get an
affix from a file into the database.** The chain is broken at four points, and this module closes all
four plus a latent key defect whose fix window closes the moment any container gains a pool.

---

## 1. The four breaks, each verified

| # | Break | Evidence |
|---|---|---|
| **1** | `SeedContent` has **no `Affixes` list** — only Atoms, Containers, Curves, Rarities, Elements, ElementMatrix, ChannelPolicies | `AtomSeedFile.cs:52-74` |
| **2** | `AtomSeedFile.TryKind` has **no `"affix"` case**, so a file with `"kind": "affix"` is refused as `UnknownKind` | `AtomSeedFile.cs:410-423` |
| **3** | `SeedScanner.OwnedFolders` = `atoms, containers, curves, rarity, elements, channel-policy` — **no `effects`** | `SeedScanner.cs:14-15` |
| **4** | `RpgStore.UpsertAffix` has **zero production callers**; `RpgStore.Import.cs:236-242` says so outright — *"Affixes are not yet part of `SeedContent`'s own import batch"* | — |

**And the destination does not exist.** seedsmith's affix stage writes to
`data/seed/effects/affixes/all.json` — a directory absent from the tree. **The affix authoring pipeline
has never been run for real.**

Sorted: **all four are wiring gaps.** Every component works; nothing connects them.

---

## 2. ⛔ The latent key defect, and why its window is closing

`AtomSeedFile.cs:253`:

```csharp
pool.Add(new ContainerPoolRow(Str(p, "atom"), Int(p, "weight", 0), StrOrNull(p, "group")));
```

It reads the JSON key **`"atom"`** into `ContainerPoolRow`'s first positional parameter — which is
**`AffixId`**, and whose own doc comment says:

> *"References an `AffixRow`, **never a bare atom directly** (`affix-schema`, T3.1 — `definitions.md`
> §4a: *"effect_container_pool rows reference affixes, not bare atoms"*)."*

So authoring `{"atom": "atom.foo.t1"}` in a pool stores it as an affix id, and
`Instantiator.ExpandSingleRefAffix` (`Instantiator.cs:238-239`) **throws** — because
`AffixLibraryGenerator` names its affixes `affix.foo.t1`, not `atom.foo.t1`.

**It is latent for exactly one reason: no shipped container has a non-empty `pool`.** All three container
files carry six entries, every one with `prefixRolls: 0` / `suffixRolls: 0` and no `pool` key. **And no
test pins the key.**

**Rename it to `"affix"` in this module.** The window closes the moment any container gains a pool, and
after that the rename is a migration instead of a one-line fix.

> **Prior art, and read it before touching this:** the `effect_container_pool` **column** rename
> `atom_id` → `affix_id` already landed (ContentHashRegistry v7 → v8 → v9), and the residual **JSON key**
> half is logged in `tasks/seed-to-concrete-todo.md` as *"a pre-existing T3.1-scope gap"*. **This is the
> other half of a rename someone already started**, not a new idea.

---

## 3. The contract

### 3.1 The four connections

1. **`SeedContent.Affixes`** — a list beside `Atoms` and `Containers`, populated by the reader.
2. **`AtomSeedFile.TryKind`** gains `"affix"`, and a `ReadAffix` mirroring `ReadAtom`/`ReadContainer`:
   the same envelope (`schemaVersion: 1` / `kind` / `entries`), the same canonicalisation on read so
   **re-indenting a seed file cannot move the content hash**.
3. **`SeedScanner.OwnedFolders`** gains the folder holding affixes. **It must match where seedsmith
   writes** — today `data/seed/effects/affixes/`. Two halves of one path disagreeing is how the pipeline
   came to have never run.
4. **`RpgStore.ImportContent`** upserts affixes inside its existing single transaction, with the same
   read-everything-then-write discipline and the same single `catalog_revision` bump.

### 3.2 The affix row shape

From `AffixRow` as shipped — a bundle of refs, each **either** a concrete atom **or** a slot:

```jsonc
{
  "schemaVersion": 1,
  "kind": "affix",
  "entries": [
    { "id": "affix.authored.master-of-fire-and-ice",
      "refs": [ { "seq": 0, "atom": "atom.elemental-power.fire.t3" },
                { "seq": 1, "atom": "atom.elemental-power.ice.t3" } ] }
  ]
}
```

**`class` is never authored.** It is derived — no trigger → `prefix`, trigger → `suffix`, both →
`mixed` — mirroring `AffixValidator.AffixClassOfAtom`, and seedsmith's own `derive.py` gives the reason:
*"a model that names its own class can contradict the bundle it just picked."*

### 3.3 Generated affixes are derived at import, not committed

`AffixLibraryGenerator.Generate(content.Atoms)` produces one affix per atom, 1:1, and is called **inside
`ImportContent`**. **Do not commit the 1:1 affixes as rows** — they are a pure function of the atom set,
and `atom-family-library.md` §2's rule applies: *"do not hand-author what a pure function can
generate."* Only **hand-authored, multi-ref, slotted** affixes are files.

---

## 4. What this module must NOT do

- **Own the generation rule.** `effect-pipeline` module 3 `affix-library` owns atoms → affixes and its
  generator already exists. **E32 builds the write path that generator has never had**
  ([`effect-atom-ideal.md`](../effect-atom-ideal.md) §W7.7.9).
- **Own the authoring run.** `effect-pipeline` module 9, coordinated with `seed-to-concrete` T7.2.
- **Let a model author `class`, a weight, a tier or a magnitude.** Law 2.
- **Change the `effect_affix` schema or the registry version** unless a table joins or leaves —
  row counts alone do not move `ContentHashRegistry.CurrentSchemaVersion`.
- **Leave the pool key ambiguous.** Rename to `"affix"`, and add the test that pins it.

---

## 5. Testing strategy

| # | Test | Proves |
|---|---|---|
| 1 | A `"kind": "affix"` file under the swept folder **imports**, and its rows are queryable | The four breaks are closed |
| 2 | The same file re-imported is **idempotent** — `changed = 0`, no revision bump | Matches the atom/container contract |
| 3 | Re-indenting the file **does not move the content hash** | Canonicalisation on read, as `ReadAtom` does |
| 4 | `class` is **derived**, and an authored `class` field is **ignored or refused** — never trusted | The `derive.py` rule holds on the C# side |
| 5 | A container `pool` referencing an imported affix **rolls it** through `Instantiator.Draw` | The end-to-end reason this module exists |
| 6 | **Planted violation:** a pool row keyed `"atom"` is **refused with a message naming the rename** | §2's defect cannot silently return |
| 7 | **Planted violation:** an affix ref naming an unknown atom is refused **by id** | No silent skip |
| 8 | `AffixLibraryGenerator` runs at import: **affix count equals atom count**, ids unique | The 1:1 rule, derived not committed |
| 9 | seedsmith's output path and `SeedScanner.OwnedFolders` **name the same folder**, asserted by a test | The two-halves-disagree failure cannot recur |

**Test 9 is the one that would have prevented this module.** The pipeline wrote to a folder nothing swept
and nobody noticed, because no test compared the two.

---

## 6. Acceptance criteria

1. An authored affix file imports, is idempotent, and survives re-indentation without a hash change.
2. `class` is derived on the C# side, matching `AffixValidator.AffixClassOfAtom`.
3. A container pool referencing an affix rolls it.
4. The pool JSON key is `"affix"`; `"atom"` is refused with a message naming the rename; a test pins it.
5. Generated 1:1 affixes are derived at import and **not committed as rows**.
6. seedsmith's write path and the scanner's swept folder are asserted equal by a test.
7. No existing atom or container id, hash or behaviour moves.

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **Depends on** | **E30** — a pooled atom is what a slotted affix will reference |
| **effect-pipeline module 3** | Owns the 1:1 generation rule. E32 builds its write path; it must not reimplement the rule |
| **effect-pipeline module 9 / seed-to-concrete T7.2** | Own the authoring run. **Agree who runs it before either does** |
| **seed-to-concrete T3.1** | Already landed the column rename; this is the JSON-key half it logged as a known residual |
| **`UpsertAffix` cost** | It opens its own connection per call and runs `GetAffix` on a second one, with no batch entry point. Fine at current volumes; **if it is ever driven at scale, batch it first** |
