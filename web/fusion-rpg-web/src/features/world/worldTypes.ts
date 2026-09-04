/**
 * Wire shapes for the world map — the TypeScript mirror of `FusionRpg.Contracts/WorldDtos.cs`.
 *
 * **world-stage W2 (2026-09-04): re-export shim, not the source.** The types moved to
 * `@/lib/bus/world` — that is where every other domain's DTOs already live, and it is what makes
 * `contractGuard` actually catch a feature-local DTO import (W3). This file exists only so the
 * legacy `features/world/` tree (`WorldPage.tsx`, `worldSelection.ts`, `worldViewModel.ts`,
 * `turnPlayback.ts`, `commanderIntent.ts`, and their tests) keeps compiling unchanged until Phase 4
 * deletes that tree. Do not add a new type here — add it to `lib/bus/world.ts` and re-export it
 * below if the legacy tree still needs the name.
 *
 * The checked-in fixture is asserted against the live DTO by an E2E test, so if these drift the
 * build that catches it is the server's, not a runtime surprise in the browser.
 */
export type {
  WorldFactionDto,
  WorldSlotDto,
  WorldForceDto,
  WorldSectorDto,
  WorldLaneDto,
  WorldEntityMemberDto,
  WorldEntityDto,
  WorldStateDto,
  WorldTurnEntryDto,
  WorldTurnCommandDto,
  WorldTurnReportDto
} from "@/lib/bus/world";
