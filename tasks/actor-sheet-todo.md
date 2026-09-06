# Tasks: actor-sheet program (catalog-era)

Plan: [actor-sheet-plan.md](actor-sheet-plan.md) · Map:
[../docs/architecture/actor-sheet-map.md](../docs/architecture/actor-sheet-map.md).

**Owner unlock (this session):** FE T0–T16 implementation authorized despite Draft specs.
Server/Injector host wiring (T3) still optional — FE uses fixture catalog fallback.

### Binding rules

1. `ActorPanel.tsx` — one task at a time.
2. No fabricated data.
3. Trail specs are not implementation SSOT.
4. Buy before build.
5. Derived **families expand** over omni+elements when joining `/derived` (268 Core channels).

### Checklist

- [x] T0 FE libs (`lucide-react`, `recharts`, `motion`, `@xyflow/react`, `react-tiny-sparkline`)
- [x] T1–T4 actor-surface-catalog (FE fixture + `useActorSurfaceCatalog` + window bus; Core hubs may already exist)
- [x] Checkpoint A (FE catalog path)
- [x] T5–T7 actor-sheet-shell (near-fullscreen `size="actorSheet"`, shared widgets, eight tabs)
- [x] Checkpoint B
- [x] T8–T10 condition / aptitudes / derived
- [x] Checkpoint C
- [x] T11–T15 remaining tabs (shield / status / elements / kit / paths)
- [x] Checkpoint D
- [x] T16 HUD catalog resolve (`resolveStatusHudToken` + root catalog boot)
- [x] Checkpoint E (playwright viewport screenshots + focused FE suite green)
- [ ] T3 Server/Injector host wiring (optional / out of FE scope this pass)
