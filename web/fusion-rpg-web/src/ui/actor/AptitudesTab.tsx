import { useEffect, useMemo, useRef, useState } from "react";
import type { ActorView } from "@/contract/types";
import type { ActorSurfaceCatalog } from "@/lib/bus/actorSurface";
import {
  useAptitudes,
  usePlayers,
  useSaveAptitudes,
  useSaveUniqueAptitudes,
  useUniqueAptitudes
} from "@/lib/bus";
import { useAllocationDraft } from "@/hooks/useAllocationDraft";
import { bindSurface } from "@/features/gui-lego/bindSurface";
import { asSurfaceBusLike } from "@/features/gui-lego/createSurfaceBus";
import { createAptitudesSurfaceBus } from "@/features/gui-lego/aptitudesSurfaceBus";
import { foldAptitudesSurfaceVm } from "@/features/gui-lego/foldAptitudesSurfaceVm";
import { getRecipe } from "@/features/gui-lego/recipeRegistry";
import { Banner, EmptyState } from "@/ui";
import { RecipeMount } from "@/ui/gui-lego/RecipeMount";
import { ensureAptitudesGuiLegoRegistered } from "@/ui/gui-lego/registerAptitudes";
import { emitAptitudeObs } from "./aptitudeObs";
import { fillAutoAssign, type AutoAssignRule } from "@/features/aptitudes/autoAssign";

export type AptitudesHostRole = "creature" | "commander";

export type AptitudeDraftState = {
  budget: number;
  spent: number;
  leftover: number;
  dirty: boolean;
  withinBudget: boolean;
  saving: boolean;
  mode: "unique" | "commander";
  revert: () => void;
  save: () => Promise<void>;
};

/**
 * Thin host: Mode A UniqueDemon (creature) vs Mode C commander → aptitudes-console RecipeMount.
 * Shell footer may mirror Confirm/Cancel; in-console decision strip is the primary (D7/S8).
 */
