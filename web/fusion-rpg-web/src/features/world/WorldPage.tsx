import { memo, useEffect, useMemo, useReducer, useRef, useState } from "react";
import { ReactFlow, Background, Controls, MiniMap, type NodeMouseHandler } from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { usePlayers } from "@/lib/bus";
import {
  useCommitWorldTurn,
  useSubmitWorldCommands,
  useWorldHeader,
  useWorldState,
  useWorldTurnReport
} from "@/lib/bus/world";
import { Page } from "@/layouts/Page";
import { cn } from "@/lib/cn";
import { Badge, Banner, Button, KeyValue, Panel } from "@/ui";
import firstLight from "./fixtures/first-light.json";
import { LaneEdge } from "./LaneEdge";
import { LoamGauge } from "./LoamGauge";
import { SectorNode, type SectorNodeProps } from "./SectorNode";
import { SectorPanel } from "./SectorPanel";
import { toCommanderIntents } from "./commanderIntent";
import { stepPlayback, toKeyframes } from "./turnPlayback";
import type { WorldStateDto } from "./worldTypes";
import { summarizeLoam, toGraph } from "./worldViewModel";
import {
  initialWorldUi,
  orderId,
  routeForLegion,
  toRequests,
  worldUiReducer,
  type PendingOrder
} from "./worldSelection";

/**
 * `#/world` — the graph map (world-map-program.md).
 *
 * Live when the player has a world, and falling back to the checked-in `first-light` fixture when
 * they do not, so the map is still worth opening against a server with nothing in it. An E2E test
 * asserts the fixture is exactly what the state endpoint returns, so the two never drift.
 *
 * `nodeTypes` and `edgeTypes` are module-scope constants: React Flow re-mounts every custom node if
 * those object identities change between renders.
 */
const nodeTypes = { sector: SectorNode };

// A second module-scope map rather than a flag on the node data: React Flow re-mounts every custom
// node when the `nodeTypes` object identity changes, so both maps have to be constants. Toggling
// swaps between two stable maps, which costs one re-mount — a deliberate button press, not a pan.
const SectorNodeWithLifelines = memo((props: SectorNodeProps) => <SectorNode {...props} showLifelines />);
SectorNodeWithLifelines.displayName = "SectorNodeWithLifelines";
const lifelineNodeTypes = { sector: SectorNodeWithLifelines };
const edgeTypes = { lane: LaneEdge };

const fixtureWorld = firstLight as WorldStateDto;

