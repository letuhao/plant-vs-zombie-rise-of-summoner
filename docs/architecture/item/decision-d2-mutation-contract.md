# Decision D2 — the mutation contract: what reproducibility the item system can honestly promise

## 1. Status

**R2 debate decision, 2026-08-22.** Settles the contested question named in
[reconciliation-plan.md](reconciliation-plan.md) §R2, row D2. Bound by
[enrichment-contract.md](enrichment-contract.md), especially **SC5**.

Binding on **I4** (sockets), **I6** (enhancement), **I7** (reroll) and **I9** (materials and costs).
Where this file and a lane doc disagree about reproducibility, this file wins until amended. It does
not amend the contract; it interprets SC5 against what the code actually does.

Every claim about this repo is cited `file:line` and was read this session. Where I ran something, I say
so. Suites run: `dotnet test tests/FusionRpg.Data.Tests --filter "BindResolution|AtomInstanceStore"` →
**26 passed**; `dotnet test tests/FusionRpg.Core.Tests --filter "Instantiator"` → **13 passed**.

---

## 2. The question

> Once items can be enhanced, rerolled and socketed, what reproducibility guarantee can the item system
> **honestly** make?

SC5 says: *same `(container_id, catalog_revision, roll_seed)` ⇒ byte-identical instance*, and *"an item's
current state should be derivable from its origin seed plus an ordered, recorded list of operations."*

Three lanes read that sentence three different ways, and one of them is asking for something the code
cannot deliver.

---

## 3. The three positions, stated fairly

### 3.1 I6 — materialised head plus an append-only op log

[ssot-enhancement.md](ssot-enhancement.md) §3.1 proposes:

```text
origin = instantiate(container_id, origin_catalog_revision, roll_seed)   -- pure, contractual
head   = replay(origin, ops[1..n])                                       -- pure, transcript
invariant: hash(head) == effect_instance.state_hash
```

and extends the contract to *same `(container_id, origin_catalog_revision, roll_seed, ops[1..n])` ⇒
byte-identical instance*.

**Where I6 is right, and it is right about the load-bearing part.** Decision A1 (head materialised,
log cold) is correct: composing a delta stack on read would cost O(N×M) per inventory screen and would
create two sources of truth for one number. Decision B2 — *replay is a transcript, not a simulation* —
is the best single idea in this whole round. Recording the **materialised result** of an op rather than
its recipe is what makes a rebalance structurally unable to reach backwards into an item a player
already owns (§3.2, §8.7). That property is worth more than byte-replay and it is available for free.

**Where I6 is wrong.** Two factual claims underneath the model do not survive contact with the code:

- that `effect_instance` does not store the revision it rolled under (§5.1, §9.10 — *"a defect in E6"*);
- that `values_json` has nowhere to hold an unresolved `OnApply` range, so a new
  `effect_instance_atom.overrides_json` column is *"load-bearing"* for three lanes (§5.1, §7.1).

Both are refuted in §4. And I6's own extended contract keeps `origin` as a **derived** term — a pure
function of the catalog — which is precisely the term I7 says is unrecoverable.

### 3.2 I7 — byte-reproducibility is not achievable; ask for an honest bound instead

[ssot-reroll.md](ssot-reroll.md) §6.5 splits the guarantee in two and says only one half is real:

| Guarantee | I7's verdict |
|---|---|
| Auditable + idempotent | **Yes**, achievable today |
| Byte-reproducible after mutation | **No, not in general** — `catalog_revision` is one monotonic integer and the catalog at a revision is archived nowhere |

**Where I7 is right, and this is the finding of the round.** The catalog at a past revision is
recoverable nowhere in this tree (§4.2). Any contract whose terms include *"the catalog as it was"* is
unmeetable. I7 also correctly refuses to design around a promise it cannot keep, and correctly hands the
archiving question to the owner as a funded call rather than assuming it.

**Where I7 is too pessimistic.** It accepts I6's framing that the origin must be *re-derived*. It need
not be. Once the origin values are **recorded** rather than recomputed, byte-exact reconstruction of the
head is available for all time with no catalog involved. I7 conceded a guarantee it could have had.

I7 also names a second thing correctly and it matters for the audit rung: `Freeze` copies `spec.Min`
verbatim for a `Fixed` spec (`src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:204`) and leaves an
`OnApply` spec entirely alone (`:206`), so tempering either is a paid no-op and must be refused
(`NotRerollable`, ssot-reroll.md:475-477).

### 3.3 I4 — sockets need almost none of this

[ssot-sockets.md](ssot-sockets.md) §4.8: an insert is **its own instance** with **its own binding** on
the same owner. Composition happens at the binding layer, so no frozen row is ever rewritten and
`InstanceRow.ContentFingerprint()` is untouched.

