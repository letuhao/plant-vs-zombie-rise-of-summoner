# Expedition slots and recall

**Status:** Shipped  
**Loop:** Idle expeditions — see [The loops](../the-loops.md)  
**Pillar:** [Expeditions](../expeditions.md)  
**HTML guide:** [site/mechanisms/expedition-slots-recall.html](../site/mechanisms/expedition-slots-recall.html)

---

## In one sentence

**Slots** let you run more than one expedition at once; **recall** brings a squad home early for what you have earned so far.

---

## Blind spots (if you are new)

These words get guessed wrong. Read them once before the rest of the page.

| Word | What it actually means here |
|---|---|
| **Slot** | A parallel expedition berth. You start with a few and grow into more as the system allows — not infinite dispatches. |
| **Recall** | Pull an expedition home before the timer ends. Rewards pro-rate to completed ticks. |
| **Pro-rate** | You take what completed so far — not a full payout and not a wipe of the run. |
| **Live task** | A demon already out on an expedition (or another live assignment) cannot be double-booked. |

**Also true:**

- Recall anytime is intentional — leave early, take earned so far. Nothing expires if you forget to collect.
- How many slots you have grows with progression; trust the UI for the current count.

---

## What it is

Parallelism is gated by expedition slots — you grow from a few to more as you progress.

Recall is the escape hatch when you need those demons back before the timer ends.

---

## How it works

Two controls around the same dispatch loop:

```text
  free slot  ->  dispatch  ->  wait
                    |
                    +->  collect (full timer)
                    +->  recall  ->  pro-rated return
```

| Piece | What it does |
|---|---|
| **Slots** | Each free slot can hold one expedition. Fill what you have; unlock more when the system allows. |
| **Recall** | End early. Rewards scale to completed ticks — you keep progress earned so far, not the full end payout. |

---

## Where you find it

### On the Expeditions layer

Open Expeditions (`E`) after you have a bound demon.

1. Look at how many slots are free versus filled.
2. Dispatch into an empty slot.
3. If you need the squad back early, use recall and read the pro-rated report.

> Exact slot counts change with progression — read the live UI, not a remembered number.

---

## What you do (first time)

1. Open Expeditions and note how many slots you have.
2. Fill one slot with a short dispatch.
3. Try recall once on a later run so you see a pro-rated report — then decide whether early return was worth it.

> Stop when slot ≠ infinite and recall ≠ full payout. Core loop: [expeditions](expeditions.md).

---

## Common mix-ups

**Can I run unlimited expeditions at once?**  
No. Slots gate parallelism. You grow into more over time.

**Does recall give the full reward?**  
No. You get what completed ticks earned — leave early, take less.

**Do unfinished rewards expire?**  
No. Collect when ready; nothing times out from neglect.

**Can I put the same demon on two slots?**  
No. Demons on a live task are not free to dispatch again.

---

## Related

- Next: [Expeditions](expeditions.md)
- [Expeditions](expeditions.md)
- [Persistent specimens](specimens.md)
- [Wild joins](wild-joins.md)
- Pillar: [Expeditions](../expeditions.md)
- Fancy skim: [Vision site — Mechanisms](../site/index.html#mechanisms)
- [Mechanism index](README.md)
