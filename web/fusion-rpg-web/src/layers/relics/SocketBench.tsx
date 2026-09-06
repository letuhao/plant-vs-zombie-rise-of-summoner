import { useMemo, useState } from "react";
import { adaptCombinations, adaptWorkbenchOutcome } from "@/contract/adapt";
import type { CombinationView, SocketCellView } from "@/contract/types";
import { newCorrelationId } from "@/lib/bus/demons";
import { useItemCombinations, useSocketAdd, useSocketInsert } from "@/lib/bus/items";
import { cn } from "@/lib/cn";
import { DialogShell } from "@/shell/DialogShell";
import { Banner, Button, TextInput } from "@/ui";
import { EmptyState } from "@/ui/EmptyState";
import { formatMagnitude } from "@/i18n/magnitude";
import { RecipeField, WorkbenchRefusal, WorkbenchResult, useWorkbenchFeedback } from "./Workbench";

/**
 * The socket bench — what the current fill produces, and what is one insert away. A band-3 dialog,
 * pushed from the item detail, so the budget stays at three levels: Relics, item, bench.
 *
 * **Without this the resonance layer is invisible and reverts to being a stat tax.** The preview is
 * a requirement of the socket design, not a nicety.
 *
 * ⛔ **One evaluator, one read.** The preview and the compendium both come off the same route, so
 * "the tooltip said one more and it did not fire" cannot happen: there is no second pass here that
 * could disagree with the one the server ran.
 *
 * ⛔ **Recipes are unordered**, so there is no swap hint and there is deliberately no code here that
 * could produce one — a distance counts missing kinds, never positions.
 *
 * **Affinity is a bonus, not a gate.** A matched affinity changes the granted tier, never the
 * distance, and both are shown so a matched fill is not misread as a different recipe.
 *
 * ⛔ **Fully controlled.** It opens only when its caller says so; nothing here opens itself from a
 * background event.
 *
 * ⭐ **The two write verbs are real** — `POST /api/items/workbench/socket-add` and `/socket-insert`
 * (item module 16, executed by the workbench). The fill this dialog shows after an operation is the
 * server's own reply, not a local guess at what the write did.
 *
 * ⏸ **Imbue is wired on the server and cannot be paid for**, so it is stated rather than drawn as a
 * control — see the note this dialog renders. That is content missing from the recipe corpus, not a
 * missing capability, and the day a recipe authors the operation the control is three lines.
 */

/**
 * ⏸ The one honest reason `socket-imbue` is not offered. Verified against the shipped corpus
 * (`data/seed/items/recipes/recipes.json`: 30 rows across forge, upcycle, elevate, temper,
 * reroll-one, reroll-all, bore and socket — and no `imbue`), not inferred from a comment. The route
 * exists and would refuse every call with `material.recipe-unknown`.
 */
const IMBUE_UNAVAILABLE_REASON =
  "Setting a socket's element needs a recipe to price it, and none has been written yet.";

function remedy(combo: CombinationView): string {
  const missing = [...combo.missingFamilies, ...combo.missingElements];
  if (missing.length === 0) return "";
  const count = combo.distance ?? missing.length;
  return `needs ${formatMagnitude({ unit: "count", value: count })} more: ${missing.join(", ")}`;
}

function ComboRow({ combo }: { combo: CombinationView }) {
  const active = combo.state === "active";
  return (
    <li
      className={cn("flex flex-col border-b border-border px-2 py-1 last:border-b-0", active ? "text-text" : "text-muted")}
      data-testid={`bench-combo-${combo.comboId}`}
      data-state={combo.state}
    >
      <span className="flex items-baseline justify-between gap-2">
        <span className="truncate font-semibold">{combo.comboId}</span>
        <span className="text-2xs uppercase tracking-wide">{combo.shape}</span>
      </span>
      {active ? (
        <span className="text-xs">
          Firing at tier {formatMagnitude({ unit: "count", value: combo.grantedTier })}
        </span>
      ) : (
        <span className="text-xs">{remedy(combo)}</span>
      )}
    </li>
  );
}