**Where I4 is right — verified.** `ContentFingerprint()` is computed over `ContainerId` plus the
instance's atom rows and nothing else (`Instantiator.cs:47-55`). Bindings are not in it. `effect_binding`
already supports many bindings per owner (`src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:75-89`).
Socketing writes new rows for new things; it mutates nothing. **SC5 is not strained by sockets at all.**

**Where I4 overreaches.** It then asks I6 to *"declare the operation log the SSOT and `item_socket` a
materialized view"* (§9.2). That request undoes its own argument — it re-couples the layer that needs the
mutation model least to the mutation model's hardest guarantee. §6 refuses it, in I4's favour.

I4's `bind_ordinal` request (§5.4) is a real total-order defect and is **out of scope for D2** — it is an
ordering bug in definitions §5, independent of mutation. The fact that it is independent is itself
evidence for the narrowing in §6.

---

## 4. The factual finding — is byte-identical replay achievable today?

### 4.1 First, correct the record: the revision column exists

I6's §9.10 calls the missing `origin_catalog_revision` *"a defect in E6 today"*. It is not missing.

| Claim | Evidence |
|---|---|
| The column exists | `RpgStore.AtomInstances.cs:60` — `catalog_revision INTEGER NOT NULL DEFAULT 0` |
| It is written | `RpgStore.AtomInstances.cs:107-117` |
| It is read on both load paths | `RpgStore.AtomInstances.cs:144`, `:337` |
| It exists on the Core record | `Instantiator.cs:37`, stamped at `:114` from the `catalogRevision` parameter at `:76` |
| It is tested | `tests/FusionRpg.Data.Tests/BindResolutionTests.cs:167-176` — passing |

What misled I6 is the **spec**: the `effect_instance` column table at
[spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md):15-21 lists
`instance_id / container_id / roll_seed / created_utc / origin` and omits `catalog_revision`, while line
25 of the same file claims reproduction over it. The spec is stale; the code is right. Code beats docs.

### 4.2 The catalog at a past revision is recoverable nowhere. I7 is correct.

Checked, not assumed:

- `content_meta` holds **one integer, one row** — `id INTEGER PRIMARY KEY CHECK (id = 1)`,
  `catalog_revision` (`src/FusionRpg.Data/Sqlite/RpgStore.Atoms.cs:59-63`). `BumpCatalogRevision`
  increments it (`:83-95`). Nothing else is written at bump time.
- The six content tables the hash covers — `effect_atom`, `effect_container`, `effect_container_atom`,
  `effect_container_pool`, `effect_curve`, `rarity`
  (`src/FusionRpg.Core/Effects/Atoms/ContentHashRegistry.cs:43,62,76,83,90,97`) — are all **upsert-in-
  place**. There is no history table, no validity range, no shadow copy.
- I swept every `CREATE TABLE` in `src/FusionRpg.Data/Sqlite/`. Sixty-six tables. One is named
  `archive_catalog` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:215-225`) and it is **not** a catalog
  archive — it is the cold-storage index for compacted `rpg_soul_ledger` JSONL files
  (`src/FusionRpg.Data/Sqlite/RpgStore.Compaction.cs:175-186`). The name is a trap; noting it so the next
  reader does not lose an hour to it.
- `ComputeContentHash` (`src/FusionRpg.Data/Sqlite/RpgStore.ContentHash.cs:21-42`) computes over the
  tables **as they are now**. It is deliberately not cached, and no stamp is persisted anywhere.

So: **an operation resolved against revision 41 cannot be re-derived once the catalog reaches 42.**
Confirmed.

### 4.3 Worse: `catalog_revision` is not even a faithful label for the catalog

The revision is bumped explicitly, once per import transaction. A direct upsert changes content **without
touching it** — this is documented in the shipped code that exists because of it:

> *"Caching on `catalog_revision` looks obvious and is wrong: the revision is bumped explicitly (once per
> import transaction), and a direct upsert changes content without touching it — a cache keyed on it
> would serve a stale hash for exactly the hand edit this module exists to make visible."*
> — `src/FusionRpg.Data/Sqlite/RpgStore.ContentHash.cs:10-13`

So even *with* an archive keyed on the revision, two different catalogs can share a revision number. The
faithful label already exists and it is `contentHash`; it is simply never stored. That fact decides §8.

### 4.4 What the roll actually depends on

Reproducing `instantiate(...)` requires, beyond the seed: the container's `pool_rolls`, every pool row's
`weight` and `group_key` and the **order they are read in** (`Instantiator.cs:134-157`; ordered by
`atom_id` at `src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:235-236`), each atom's `family_id` and
`variant` (the group fallback, `Instantiator.cs:163-164`), and every atom's `params_json` value specs
(`:187-208`). That is four of the six covered tables. Byte-replay of the *origin* is a function of the
catalog, not of the instance.

The RNG itself is not the problem. `AtomRandom` streams are named and domain-separated
(`Instantiator.cs:132`, `:182-183`), and the owned xoshiro256** PRNG is version-pinned
(`src/FusionRpg.Core/Battle/SeededRng.cs:11`, `:26-27`). Determinism of the *algorithm* is solid. What is
missing is the *inputs*.

### 4.5 The bigger finding, which changes the shape of the question

`ResolveBindings` refuses **every** binding whose instance was rolled under a different revision than the
current one:

```csharp
// An instance rolled against an older catalog no longer means what it meant. Reproducing
// it would need the catalog it was rolled against, which we do not keep.
if (instance.CatalogRevision != current)
    refused.Add(new BindRefusal(binding.BindingId, AtomRejectionReason.StaleInstance, …));
