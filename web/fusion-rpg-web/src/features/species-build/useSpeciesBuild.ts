import { newCorrelationId } from "@/lib/bus/demons";
import { useRespecSpecies, useSpeciesAptitudes, useSpeciesRespecPrice } from "@/lib/bus";

/**
 * spec-allocation-surface.md — hooks over the existing bus, nothing invented: the species query,
 * the respec price preview, and the ONE save mutation (`useRespecSpecies`) that handles a first
 * override, a revert, and a priced change alike. Never `useSaveAptitudes`'s sibling
 * `/api/aptitudes/species/allocate` route — that path has no pricing at all
 * (`SpeciesBuildEndpoints.cs`'s own doc comment names it a real, deliberately un-retired gap), and
 * this hook is the one call site in the whole web app that must not reopen it.
 */
export function useSpeciesBuild(playerId: number, speciesId: string | null) {
  const state = useSpeciesAptitudes(playerId, speciesId);
  const price = useSpeciesRespecPrice(playerId, speciesId);
  const respec = useRespecSpecies();

  function save(shares: Record<string, number>) {
    if (!speciesId) return Promise.reject(new Error("no species selected"));
    return respec.mutateAsync({ playerId, speciesId, shares, correlationId: newCorrelationId() });
  }

  return { state, price, respec, save };
}
