import { useMemo, useState } from "react";
import { usePlayers } from "@/lib/bus";
import {
  newCorrelationId,
  useDemonCatalog,
  useDemonCodex,
  useDemonRoster,
  useSetDemonLocked,
  useSetDemonNickname,
  useSummon,
  useSummonState,
  type DemonSpecimenDto,
  type SummonOutcomeDto
} from "@/lib/bus/demons";
import { Page } from "@/layouts/Page";
import { Badge, Banner, Button, EmptyState, Panel, TabList, TextInput, TypeIcon } from "@/ui";
import { displayName, pityLine, splitRoster } from "./rosterSplit";

const RARITY_BADGE: Record<string, string> = {
  legendary: "★★★★ Legendary",
  epic: "★★★ Epic",
  rare: "★★ Rare",
  common: "★ Common"
};

/**
 * Demon Domain V1 (spec-demon-summoning.md): Souls header, Summon panel with visible pity
 * counters, reveal with nickname/lock, Active/Reserve roster, Codex with silhouettes.
 */
export function DemonsPage() {
  const players = usePlayers();
  const playerId = players.data?.currentPlayerId ?? 0;
  const catalog = useDemonCatalog();
  const roster = useDemonRoster(playerId);
  const codex = useDemonCodex(playerId);
  const summonState = useSummonState(playerId);
  const summon = useSummon();
  const setNickname = useSetDemonNickname();
  const setLocked = useSetDemonLocked();

  const [tab, setTab] = useState<"summon" | "roster" | "codex">("summon");
  const [reveal, setReveal] = useState<SummonOutcomeDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [nickDrafts, setNickDrafts] = useState<Record<string, string>>({});

  const speciesById = useMemo(() => {
    const map = new Map<string, { name: string; side: "plant" | "zombie"; gameTypeId: number }>();
    for (const s of catalog.data?.species ?? [])
      map.set(s.speciesId, { name: s.name, side: s.side, gameTypeId: s.gameTypeId });
    return map;
  }, [catalog.data]);

  const split = useMemo(() => splitRoster(roster.data?.items ?? []), [roster.data?.items]);
  const balance = summonState.data?.balance.balance ?? 0;
  const pity = summonState.data?.pity;

  async function pull(bannerId: string, count: 1 | 10, cost: number) {
    setError(null);
    if (balance < cost) {
      setError(`Not enough Souls (${balance}/${cost}). Play matches to earn more.`);
      return;
    }
    try {
      const outcome = await summon.mutateAsync({
        playerId,
        bannerId,
        count,
        correlationId: newCorrelationId()
      });
      const ordered = [...outcome.specimens].sort(
        (a, b) => rarityRank(a.profile.rarity) - rarityRank(b.profile.rarity)
      );
      setReveal({ ...outcome, specimens: ordered });
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  }

  function specimenCard(s: DemonSpecimenDto, showControls: boolean) {
    const species = speciesById.get(s.profile.speciesId);
    const id = s.profile.instanceId;
    return (
      <div
        key={id}
        className="flex items-center gap-3 rounded-sm border border-border bg-soil-raised/40 p-2"
        data-testid={`demon-card-${id}`}
      >
        {species ? <TypeIcon side={species.side} typeId={species.gameTypeId} size={40} /> : null}
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold">
            {displayName(s, species?.name)}
            {s.profile.variant !== "normal" ? <Badge className="ml-2">{s.profile.variant}</Badge> : null}
          </p>
          <p className="text-xs text-muted">
            {RARITY_BADGE[s.profile.rarity] ?? s.profile.rarity} · {s.profile.elementPrimary}
            {s.profile.elementSecondary ? `/${s.profile.elementSecondary}` : ""} ·{" "}
            {s.profile.traitIds.join(", ") || "no traits"}
          </p>
        </div>
        {showControls ? (
          <div className="flex items-center gap-2">
            <TextInput
              value={nickDrafts[id] ?? s.profile.nickname ?? ""}
              onChange={(e) => setNickDrafts((d) => ({ ...d, [id]: e.target.value }))}
              placeholder="Nickname"
              className="w-28"
              data-testid={`nickname-${id}`}
            />
            <Button
              size="sm"
              variant="ghost"
              onClick={() => void setNickname.mutateAsync({ instanceId: id, nickname: nickDrafts[id] ?? null })}
            >
              Name
            </Button>
            <Button
              size="sm"
              variant={s.profile.locked ? "primary" : "ghost"}
              onClick={() => void setLocked.mutateAsync({ instanceId: id, locked: !s.profile.locked })}
              data-testid={`lock-${id}`}
            >
              {s.profile.locked ? "🔒" : "🔓"}
            </Button>
          </div>
        ) : null}
      </div>
    );
  }

  const codexBySpecies = useMemo(() => {
    const map = new Map<string, string>();
    for (const e of codex.data?.entries ?? []) map.set(e.speciesId, e.state);
    return map;
  }, [codex.data]);
  const discoveredCount = useMemo(
    () => [...codexBySpecies.values()].filter((s) => s === "discovered").length,
    [codexBySpecies]
  );

  return (
    <Page title="Demons" description="Summon, manage, and catalog your demon roster">
      <div className="mb-3 flex items-center gap-4">
        <Badge data-testid="souls-balance">Souls: {balance.toLocaleString()}</Badge>
        {pity ? (
          <span className="text-xs text-muted" data-testid="pity-counters">
            {pityLine(pity.pullsSinceEpic, pity.epicGuaranteeAt, pity.pullsSinceLegendary, pity.legendaryGuaranteeAt)}
          </span>
        ) : null}
      </div>

      <TabList
        tabs={[
          { id: "summon", label: "Summon" },
          { id: "roster", label: `Roster (${roster.data?.items.length ?? 0})` },
          { id: "codex", label: "Codex" }
        ]}
        value={tab}
        onChange={(id) => setTab(id as typeof tab)}
      />

      {error ? <Banner tone="error">{error}</Banner> : null}

      {tab === "summon" ? (
        <div className="mt-3 grid gap-3 md:grid-cols-2">
          {(summonState.data?.banners ?? []).map((b) => (
            <Panel
              key={b.bannerId}
              title={b.bannerId === "standard-rift" ? "Standard Rift" : `Element Focus — ${b.focusElement ?? "?"}`}
            >
              <p className="mb-2 text-xs text-muted">
                {b.bannerId === "standard-rift"
                  ? "All summonable demons at base rates."
                  : `3× weight for ${b.focusElement} demons within each rarity.`}
              </p>
              <div className="flex gap-2">
                <Button
                  disabled={summon.isPending || balance < b.costPerPull}
                  onClick={() => void pull(b.bannerId, 1, b.costPerPull)}
                  data-testid={`pull-1-${b.bannerId}`}
                >
                  Summon ×1 ({b.costPerPull})
                </Button>
                <Button
                  disabled={summon.isPending || balance < b.costPerTen}
                  onClick={() => void pull(b.bannerId, 10, b.costPerTen)}
                  data-testid={`pull-10-${b.bannerId}`}
                >
                  Summon ×10 ({b.costPerTen})
                </Button>
              </div>
            </Panel>
          ))}
          {reveal ? (
            <Panel title={reveal.replayed ? "Summon results (replayed)" : "Summon results"} className="md:col-span-2">
              {reveal.discoverySouls > 0 ? (
                <Banner tone="info">New discoveries! +{reveal.discoverySouls} Souls</Banner>
              ) : null}
              <div className="mt-2 flex flex-col gap-2" data-testid="reveal-list">
                {reveal.specimens.map((s) => specimenCard(s, true))}
              </div>
            </Panel>
          ) : null}
        </div>
      ) : null}

      {tab === "roster" ? (
        <div className="mt-3 flex flex-col gap-3">
          <Panel title={`Active (${split.active.length}/24)`}>
            {split.active.length === 0 ? (
              <EmptyState title="No demons yet" hint="Summon your first demon from the Summon tab." />
            ) : (
              <div className="flex flex-col gap-2">{split.active.map((s) => specimenCard(s, true))}</div>
            )}
          </Panel>
          <Panel title={`Reserve (${split.reserve.reduce((n, r) => n + r.count, 0)})`}>
            <p className="mb-2 text-xs text-muted">
              Reserved demons become fusion and ritual material in a future update.
            </p>
            {split.reserve.length === 0 ? (
              <p className="text-sm text-muted">Empty — overflow past 24 active demons stacks here by species.</p>
            ) : (
              <div className="flex flex-wrap gap-2">
                {split.reserve.map((r) => {
                  const species = speciesById.get(r.speciesId);
                  return (
                    <div
                      key={r.speciesId}
                      className="flex items-center gap-2 rounded-sm border border-border px-2 py-1"
                      data-testid={`reserve-${r.speciesId}`}
                    >
                      {species ? <TypeIcon side={species.side} typeId={species.gameTypeId} size={28} /> : null}
                      <span className="text-sm">{species?.name ?? r.speciesId}</span>
                      <Badge>×{r.count}</Badge>
                    </div>
                  );
                })}
              </div>
            )}
          </Panel>
        </div>
      ) : null}

      {tab === "codex" ? (
        <Panel title={`Codex (${discoveredCount}/${catalog.data?.species.length ?? 0} discovered)`} className="mt-3">
          <div className="grid grid-cols-2 gap-2 md:grid-cols-4" data-testid="codex-grid">
            {(catalog.data?.species ?? []).map((s) => {
              const state = codexBySpecies.get(s.speciesId);
              const known = state === "discovered" || state === "seen";
              return (
                <div
                  key={s.speciesId}
                  className="flex items-center gap-2 rounded-sm border border-border p-2"
                  data-testid={`codex-${s.speciesId}`}
                >
                  <span className={known ? "" : "opacity-30 grayscale"}>
                    <TypeIcon side={s.side} typeId={s.gameTypeId} size={32} />
                  </span>
                  <div className="min-w-0">
                    <p className="truncate text-sm">{known ? s.name : "???"}</p>
                    <p className="text-xs text-muted">
                      {known ? RARITY_BADGE[s.rarity] : s.captureOnly ? "Capture only" : "Undiscovered"}
                    </p>
                  </div>
                </div>
              );
            })}
          </div>
        </Panel>
      ) : null}
    </Page>
  );
}

function rarityRank(rarity: string): number {
  switch (rarity) {
    case "common": return 0;
    case "rare": return 1;
    case "epic": return 2;
    default: return 3;
  }
}