```
— `src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:288-295`

This is shipped, tested and green
(`tests/FusionRpg.Data.Tests/BindResolutionTests.cs:178-190`, in the 26 that passed).

**Consequence: today, one content import unequips every item every player owns.** Not "makes it
unreproducible" — makes it *unequippable*. Every I-lane design in this folder assumes items survive a
patch. None of them can, on this gate.

This reframes D2. The interesting question is not *"can we replay a mutated item byte-exactly across a
catalog bump"*; it is *"can an item survive a catalog bump at all"*. The answer must be yes, and the
mutation contract has to be stated so that it is yes.

The per-atom check that already runs immediately below (`:297-307` — every atom the instance names must
still be in the catalog) is the right gate and it is already implemented. The revision-equality line is
the wrong one. **Removing it costs exactly one Data test**, and nothing in production: `TryInstantiate`
and `SaveInstance` have **no production callers** — grep across `src/` returns only `Instantiator.cs`
itself and the store; every other hit is in `tests/`. The loot path (I12) does not exist yet. This is a
tested constraint, not an assumed one.

One adjacent gap while I was in there: `ResolveBindings` calls `ListAtoms()` with the default
`enabledOnly = false` (`RpgStore.AtomInstances.cs:267`; signature at
`src/FusionRpg.Data/Sqlite/RpgStore.Atoms.cs:207`), so definitions §6's *"atom disabled ⇒ new binds reject
with `StaleInstance`"* is **not enforced**. Handing that to R1's defect register; it is not D2's to fix.

### 4.6 The finding that dissolves the disagreement

I6 and I7 are arguing past each other because I6's contract has two halves and only one of them is
catalog-dependent:

| Term | Depends on the catalog? | Recoverable? |
|---|---|---|
| `origin = instantiate(container, rev₀, seed)` | **yes** — §4.4 | **no** — §4.2 |
| `head = replay(origin, ops)` under **transcript** replay (I6's B2) | **no** — the ops carry materialised deltas | **yes**, always |

I6 already chose transcript replay. It then kept the origin as a derived term out of habit, and that one
term is what makes the whole contract unmeetable.

**Remove the dependency instead of funding it: record the origin values rather than recomputing them.**
I6 already proposes the column — `effect_instance_atom.origin_values_json`, §5.1 — but demotes it to *"a
cache, not the authority."* Invert that. Promote it to the record. Then:

```text
head = replay(recorded_origin_values, ops[1..n])
```

is closed over data the database holds forever, needs no catalog, no archive, and no revision, and it is
byte-exact for the life of the save. Byte-exactness of the *head* — the thing every reader, every
tooltip, every test and every support question actually cares about — turns out to be achievable. It is
byte-exactness of the *drop, re-derived from content* that is not.

**Answer to the question posed:** byte-identical replay **from the catalog** is not achievable today and
is not worth making achievable (§8). Byte-identical reconstruction **from record** is achievable today,
costs one column, and is what the system should promise.

---

## 5. The guarantee ladder, and the pick

The four rungs, honestly costed. Rungs 2 and 3 are separate axes, not one step — a system can be
auditable and non-idempotent, or the reverse — but they are listed in the order a design should acquire
them.

| Rung | What it means | What it buys | What it costs | Achievable today? |
|---|---|---|---|---|
| **1. Byte-identical replay (from catalog)** | `instantiate(container, rev₀, seed) + ops` recomputes the item from content | independent re-derivation of a drop; a golden that pins the generator across time; reconstruction of an item whose atom rows were lost | a catalog archive (§8), an as-of read path parallel to `RpgStore.Atoms`/`.Containers`, and a permanent maintenance tax on every covered-table schema change | **No** (§4.2) |
| **1′. Byte-identical reconstruction (from record)** | `recorded_origin_values + ops` reproduces the head exactly, forever | the same head bytes on every load; a total replay test over a fixture DB; "62 rolled · +25 enhanced" in the tooltip, permanently | one column (`origin_values_json`) and one hash (`state_hash`) | **Yes** |
| **2. Auditable** | every change is attributable — who, when, which atom, what delta, what it cost, what the outcome was — and the current state is verifiable against that record | support answers *"what happened to my item"*; drift between head and log becomes a loud defect instead of a silent one; a rebalance provably cannot rewrite an owned item | the op log, `state_hash`, one replay test | **Yes** |
| **3. Idempotent** | the same operation applied twice has the effect of once | crash- and retry-safety; a double-clicked enhance button spends once | a `correlation_id` and a UNIQUE index — the pattern already ships | **Yes** |
| **4. None** | the head is the only truth; how it got there is unknown | nothing | a class of bug with no forensic path, and no way to prove a nerf did not eat someone's item | — |

### The pick

> **The item system commits to 1′ + 2 + 3: byte-identical reconstruction from record, auditable, and
> idempotent. It does not commit to rung 1, and does not pretend to.**

Being explicit about the honest part, as I7 asked for:

> **Byte-identical re-derivation of a drop from the catalog holds only while `catalog_revision` has not
> moved past the value recorded on the instance — and, because a direct upsert can change content without
> bumping the revision (§4.3), even that window is not guaranteed. Past it, the recorded rows are the
> SSOT and the log is provenance. This is stated, not promised away.**

**Is byte-replay a want or a need?** A want. Trace what rung 1 actually buys, in this game:

- *Anti-cheat* — the buyer that justifies it in an online game. This game is **standalone-first** (SC8),
  the database is a local SQLite file the player owns, and the server binds to `127.0.0.1`. You cannot
  defend a local file from its owner. Re-deriving a drop to prove it was legitimate defends nothing.
- *Reconstruction after data loss* — recorded origin values (1′) cover it without the catalog.
- *A golden that pins the generator* — a fixture catalog checked into the test project pins it better,
  because it does not drift with production content at all.
- *Explaining a number to a player* — 1′ covers it, and covers it after the patch too, which rung 1 does
  not.

Every real buyer is served by 1′. Rung 1 buys the ability to answer a question nobody in this project is
going to ask, at the price of a permanent tax on content schema changes.

**And the thing that actually matters is not on the ladder at all.** I6's transcript rule (§3.2) — record
the result, never the recipe — is what stops a rebalance reaching backwards into an owned item. That
property is *stronger* than rung 1 and it is free. Rung 1 would even work against it: a system that can
faithfully re-simulate the past is a system someone will eventually be tempted to re-simulate with
today's numbers.

---

## 6. Do sockets need the op log?

**No, and saying so narrows the model.**

I4's own argument is sound and I verified it: socketing writes new `effect_instance`, new
`effect_instance_atom` for the *insert's own* instance, and new `effect_binding` rows. It rewrites nothing
on the host. `ContentFingerprint()` (`Instantiator.cs:47-55`) covers `ContainerId` plus the host's atom
rows, so it cannot move.

What socketing needs durably is `item_socket(instance_id, socket_index, affinity, insert_container_id,
insert_instance_id)` (ssot-sockets.md §5.2). That table is **complete state on its own**. Replaying I4's
own six-op worked example (ssot-sockets.md §7.3) produces exactly the `item_socket` rows that already sit
in the table. The log adds nothing a reader needs.

So I4's request #2 — *"declare the operation log the SSOT and `item_socket` a materialized view"* — is
**refused**:

> **`item_socket` is the SSOT for socket state. It is not a materialized view of anything.** The socket
> layer is not a client of the mutation model's reconstruction guarantee.

I4 may still **append** `socket-add` / `socket-insert` / `socket-remove` rows to the op log, and should,
because destructive removal at t4–t5 destroys player value and support will be asked about it. But it
appends for **audit and idempotency only** (rungs 2 and 3), and nothing reads those rows to rebuild state.
That gives I4 the history it wants without the coupling it does not.

**The narrowing, stated plainly:** the op log's reconstruction guarantee (rung 1′) serves exactly **two**
lanes — I6 and I7 — the two that rewrite frozen values in place. I6's §7.8 presents a ten-point contract
as if three lanes were equally bound by it. They are not. The model is narrower than proposed, and the
lane that argued it needed the least was right.

### The shape this settles into, and it already ships in this tree

Not a novel design — it is the souls ledger, reused:

| Souls | Items |
|---|---|
| `rpg_soul_balances` — authoritative current balance | the **head**: `effect_instance_atom.values_json` (and `item_socket` for sockets) |
| `rpg_soul_ledger` — append-only, explains the balance (`RpgStore.cs:440-452`) | `effect_instance_op` — append-only, explains the head |
| `UNIQUE(player_id, reason, dedupe_key)` enforces idempotency in schema (`RpgStore.cs:451`) | `UNIQUE(instance_id, correlation_id)` |
| A reused correlation with a different amount is **refused**, not silently replayed (`src/FusionRpg.Data/Sqlite/RpgStore.Souls.cs:196-202`) | same rule, same reason |

> **The op log is a ledger, not a source of truth.** The head is authoritative; the log explains it; a
> mismatch between them is a defect, not a warning. That is exactly the balance/ledger pair this repo
> already runs, and copying a shipped shape is cheaper than inventing one.

---

## 7. The two schema asks, ruled on

### 7.1 `origin_catalog_revision` on `effect_instance` — **REFUSED as a new column; GRANTED as a semantic lock**

The column exists. It is `effect_instance.catalog_revision` (`RpgStore.AtomInstances.cs:60`), written
(`:107-117`), read (`:144`, `:337`), and tested (`BindResolutionTests.cs:167-176`). Adding
`origin_catalog_revision` beside it would create two columns for one fact — the two-sources-of-truth
failure I6's own Decision A2 rejects.

What is granted instead, and it is what I6 was actually after:

1. **`effect_instance.catalog_revision` means the revision the instance rolled under, at origin. No
   operation ever rewrites it.** Ops stamp their **own** `catalog_revision` on their own row (I7 §6.2 has
   this right and it is the correct place for it).
2. **[spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md):15-21 is stale** and must
   list the column. That omission is what produced a false defect claim in a lane doc; it will produce
   another one if it is left.
3. It is a **label, not an input.** It is not fed to the RNG (`Instantiator.cs:70-118` never reads
   `catalogRevision` except to stamp it at `:114`) and it is not in `ContentFingerprint()` (`:47-55`). It
   records *which content*, and per §4.3 it does so imperfectly.

**Blocking precondition attached, and it is not mine to authorise.** The revision-equality refusal at
`RpgStore.AtomInstances.cs:288-295` must be removed and replaced by the per-atom existence-and-enabled
check already present at `:297-307`, or no item in this program survives a patch (§4.5). E6 is
**ask-first** ([reconciliation-plan.md](reconciliation-plan.md):66-67; boundaries at
[spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md):119-125), so this
is a recommendation with a measured cost — one Data test, zero production callers — routed to R1's
defect register and to §11.1 for the owner.

### 7.2 `overrides_json` on `effect_instance_atom` — **REFUSED**

The justification is factually wrong, and a passing test says so.

I6 §5.1 argues the column is *"load-bearing: without it no `OnApply` affix can ever be enhanced or
rerolled, because `values_json` holds only frozen `OnInstantiate` results and an unresolved range has
nowhere else to live."*

`values_json` holds the unresolved range. `Freeze` writes every param into one dictionary and serialises
the lot:

```csharp
frozen[key] = spec.Roll switch
{
    RollPolicy.OnInstantiate => spec.Resolve(rng),
    RollPolicy.Fixed         => spec.Min,
    // Left as authored: an OnApply range belongs to the hit, not the item.
    _                        => raw,
};
…
valuesJson = JsonSerializer.Serialize(frozen, JsonOpts);
```
— `Instantiator.cs:201-210`

`raw` is the authored value-spec object, and it lands in `values_json` verbatim. Pinned by
`tests/FusionRpg.Core.Tests/Atoms/InstantiatorTests.cs:130-142`, which asserts that after instantiate
`values_json.amount` is **still an object** with `min = 100` and `max = 200` — one of the 13 tests I ran
green this session.

So enhancing an `OnApply` affix is: read `values_json`, multiply `min` and `max` in the spec object,
write `values_json` back, record the before/after in `result_json`. Identical in shape to enhancing a
frozen scalar. **No column. No third resolution layer. No change to `Freeze`.**

Three further reasons to refuse:

- **It is load-bearing for one lane, not three.** I7 explicitly **refuses** to reroll `OnApply` and
  `Fixed` values — `NotRerollable`, ssot-reroll.md:475-477 — so it does not want the column. I4 composes
  at the binding layer and does not want it either.
- **It reintroduces the failure I6 rejected.** A `values_json` *and* an `overrides_json` on one row is two
  places one number can live, resolved in a three-step order (atom params → container override →
  instance override). That is Decision A2's two-sources-of-truth defect, readmitted through a side door.
- **`effect_container_atom.overrides_json` is content authoring; this is not.** The container column
  exists so an author can retune an atom for one container. An instance is not authored.

**What is granted in its place**, because these two are the columns that actually carry the ruling:

| Column | Ruling | Why |
|---|---|---|
| `effect_instance_atom.origin_values_json` TEXT NULL | **GRANTED, and promoted from cache to record** | This is the column that makes rung 1′ real. Written at first mutation (or at instantiate, I12's choice), never rewritten. With it the guarantee is closed over recorded data and needs no catalog, no archive, no revision. I6 proposed it as *"a cache, not the authority"* (§5.1) — it is the authority |
| `effect_instance_atom.suppressed` INT NOT NULL DEFAULT 0 | **GRANTED** | `seq` is half the primary key (`RpgStore.AtomInstances.cs:72`) and may never be renumbered, so I7's identity change has to be suppress-then-append. Without it, deletion is the only option and history is destroyed |
| `effect_instance.state_hash` TEXT, `mutation_seq` INT | **GRANTED** | Rung 2's verifiability. `state_hash` uses definitions §8's canonical form — SHA256 over length-prefixed columns, sort-then-concatenate, **XOR-fold banned**, `N:` for NULL. One algorithm in the tree |
| `effect_instance.enhance_level`, `enhance_pity_permille` | **I6's, not mine** | Mechanic state, not mutation-contract state |

---

## 8. Catalog archiving — required?

**No. Not required, and not recommended in its full form.** Here is the costed comparison, because it is
an owner-funded call and it deserves numbers rather than a preference.

| Option | What it buys | What it costs | Ruling |
|---|---|---|---|
| **A. Full snapshot per revision** | rung 1 — genuine historical re-derivation | Copy six tables (`ContentHashRegistry.cs:43-97`) on every `BumpCatalogRevision`. Storage is *not* the problem: the whole catalog is low thousands of rows (definitions of ~71 families expanding to a few hundred atoms — [atom-family-library.md](../effect-atom/atom-family-library.md):27, :265-270), so a snapshot is a few hundred KB and a few hundred revisions is tens of MB. The **real** cost is a second read path — an as-of `AtomRow`/`ContainerRow` loader parallel to `RpgStore.Atoms.cs` and `RpgStore.Containers.cs` — plus a permanent tax: every column added to a covered table must now be added to its archive too, or the archive silently stops being faithful. Rough size: a table set, ~1 dev-day of DDL/IO, and a maintenance obligation with no end date | **Rejected** — pays a standing tax for a rung nothing needs (§5) |
| **B. Content-hash pinning per revision** | *detection*: whether the catalog you are looking at is the one the item rolled under; and it closes the §4.3 hole where a direct upsert changes content without moving the revision | One table — `catalog_stamp(catalog_revision INTEGER PK, content_hash TEXT NOT NULL, schema_version INTEGER NOT NULL, stamped_utc TEXT NOT NULL)` — and one insert inside `BumpCatalogRevision` (`RpgStore.Atoms.cs:83-95`) reusing `ComputeContentHash`, which already exists and already returns a compact stamp (`RpgStore.ContentHash.cs:21-42`, `ContentHashStamp.cs:16`). About **30 lines and two tests**. No read path, no maintenance tax — the registry version is already carried in the stamp | **Recommended, and cheap enough to be uncontroversial** |
| **C. Copy-on-write of touched rows** | rung 1, at lower storage than A | Six shadow tables with validity ranges, an as-of query per table, and importer diffing. Strictly more complex than A. And it is broken at the root by §4.3: a direct upsert that does not bump the revision produces a version range that silently lies | **Rejected** — more complexity than A for a weaker guarantee |

**Ruling: fund B, not A.** It is the cheapest thing that makes the honest bound *checkable* rather than
merely *stated*. Without it, "the catalog has moved since this item dropped" is an inference from an
integer that §4.3 shows can be wrong. With it, it is a hash comparison.

B is also the index A would hang off. If the owner later decides a real archive is wanted, the stamp is
already the key, and nothing built now has to be undone.

**One reason code ruling, so this does not become scar tissue.** I6 proposes
`OriginRevisionUnavailable` as a rejection code (§6.2). **Refused as a rejection code.** The closed list
is 33 + `None`, asserted at `tests/FusionRpg.Core.Tests/Atoms/AtomKindRegistryTests.cs:32`; every addition
moves that test and every code is meant to name a *refused action* (SC6). Nothing a player does is refused
by this condition. It is a **diagnostic status on the audit report**, not a rejection. Adding a code no
action can emit is precisely the `status.expose.*` failure SC7 exists to stop.

`OpSequenceGap` and `ReplayDivergence` **are** granted as rejection codes — both name a refused write.

---

## 9. The final contract — for I4, I7 and I9 to paste

Copy this list into your lane doc. It replaces ssot-enhancement.md §7.8 where the two disagree.

1. **The head is the SSOT.** `effect_instance_atom.values_json` always holds the item's current numbers,
   and `item_socket` always holds current socket state. Every existing reader reads what it reads today.
   Nothing composes anything at read time.
2. **The op log is a ledger, not a source of truth.** `effect_instance_op` explains the head; it does not
   define it. Nothing on any read path consults it. Same shape as `rpg_soul_balances` /
   `rpg_soul_ledger` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:440-452`).