export function WorldPage() {
  const players = usePlayers();
  const playerId = players.data?.currentPlayerId ?? 0;
  const header = useWorldHeader(playerId);
  const worldId = header.data?.worldId ?? null;

  // Declared before the query that reads it: `const` is in the temporal dead zone until its own
  // line, so a `useState` further down would throw on first render rather than merely read stale.
  const [lifelines, setLifelines] = useState(false);
  const live = useWorldState(worldId, { lifelines });

  const world = live.data ?? fixtureWorld;
  const isLive = live.data != null;

  const [ui, dispatch] = useReducer(worldUiReducer, initialWorldUi);
  const [frameIndex, setFrameIndex] = useState(0);
  const [notice, setNotice] = useState<string | null>(null);

  const submit = useSubmitWorldCommands(worldId);
  const commit = useCommitWorldTurn(worldId);

  // The last turn that actually resolved; turn 0 has no report until the first commit.
  const lastTurn = world.currentTurn - 1;
  const report = useWorldTurnReport(worldId, isLive && lastTurn >= 0 ? lastTurn : null);
  const frames = useMemo(() => toKeyframes(report.data), [report.data]);
  const intents = useMemo(() => toCommanderIntents(report.data), [report.data]);

  // The world as it was before the last turn resolved: what makes a march slide along its lane
  // rather than snap to wherever the turn left it.
  const previousWorld = useRef<WorldStateDto | null>(null);
  const graph = useMemo(() => toGraph(world, previousWorld.current), [world]);
  useEffect(() => {
    previousWorld.current = world;
  }, [world]);
  const selected = useMemo(
    () => graph.nodes.find((n) => n.id === ui.selectedSectorId)?.data ?? null,
    [graph, ui.selectedSectorId]
  );
  const legion = useMemo(
    () => world.entities.find((e) => e.entityId === ui.selectedEntityId) ?? null,
    [world, ui.selectedEntityId]
  );
  const loam = useMemo(() => summarizeLoam(graph.nodes.map((n) => n.data)), [graph]);

  const onNodeClick: NodeMouseHandler = (_, node) => dispatch({ type: "select-sector", sectorId: node.id });

  const queue = (order: PendingOrder) => {
    dispatch({ type: "queue", order });
    setNotice(null);
  };

  const queueMove = () => {
    if (!legion || !selected) return;

    // A legion in mid-stride keeps the lane it is on at the head of the route — see routeForLegion.
    const lanePath = routeForLegion(graph, legion, selected.sectorId);
    if (!lanePath) return setNotice(`no open route to ${selected.sectorId}`);

    queue({
      commandId: orderId(world.currentTurn, "move", legion.entityId),
      kind: "move",
      entityId: legion.entityId,
      lanePath,
      label: `${legion.entityId} → ${selected.label} (${lanePath.length} lane${lanePath.length > 1 ? "s" : ""})`
    });
  };

  const queueClear = (slotIndex: number) => {
    if (!legion || !selected) return;
    queue({
      commandId: orderId(world.currentTurn, `clear${slotIndex}`, legion.entityId),
      kind: "clear",
      entityId: legion.entityId,
      sectorId: selected.sectorId,
      slotIndex,
      label: `${legion.entityId} clears ${selected.label} slot ${slotIndex}`
    });
  };

  const queueClaim = () => {
    if (!legion || !selected) return;
    queue({
      commandId: orderId(world.currentTurn, "claim", legion.entityId),
      kind: "claim",
      entityId: legion.entityId,
      sectorId: selected.sectorId,
      label: `${legion.entityId} claims ${selected.label}`
    });
  };

  // Both writes report their own failure rather than letting the rejection escape: a dropped
  // connection has to look like something on screen, not like a button that did nothing.
  const sendOrders = async () => {
    if (!worldId || ui.pending.length === 0) return;
    try {
      const result = await submit.mutateAsync({ commands: toRequests(ui.pending) });
      const refused = result.results.filter((r) => !r.ok);
      setNotice(
        refused.length === 0
          ? `${result.results.length} order(s) filed`
          : refused.map((r) => `${r.commandId}: ${r.reason}`).join(" · ")
      );
      if (refused.length === 0) dispatch({ type: "clear-queue" });
    } catch (err) {
      setNotice(`orders not filed — ${(err as Error).message}`);
    }
  };

  const endTurn = async () => {
    if (!worldId) return;
    try {
      const result = await commit.mutateAsync({ turn: world.currentTurn });
      setNotice(
        result.advanced
          ? `turn ${result.currentTurn - 1} resolved`
          : "turn ended — waiting on the other commanders"
      );
      if (result.advanced) setFrameIndex(0);
    } catch (err) {
      setNotice(`turn not ended — ${(err as Error).message}`);
    }
  };

  const focus = frames[frameIndex]?.focusId ?? null;

  return (
    <Page
      title="World"
      description={`${world.templateId} · turn ${world.currentTurn}${isLive ? "" : " · sample map (no world yet)"}`}
      className="max-w-none"
      testId="world-page"
      actions={
        isLive ? (
          <>
            <Button onClick={sendOrders} disabled={ui.pending.length === 0 || submit.isPending}>
              File {ui.pending.length} order{ui.pending.length === 1 ? "" : "s"}
            </Button>
            <Button onClick={endTurn} disabled={commit.isPending} data-testid="end-turn">
              End turn
            </Button>
            <Button onClick={() => setLifelines((v) => !v)} data-testid="toggle-lifelines">
              {lifelines ? "Hide lifelines" : "Lifelines"}
            </Button>
          </>
        ) : null
      }
    >
      {notice ? <Banner tone="info">{notice}</Banner> : null}
      {!isLive ? (
        <Banner tone="info">
          No world for this player yet — this is the checked-in <code>first-light</code> sample. Create one
          from the Sim page to play.
        </Banner>
      ) : null}

      <div className="grid gap-3 lg:grid-cols-[1fr_300px]">
        <Panel className="h-[620px] p-0" testId="world-canvas">
          <ReactFlow
            nodes={graph.nodes}
            edges={graph.edges}
            nodeTypes={lifelines ? lifelineNodeTypes : nodeTypes}
            edgeTypes={edgeTypes}
            onNodeClick={onNodeClick}
            // The map is authored, not drawn: positions come from the world, and nothing here may
            // move or connect a sector.
            nodesDraggable={false}
            nodesConnectable={false}
            elementsSelectable
            edgesFocusable={false}
            fitView
            minZoom={0.3}
            maxZoom={1.8}
          >
            <Background gap={32} size={1} />
            <Controls showInteractive={false} />
            <MiniMap pannable zoomable className="!bg-soil" />
          </ReactFlow>
        </Panel>

        <div>
          <Panel title="Loam" testId="world-loam-gauge">
            <LoamGauge summary={loam} />
          </Panel>

          <Panel title="Sector" testId="world-inspector">
            {selected == null ? (
              <p className="text-sm text-muted">Pick a sector on the map.</p>
            ) : (
              <div className="space-y-2">
                <div className="flex items-center gap-2">
                  <span className="font-display text-text">{selected.label}</span>
                  <Badge>{selected.ownership}</Badge>
                </div>
                <SectorPanel sector={selected} />
                <KeyValue
                  items={[
                    { label: "Type", value: selected.typeId },
                    { label: "Climate", value: selected.climate ?? "—" },
                    { label: "Phase", value: selected.phase },
                    { label: "Intel", value: selected.intel },
                    { label: "Danger", value: String(selected.dangerBand) },
                    { label: "Owner", value: selected.ownerFactionId ?? "unclaimed" },
                    {
                      label: "Guards left",
                      value: String(selected.slots.filter((s) => s.guard === "intact").length)
                    },
                    { label: "Claimable", value: selected.claimable ? "yes" : "no" },
                    { label: "Last seen", value: selected.unknown ? "never" : selected.age === 0 ? "now" : `${selected.age} turns ago` },
                    {
                      label: "Lifeline",
                      value: selected.lifeline
                        ? "losing this splits your territory"
                        : selected.lifelineCost > 0
                          ? "a route runs through it"
                          : "—"
                    }
                  ]}
                />

                <div className="pt-1 text-xs text-muted">Slots</div>
                <ul className="space-y-0.5 text-xs text-text" data-testid="inspector-slots">
                  {selected.slots.map((slot) => (
                    <li key={slot.slotIndex} className="flex items-center justify-between gap-2">
                      <span>
                        {slot.slotIndex}. {slot.slotTypeId}
                        {slot.element ? ` (${slot.element})` : ""}
                      </span>
                      {isLive && legion && slot.guard === "intact" ? (
                        <button
                          className="rounded border border-border px-1.5 text-[10px] text-muted hover:text-text"
                          onClick={() => queueClear(slot.slotIndex)}
                        >
                          clear
                        </button>
                      ) : null}
                    </li>
                  ))}
                </ul>

                {selected.forces.length > 0 ? (
                  <>
                    <div className="pt-1 text-xs text-muted">Forces — pick one to give it orders</div>
                    <ul className="space-y-1 text-xs" data-testid="inspector-forces">
                      {selected.forces.map((force) => {
                        const picked = force.entityId === ui.selectedEntityId;
                        return (
                          <li key={force.entityId}>
                            {/* A real control, not a line of text that happens to be clickable. The
                                first version was a bare <button> styled `text-muted`, which read as
                                a label — the one person who tried it could not find the picker. */}
                            <button
                              type="button"
                              aria-pressed={picked}
                              data-testid={`pick-force-${force.entityId}`}
                              onClick={() => dispatch({ type: "select-entity", entityId: force.entityId })}
                              className={cn(
                                "w-full cursor-pointer rounded-sm border px-2 py-1 text-left transition-colors",
                                picked
                                  ? "border-lawn bg-lawn/15 text-text"
                                  : "border-border text-muted hover:border-lawn hover:text-text"
                              )}
                            >
                              <span className="font-semibold">{picked ? "▸ " : ""}{force.entityId}</span>
                              <span className="text-muted"> — {force.strength} hp</span>
                              {force.routed ? <span className="text-bad"> (routed)</span> : null}
                            </button>
                          </li>
                        );
                      })}
                    </ul>
                  </>
                ) : null}

                {isLive ? (
                  <div className="space-y-1 pt-2">
                    <div className="flex flex-wrap gap-2">
                      {/* Shown disabled rather than hidden. An absent button teaches nothing: you
                          cannot tell "not allowed here" from "you missed a step". */}
                      <Button
                        onClick={queueMove}
                        disabled={!legion}
                        title={legion ? undefined : "Pick a force first — click the sector it is standing in"}
                      >
                        March here
                      </Button>
                      <Button onClick={queueClaim} disabled={!legion || !selected.claimable}>
                        Claim
                      </Button>
                    </div>
                    <p className="text-xs text-muted" data-testid="order-hint">
                      {legion
                        ? `${legion.entityId} is selected — click a sector, then March here.`
                        : "No force selected. Click the sector one of yours is standing in, then pick it above."}
                    </p>
                  </div>
                ) : null}
              </div>
            )}
          </Panel>

          {isLive ? (
            <Panel title={`Orders (${ui.pending.length})`} testId="world-orders">
              {ui.pending.length === 0 ? (
                <p className="text-sm text-muted">Nothing queued for this turn.</p>
              ) : (
                <ul className="space-y-1 text-xs text-text">
                  {ui.pending.map((order) => (
                    <li key={order.commandId} className="flex items-center justify-between gap-2">
                      <span>{order.label}</span>
                      <button
                        className="text-muted hover:text-text"
                        onClick={() => dispatch({ type: "unqueue", commandId: order.commandId })}
                      >
                        ×
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </Panel>
          ) : null}

          {intents.length > 0 ? (
            <Panel title="What they were thinking" testId="world-intents">
              <ul className="space-y-1 text-xs" data-testid="intent-list">
                {intents.map((intent) => (
                  <li key={`${intent.commanderId}/${intent.commandId}`} data-testid={`intent-${intent.commanderId}`}>
                    <span className="text-text">{intent.action}</span>
                    <span className="text-muted"> — {intent.reason}</span>
                  </li>
                ))}
              </ul>
            </Panel>
          ) : null}

          {frames.length > 0 ? (
            <Panel title={`Turn ${lastTurn}`} testId="world-playback">
              <div className="mb-2 flex items-center gap-2">
                <Button onClick={() => setFrameIndex((i) => stepPlayback(i, frames, -1))}>Back</Button>
                <Button onClick={() => setFrameIndex((i) => stepPlayback(i, frames, 1))}>Next</Button>
                <span className="text-xs text-muted">
                  {frameIndex + 1} / {frames.length}
                  {focus ? ` · ${focus}` : ""}
                </span>
              </div>
              <ol className="space-y-0.5 text-xs" data-testid="playback-rail">
                {frames.map((frame) => (
                  <li
                    key={frame.index}
                    data-active={frame.index === frameIndex}
                    className={frame.index === frameIndex ? "text-text" : "text-muted"}
                  >
                    <span className="opacity-60">{frame.phase}</span> {frame.text}
                  </li>
                ))}
              </ol>
            </Panel>
          ) : null}
        </div>
      </div>
    </Page>
  );
}
