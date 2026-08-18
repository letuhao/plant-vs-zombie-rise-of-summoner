import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from "react";
import {
  getLastHitEvent,
  getLawnMembershipRing,
  subscribeLastHit,
  subscribeLog,
  useLawnDebugPost,
  useSpawnExtraIntent,
  useUniqueActor
} from "@/lib/bus";
import type { LawnSelectPayload } from "@/game/EventBus";
import { cn } from "@/lib/cn";
import { Page } from "@/layouts/Page";
import { Split } from "@/layouts/Split";
import {
  Badge,
  Banner,
  Button,
  Field,
  HelpText,
  KeyValue,
  NumberInput,
  Panel,
  Select,
  TextInput,
  TypeIcon
} from "@/ui";
import { LawnGameHost } from "./LawnGameHost";
import { LawnOccupantList } from "./LawnOccupantList";
import { LawnStatsModal } from "./LawnStatsModal";
import {
  canEnterSpawnTargeting,
  idleInteraction,
  reduceInteraction,
  type InteractionState
} from "./interactionMode";
import { formatLastHit, lastHitFromEnvelope } from "./lawnLogFilter";
import { selectionStillValid } from "./lawnProjectorFold";
import { applyLawnSession } from "./lawnSessionFold";
import {
  isLargeCanvas,
  loadLawnViewMode,
  saveLawnViewMode,
  type LawnViewMode
} from "./lawnViewMode";
import {
  emptyLawnViewModel,
  findMarker,
  findOccupant,
  listOccupants,
  normalizePtr,
  shouldPollBoardStats,
  tilesAt
} from "./lawnViewModel";
import { PHASER_OCCUPANT_BUDGET, pickPhaserOccupants } from "./pickPhaserOccupants";

const VIEW_MODES: LawnViewMode[] = ["split", "large", "stack"];

/**
 * Lawn projector page (W6 monitor + W7 Intent/debug interact).
 *
 * Living membership comes only from event/Snapshot fold — NEVER from
 * PvzActivity rollups. Intent never writes Occupants (RT-12).
 */