3. **The guarantee is rung 1′ + 2 + 3, and it is closed over recorded data.**
   `replay(origin_values_json, ops[1..n]) == head`, byte-exact, forever, with **no catalog involved**.
   `state_hash` is the check; a mismatch is `ReplayDivergence` — a defect, not a warning.
4. **Record the result, never the recipe.** Write materialised deltas into `result_json` and the decided
   `outcome`. Replay never re-runs your formula and never re-rolls your dice. This is what makes a
   rebalance structurally unable to change an item a player already owns; it is the single most valuable
   property in this contract and it must not be traded for log size.
5. **`effect_instance.catalog_revision` is origin-only.** It records which catalog the drop rolled under.
   No operation rewrites it. Your op row stamps its **own** `catalog_revision` and its own
   `rules_version`.
6. **Byte-re-derivation from the catalog is not promised.** It holds only while the catalog has not moved
   past the recorded revision, and §4.3 means even that window is not guaranteed. Do not write a spec
   sentence that claims otherwise. Past that point the recorded rows are the SSOT and the log is
   provenance.
7. **Ordering is dense and final.** `op_seq` is 1-based, gapless, applied in order, never reordered.
   Out-of-order arrival is `OpSequenceGap`.
8. **Every op is idempotent.** Carry a `correlation_id` with `UNIQUE(instance_id, correlation_id)`. A
   replay of the same request returns the recorded result; a reused correlation with **different**
   parameters is refused, not silently applied — copy `TrySpendSouls`
   (`src/FusionRpg.Data/Sqlite/RpgStore.Souls.cs:189-202`).
