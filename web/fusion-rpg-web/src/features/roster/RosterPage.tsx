import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  useAwardUniqueActorXp,
  useClearUniqueEquipment,
  useCreateUniqueActor,
  useDeployUniqueActor,
  usePlayers,
  usePutUniqueEquipment,
  useRetireUniqueActor,
  useUniqueActors,
  useUniqueEquipment,
  type UniqueActorDto
} from "@/lib/bus";
import { Page } from "@/layouts/Page";
import {
  Banner,
  Button,
  Checkbox,
  ConfirmDialog,
  DataTable,
  EmptyState,
  Field,
  HelpText,
  NumberInput,
  Panel,
  Select,
  TextInput,
  type DataTableColumn
} from "@/ui";
import { canAwardXp, canDeploy, canEquip, canRetire } from "./rosterPhase";

const EQUIP_SLOTS = ["weapon", "armor", "trinket"] as const;
const STUB_HINT = "stub.atk_ring | stub.butter_bead | stub.hp_charm";

/**
 * UniqueActor roster FE (W8-C + W8-A equip + W8-B specimen XP).
 * Equip only in Roster; awards apply on next Deploy Bound loadout.
 */
export function RosterPage() {
  const players = usePlayers();
  const playerId = players.data?.currentPlayerId ?? 0;
  const list = useUniqueActors(playerId);
  const create = useCreateUniqueActor();
  const deploy = useDeployUniqueActor();
  const retire = useRetireUniqueActor();
  const putEquip = usePutUniqueEquipment();
  const clearEquip = useClearUniqueEquipment();
  const awardXp = useAwardUniqueActorXp();

  const [side, setSide] = useState<"plant" | "zombie">("plant");
  const [typeId, setTypeId] = useState(0);
  const [pinCell, setPinCell] = useState(false);
  const [deployCol, setDeployCol] = useState(0);
  const [deployRow, setDeployRow] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [okMsg, setOkMsg] = useState<string | null>(null);
  const [retireTarget, setRetireTarget] = useState<UniqueActorDto | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [slotDrafts, setSlotDrafts] = useState<Record<string, string>>({});
  const [xpDelta, setXpDelta] = useState(50);

  const equipment = useUniqueEquipment(selectedId);
  const selected = useMemo(
    () => list.data?.items.find((x) => x.instanceId === selectedId) ?? null,
    [list.data?.items, selectedId]
  );

  useEffect(() => {
    const items = equipment.data?.items ?? [];
    const next: Record<string, string> = {};
    for (const slot of EQUIP_SLOTS) {
      next[slot] = items.find((x) => x.slot === slot)?.itemId ?? "";
    }
    setSlotDrafts(next);
  }, [equipment.data]);

  const busy =
    create.isPending ||
    deploy.isPending ||
    retire.isPending ||
    putEquip.isPending ||
    clearEquip.isPending ||
    awardXp.isPending;
  const items = list.data?.items ?? [];
  const equipEnabled = selected != null && canEquip(selected.phase);

  const run = async (label: string, fn: () => Promise<unknown>) => {
    setError(null);
    setOkMsg(null);
    try {
      await fn();
      setOkMsg(label);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  const columns: DataTableColumn<UniqueActorDto>[] = [
    {
      key: "instanceId",
      header: "instanceId",
      cell: (r) => <span className="font-mono text-xs">{r.instanceId}</span>
    },
    { key: "side", header: "Side", cell: (r) => r.side },
    { key: "typeId", header: "typeId", cell: (r) => String(r.typeId) },
    { key: "phase", header: "Phase", cell: (r) => r.phase },
    { key: "level", header: "Lv", cell: (r) => String(r.level) },
    { key: "xp", header: "XP", cell: (r) => String(r.xp) },
    {
      key: "lastPtr",
      header: "lastPtr",
      cell: (r) => r.lastPtr ?? "—"
    },
    {
      key: "actions",
      header: "Actions",
      cell: (r) => (
        <div className="flex flex-wrap gap-1" onClick={(e) => e.stopPropagation()}>
          <Button
            size="sm"
            disabled={busy || !canDeploy(r.phase)}
            title={busy ? "Working…" : !canDeploy(r.phase) ? `Not deployable in phase ${r.phase}` : undefined}
            data-testid={`roster-deploy-${r.instanceId}`}
            onClick={() =>
              void run("Deploy enqueued", () =>
                deploy.mutateAsync({
                  instanceId: r.instanceId,
                  playerId,
                  ...(pinCell ? { col: deployCol, row: deployRow } : {})
                })
              )
            }
          >
            Deploy
          </Button>
          <Button
            size="sm"
            variant="danger"
            disabled={busy || !canRetire(r.phase)}
            title={busy ? "Working…" : !canRetire(r.phase) ? `Not retireable in phase ${r.phase}` : undefined}
            data-testid={`roster-retire-${r.instanceId}`}
            onClick={() => setRetireTarget(r)}
          >
            Retire
          </Button>
        </div>
      )
    }
  ];

  return (
    <Page
      testId="page-roster"
      title="Roster"
      description="UniqueActor Cold specimens — create, equip, deploy Intent, award XP, retire."
      className="max-w-[1100px]"
    >
      <Banner tone="info" className="mb-3">
        Deploy uses Server Admit via unique-deploy Intent. Bound observe stays on{" "}
        <Link className="underline" to="/lawn">
          Lawn
        </Link>
        .
      </Banner>
      {error ? (
        <Banner tone="error" className="mb-3" data-testid="roster-error">
          {error}
        </Banner>
      ) : null}
      {okMsg ? (
        <Banner tone="info" className="mb-3" data-testid="roster-ok">
          {okMsg}
        </Banner>
      ) : null}

      <div className="grid gap-3 lg:grid-cols-[320px_1fr]">
        <Panel title="Create specimen" testId="panel-roster-create">
          <Field label="Side">
            <Select
              value={side}
              data-testid="roster-create-side"
              onChange={(e) => setSide(e.target.value === "zombie" ? "zombie" : "plant")}
            >
              <option value="plant">plant</option>
              <option value="zombie">zombie</option>
            </Select>
          </Field>
          <Field label="typeId">
            <NumberInput
              value={typeId}
              data-testid="roster-create-typeid"
              onChange={(v) => setTypeId(Number.isFinite(v) ? v : 0)}
            />
          </Field>
          <Button
            className="mt-3"
            data-testid="roster-create"
            disabled={busy || playerId <= 0}
            title={busy ? "Working…" : playerId <= 0 ? "No player selected" : undefined}
            onClick={() =>
              void run("Specimen created", () =>
                create.mutateAsync({ side, typeId, playerId })
              )
            }
          >
            Create
          </Button>
          <HelpText className="mt-2">
            Starts in Roster. Equip compiles grant templates into mods_json for next Deploy.
          </HelpText>
        </Panel>

        <Panel title="Specimens" testId="panel-roster-list">
          <Checkbox
            label="Pin deploy cell"
            checked={pinCell}
            data-testid="roster-pin-cell"
            onChange={(e) => setPinCell(e.target.checked)}
          />
          {pinCell ? (
            <div className="mb-2 flex flex-wrap gap-3">
              <Field label="Deploy col" className="mt-0 w-28">
                <NumberInput
                  value={deployCol}
                  data-testid="roster-deploy-col"
                  onChange={(v) => setDeployCol(Number.isFinite(v) ? v : 0)}
                />
              </Field>
              <Field label="Deploy row" className="mt-0 w-28">
                <NumberInput
                  value={deployRow}
                  data-testid="roster-deploy-row"
                  onChange={(v) => setDeployRow(Number.isFinite(v) ? v : 0)}
                />
              </Field>
            </div>
          ) : (
            <HelpText className="mb-2">
              Unpinned: omit col/row so Injector uses CheatState spawn defaults.
            </HelpText>
          )}
          <HelpText className="mb-2">
            Click a row for equip / XP. Deploy enabled in Roster only. Retire blocked while
            Deploying / Recovering.
          </HelpText>
          {list.isError ? (
            <Banner tone="error">Failed to load roster.</Banner>
          ) : (
            <DataTable
              columns={columns}
              rows={items}
              rowKey={(r) => r.instanceId}
              onRowClick={(r) => setSelectedId(r.instanceId)}
              empty={
                <EmptyState
                  title="No specimens"
                  hint="Create a plant or zombie UniqueActor for the current player."
                />
              }
            />
          )}
        </Panel>
      </div>

      {selected ? (
        <Panel
          className="mt-3"
          title={`Selected ${selected.side} #${selected.typeId}`}
          testId="panel-roster-selected"
        >
          <HelpText className="mb-2">
            instanceId <span className="font-mono text-xs">{selected.instanceId}</span> · phase{" "}
            {selected.phase} · Lv {selected.level} / XP {selected.xp}
          </HelpText>

          <div className="mb-4 grid gap-3 md:grid-cols-3" data-testid="roster-equip-slots">
            {EQUIP_SLOTS.map((slot) => (
              <div key={slot} className="rounded border border-soil-line/40 p-2">
                <Field label={slot}>
                  <TextInput
                    value={slotDrafts[slot] ?? ""}
                    data-testid={`roster-equip-${slot}`}
                    disabled={!equipEnabled || busy}
                    title={busy ? "Working…" : !equipEnabled ? `Equip disabled in phase ${selected.phase}` : undefined}
                    placeholder={STUB_HINT}
                    onChange={(e) =>
                      setSlotDrafts((prev) => ({ ...prev, [slot]: e.target.value }))
                    }
                  />
                </Field>
                <div className="mt-2 flex flex-wrap gap-1">
                  <Button
                    size="sm"
                    disabled={!equipEnabled || busy}
                    title={busy ? "Working…" : !equipEnabled ? `Equip disabled in phase ${selected.phase}` : undefined}
                    data-testid={`roster-equip-save-${slot}`}
                    onClick={() =>
                      void run(`Equipped ${slot}`, () =>
                        putEquip.mutateAsync({
                          instanceId: selected.instanceId,
                          slot,
                          itemId: (slotDrafts[slot] ?? "").trim(),
                          playerId
                        })
                      )
                    }
                  >
                    Save
                  </Button>
                  <Button
                    size="sm"
                    variant="danger"
                    disabled={!equipEnabled || busy}
                    title={busy ? "Working…" : !equipEnabled ? `Equip disabled in phase ${selected.phase}` : undefined}
                    data-testid={`roster-equip-clear-${slot}`}
                    onClick={() =>
                      void run(`Cleared ${slot}`, () =>
                        clearEquip.mutateAsync({
                          instanceId: selected.instanceId,
                          slot,
                          playerId
                        })
                      )
                    }
                  >
                    Clear
                  </Button>
                </div>
              </div>
            ))}
          </div>
          <HelpText data-testid="roster-equip-help">
            Applies on next Deploy (Bound loadout). Equip disabled when phase ≠ Roster
            {equipEnabled ? "" : ` (now ${selected.phase})`}.
          </HelpText>
          {equipment.isError ? (
            <Banner tone="error" className="mt-2">
              Failed to load equipment.
            </Banner>
          ) : null}

          <div className="mt-4 flex flex-wrap items-end gap-2" data-testid="roster-award-xp">
            <Field label="Award XP" className="mt-0 w-36">
              <NumberInput
                value={xpDelta}
                data-testid="roster-xp-delta"
                disabled={!canAwardXp(selected.phase) || busy}
                title={
                  busy ? "Working…" : !canAwardXp(selected.phase) ? `Can't award XP in phase ${selected.phase}` : undefined
                }
                onChange={(v) => setXpDelta(Number.isFinite(v) ? v : 0)}
              />
            </Field>
            <Button
              data-testid="roster-xp-award"
              disabled={!canAwardXp(selected.phase) || busy || xpDelta <= 0}
              title={
                busy
                  ? "Working…"
                  : !canAwardXp(selected.phase)
                    ? `Can't award XP in phase ${selected.phase}`
                    : xpDelta <= 0
                      ? "Enter a positive amount"
                      : undefined
              }
              onClick={() =>
                void run("XP awarded", () =>
                  awardXp.mutateAsync({
                    instanceId: selected.instanceId,
                    delta: xpDelta,
                    reason: "roster-fe",
                    playerId
                  })
                )
              }
            >
              Award XP
            </Button>
            <HelpText className="mb-1">
              Specimen grain only (not type RpgProgression). Refused when Retired.
            </HelpText>
          </div>
        </Panel>
      ) : null}

      <ConfirmDialog
        open={retireTarget != null}
        title="Retire specimen?"
        message={
          retireTarget
            ? `Retire ${retireTarget.side} #${retireTarget.typeId} (${retireTarget.instanceId})?`
            : ""
        }
        confirmLabel="Retire"
        tone="danger"
        busy={retire.isPending}
        testId="roster-retire-confirm"
        onCancel={() => setRetireTarget(null)}
        onConfirm={() => {
          if (!retireTarget) return;
          const target = retireTarget;
          setRetireTarget(null);
          void run("Specimen retired", () =>
            retire.mutateAsync({ instanceId: target.instanceId, playerId })
          );
        }}
      />
    </Page>
  );
}
