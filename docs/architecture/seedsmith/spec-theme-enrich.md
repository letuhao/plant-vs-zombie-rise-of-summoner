# Spec: `theme-enrich`

**Module id:** `theme-enrich` · **Program:** seedsmith · **Depends on:** `theme-refresh`, `pipeline`

## Objective

Turn a name-only demon theme into usable item-generation context without relabelling model-authored
text as observed almanac prose. The stage is resumable and only upgrades themes that are not already
referenced by item content.

## Contract

- Input is the complete published theme registry; only active rows with `basis: "name"` are eligible.
- Synthetic unnamed capture slots (`enumvalue…`, `extract_single`, `extract_ten`) are eligible only
  when their runtime anchor supplies independent traits/element context; a bare unnamed slot is
  deterministically blocked.
- The model receives the species id, display name, motifs, anti-motifs and item expression rule.
- Output is `{blocked, flavor, reason}` under a schema audited by `pipeline.audit_schema`.
- A non-blocked flavor must be non-empty, contain no digits, citations or placeholders, and be long
  enough to provide context. A blocked response is recorded as a terminal outcome and does not alter
  the theme.
- Accepted lore is written atomically to the same registry row as `flavor` and `basis: "enriched"`.
  `enriched` is an honest provenance class: it is generatable by item consumers, but is not claimed
  to be source text. Existing `text`/`name` snapshots and all item-bound keys remain untouched.
- Each subject is checkpointed in `theme-enrich.ledger.json`; normal resume skips persisted,
  blocked and escalated subjects. A later explicit retry may select terminal failures.

## Determinism boundary

Planning, eligibility, context construction, validation, ledger ordering and atomic writes are
deterministic. The model chooses only the prose field. No model output chooses a species, registry
key, motif, numeric value or file destination.

## Verification

```powershell
cd tools/seedsmith
python -m seedsmith demons theme-enrich --dry-run
python -m pytest tests/test_theme_enrich.py -q
```

The dry run reports the held count. Completion is true only when the registry has no active
`basis: "name"` rows; blocked or escalated rows remain visible rather than being called complete.