9. **Never renumber `seq`.** Rewrite `values_json`, append rows continuing the numbering, set
   `suppressed = 1`. Never delete a row. Identity changes are suppress-then-append. `(instance_id, seq)`
   is the primary key (`RpgStore.AtomInstances.cs:72`).
10. **Derive your randomness.** `SeededRng.DeriveStream(op_seed, "item.{op_kind}")`
    (`src/FusionRpg.Core/Battle/SeededRng.cs:26`), one named stream per op kind. Record `op_seed` even
    when the op consumed none. `System.Random` never touches this path
    (`src/FusionRpg.Core/Battle/SeededRng.cs:5-6`).
11. **Spend atomically.** Op row, material debit and head rewrite commit in one transaction, and
    `cost_json` records the spend in I9's vocabulary. A spent cost with no op is theft; an op with no cost
    is duplication.
12. **Rehash and bump.** After your op, recompute `state_hash` over the instance's atom rows using
    definitions §8's canonical form — SHA256, length-prefixed columns, sort-then-concatenate, **XOR-fold
    banned**, `N:` for NULL — including `suppressed` rows. Set `mutation_seq = op_seq`.
13. **Sockets are exempt from clauses 3 and 4.** `item_socket` is the SSOT for socket state, not a view.
    I4 appends `socket-*` ops for audit and idempotency (clauses 2, 8, 11) and for nothing else. No socket
    operation touches the host's `effect_instance_atom` rows or its `ContentFingerprint()`
    (`Instantiator.cs:47-55`).
