import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import {
  getLastHitEvent,
  getLawnMembershipRing,
  subscribeLastHit,
  subscribeLog,
  useCommanders,
  useDeployUniqueActor,
  useLawnDebugPost,
  usePlayers,
  useSetDefaultCommander,
  useSpawnExtraIntent,
  useUniqueActor
} from "@/lib/bus";
import type { LawnSelectPayload } from "@/game/EventBus";
import { cn } from "@/lib/cn";
import { claimStageEscape } from "@/shell/keymap";
import { useDevModeLive } from "@/dev/useDevModeLive";
import { adaptCommanderSheet } from "@/contract/adapt";
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
import { ActorHudInspector } from "./ActorHudInspector";
import { LawnHud } from "./LawnHud";
import { LawnOccupantList } from "./LawnOccupantList";
import { LawnStatsModal } from "./LawnStatsModal";
import { ActorPanel, type ActorRungState } from "@/ui/actor";
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
  const navigate = useNavigate();
  const players = usePlayers();
  const playerId = players.data?.currentPlayerId ?? 1;
  const commandersQuery = useCommanders(playerId);
  const setDefaultCommander = useSetDefaultCommander(playerId);
  const [commanderSheetOpen, setCommanderSheetOpen] = useState(false);
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
  const boardStatsPost = useLawnDebugPost({ silent: true });

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

  // T28 — the debug Spawn/Inspector/toolbar apparatus below stays exactly as it is (GG-41: a
  // developer surface doesn't get deleted for a player-facing pass), gated behind the same
  // developer-mode flag T12 already built rather than shown to every player by default. The new
  // `LawnHud` and the deploy-targeting banner (T22, a real player feature) are unconditional.
  const devMode = useDevModeLive();
  const living = listOccupants(model);
  const livingCount = living.length;
  const deployedChips = useMemo(
    () =>
      living
        .filter((o) => o.side === "plant")
        .map((o) => ({ ptr: o.ptr, side: o.side, typeId: o.typeId, typeName: o.typeName })),
    [living]
  );
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
  const matchCommanderChip = model.matchCommander;
  const commanderListRow = matchCommanderChip
    ? commandersQuery.data?.commanders.find((row) => row.id === matchCommanderChip.id)
    : undefined;
  const commanderSheetState: ActorRungState | null =
    commanderListRow && matchCommanderChip
      ? { kind: "ready", data: adaptCommanderSheet(commanderListRow, playerId) }
      : null;

  async function handleCommanderSetDefault(commanderId: string) {
    await setDefaultCommander.mutateAsync(commanderId);
  }

  useEffect(() => {
    if (!matchCommanderChip) setCommanderSheetOpen(false);
  }, [matchCommanderChip]);

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

  // T22 — deploying a real, player-owned creature (Creatures layer's "Deploy to the lawn"),
  // distinct from the debug Spawn panel below: reuses the same SpawnTargeting FSM and the same
  // ghost-preview board (interactionMode.ts, LawnWorldScene) rather than inventing a second one,
  // but confirms through the real `useDeployUniqueActor` endpoint instead of the debug intent
  // paths — nothing is spent and no occupant exists until the server actually admits it (the
  // mutation only invalidates on success; there is no optimistic write to react to before that).
  const [searchParams, setSearchParams] = useSearchParams();
  const deployInstanceId = searchParams.get("deploy");
  const deployActorQ = useUniqueActor(deployInstanceId);
  // UniqueActorDto has no resolved display name (T4/T8's honest-Pending precedent — names
  // resolve from the almanac catalog, not wired to any reader yet); build the label from what's
  // actually real (side, typeId, level) rather than fabricating one.
  const deployActor = deployActorQ.data;
  const deployActorName = deployActor
    ? `${deployActor.side === "zombie" ? "zombie" : "plant"} #${deployActor.typeId} (Lv ${deployActor.level})`
    : "your creature";
  const deployMutation = useDeployUniqueActor();

  const clearDeploy = useCallback(() => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.delete("deploy");
      return next;
    });
  }, [setSearchParams]);

  useEffect(() => {
    if (!deployInstanceId) return;
    setInteraction((prev) =>
      reduceInteraction(prev, { type: "enterSpawnTargeting" }, model.phase)
    );
    // Only re-arm when the target itself changes — model.phase changes on every board tick.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [deployInstanceId]);

  // GG-6: Esc must cancel deploy-targeting rather than falling through to the empty-stack
  // System fallback. This isn't a PanelShell layer (it's stage chrome, per the plate), so it
  // registers directly on the real stack — the same single mechanism `handleEscape()` already
  // walks, not a second Escape-handling path.
  useEffect(() => {
    if (!deployInstanceId) return undefined;
    return claimStageEscape("lawn-deploy-targeting", cancelDeploy);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [deployInstanceId]);

  const confirmDeploy = () => {
    if (!deployInstanceId || !hasCell) return;
    void runAction(`Deployed ${deployActorName}`, async () => {
      await deployMutation.mutateAsync({
        instanceId: deployInstanceId,
        col: targetCol,
        row: targetRow,
        matchKey: model.matchKey ?? undefined
      });
      clearDeploy();
      setInteraction(idleInteraction());
    });
  };

  const cancelDeploy = () => {
    clearMsgs();
    clearDeploy();
    setInteraction(idleInteraction());
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

  const combatHitSelected = () => {
    if (!selected?.ptr) return;
    void runAction("Fire-synthetic enqueued", async () => {
      await debugPost.mutateAsync({
        path: "select",
        body: { ptr: selected.ptr, side: selected.side }
      });
      await debugPost.mutateAsync({
        path: "effect/fire-synthetic",
        body: { trigger: "OnDamageDealt", side: "plant", targetPtr: selected.ptr }
      });
    });
  };

  const combatDeltaSelected = () => {
    if (!selected?.ptr) return;
    void runAction("Enqueue-delta enqueued", async () => {
      await debugPost.mutateAsync({
        path: "select",
        body: { ptr: selected.ptr, side: selected.side }
      });
      await debugPost.mutateAsync({
        path: "effect/enqueue-delta",
        body: { amount: -50, targetPtr: selected.ptr }
      });
    });
  };

  const probeShaders = () => {
    void runAction("Shader probe enqueued", async () => {
      await debugPost.mutateAsync({ path: "fx/probe-shaders", body: {} });
    });
  };

  const cellFlash = () => {
    if (!hasCell) {
      setActionError("Select a cell first.");
      setActionOk(null);
      return;
    }
    void runAction("Cell flash enqueued", async () => {
      await debugPost.mutateAsync({
        path: "fx/world-flash",
        body: { row: targetRow, col: targetCol }
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
            title={busy ? "Working…" : !canSpawn ? `Spawn targeting disabled in ${model.phase}` : undefined}
            onClick={enterTargeting}
            data-testid="lawn-target-cell"
          >
            Target cell
          </Button>
          <Button
            size="sm"
            disabled={!canSpawn || busy || !hasCell}
            title={
              busy
                ? "Working…"
                : !canSpawn
                  ? `Spawn targeting disabled in ${model.phase}`
                  : !hasCell
                    ? "Target a cell first"
                    : undefined
            }
            onClick={enqueueIntentSpawn}
            data-testid="lawn-intent-spawn"
          >
            Enqueue Intent
          </Button>
          <Button
            size="sm"
            variant="ghost"
            disabled={!canSpawn || busy || !hasCell}
            title={
              busy
                ? "Working…"
                : !canSpawn
                  ? `Spawn targeting disabled in ${model.phase}`
                  : !hasCell
                    ? "Target a cell first"
                    : undefined
            }
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

      <div className="mt-4 space-y-2 border-t border-border pt-3" data-testid="lawn-overlay-fx">
        <p className="text-sm font-semibold text-text">Overlay FX</p>
        <div className="flex flex-wrap gap-2">
          <Button
            size="sm"
            variant="ghost"
            disabled={busy}
            title={busy ? "Working…" : undefined}
            onClick={probeShaders}
            data-testid="lawn-probe-shaders"
          >
            Probe shaders
          </Button>
          <Button
            size="sm"
            variant="ghost"
            disabled={busy || !hasCell}
            title={busy ? "Working…" : !hasCell ? "Target a cell first" : undefined}
            onClick={cellFlash}
            data-testid="lawn-cell-flash"
          >
            Cell flash
          </Button>
        </div>
        <HelpText>
          World quad at the selected cell. HP still uses overlay hit / HP −50.
        </HelpText>
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
          {selected.hud ? <ActorHudInspector hud={selected.hud} /> : null}
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
              ...(selected.hud
                ? []
                : [
                    {
                      label: "Shield",
                      value:
                        selected.rpgShield != null
                          ? `${selected.rpgShield}/${selected.rpgShieldMax ?? "?"}`
                          : "—"
                    }
                  ]),
              {
                label: "Speed",
                value: selected.speed != null ? String(selected.speed) : "—"
              },
              {
                label: "Interval",
                value: selected.interval != null ? String(selected.interval) : "—"
              },
              ...(selected.hud
                ? []
                : [
                    {
                      label: "Chips",
                      value: selected.statusChips.length
                        ? selected.statusChips.join(", ")
                        : "—"
                    }
                  ]),
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
              title={busy ? "Working…" : undefined}
              onClick={killSelected}
              data-testid="lawn-kill"
            >
              Kill
            </Button>
            <Button
              size="sm"
              variant="ghost"
              disabled={busy}
              title={busy ? "Working…" : undefined}
              onClick={combatHitSelected}
              data-testid="lawn-combat-hit"
            >
              Overlay hit
            </Button>
            <Button
              size="sm"
              variant="ghost"
              disabled={busy}
              title={busy ? "Working…" : undefined}
              onClick={combatDeltaSelected}
              data-testid="lawn-combat-delta"
            >
              HP −50
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
                    title={busy ? "Working…" : undefined}
                    onClick={applyStatus}
                    data-testid="lawn-apply-status"
                  >
                    Apply status
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    disabled={busy}
                    title={busy ? "Working…" : undefined}
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
            title={busy ? "Working…" : !canSpawn ? `Spawn targeting disabled in ${model.phase}` : undefined}
            onClick={enterTargeting}
          >
            Target cell
          </Button>
          <Button
            size="sm"
            disabled={!canSpawn || busy || !hasCell}
            title={
              busy
                ? "Working…"
                : !canSpawn
                  ? `Spawn targeting disabled in ${model.phase}`
                  : !hasCell
                    ? "Target a cell first"
                    : undefined
            }
            onClick={enqueueIntentSpawn}
          >
            Enqueue Intent
          </Button>
        </div>
      ) : null}
    </Panel>
  );

  const devToolbarAndInspector = (
    <>
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
              className="band-panel fixed inset-0 bg-black/40"
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
    </>
  );

  return (
    <>
    <Page
      testId="page-lawn"
      title="Lawn"
      description={devMode ? "Observe projector + Intent/debug enqueue — never FE Admit or optimistic living." : undefined}
      className={devMode && large ? "max-w-none" : "max-w-[1600px]"}
      actions={
        devMode ? (
          <>
            <Badge tone={model.phase === "InMatch" ? "ok" : "neutral"}>{model.phase}</Badge>
            <Badge tone="neutral">canvas {canvasCount} / living {livingCount}</Badge>
          </>
        ) : undefined
      }
    >
      <LawnHud
        sun={model.economy?.sun}
        wave={model.economy?.wave}
        maxWave={model.economy?.maxWave}
        hugeWave={model.economy?.hugeWave}
        matchCommander={matchCommanderChip}
        deployed={deployedChips}
        onOpenCommanderSheet={
          matchCommanderChip && commanderListRow ? () => setCommanderSheetOpen(true) : undefined
        }
      />

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
      {deployInstanceId ? (
        // Stage chrome (plate 07 §B): no scrim, the board stays fully visible and interactive
        // underneath — this is an inline banner, never a Dialog/layer. Unconditional — deploying a
        // real, owned creature (T22) is a player feature, not diagnostic tooling.
        <Banner tone="info" className="mb-3 flex flex-wrap items-center justify-between gap-2" data-testid="lawn-deploy-banner">
          <span>
            Choosing a place for {deployActorName}
            {hasCell ? ` — cell (${targetRow}, ${targetCol})` : ""}
          </span>
          <span className="flex gap-2">
            <Button size="sm" variant="ghost" onClick={cancelDeploy} data-testid="lawn-deploy-cancel">
              Cancel
            </Button>
            <Button
              size="sm"
              disabled={!hasCell || deployMutation.isPending}
              title={deployMutation.isPending ? "Deploying…" : !hasCell ? "Click a cell on the board first" : undefined}
              onClick={confirmDeploy}
              data-testid="lawn-deploy-confirm"
            >
              Deploy here
            </Button>
          </span>
        </Banner>
      ) : null}

      {devMode ? (
        devToolbarAndInspector
      ) : (
        <div data-testid="lawn-canvas-plain">
          <LawnGameHost model={model} interaction={interaction} viewMode="large" onSelect={onSelect} />
        </div>
      )}
    </Page>

    {commanderSheetState && commanderListRow && matchCommanderChip ? (
      <ActorPanel
        state={commanderSheetState}
        open={commanderSheetOpen}
        onOpenChange={setCommanderSheetOpen}
        role="commander"
        matchBanner={{
          displayName: matchCommanderChip.displayName,
          auraDisplayName: matchCommanderChip.auraDisplayName
        }}
        commanderMeta={{
          isDefault: commanderListRow.isDefault,
          activeAuraName: commanderListRow.activeAuraName,
          locationStub: commanderListRow.locationStub,
          legionStub: commanderListRow.legionStub
        }}
        setDefaultPending={setDefaultCommander.isPending}
        onSetDefault={() => void handleCommanderSetDefault(commanderListRow.id)}
        onDefendLawn={() => setCommanderSheetOpen(false)}
        onOpenCommandersList={() => {
          setCommanderSheetOpen(false);
          navigate(`/sanctum?panel=commanders&sel=${encodeURIComponent(commanderListRow.id)}`);
        }}
      />
    ) : null}
    </>
  );
}
