# Actor-sheet derived surface — todo

**Plan:** [actor-sheet-derived-plan.md](actor-sheet-derived-plan.md)

## Runtime cook (this plan)

- [x] Author `data/tuning/derived-stat-catalog.v2.json` (28 combat + 6 status + 4 resource + other)
- [x] v2 loader + rejection rules (missing `en`, leaf-as-family, combat/status/resource parity)
- [x] `ConfigureAll` in `Program.cs`; boot off v2
- [x] `DerivedSurfaceCook` + Contracts DTOs
- [x] `GET /api/catalogs/derived-surface?lang=&side=`
- [x] Expand ↔ registry parity tests (269 unchanged)
- [x] Gap table in plan; map/ideal/spec pointers

## Follow-ons (not this plan)

- [ ] FE Derived rewrite / plate 13 InspectSplit join to cook + `/sheet`
- [ ] Sparse open-prefix status live join on sheet
- [ ] Embed cook on full `GET /api/catalogs/actor-surface` when that endpoint ships
- [ ] Hub §6.1 unattributed producers
