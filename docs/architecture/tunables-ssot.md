# Tunables — SSOT

**Status:** **Proposed 2026-08-23.** Owner instruction: *"building a game with magic numbers is bad
and we will pay for it when adjusting/balancing the game — so audit and enforce it now."*
**Companion:** [power/ssot-power-scale.md](power/ssot-power-scale.md) §9 (PS-7) generalised from one
program to the whole repo. **Audit tool:** `scripts/audit-magic-numbers.py`.

---

## 0. The tax this exists to avoid

A balance pass is not one change. It is *hundreds* of small changes, made repeatedly, mostly by
feel, mostly at the point where the game is finally playable enough to judge — which is exactly when
a rebuild-per-tweak loop is most expensive.

The cost of a magic number is not writing it. It is:

- Every adjustment is an edit, a rebuild, and a test run instead of a file save.
- Nobody can see the balance surface, because it is scattered across 37 files.
- A number used in two places drifts, and the drift is invisible until it is a bug.
- Reverting a bad balance pass means reading a diff instead of restoring a version.
- **Goldens move on a tuning change**, so a balance edit and a code regression become
  indistinguishable — the exact ambiguity `power-dial` was designed to avoid.

This document is cheap now and unaffordable later, in the same way the power ladder was: the repo
already shipped three incompatible level curves before anyone wrote one down.

---

## 1. Three classes, and the one test that separates them

> **The test: would a balance pass ever want to change this number?**
> If yes, it is a **tunable** and belongs in config. If changing it breaks *whether the system works*
> rather than *how the game feels*, it is **structural**. Everything else is a literal.

| Class | Examples | Where it lives |
|---|---|---|
| **Tunable** — balance surface | costs, yields, rates, chances, durations, gains, decay, weights, multipliers, thresholds, soft caps, XP steps, drop odds | `data/tuning/<domain>.v{n}.json` |
| **Structural** — correctness | buffer sizes, recursion depth, contract/protocol versions, id-namespace offsets, byte widths, hash depth | named `const`, **with a comment saying why it is not tunable** |
| **Literal** — arithmetic | `0`, `1`, `-1`, `2` in `/2`, the per-mille `1000`, array indices, identity values | inline, no ceremony |

**The grey zone is real, and the tiebreaker is ownership.** `MaxRounds = 50` bounds a battle so it
cannot hang — structural. But a designer might well want 30 or 80 for pacing — tunable. When both
readings are defensible, **it is tunable**: the cost of a needless config row is one line; the cost of
a needed one is a rebuild loop during the week you are least able to afford it.

---

## 2. Where a tunable lives

`data/tuning/<domain>.v{n}.json`, following the shape `data/seed/items/_tuning/tier-bands.v1.json`
already set:

```jsonc
{
  "schemaVersion": 1,
  "version": 1,
  "_meta": {
    "owner": "docs/architecture/demons/spec-demon-contracts.md",
    "note": "Working values, not a validated balance decision.",
    "rebalance": "Never hand-edit. `python -m tuning set contracts.slotPriceStep=400 --publish`
                  writes v{n+1}; the old version stays for revert."
  },
  "slotPriceStep": 300,
  "baseSlots": 12,
  "loyalty": { "max": 1000, "deployFloor": 200, "winGain": 15, "decayPerDay": 25 }
}
```

**One domain, one file.** `contracts`, `souls`, `loam`, `shield`, `status`, `battle`, `power-scale`,
`vfx`. A number two domains need belongs to whichever **owns the concept**; the other reads it rather
than copying it. A copied number is a future drift bug with a delay fuse.

---

## 3. The rules

> **T1. A number a balance pass would change lives in config, never in code.** §1's test decides.

> **T2. A structural constant stays a named `const` and says why it is not tunable.** A bare
> `const int Foo = 64;` with no comment is indistinguishable from a magic number, and the next person
> has to re-derive the judgement you already made.

> **T3. No bare numeric literal in a balance-surface file.** Policy, Catalog, Rules, Ruleset and Math
> files *are* the balance surface. A number there is either a named tunable or a named structural
> constant — never an inline literal.

> **T4. Config is versioned and never hand-edited.** A tool republishes `v{n+1}`; the old version
> stays on disk. Reverting a balance pass is restoring a file, not reading a diff.

> **T5. A missing tunable is a load rejection naming it.** Never a built-in default. A default is a
> number nobody chose that behaves like one somebody did — the failure mode `numerics` already refuses
> (*"a generator with no authored share must reject at import, not guess one"*).