14. **`OnApply` values live in `values_json` and are editable there.** The unresolved value spec is
    already stored verbatim (`Instantiator.cs:206`, `:210`; pinned by `InstantiatorTests.cs:130-142`).
    There is no `effect_instance_atom.overrides_json` and none is coming. Enhancing an `OnApply` affix
    rewrites `min`/`max` inside the spec object in `values_json`. Rerolling one is still refused
    (`NotRerollable`) — that is I7's design choice, not a schema limit.
15. **If your op cannot be expressed as value deltas + appends + suppressions, say so.** That is a finding
    against this contract, and a finding is not a failure.

---

## 10. What I rejected, and why

| Rejected | Whose | Why |
|---|---|---|
| **The extended contract *"same `(container_id, origin_catalog_revision, roll_seed, ops)` ⇒ byte-identical instance"*** | I6 §3.1 | Its `origin` term is a pure function of a catalog that is archived nowhere (§4.2). Unmeetable as written. Replaced by a contract closed over recorded origin values, which is meetable and no weaker in practice |
| **A new `origin_catalog_revision` column** | I6 §5.1, §9.10 | The column exists — `RpgStore.AtomInstances.cs:60`, written `:107-117`, tested `BindResolutionTests.cs:167-176`. The claim came from a stale spec table (spec-instance-and-binding.md:15-21), not from the code |
| **`effect_instance_atom.overrides_json`** | I6 §5.1, §7.1 | The premise is refuted by a passing test: `values_json` already carries the unresolved `OnApply` spec (`Instantiator.cs:206`; `InstantiatorTests.cs:130-142`). It is wanted by one lane, not three, and it readmits the two-sources-of-truth defect Decision A2 rejects |
| **`OriginRevisionUnavailable` as a rejection code** | I6 §6.2 | No player action is refused by it. The closed list is 33 + `None` (`AtomKindRegistryTests.cs:32`) and SC6 reserves codes for refused actions. It is a diagnostic on the audit report |
| **Conceding that byte-reproducibility after mutation is unachievable** | I7 §6.5 | Right about the catalog, too pessimistic about the consequence. I7 accepted I6's framing that the origin must be re-derived. Recording it costs one column and buys the guarantee back |
| **Funding a catalog archive (full snapshot)** | I7 §13.7 offers it as the alternative | Real cost is not storage, it is a parallel as-of read path plus a permanent tax on every covered-table schema change (§8 option A). Its only unique buyer — proving a drop was legitimate — is worthless in a standalone-first, local-SQLite game (SC8) |
| **Copy-on-write versioning of touched rows** | considered here | More complex than a full snapshot for a weaker guarantee, and broken at the root by the direct-upsert hole (`RpgStore.ContentHash.cs:10-13`) |
| **Declaring the op log the SSOT with `item_socket` as a materialized view** | I4 §9.2 | Undoes I4's own correct argument. `item_socket` is complete state on its own; making it derived buys nothing and couples the layer that needs the mutation model least to its hardest guarantee |
| **Re-simulating replay (re-run the formula, re-roll from `op_seed`)** | I6 Decision B1, considered and rejected there | Agreed and re-affirmed. A nerf would retroactively un-succeed attempts players paid for. The log-size argument is weak; the correctness argument is decisive |
| **Composing a delta layer on read** | I6 Decision A2 | Agreed and re-affirmed. O(N×M) on the web inventory listing, and two sources of truth for one number |
| **Versioning the instance — a new `instance_id` per op** | I6 Decision A3 | Agreed and re-affirmed. Bindings, socket contents and any thin item row all re-point on every `+1` |