export function LawnPage() {
  const events = useSyncExternalStore(
    subscribeLog,
    getLawnMembershipRing,
    getLawnMembershipRing
  );
  const hitEvent = useSyncExternalStore(
    subscribeLastHit,
    getLastHitEvent,
    getLastHitEvent
  );
  const lastHit = lastHitFromEnvelope(hitEvent);
  const sessionRef = useRef({ model: emptyLawnViewModel(), lastEventId: 0 });
  const model = useMemo(() => {
    const next = applyLawnSession(
      sessionRef.current.model,
      events,
      sessionRef.current.lastEventId
    );
    sessionRef.current = next;
    return next.model;
  }, [events]);
  const chrome = useMemo(
    () => (lastHit ? { ...model, lastHit } : model),
    [model, lastHit]
  );

  const [viewMode, setViewMode] = useState<LawnViewMode>(loadLawnViewMode);
  const large = isLargeCanvas(viewMode);
  const [statsOpen, setStatsOpen] = useState(() => isLargeCanvas(loadLawnViewMode()));
  const userClosedStats = useRef(false);
  const [inspectorOpen, setInspectorOpen] = useState(true);

  const [interaction, setInteraction] = useState<InteractionState>(idleInteraction);
  const [spawnTypeId, setSpawnTypeId] = useState(0);
  const [spawnSide, setSpawnSide] = useState<"plant" | "zombie">("zombie");
  const [statusName, setStatusName] = useState("butter");
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionOk, setActionOk] = useState<string | null>(null);

  const spawnExtra = useSpawnExtraIntent();
  const debugPost = useLawnDebugPost();
  const boardStatsPost = useLawnDebugPost();

  useEffect(() => {
    if (!shouldPollBoardStats(model.phase)) return;
    let inFlight = false;
    const tick = () => {
      if (inFlight) return;
      inFlight = true;
      boardStatsPost.mutate(
        { path: "board-stats", body: {} },
        { onSettled: () => { inFlight = false; } }
      );
    };
    tick();
    const id = window.setInterval(tick, 1500);
    return () => window.clearInterval(id);
    // Poll while InMatch or Paused; mutate identity is stable.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [model.phase]);

  useEffect(() => {
    setInteraction((prev) => {
      let next = reduceInteraction(
        prev,
        { type: "phaseChanged", phase: model.phase },
        model.phase
      );
      if (
        next.mode === "OccupantSelected" &&
        next.ptr &&
        !selectionStillValid(model, next.ptr)
      ) {
        next = idleInteraction();
      }
      return next;
    });
  }, [model]);

  const selected = interaction.ptr
    ? findOccupant(model, interaction.ptr)
    : undefined;
  const selectedMarker =
    !selected && interaction.ptr ? findMarker(model, interaction.ptr) : undefined;

  const uniqueQ = useUniqueActor(selected?.instanceId);

  const onSelect = useCallback(
    (payload: LawnSelectPayload) => {
      setInteraction((prev) => {
        if (payload.kind === "occupant" && payload.ptr) {
          return reduceInteraction(
            prev,
            {
              type: "selectOccupant",
              ptr: payload.ptr,
              row: payload.row,
              col: payload.col
            },
            model.phase
          );
        }
        if (payload.kind === "tile" && payload.row != null && payload.col != null) {
          return reduceInteraction(
            prev,
            { type: "selectTile", row: payload.row, col: payload.col },
            model.phase
          );
        }
        return prev;
      });
    },
    [model.phase]
  );

  const living = listOccupants(model);
  const livingCount = living.length;
  const picked = pickPhaserOccupants(
    living,
    PHASER_OCCUPANT_BUDGET,
    interaction.ptr
  );
  const canvasPtrs = new Set(picked.onCanvas.map((o) => normalizePtr(o.ptr)));
  const canvasCount = picked.onCanvas.length;
  const canSpawn = canEnterSpawnTargeting(model.phase);
  const targetRow = interaction.row;
  const targetCol = interaction.col;
  const hasCell = targetRow != null && targetCol != null;
  const cellTiles =
    hasCell &&
    (interaction.mode === "TileSelected" || interaction.mode === "SpawnTargeting")
      ? tilesAt(model, targetRow, targetCol)
      : [];
  const busy = spawnExtra.isPending || debugPost.isPending;

  const changeViewMode = (mode: LawnViewMode) => {
    setViewMode(mode);
    saveLawnViewMode(mode);
    if (
      isLargeCanvas(mode) &&
      !isLargeCanvas(viewMode) &&
      !userClosedStats.current
    ) {
      setStatsOpen(true);
    }
  };

  const clearMsgs = () => {
    setActionError(null);
    setActionOk(null);
  };

  const runAction = async (label: string, fn: () => Promise<unknown>) => {
    clearMsgs();
    try {
      await fn();
      setActionOk(label);
    } catch (e) {
      setActionError(e instanceof Error ? e.message : String(e));
    }
  };

  const enterTargeting = () => {
    clearMsgs();
    setInteraction((prev) =>
      reduceInteraction(prev, { type: "enterSpawnTargeting" }, model.phase)
    );
  };

  const enqueueIntentSpawn = () => {
    if (!hasCell) {
      setActionError("Select a cell first (Target cell, then click a tile).");
      setActionOk(null);
      return;
    }
    void runAction("Intent spawn enqueued", () =>
      spawnExtra.mutateAsync({
        typeId: spawnTypeId,
        row: targetRow,
        col: targetCol,
        side: spawnSide,
        reason: "lawn-w7"
      })
    );
  };

  const debugCellSpawn = () => {
    if (!hasCell) {
      setActionError("Select a cell first.");
      setActionOk(null);
      return;
    }
    const path = spawnSide === "plant" ? "spawn-plant" : "spawn-zombie";
    void runAction(`Debug ${path} enqueued`, async () => {
      await debugPost.mutateAsync({
        path: "spawn-cell",
        body: { row: targetRow, col: targetCol }
      });
      await debugPost.mutateAsync({
        path,
        body: { typeId: spawnTypeId }
      });
    });
  };

  const killSelected = () => {
    if (!selected?.ptr) return;
    const path = selected.side === "plant" ? "kill-plant" : "kill";
    void runAction("Kill enqueued", async () => {
      await debugPost.mutateAsync({
        path: "select",
        body: { ptr: selected.ptr, side: selected.side }
      });
      await debugPost.mutateAsync({
        path,
        body: { target: "selected", ptr: selected.ptr }
      });
    });
  };

  const applyStatus = () => {
    if (!selected?.ptr || selected.side !== "zombie") {
      setActionError("Apply status requires a selected zombie.");
      setActionOk(null);
      return;
    }
    void runAction("Apply status enqueued", () =>
      debugPost.mutateAsync({
        path: "apply-status",
        body: {
          status: statusName,
          duration: 4,
          level: 1,
          ptr: selected.ptr,
          target: "ptr"
        }
      })
    );
  };

  const clearStatus = () => {
    if (!selected?.ptr || selected.side !== "zombie") {
      setActionError("Clear status requires a selected zombie.");
      setActionOk(null);
      return;
    }
    void runAction("Clear status enqueued", () =>
      debugPost.mutateAsync({
        path: "clear-status",
        body: { ptr: selected.ptr, target: "ptr" }
      })
    );
  };

  const inspector = (
    <Panel title="Inspector" testId="panel-lawn-inspector">
      <KeyValue
        items={[
          { label: "Match", value: model.matchKey ?? "—" },
          { label: "Phase", value: model.phase },
          { label: "Level", value: model.levelName ?? "—" },
          { label: "Result", value: model.result ?? "—" },
          { label: "Revision", value: String(model.revision) },
          { label: "Living", value: String(livingCount) },
          {
            label: "Canvas",
            value: `${canvasCount} / ${livingCount}`
          },
          {
            label: "Economy",
            value: model.economy
              ? `sun ${model.economy.sun ?? "?"} · money ${model.economy.money ?? "?"} · wave ${model.economy.wave ?? "?"}${model.economy.hugeWave ? " · huge" : ""}`
              : "—"
          },
          {
            label: "Hand",
            value: model.hand.length
              ? model.hand
                  .map((c) => c.typeName ?? `#${c.typeId ?? "?"}`)
                  .join(", ")
              : "—"
          },
          {
            label: "Travel",
            value: model.travelBuffs.length
              ? model.travelBuffs.map((b) => `${b.kind}:${b.name}`).join(", ")
              : "—"
          },
          {
            label: "Last action",
            value: model.lastAction
              ? `${model.lastAction.kind} ${model.lastAction.summary}`
              : "—"
          },
          {
            label: "Last hit",
            value: formatLastHit(chrome.lastHit)
          },
          {
            label: "Invade",
            value: model.lastInvade
              ? `${model.lastInvade.typeName ?? model.lastInvade.ptr}`
              : "—"
          },
          { label: "Mode", value: interaction.mode }
        ]}
      />

      {(interaction.mode === "TileSelected" ||
        interaction.mode === "SpawnTargeting") &&
      hasCell ? (
        <div className="mt-3" data-testid="lawn-tile-sel">
          <p className="text-sm text-muted">
            Cell ({targetRow}, {targetCol})
            {interaction.mode === "SpawnTargeting" ? " · targeting" : ""}
          </p>
          {cellTiles.length ? (
            <KeyValue
              items={cellTiles.map((t) => ({
                label: t.typeName ?? `grid #${t.typeId}`,
                value: t.ptr
              }))}
            />
          ) : (
            <p className="mt-1 text-xs text-muted">No grid item on this cell.</p>
          )}
        </div>
      ) : null}

      <div className="mt-4 space-y-2 border-t border-border pt-3" data-testid="lawn-spawn-panel">
        <p className="text-sm font-semibold text-text">Spawn</p>
        <Field label="Side">
          <Select
            value={spawnSide}
            onChange={(e) =>
              setSpawnSide(e.target.value === "plant" ? "plant" : "zombie")
            }
            data-testid="lawn-spawn-side"
          >
            <option value="zombie">zombie</option>
            <option value="plant">plant</option>
          </Select>
        </Field>
        <Field label="typeId">
          <NumberInput
            value={spawnTypeId}
            onChange={(v) => setSpawnTypeId(Number.isFinite(v) ? v : 0)}
            data-testid="lawn-spawn-typeid"
          />
        </Field>
        <div className="flex flex-wrap gap-2">
          <Button
            size="sm"
            variant="ghost"
            disabled={!canSpawn || busy}
            onClick={enterTargeting}
            data-testid="lawn-target-cell"
          >
            Target cell
          </Button>
          <Button
            size="sm"
            disabled={!canSpawn || busy || !hasCell}
            onClick={enqueueIntentSpawn}
            data-testid="lawn-intent-spawn"
          >
            Enqueue Intent
          </Button>
          <Button
            size="sm"
            variant="ghost"
            disabled={!canSpawn || busy || !hasCell}
            onClick={debugCellSpawn}
            data-testid="lawn-debug-spawn"
          >
            Debug spawn
          </Button>
        </div>
        {!canSpawn ? (
          <HelpText>Spawn targeting disabled in {model.phase}.</HelpText>
        ) : null}
      </div>

      {selected ? (
        <div className="mt-4 space-y-2 border-t border-border pt-3" data-testid="lawn-occupant-sel">
          <div className="flex items-center gap-2">
            <TypeIcon side={selected.side} typeId={selected.typeId} size={48} />
            <div>
              <p className="font-semibold text-text">
                {selected.typeName ?? selected.side} #{selected.typeId}
              </p>
              <p className="font-mono text-xs text-muted">{selected.ptr}</p>
            </div>
          </div>
          <KeyValue
            items={[
              {
                label: "Cell",
                value:
                  selected.row != null && selected.col != null
                    ? `${selected.row}, ${selected.col}`
                    : "orphan"
              },
              {
                label: "HP",
                value:
                  selected.hp != null
                    ? `${selected.hp}/${selected.maxHp ?? "?"}`
                    : "—"
              },
              {
                label: "ATK",
                value: selected.atk != null ? String(selected.atk) : "—"
              },
              {
                label: "Armor",
                value:
                  selected.armor != null
                    ? `${selected.armor}/${selected.armorMax ?? "?"}`
                    : "—"
              },
              {
                label: "Armor2",
                value:
                  selected.armor2 != null
                    ? `${selected.armor2}/${selected.armor2Max ?? "?"}`
                    : "—"
              },
              {
                label: "Speed",
                value: selected.speed != null ? String(selected.speed) : "—"
              },
              {
                label: "Interval",
                value: selected.interval != null ? String(selected.interval) : "—"
              },
              {
                label: "Chips",
                value: selected.statusChips.length
                  ? selected.statusChips.join(", ")
                  : "—"
              },
              {
                label: "instanceId",
                value: selected.instanceId ?? "binding unknown/stale"
              },
              {
                label: "Hypno",
                value: selected.flags.hypnotized ? "yes" : "no"
              },
              {
                label: "Flags",
                value: [
                  selected.flags.mixed ? "mix" : null,
                  selected.flags.unique ? "unique" : null,
                  selected.flags.crashed ? "crash" : null
                ]
                  .filter(Boolean)
                  .join(", ") || "—"
              }
            ]}
          />

          {selected.instanceId ? (
            <div className="space-y-1" data-testid="lawn-bound-observe">
              {uniqueQ.data ? (
                <>
                  <Badge tone="ok">Bound</Badge>
                  <KeyValue
                    items={[
                      { label: "Cold phase", value: uniqueQ.data.phase },
                      { label: "Cold side", value: uniqueQ.data.side },
                      {
                        label: "Cold typeId",
                        value: String(uniqueQ.data.typeId)
                      },
                      {
                        label: "Level",
                        value: String(uniqueQ.data.level)
                      }
                    ]}
                  />
                </>
              ) : uniqueQ.isFetching ? (
                <p className="text-sm text-muted">Loading Cold UniqueActor…</p>
              ) : (
                <p className="text-sm text-muted" data-testid="lawn-bound-stale">
                  Binding unknown/stale (no UniqueActor for instanceId).
                </p>
              )}
            </div>
          ) : null}

          <div className="space-y-2" data-testid="lawn-occupant-actions">
            <p className="text-sm font-semibold text-text">Occupant actions</p>
            <Button
              size="sm"
              variant="danger"
              disabled={busy}
              onClick={killSelected}
              data-testid="lawn-kill"
            >
              Kill
            </Button>
            {selected.side === "zombie" ? (
              <>
                <Field label="Status">
                  <TextInput
                    value={statusName}
                    onChange={(e) => setStatusName(e.target.value)}
                    data-testid="lawn-status-name"
                  />
                </Field>
                <div className="flex flex-wrap gap-2">
                  <Button
                    size="sm"
                    variant="ghost"
                    disabled={busy}
                    onClick={applyStatus}
                    data-testid="lawn-apply-status"
                  >
                    Apply status
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    disabled={busy}
                    onClick={clearStatus}
                    data-testid="lawn-clear-status"
                  >
                    Clear status
                  </Button>
                </div>
              </>
            ) : null}
          </div>
        </div>
      ) : selectedMarker ? (
        <div className="mt-4 space-y-2 border-t border-border pt-3" data-testid="lawn-marker-sel">
          <p className="font-semibold text-text">
            {selectedMarker.kind} {selectedMarker.typeName ?? `#${selectedMarker.typeId}`}
          </p>
          <KeyValue
            items={[
              { label: "ptr", value: selectedMarker.ptr },
              {
                label: "Cell",
                value:
                  selectedMarker.row != null
                    ? `${selectedMarker.row}, ${selectedMarker.col ?? "—"}`
                    : "orphan"
              },
              {
                label: "Started",
                value: selectedMarker.started ? "yes" : "no"
              }
            ]}
          />
        </div>
      ) : interaction.mode === "TileSelected" ||
        interaction.mode === "SpawnTargeting" ? null : (
        <p className="mt-3 text-sm text-muted" data-testid="lawn-inspector-empty">
          Select a tile or occupant on the lawn.
        </p>
      )}

      <LawnOccupantList
        living={living}
        onCanvasPtrs={canvasPtrs}
        selectedPtr={interaction.ptr}
        onSelect={(occ) =>
          setInteraction((prev) =>
            reduceInteraction(
              prev,
              {
                type: "selectOccupant",
                ptr: occ.ptr,
                row: occ.row,
                col: occ.col
              },
              model.phase
            )
          )
        }
      />
    </Panel>
  );

  const canvas = (
    <Panel title="Projector" testId="panel-lawn-canvas">
      <LawnGameHost
        model={model}
        interaction={interaction}
        viewMode={viewMode}
        onSelect={onSelect}
      />
      <HelpText className="mt-2">
        Click a tile or occupant. Use Target cell + Spawn in the inspector for
        Intent enqueue.
      </HelpText>
      {large && !inspectorOpen ? (
        <div className="mt-2 flex flex-wrap gap-2" data-testid="lawn-action-strip">
          <Button
            size="sm"
            variant="ghost"
            onClick={() => setInspectorOpen(true)}
            data-testid="lawn-open-inspector"
          >
            Inspector
          </Button>
          <Button
            size="sm"
            variant="ghost"
            disabled={!canSpawn || busy}
            onClick={enterTargeting}
          >
            Target cell
          </Button>
          <Button
            size="sm"
            disabled={!canSpawn || busy || !hasCell}
            onClick={enqueueIntentSpawn}
          >
            Enqueue Intent
          </Button>
        </div>
      ) : null}
    </Panel>
  );

  return (
    <Page
      testId="page-lawn"
      title="Lawn"
      description="Observe projector + Intent/debug enqueue — never FE Admit or optimistic living."
      className={large ? "max-w-none" : "max-w-[1600px]"}
      actions={
        <>
          <Badge tone={model.phase === "InMatch" ? "ok" : "neutral"}>
            {model.phase}
          </Badge>
          <Badge tone="neutral">
            canvas {canvasCount} / living {livingCount}
          </Badge>
        </>
      }
    >
      <div className="mb-3 flex flex-wrap gap-2" data-testid="lawn-view-toolbar">
        {VIEW_MODES.map((mode) => (
          <Button
            key={mode}
            size="sm"
            variant={viewMode === mode ? "primary" : "ghost"}
            aria-pressed={viewMode === mode}
            onClick={() => changeViewMode(mode)}
            data-testid={`lawn-view-${mode}`}
          >
            {mode === "split" ? "Split" : mode === "large" ? "Large" : "Stack"}
          </Button>
        ))}
        <Button
          size="sm"
          variant={statsOpen ? "primary" : "ghost"}
          onClick={() =>
            setStatsOpen((v) => {
              const next = !v;
              userClosedStats.current = !next;
              return next;
            })
          }
          data-testid="lawn-stats-toggle"
        >
          Stats
        </Button>
        {large ? (
          <Button
            size="sm"
            variant={inspectorOpen ? "primary" : "ghost"}
            onClick={() => setInspectorOpen((v) => !v)}
            data-testid="lawn-inspector-toggle"
          >
            Inspector
          </Button>
        ) : null}
      </div>
      <Banner tone="info" className="mb-3">
        Living set is folded from hub events / debug snapshots — not from Activity
        rollups. Mutations enqueue only; Server/Injector Admit gates apply.
      </Banner>
      {actionError ? (
        <Banner tone="error" className="mb-3" data-testid="lawn-action-error">
          {actionError}
        </Banner>
      ) : null}
      {actionOk ? (
        <Banner tone="info" className="mb-3" data-testid="lawn-action-ok">
          {actionOk}
        </Banner>
      ) : null}
      {viewMode === "split" ? (
        <Split
          className="lg:grid-cols-[minmax(0,1.75fr)_minmax(18rem,24rem)]"
          list={canvas}
          detail={inspector}
        />
      ) : (
        <>
          {canvas}
          {inspectorOpen ? (
            <div
              className="fixed inset-0 z-40 bg-black/40"
              data-testid="lawn-inspector-drawer-backdrop"
              onClick={() => setInspectorOpen(false)}
            >
              <div
                className={cn(
                  "absolute inset-y-0 right-0 w-full max-w-md overflow-auto border-l border-border bg-panel p-3 shadow-panel"
                )}
                data-testid="lawn-inspector-drawer"
                onClick={(e) => e.stopPropagation()}
              >
                {inspector}
              </div>
            </div>
          ) : null}
        </>
      )}
      <LawnStatsModal
        open={statsOpen}
        model={chrome}
        canvasCount={canvasCount}
        onClose={() => {
          userClosedStats.current = true;
          setStatsOpen(false);
        }}
      />
    </Page>
  );
}
