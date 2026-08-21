import { useEffect, useMemo, useState } from "react";
import { usePlayers } from "@/lib/bus";
import { newCorrelationId, useDemonRoster, useSpeciesIndex } from "@/lib/bus/demons";
import {
  useCollectExpedition,
  useDemonMaterials,
  useDispatchExpedition,
  useExpeditions,
  type ExpeditionCollectDto,
  type ExpeditionRowDto,
  type ExpeditionTierDto
} from "@/lib/bus/expeditions";
import { Page } from "@/layouts/Page";
import { Badge, Banner, Button, EmptyState, Panel, TypeIcon } from "@/ui";
import { expeditionProgress, formatRemaining } from "./expeditionTime";
import { useContracts } from "@/lib/bus/contracts";
import { conditionOf, contractIndex, fieldingBlockReason } from "../demons/contractView";

/**
 * Expeditions (spec-expeditions.md): dispatch from the Active roster with tier slot gating,
 * live due timers, collect reveal battle-by-battle with event cards, materials shelf.
 */
export function ExpeditionsPage() {
  const players = usePlayers();
  const playerId = players.data?.currentPlayerId ?? 0;
  const speciesById = useSpeciesIndex();
  const roster = useDemonRoster(playerId);
  // Contracts gate dispatch server-side; showing the same rule here means the refusal never
  // arrives as a surprise error banner.
  const contractRows = contractIndex(useContracts(playerId).data);
  const expeditions = useExpeditions(playerId);
  const materials = useDemonMaterials(playerId);
  const dispatch = useDispatchExpedition();
  const collect = useCollectExpedition();

  const [tierId, setTierId] = useState<string>("scout-30m");
  const [squad, setSquad] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [reveal, setReveal] = useState<ExpeditionCollectDto | null>(null);
  const [nowMs, setNowMs] = useState(() => Date.now());

  const tiers = expeditions.data?.tiers ?? [];
  const tier = tiers.find((t) => t.tierId === tierId) ?? tiers[0];
  const items = expeditions.data?.items ?? [];
  const active = items.filter((e) => e.state === "Dispatched");
  const history = items.filter((e) => e.state !== "Dispatched").slice(0, 8);

  // The 1s clock exists only for countdown rows — don't re-render an idle page every second.
  useEffect(() => {
    if (active.length === 0) return;
    const t = setInterval(() => setNowMs(Date.now()), 1000);
    return () => clearInterval(t);
  }, [active.length]);

  const lockedIds = useMemo(() => {
    const set = new Set<string>();
    for (const e of expeditions.data?.items ?? [])
      if (e.state === "Dispatched") for (const id of e.squadInstanceIds) set.add(id);
    return set;
  }, [expeditions.data]);

  function toggle(instanceId: string) {
    setSquad((prev) =>
      prev.includes(instanceId)
        ? prev.filter((x) => x !== instanceId)
        : prev.length < (tier?.squadSlots ?? 0)
          ? [...prev, instanceId]
          : prev
    );
  }

  async function sendDispatch() {
    if (!tier || squad.length === 0) return;
    setError(null);
    try {
      await dispatch.mutateAsync({ playerId, correlationId: newCorrelationId(), tierId: tier.tierId, squad });
      setSquad([]);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  }

  async function sendCollect(expedition: ExpeditionRowDto, recall: boolean) {
    setError(null);
    try {
      const result = await collect.mutateAsync({ expeditionId: expedition.id, playerId, recall });
      setReveal(result);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  }

  function activeRow(e: ExpeditionRowDto) {
    const t = tiers.find((x) => x.tierId === e.tierId);
    const p = t
      ? expeditionProgress(e.dispatchedUtc, e.dueUtc, t.tickMinutes, t.tickCount, nowMs)
      : null;
    return (
      <div key={e.id} className="rounded-sm border border-border bg-soil-raised/40 p-3" data-testid={`expedition-${e.id}`}>
        <div className="flex items-center justify-between gap-3">
          <div>
            <p className="text-sm font-semibold">
              {t?.name ?? e.tierId}
              <Badge className="ml-2">{e.squadInstanceIds.length} demons</Badge>
              {t?.hasBossWave ? <Badge className="ml-1">boss</Badge> : null}
            </p>
            <p className="text-xs text-muted">
              {p?.due
                ? "Ready to collect"
                : `${formatRemaining(p?.remainingMs ?? 0)} remaining · tick ${p?.elapsedTicks ?? 0}/${t?.tickCount ?? 0}`}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <Button size="sm" onClick={() => void sendCollect(e, false)} disabled={!p?.due || collect.isPending}>
              Collect
            </Button>
            <Button size="sm" variant="ghost" onClick={() => void sendCollect(e, true)} disabled={collect.isPending}>
              Recall
            </Button>
          </div>
        </div>
        <div className="mt-2 h-1.5 overflow-hidden rounded bg-soil-sunken">
          <div className="h-full bg-leaf" style={{ width: `${Math.round((p?.progress ?? 0) * 100)}%` }} />
        </div>
      </div>
    );
  }

  function tickCard(tick: ExpeditionCollectDto["ticks"][number]) {
    const battle = reveal?.battles.find((b) => b.battleIndex === tick.battleIndex);
    const label =
      tick.kind === "battle" || tick.kind === "boss-battle"
        ? `${tick.kind === "boss-battle" ? "Boss battle" : "Battle"} — ${battle?.outcome ?? "?"}`
        : tick.kind === "found-souls"
          ? `Found ${tick.souls} Souls`
          : tick.kind === "wild-demon-met"
            ? tick.wildJoins
              ? `A wild ${speciesById.get(tick.wildSpeciesId ?? "")?.name ?? tick.wildSpeciesId} joined!`
              : `Met a wild ${speciesById.get(tick.wildSpeciesId ?? "")?.name ?? tick.wildSpeciesId} — it slipped away`
            : tick.kind === "injury"
              ? "A squad member was injured"
              : "Quiet march";
    return (
      <li key={tick.tickIndex} className="flex items-center gap-2 text-sm">
        <span className="w-14 shrink-0 text-xs text-muted">tick {tick.tickIndex}</span>
        <span className={tick.kind === "quiet" ? "text-muted" : ""}>{label}</span>
      </li>
    );
  }

  return (
    <Page title="Expeditions" description="Send demon squads into the rifts — timers run without the game.">
      {error ? <Banner tone="error">{error}</Banner> : null}

      {reveal ? (
        <Panel title={`Expedition ${reveal.state === "Recalled" ? "recalled" : "collected"}`}>
          <ul className="space-y-1">{reveal.ticks.map(tickCard)}</ul>
          <div className="mt-3 flex flex-wrap gap-2 text-sm">
            <Badge>+{reveal.soulsAwarded} Souls</Badge>
            {reveal.materials.map((m) => (
              <Badge key={m.materialId}>
                {m.materialId} ×{m.qty}
              </Badge>
            ))}
            {reveal.specimenXp.map((x) => (
              <Badge key={x.instanceId}>+{Math.round(x.xp)} XP</Badge>
            ))}
          </div>
          {reveal.wildJoins.length > 0 ? (
            <p className="mt-2 text-sm">
              New recruits: {reveal.wildJoins.map((w) => w.profile.speciesId).join(", ")}
            </p>
          ) : null}
          <Button className="mt-3" size="sm" variant="ghost" onClick={() => setReveal(null)}>
            Close
          </Button>
        </Panel>
      ) : null}

      <Panel title="Active expeditions">
        {active.length === 0 ? (
          <EmptyState title="No expeditions out" hint="Pick a tier and a squad below to dispatch one." />
        ) : (
          <div className="space-y-2">{active.map(activeRow)}</div>
        )}
      </Panel>

      <Panel title="Dispatch">
        <div className="mb-3 flex flex-wrap gap-2">
          {tiers.map((t: ExpeditionTierDto) => (
            <Button
              key={t.tierId}
              size="sm"
              variant={t.tierId === tier?.tierId ? "primary" : "ghost"}
              onClick={() => {
                setTierId(t.tierId);
                setSquad([]);
              }}
            >
              {t.name} · {t.durationMinutes >= 60 ? `${t.durationMinutes / 60}h` : `${t.durationMinutes}m`} ·{" "}
              {t.squadSlots} slots
            </Button>
          ))}
        </div>
        <div className="grid gap-2 sm:grid-cols-2">
          {(roster.data?.items ?? []).map((s) => {
            const id = s.profile.instanceId;
            const species = speciesById.get(s.profile.speciesId);
            const onExpedition = lockedIds.has(id);
            const contractBlock = fieldingBlockReason(contractRows[id]);
            const blocked = onExpedition || contractBlock !== null;
            const picked = squad.includes(id);
            return (
              <button
                key={id}
                type="button"
                disabled={blocked}
                title={contractBlock ?? undefined}
                onClick={() => toggle(id)}
                className={`flex items-center gap-2 rounded-sm border p-2 text-left text-sm ${
                  picked ? "border-leaf" : "border-border"
                } ${blocked ? "opacity-40" : "cursor-pointer"}`}
                data-testid={`squad-pick-${id}`}
              >
                {species ? <TypeIcon side={species.side} typeId={species.gameTypeId} size={28} /> : null}
                <span className="min-w-0 flex-1 truncate">
                  {s.profile.nickname ?? species?.name ?? s.profile.speciesId} · L{s.actor.level}
                </span>
                {s.profile.star > 0 ? (
                  <span className="text-xs text-amber-300">{"★".repeat(s.profile.star)}</span>
                ) : null}
                {onExpedition ? (
                  <Badge>on expedition</Badge>
                ) : contractBlock ? (
                  <Badge data-testid={`squad-blocked-${id}`}>{conditionOf(contractRows[id])}</Badge>
                ) : picked ? (
                  <Badge>picked</Badge>
                ) : null}
              </button>
            );
          })}
        </div>
        <div className="mt-3 flex items-center gap-3">
          <Button
            onClick={() => void sendDispatch()}
            disabled={!tier || squad.length === 0 || dispatch.isPending}
          >
            Dispatch {squad.length}/{tier?.squadSlots ?? 0}
          </Button>
          <span className="text-xs text-muted">
            {tier
              ? `${tier.battleCount}${tier.hasBossWave ? "+boss" : ""} battles over ${tier.tickCount} ticks`
              : ""}
          </span>
        </div>
      </Panel>

      <Panel title="Materials shelf">
        {(materials.data?.items ?? []).length === 0 ? (
          <EmptyState title="No materials yet" hint="Expedition battles and encounters drop essences and shards." />
        ) : (
          <div className="flex flex-wrap gap-2">
            {materials.data!.items.map((m) => (
              <Badge key={m.materialId}>
                {m.materialId} ×{m.qty}
              </Badge>
            ))}
          </div>
        )}
      </Panel>

      {history.length > 0 ? (
        <Panel title="Recent">
          <ul className="space-y-1 text-sm">
            {history.map((e) => (
              <li key={e.id} className="flex items-center gap-2">
                <Badge>{e.state}</Badge>
                <span>{tiers.find((t) => t.tierId === e.tierId)?.name ?? e.tierId}</span>
                <span className="text-xs text-muted">{e.collectedUtc ?? ""}</span>
              </li>
            ))}
          </ul>
        </Panel>
      ) : null}
    </Page>
  );
}