export function SocketBench({
  open,
  onOpenChange,
  instanceId,
  itemName,
  playerId,
  cells
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  instanceId: string;
  itemName: string;
  playerId: number;
  /** The fill as the item card already knows it; empty until a route carries socket cells. */
  cells: SocketCellView[];
}) {
  const query = useItemCombinations(open ? instanceId : null, playerId);
  const combinations = useMemo(() => (query.data ? adaptCombinations(query.data) : []), [query.data]);

  const active = combinations.filter((c) => c.state === "active");
  const oneAway = combinations.filter((c) => c.state === "one-away");

  const [boreRecipe, setBoreRecipe] = useState("");
  const [insertRecipe, setInsertRecipe] = useState("");
  const [insertContainerId, setInsertContainerId] = useState("");
  const socketAdd = useSocketAdd();
  const socketInsert = useSocketInsert();
  const { last, failure, handlers } = useWorkbenchFeedback();

  const busy = socketAdd.isPending || socketInsert.isPending;

  return (
    <DialogShell
      open={open}
      onOpenChange={onOpenChange}
      title="Socket bench"
      subtitle={itemName}
      testId="socket-bench"
      footer={
        <Button size="sm" variant="ghost" onClick={() => onOpenChange(false)}>
          Close
        </Button>
      }
    >
      <div className="flex flex-col gap-3">
        <div>
          <p className="text-2xs font-bold uppercase tracking-wide text-muted">The fill</p>
          <div className="mt-1 flex flex-wrap gap-1" data-testid="bench-fill">
            {cells.length === 0 ? (
              <span className="text-xs text-muted">This one has no sockets.</span>
            ) : (
              cells.map((cell) => (
                <span
                  key={cell.index}
                  className={cn(
                    "inline-flex min-w-[64px] flex-col items-center rounded-sm border px-2 py-1 text-xs",
                    cell.insertName ? "border-lawn-hot text-text" : "border-dashed border-border text-muted"
                  )}
                  data-testid={`bench-cell-${cell.index}`}
                >
                  <span>{cell.insertName ?? "empty"}</span>
                  {cell.affinity ? <span className="text-2xs text-muted">{cell.affinity}</span> : null}
                </span>
              ))
            )}
          </div>
        </div>

        {query.isLoading ? (
          <p className="text-sm text-muted" data-testid="bench-loading" aria-busy="true">
            Working out what this fill makes…
          </p>
        ) : query.isError ? (
          <Banner tone="error" data-testid="bench-error">
            Couldn't work out what this fill makes.
            <Button size="sm" variant="ghost" className="ml-2" onClick={() => void query.refetch()}>
              Retry
            </Button>
          </Banner>
        ) : (
          <>
            <div>
              <p className="text-2xs font-bold uppercase tracking-wide text-muted">Firing now</p>
              {active.length === 0 ? (
                <EmptyState
                  title="Nothing is firing yet"
                  hint="Fill the sockets and matching sets light up here."
                  testId="bench-none-active"
                />
              ) : (
                <ul className="mt-1 rounded-md border border-border" data-testid="bench-active">
                  {active.map((c) => (
                    <ComboRow key={c.comboId} combo={c} />
                  ))}
                </ul>
              )}
            </div>

            <div>
              <p className="text-2xs font-bold uppercase tracking-wide text-muted">One away</p>
              {oneAway.length === 0 ? (
                <p className="mt-1 text-xs text-muted" data-testid="bench-none-one-away">
                  Nothing is one insert from firing.
                </p>
              ) : (
                <ul className="mt-1 rounded-md border border-border" data-testid="bench-one-away">
                  {oneAway.map((c) => (
                    <ComboRow key={c.comboId} combo={c} />
                  ))}
                </ul>
              )}
            </div>
          </>
        )}

        {/* ---- The two real write verbs (item module 16, via the workbench) ---- */}
        <div className="flex flex-col gap-3 border-t border-border pt-3" data-testid="bench-actions">
          {busy ? (
            <p className="text-sm text-muted" data-testid="bench-busy" aria-busy="true">
              Working the bench…
            </p>
          ) : null}

          {failure ? <WorkbenchRefusal error={failure} testId="bench-write-error" /> : null}

          {last ? <WorkbenchResult outcome={adaptWorkbenchOutcome(last)} testId="bench-result" /> : null}

          <div className="flex flex-col gap-1">
            <p className="text-2xs font-bold uppercase tracking-wide text-muted">Open a socket</p>
            <RecipeField
              label="Bore recipe"
              hint="No route lists craft recipes yet, so name the one you want — a wrong id comes back named."
              value={boreRecipe}
              onChange={setBoreRecipe}
              testId="bench-bore-recipe"
            />
            <div>
              <Button
                size="sm"
                data-testid="bench-socket-add-btn"
                disabled={busy || boreRecipe.trim().length === 0}
                title={
                  busy
                    ? "The bench is busy"
                    : boreRecipe.trim().length === 0
                      ? "Name a bore recipe first"
                      : undefined
                }
                onClick={() =>
                  socketAdd.mutate(
                    {
                      playerId,
                      instanceId,
                      recipeId: boreRecipe.trim(),
                      correlationId: newCorrelationId()
                    },
                    handlers()
                  )
                }
              >
                Open a socket
              </Button>
            </div>
          </div>

          <div className="flex flex-col gap-1">
            <p className="text-2xs font-bold uppercase tracking-wide text-muted">Set an insert</p>
            <RecipeField
              label="Socket recipe"
              hint="The insert has to be one you already hold — it leaves your stock in the same write."
              value={insertRecipe}
              onChange={setInsertRecipe}
              testId="bench-insert-recipe"
            />
            <TextInput
              data-testid="bench-insert-container"
              value={insertContainerId}
              placeholder="insert you hold"
              aria-label="Insert to set"
              onChange={(e) => setInsertContainerId(e.target.value)}
            />
            <p className="text-2xs text-muted">
              It goes in the first open socket. Which socket is not yours to pick yet.
            </p>
            <div>
              <Button
                size="sm"
                data-testid="bench-socket-insert-btn"
                disabled={busy || insertRecipe.trim().length === 0 || insertContainerId.trim().length === 0}
                title={
                  busy
                    ? "The bench is busy"
                    : insertRecipe.trim().length === 0
                      ? "Name a socket recipe first"
                      : insertContainerId.trim().length === 0
                        ? "Name the insert you want to set"
                        : undefined
                }
                onClick={() =>
                  socketInsert.mutate(
                    {
                      playerId,
                      instanceId,
                      recipeId: insertRecipe.trim(),
                      insertContainerId: insertContainerId.trim(),
                      correlationId: newCorrelationId()
                    },
                    handlers()
                  )
                }
              >
                Set the insert
              </Button>
            </div>
          </div>

          {/* ⏸ Real, and honestly unavailable — the reason is content, not capability. */}
          <div className="flex flex-col gap-1" data-testid="bench-imbue">
            <p className="text-2xs font-bold uppercase tracking-wide text-muted">Set an element</p>
            <div>
              <Button
                size="sm"
                variant="ghost"
                data-testid="bench-socket-imbue-btn"
                disabled
                title={IMBUE_UNAVAILABLE_REASON}
              >
                Set an element
              </Button>
            </div>
            <p className="text-xs text-muted" data-testid="bench-imbue-reason">
              {IMBUE_UNAVAILABLE_REASON}
            </p>
          </div>
        </div>

        <p className="text-2xs text-muted">
          Order never matters — collect the right kinds and it fires however you arrange them. Matching a
          socket's own affinity raises the tier you get, not how close you are.
        </p>
      </div>
    </DialogShell>
  );
}
