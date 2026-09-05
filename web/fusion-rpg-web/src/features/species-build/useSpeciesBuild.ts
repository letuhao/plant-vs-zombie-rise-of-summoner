import { newCorrelationId } from "@/lib/bus/demons";
import { useRespecSpecies, useSpeciesAptitudes, useSpeciesRespecPrice } from "@/lib/bus";

/**
 * spec-allocation-surface.md — hooks over the existing bus, nothing invented: the species query,
 * the respec price preview, and the ONE save mutation (`useRespecSpecies`) that handles a first
 * override, a revert, and a priced change alike. The old unpriced `/api/aptitudes/species/allocate`
 * route this hook was written to avoid was retired server-side (species-build-todo.md T4.3,
 * 2026-09-05) — `useRespecSpecies` is now the only write path for a species aptitude override.
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
