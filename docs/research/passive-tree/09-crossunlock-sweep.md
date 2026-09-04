# Cross-unlock, measured (2026-09-05)

**Owner instruction:** *"model it and re-sweep before deciding."* Done. The result **reverses the
red-team finding** and **confirms `passive-tree-ideal.md` §4 as originally written.**

Tool: `tools/HybridViability --crossunlock` (Θ=100, budget 300 aptitude points, `b=5`, `Fmax=1.20`).
52 builds: 12 corners · 18 within-posture pairs · **3 all-four-of-one-posture** (the build the
red team argued was broken) · 18 cross-posture pairs · even-twelve. Posture is read from the shipped
catalog (`Aptitude.cs:11,38-51`), never re-declared.

Gate quantity for tree `i` becomes `p_i + credit(posture-mates of i)`, under four candidate rules,
against both tier ladders. **Power stays linear per tier** — only the *gate* reads the credit.

## Result — mean win share

| ladder | credit rule | corner | inPos2 | **inPos4** | xPos2 | spread | treePwr in4/pure |
|---|---|---|---|---|---|---|---|
| D20 | **none** (the --trees sweep) | 43.4% | 50.4% | 55.2% | 52.9% | **54.4%** | 1.02× |
| D20 | **largest** | **49.9%** | 47.3% | 48.9% | 53.0% | 47.7% | **0.69×** |
| D20 | quarter | 46.2% | 48.8% | 52.2% | 53.2% | 52.1% | 1.20× |
| D20 | full | 49.0% | 49.4% | 49.8% | 51.5% | 46.2% | 1.00× |
| D26 | none | 42.9% | 50.6% | 54.7% | 53.1% | 54.4% | 0.82× |
| D26 | **largest** | **50.0%** | 47.8% | 49.9% | 52.3% | 48.3% | **0.62×** |
| D26 | quarter | 46.4% | 49.3% | 51.4% | 52.8% | 51.9% | 1.02× |
| D26 | full | 48.6% | 49.4% | 50.0% | 51.8% | 46.6% | 1.00× |

## What it says

**1. Cross-unlock is a CONCENTRATION reward. §4 was right.** With it off, a corner scores 43.4% and
loses to everything. Under the largest-mate rule it scores **49.9%** and **beats spread for the first
time in any sweep this program has run** (49.9% vs 47.7%).

**2. The mechanism, stated plainly — and it is the owner's own design working.** A pure Might build
is a **Force** build. Its Fortitude, Vigor and Onslaught gates are satisfied by the Might points it
already spent, so **its whole posture comes along for free**. That is exactly *"user have advantage if
pure 1 primary build"*. The four-of-one-posture build gets the opposite: four medium trees, each
crediting three medium mates, which saturates. Its tree power is **0.62–0.69× a pure build's**, not
3.4× and not 10.2×.

**3. Why the red-team arithmetic came out inverted.** It credited only the *invested* trees and
compared their totals. The model here credits every tree the rule actually reaches — including the
eleven floored ones, which is where a pure build collects its entire advantage. Cross-unlock does not
reward you for spreading inside a posture; it rewards you for having **one very large tree** that
every mate can borrow from. The red team's F1 mechanism was real; its sign was wrong.

**4. `full` also works but is worse.** It compresses every build into 48.6–51.8% — a rule that gives
everyone everything stops discriminating. `quarter` is too weak to flip the ordering (spread 52.1%
still beats corner 46.2%).

**5. D26 does not change the ordering**, which is the correct outcome: it was a reward-per-point
correction, not a balance change. Corner is a hair better under it (50.0% vs 49.9%).

## Still open, and reported rather than buried

**Cross-posture pairs remain the strongest kind (53.0%)**, above a corner's 49.9%. The owner's stated
intent was that mixing two major categories carries **no advantage**. It still carries one, because two
spikes in different postures cover two different defensive layers, and §3.3's multiplicative-layer
finding has not gone away. Cross-unlock fixed *focus vs spread*; it did not fix *focus vs two-posture
hybrid*. That is the next thing mechanism nodes (§05) have to earn.

## Recommendation

**Adopt the largest-mate rule.** It is the only candidate that makes the design's stated ordering true
on the focus-vs-spread axis, it is O(1) to compute and to explain to a player, and it bounds the term
by construction — one mate, never a sum, so no k-way build can compound it.