---

## 11. Open questions for the owner

1. **The bind gate's revision-equality refusal (`RpgStore.AtomInstances.cs:288-295`) must go, and E6 is
   ask-first.** As it stands, one content import unequips every item every player owns (§4.5). The
   replacement — the per-atom existence check at `:297-307` — is already implemented. Measured cost: one
   Data test (`BindResolutionTests.cs:178-190`); zero production callers, because `TryInstantiate` and
   `SaveInstance` are only reached from tests today. **This is the one item in this document that blocks
   the item program if it is not decided.**
2. **Fund the `catalog_stamp` table?** ~30 lines and two tests (§8 option B). Recommended. Declining it
   is survivable — the guarantee in §9 does not depend on it — but then "has the catalog moved" stays an
   inference from an integer that can be wrong.
3. **Where is `origin_values_json` written — at instantiate, or lazily at first mutation?** At instantiate
   costs a duplicate copy of every instance's values from day one and is simpler. Lazily costs nothing
   for the ~95% of items nobody ever mutates, and adds one branch. I lean lazy; it is I12's call once the
   loot path exists, and the guarantee holds either way.
4. **Should the op log survive the item?** `CollectOrphanInstancesUnlocked`
   (`RpgStore.AtomInstances.cs:460-472`) deletes any instance with no binding, which today means an
   unequipped item is deleted — I7 §9.3 flags this and it belongs to R1's register. But there is a
   contract question inside it that is mine to surface: when an item is legitimately salvaged, does its
   op log go with it (`ON DELETE CASCADE`, cheap, and "what happened to my item" becomes unanswerable
   for a deleted item), or does the log outlive it? I have not decided this.
5. **I did not decide whether `state_hash` covers `power_json`.** E9 backfills power later (SC9,
   `Instantiator.cs:20-21`), and a backfill that moves every `state_hash` would fire `ReplayDivergence` on
   every mutated item at once. Either exclude `power_json` from the hash, or make the backfill a recorded
   op. Excluding it is simpler and I lean that way, but it weakens what the hash verifies, and E9 is not
   mine.
