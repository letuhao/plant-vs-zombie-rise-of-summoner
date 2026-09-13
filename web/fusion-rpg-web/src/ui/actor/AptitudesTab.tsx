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
import { probeAptitudePresetsApi } from "@/lib/bus/aptitudePresets";
import { useAllocationDraft } from "@/hooks/useAllocationDraft";
import { bindSurface } from "@/features/gui-lego/bindSurface";
import { asSurfaceBusLike } from "@/features/gui-lego/createSurfaceBus";
import { createAptitudesSurfaceBus } from "@/features/gui-lego/aptitudesSurfaceBus";
import { foldAptitudesSurfaceVm } from "@/features/gui-lego/foldAptitudesSurfaceVm";
import { getRecipe } from "@/features/gui-lego/recipeRegistry";
import { runAutoAssign } from "@/features/aptitudes/runAutoAssign";
import { AptitudePresetConsoleHost } from "@/features/aptitudes/AptitudePresetConsoleHost";
import { Banner, EmptyState } from "@/ui";
import { RecipeMount } from "@/ui/gui-lego/RecipeMount";
import { ensureAptitudesGuiLegoRegistered } from "@/ui/gui-lego/registerAptitudes";
import { useToastStack } from "@/shell/toastStack";
import { emitAptitudeObs } from "./aptitudeObs";
import { useAptitudePresetActive } from "@/lib/bus/aptitudePresets";

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
 * Thin host: Mode A UniqueCreature (creature) vs Mode C commander → aptitudes-console RecipeMount.
 * Shell footer may mirror Confirm/Cancel; in-console decision strip is the primary (D7/S8).
 */
export function AptitudesTab({
  data,
  surface,
  role = "creature",
  onDraftState,
  speciesId
}: {
  data: ActorView;
  surface: ActorSurfaceCatalog;
  role?: AptitudesHostRole;
  onDraftState?: (state: AptitudeDraftState | null) => void;
  /** Mode A favour seed when the specimen's species is known (optional). */
  speciesId?: string | null;
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
        speciesId={null}
        saveCommander={saveCommander}
        saveUnique={saveUnique}
        onDraftState={onDraftState}
      />
    );
  }

  if (!data.instanceId) {
    return (
      <div className="mt-4">
        <EmptyState title="No specimen bound for UniqueCreature aptitudes." testId="aptitudes-no-specimen" />
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
      speciesId={speciesId ?? null}
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
  speciesId,
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
  speciesId: string | null;
  saveCommander: ReturnType<typeof useSaveAptitudes>;
  saveUnique: ReturnType<typeof useSaveUniqueAptitudes>;
  onDraftState?: (state: AptitudeDraftState | null) => void;
}) {
  const pushToast = useToastStack((s) => s.push);
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
        throw new Error("UniqueCreature allocate requires instanceId");
      }
      emitAptitudeObs("aptitude.confirm", { mode, instanceId });
      await saveUnique.mutateAsync({ instanceId, shares: draft });
    }
  });

  const allocationRef = useRef(allocation);
  allocationRef.current = allocation;

  const [selectedId, setSelectedId] = useState<string | null>(surface.aptitudes[0]?.id ?? null);
  const [presetOpen, setPresetOpen] = useState(false);
  const revisionRef = useRef(0);
  const typedBus = useMemo(() => createAptitudesSurfaceBus(), []);
  const bus = useMemo(() => asSurfaceBusLike(typedBus), [typedBus]);
  const presetScope = mode === "unique" ? "unique" : "commander";
  const presetScopeKey = mode === "unique" ? (instanceId ?? "") : "";
  const activePreset = useAptitudePresetActive(playerId, presetScope, presetScopeKey);
  const activePresetName = useMemo(() => {
    const id = activePreset.data?.presetId;
    return id ? id.slice(0, 8) : null;
  }, [activePreset.data?.presetId]);

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
      typedBus.on("aptitude.set", (p) => {
        const id = (p as { aptitudeId?: string })?.aptitudeId;
        const value = (p as { value?: number })?.value;
        if (typeof id !== "string" || typeof value !== "number") return;
        allocationRef.current.setValue(id, value);
      }),
      typedBus.on("aptitude.reset", () => {
        emitAptitudeObs("aptitude.reset", { mode, instanceId, playerId });
        allocationRef.current.revert();
      }),
      typedBus.on("aptitude.confirm", () => {
        void allocationRef.current.save();
      }),
      typedBus.on("preset.open", () => {
        void (async () => {
          emitAptitudeObs("preset.open", { mode, instanceId, playerId });
          const ok = await probeAptitudePresetsApi(playerId);
          if (!ok) {
            emitAptitudeObs("preset.open.unavailable", {
              mode,
              reason: "presets.api.missing"
            });
            pushToast({
              tone: "warn",
              title: "Build presets unavailable",
              message: "The presets API is not reachable — start the server or retry."
            });
            return;
          }
          emitAptitudeObs("preset.open.opened", { mode });
          setPresetOpen(true);
        })();
      }),
      typedBus.on("aptitude.autoAssign", (p) => {
        const rule = (p as { rule?: string })?.rule ?? "even";
        void (async () => {
          emitAptitudeObs("aptitude.autoAssign", { mode, rule });
          const alloc = allocationRef.current;
          if (!alloc.draft) return;
          const result = await runAutoAssign({
            rule,
            budget,
            mode,
            playerId,
            speciesId: mode === "unique" ? speciesId : null,
            scopeKey: mode === "unique" ? instanceId : "",
            setValue: (id, next) => alloc.setValue(id, next)
          });
          if (!result.ok) {
            emitAptitudeObs("aptitude.autoAssign.refused", { mode, rule, reason: result.reason });
            if (result.reason === "autoAssign.favour.empty" || result.reason === "autoAssign.favour.incomplete") {
              pushToast({
                tone: "warn",
                title: "No species favour",
                message: "Favour seed is empty — try Even instead."
              });
            } else if (result.reason === "autoAssign.favour.modeC") {
              pushToast({
                tone: "warn",
                title: "Species favour unavailable",
                message: "Commander builds use Even or a posture lean."
              });
            } else {
              pushToast({
                tone: "warn",
                title: "Auto-assign refused",
                message: result.reason
              });
            }
            return;
          }
          emitAptitudeObs("aptitude.autoAssign.applied", {
            mode,
            rule,
            leftover: result.leftover
          });
        })();
      })
    ];
    return () => offs.forEach((off) => off());
  }, [typedBus, mode, instanceId, playerId, speciesId, budget, pushToast]);

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
      activePresetName,
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
    commanderAddOn,
    activePresetName
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
      {presetOpen ? (
        <AptitudePresetConsoleHost
          open={presetOpen}
          onOpenChange={setPresetOpen}
          mode={mode}
          playerId={playerId}
          scopeKey={presetScopeKey}
          speciesId={speciesId}
          budget={budget}
          surface={surface}
          onApplyDraft={(shares) => {
            const alloc = allocationRef.current;
            if (!alloc.draft) return;
            for (const [id, v] of Object.entries(shares)) {
              alloc.setValue(id, v);
            }
          }}
          onActivated={(shares) => {
            allocationRef.current.acceptCommitted(shares);
          }}
        />
      ) : null}
    </div>
  );
}