export function AptitudesTab({
  data,
  surface,
  role = "creature",
  onDraftState
}: {
  data: ActorView;
  surface: ActorSurfaceCatalog;
  role?: AptitudesHostRole;
  onDraftState?: (state: AptitudeDraftState | null) => void;
}) {
  ensureAptitudesGuiLegoRegistered();
  const isCommander = role === "commander";
  const players = usePlayers();
  const playerId = players.data?.currentPlayerId ?? data.playerId ?? 0;
  const commander = useAptitudes(isCommander ? playerId : null);
  const unique = useUniqueAptitudes(isCommander ? null : data.instanceId);
  const saveCommander = useSaveAptitudes();
  const saveUnique = useSaveUniqueAptitudes();

  useEffect(() => {
    emitAptitudeObs("aptitude.mode.bind", {
      mode: isCommander ? "commander" : "unique",
      instanceId: data.instanceId,
      playerId
    });
    return () => onDraftState?.(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isCommander, data.instanceId, playerId]);

  if (isCommander) {
    if (commander.isLoading || !commander.data) {
      return (
        <div className="mt-4">
          <EmptyState title="Loading aptitudes…" testId="aptitudes-loading" />
        </div>
      );
    }
    return (
      <AptitudesConsoleHost
        mode="commander"
        surface={surface}
        budget={commander.data.budget}
        theta={commander.data.theta}
        serverShares={commander.data.shares}
        playerId={playerId}
        instanceId={null}
        saveCommander={saveCommander}
        saveUnique={saveUnique}
        onDraftState={onDraftState}
      />
    );
  }

  if (!data.instanceId) {
    return (
      <div className="mt-4">
        <EmptyState title="No specimen bound for UniqueDemon aptitudes." testId="aptitudes-no-specimen" />
      </div>
    );
  }

  if (unique.isLoading || !unique.data) {
    return (
      <div className="mt-4">
        <EmptyState title="Loading aptitudes…" testId="aptitudes-loading" />
      </div>
    );
  }

  const theta = unique.data.theta ?? unique.data.specimenLevel;
  return (
    <AptitudesConsoleHost
      mode="unique"
      surface={surface}
      budget={unique.data.budget}
      theta={theta}
      serverShares={unique.data.shares}
      commanderAddOn="Commander contribution is read-only on the lawn"
      playerId={playerId}
      instanceId={data.instanceId}
      saveCommander={saveCommander}
      saveUnique={saveUnique}
      onDraftState={onDraftState}
    />
  );
}

function AptitudesConsoleHost({
  mode,
  surface,
  budget,
  theta,
  serverShares,
  commanderAddOn,
  playerId,
  instanceId,
  saveCommander,
  saveUnique,
  onDraftState
}: {
  mode: "unique" | "commander";
  surface: ActorSurfaceCatalog;
  budget: number;
  theta: number;
  serverShares: Record<string, number>;
  commanderAddOn?: string;
  playerId: number;
  instanceId: string | null;
  saveCommander: ReturnType<typeof useSaveAptitudes>;
  saveUnique: ReturnType<typeof useSaveUniqueAptitudes>;
  onDraftState?: (state: AptitudeDraftState | null) => void;
}) {
  const saving = mode === "commander" ? saveCommander.isPending : saveUnique.isPending;
  const seeded = useMemo(() => {
    const next: Record<string, number> = {};
    for (const row of surface.aptitudes) {
      next[row.id] = serverShares[row.id] ?? 0;
    }
    return next;
  }, [surface.aptitudes, serverShares]);

  const allocation = useAllocationDraft({
    serverValues: seeded,
    budget,
    isSaving: saving,
    onSave: async (draft) => {
      if (mode === "commander") {
        emitAptitudeObs("aptitude.confirm", { mode, playerId });
        await saveCommander.mutateAsync({ playerId, shares: draft });
        return;
      }
      if (!instanceId) {
        emitAptitudeObs("aptitude.confirm.refused", { mode, reason: "no-instance" });
        throw new Error("UniqueDemon allocate requires instanceId");
      }
      emitAptitudeObs("aptitude.confirm", { mode, instanceId });
      await saveUnique.mutateAsync({ instanceId, shares: draft });
    }
  });

  const allocationRef = useRef(allocation);
  allocationRef.current = allocation;

  const [selectedId, setSelectedId] = useState<string | null>(surface.aptitudes[0]?.id ?? null);
  const revisionRef = useRef(0);
  const typedBus = useMemo(() => createAptitudesSurfaceBus(), []);
  const bus = useMemo(() => asSurfaceBusLike(typedBus), [typedBus]);

  useEffect(() => {
    const offs = [
      typedBus.on("aptitude.select", (p) => {
        const id = (p as { aptitudeId?: string })?.aptitudeId;
        if (typeof id === "string") setSelectedId(id);
      }),
      typedBus.on("aptitude.step", (p) => {
        const id = (p as { aptitudeId?: string; delta?: number })?.aptitudeId;
        const delta = (p as { delta?: number })?.delta ?? 0;
        if (typeof id !== "string" || !allocationRef.current.draft) return;
        const cur = allocationRef.current.draft[id] ?? 0;
        allocationRef.current.setValue(id, cur + delta);
      }),
      typedBus.on("aptitude.reset", () => {
        emitAptitudeObs("aptitude.reset", { mode, instanceId, playerId });
        allocationRef.current.revert();
      }),
      typedBus.on("aptitude.confirm", () => {
        void allocationRef.current.save();
      }),
      typedBus.on("preset.open", () => {
        emitAptitudeObs("preset.open", { mode, instanceId, playerId });
      }),
      typedBus.on("aptitude.autoAssign", (p) => {
        const rule = ((p as { rule?: string })?.rule ?? "even") as AutoAssignRule;
        // Mode C: never species-favour — fall back to Even (S7 / Mode C lock).
        const effective: AutoAssignRule =
          mode === "commander" && rule === "species-favour" ? "even" : rule;
        const filled = fillAutoAssign(effective, budget);
        emitAptitudeObs("aptitude.autoAssign", {
          mode,
          rule: effective,
          ok: filled.ok,
          reason: filled.reason
        });
        if (!filled.ok) return;
        for (const [id, value] of Object.entries(filled.shares)) {
          allocationRef.current.setValue(id, value);
        }
      })
    ];
    return () => offs.forEach((off) => off());
  }, [typedBus, mode, instanceId, playerId, budget]);

  useEffect(() => {
    if (allocation.draft === null) {
      onDraftState?.(null);
      return;
    }
    onDraftState?.({
      budget,
      spent: allocation.spent,
      leftover: budget - allocation.spent,
      dirty: allocation.dirty,
      withinBudget: allocation.withinBudget,
      saving,
      mode,
      revert: () => {
        emitAptitudeObs("aptitude.reset", { mode, instanceId, playerId, via: "shell" });
        allocationRef.current.revert();
      },
      save: () => allocationRef.current.save()
    });
  }, [
    allocation.draft,
    allocation.spent,
    allocation.dirty,
    allocation.withinBudget,
    budget,
    saving,
    mode,
    instanceId,
    playerId,
    onDraftState
  ]);

  const vm = useMemo(() => {
    if (allocation.draft === null) return null;
    revisionRef.current += 1;
    return foldAptitudesSurfaceVm({
      mode,
      surface,
      draftShares: allocation.draft,
      budget,
      spent: allocation.spent,
      leftover: budget - allocation.spent,
      dirty: allocation.dirty,
      withinBudget: allocation.withinBudget,
      saving,
      selectedAptitudeId: selectedId,
      theta,
      commanderAddOn: commanderAddOn ?? null,
      availability: "ready",
      revision: revisionRef.current
    });
  }, [
    allocation.draft,
    allocation.spent,
    allocation.dirty,
    allocation.withinBudget,
    budget,
    saving,
    mode,
    surface,
    selectedId,
    theta,
    commanderAddOn
  ]);

  const recipe = getRecipe("aptitudes-console");
  const plan = useMemo(
    () => (recipe && vm ? bindSurface(recipe, vm, { preferOverlay: false }) : null),
    [recipe, vm]
  );

  if (allocation.draft === null || !plan) {
    return (
      <div className="mt-4">
        <EmptyState title="Loading aptitudes…" testId="aptitudes-loading" />
      </div>
    );
  }

  return (
    <div className="flex min-h-0 min-w-0 flex-1 flex-col">
      {allocation.error ? <Banner tone="error">{allocation.error}</Banner> : null}
      <RecipeMount plan={plan} bus={bus} />
    </div>
  );
}