> **T6. Every tunable carries its unit.** `Milli`, `Ms`, `PerMatch`, `PerDay`, `Permille`. The units
> trap is documented in `spec-power-vector.md` and it is the most expensive kind of balance bug —
> `+10 hp` and `+10 fire power` differ by an order of magnitude and read identically.

> **T7. A tuning change must not be able to hide a code regression.** Because config is versioned and
> separate, a golden that moves is attributable to exactly one of them. This is `power-dial`'s
> two-step rule generalised: never land a refactor and a rebalance in one change.

---

## 4. The audit

```powershell
python scripts\audit-magic-numbers.py                 # full report
python scripts\audit-magic-numbers.py --domain contracts
python scripts\audit-magic-numbers.py --targets M1    # file:line list
```

| Code | Severity | Finding |
|---|---|---|
| **M1** | HIGH | Bare numeric literal in a balance-surface file (T3) |
| **M2** | HIGH | `const` with balance vocabulary in its name — should be config (T1) |
| **M3** | MEDIUM | `const` with no comment and no obvious structural role (T2) |
| **M4** | LOW | Tunable with no unit in its name (T6) |

**Precision over coverage**, the lesson from `audit-overflow.py`'s first run, which reported 121
critical findings of which every one was a false positive. Literals `0`, `1`, `-1`, `2`, `100`, `1000`
are exempt; so are array indices, `switch` cases, version numbers and test files.

---

## 5. Migration order — biggest balance surface first

Measured 2026-08-23 over `src/FusionRpg.Core`, counting numeric literals in Policy/Rules/Ruleset
files:

| Domain | Literals | File |
|---|---|---|
| **contracts** | **47** | `Demons/Contracts/ContractPolicy.cs` |
| **loam** | 20 | `World/Loam/LoamPolicy.cs` |
| **souls** | 16 | `Demons/SoulEarnPolicy.cs` |
| **patron** | 15 | `Demons/Patron/PatronPolicy.cs` |
| **vfx** | 11 | `Vfx/VfxRules.cs` |
| **fusion** | 9 | `Demons/Fusion/StarPolicy.cs` |
| **shield** | 7 | `Combat/Shield/ShieldPolicy.cs` |
| others | ~14 | overlay, frontier, status, cap, combat |

37 balance-surface files in total. **`contracts` first** — it is the largest, it is already being
touched by the caps work (`MaxSlots` removal), and it is a self-contained domain with one owner spec.

**Migrate a domain at a time, values unchanged.** A migration that also retunes is unreviewable, by
T7. Extract to config, prove byte-identical behaviour, then tune in a separate change.

---

## 6. What this is not

- **Not a ban on numeric literals.** `x / 2` is not a magic number. §1's literal class exists so the
  rule stays enforceable rather than performative.
- **Not a demand that everything be runtime-reloadable.** Load at startup is enough. Hot-reload is a
  convenience, not the standard.
- **Not retroactive on structural constants.** They stay `const`; T2 asks only for a comment.
- **Not a licence to add config rows nobody reads.** A tunable with one value and no plausible second
  value is a literal with extra ceremony.

---

## 7. Decided

**7.1 The publishing tool is built by the first domain, not up front.** T4 requires that a tool
republishes `v{n+1}` rather than a human hand-editing. Building a general `tuning` CLI before any
domain has migrated would be designing against one example — the mistake
[world-map-program.md](world-map-program.md) records for its generator (*"every knob a generator owns
is tuned against a loop that does not exist yet"*).

So: **`contracts` migrates first and builds the minimal publish path it actually needs.** The second
domain generalises it if the shape holds. `seedsmith numerics rebalance --publish` is the working
precedent to copy.

**7.2 `Core` never reads a file. Hosts load and inject.**

`FusionRpg.Core` is Unity-free *and* DB-free and runs inside two hosts with different filesystem
layouts — the injector from a plugin folder, the server from `dist/.../data/`. A file read inside
Core would need to know which, and would break the property that makes Core testable.

```text
Core         →  takes a loaded tuning object; no I/O, no path, no default
Injector     →  loads from its plugin folder, injects at RpgHost startup
Server       →  loads from data/tuning/, injects at composition root
Tests        →  construct one inline; no fixture files
```

This is exactly the shipped `IProgressionPowerProvider` pattern — *"Hot progression read — Core stays
DB-free; injector/server hydrate levels"* — applied to configuration. No new architecture, and the
seam is already proven.

**Consequence for `power-ladder`:** its §2.3 loader belongs on the **host** side of that line. The
spec's `PowerTuningLoader.cs` sits in Core only as a pure parser over a string or stream; the file
read is the host's. Recorded here rather than left for the implementer to guess.
