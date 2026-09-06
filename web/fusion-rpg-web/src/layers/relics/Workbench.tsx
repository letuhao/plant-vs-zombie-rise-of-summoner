import { useState } from "react";
import { adaptWorkbenchOutcome } from "@/contract/adapt";
import type { WorkbenchOutcomeView } from "@/contract/types";
import { formatMagnitude } from "@/i18n/magnitude";
import { newCorrelationId } from "@/lib/bus/demons";
import {
  useEnhanceItem,
  useSalvageItem,
  useUpcycleMaterials,
  type WorkbenchOutcomeDto
} from "@/lib/bus/items";
import { DialogShell } from "@/shell/DialogShell";
import { Banner, Button, Checkbox, Field, TextInput } from "@/ui";

/**
 * The craft bench — item modules 14 and 15's verbs, against the real
 * `POST /api/items/workbench/*` executor. A band-3 dialog pushed from the item detail, so the depth
 * budget stays at three: Relics, item, bench.
 *
 * ⛔ **Nothing here prices anything.** Every quantity on screen is a line the server resolved and
 * sent back in the operation's own reply — there is no cost preview, because no route prices an
 * operation before it runs and a client-composed estimate would be a second, disagreeing price.
 *
 * ⛔ **A refusal is the server's sentence, verbatim.** The executor answers 409 with the named rule
 * (`item.locked`, `material.recipe-unknown`, `ContentRuleViolated{…}`) and this surface prints it
 * rather than translating it into something friendlier and less true.
 *
 * ⚠ **The recipe id is typed, and that is a named gap rather than a design choice.** The shipped
 * corpus is 30 rows in `material_recipe`, and **no read route serves them** — `GET /api/recipes` is
 * the PvZ fusion table, a different thing entirely. Until a craft-recipe read route exists, naming
 * the recipe is the player's job and the server's `material.recipe-unknown` is what corrects a
 * wrong one. Hard-coding the corpus here instead would put a second copy of it in the browser.
 */

/** A verb that spends is only offered once the player has named the recipe that prices it. */
export function RecipeField({
  label,
  hint,
  value,
  onChange,
  testId
}: {
  label: string;
  hint: string;
  value: string;
  onChange: (value: string) => void;
  testId: string;
}) {
  return (
    <Field label={label} hint={hint}>
      <TextInput
        data-testid={testId}
        value={value}
        placeholder="recipe id"
        onChange={(e) => onChange(e.target.value)}
      />
    </Field>
  );
}

function CostLines({
  title,
  lines,
  testId
}: {
  title: string;
  lines: WorkbenchOutcomeView["spent"];
  testId: string;
}) {
  if (lines.length === 0) return null;
  return (
    <div data-testid={testId}>
      <p className="text-2xs font-bold uppercase tracking-wide text-muted">{title}</p>
      {lines.map((line) => (
        <p key={`${line.materialClass}-${line.materialId}`} className="flex items-baseline justify-between gap-3 text-sm">
          <span className="truncate text-muted">{line.materialId}</span>
          <span className="font-mono text-text">{formatMagnitude(line.qty)}</span>
        </p>
      ))}
    </div>
  );
}

/**
 * What the last operation did.
 *
 * ⚠ **A 200 is not a success.** A failed enhance still spends, still records an op and still moves
 * the pity counter, so this reads `outcome` and never the transport status — showing "done" over a
 * `failure` is exactly the "pretend it worked" the honest-state rule forbids.
 */
export function WorkbenchResult({
  outcome,
  testId = "workbench-result"
}: {
  outcome: WorkbenchOutcomeView;
  testId?: string;
}) {
  const failedRoll = outcome.outcome === "failure" || outcome.outcome === "failure-downgrade";
  return (
    <div
      className="rounded-md border border-border bg-panel-raised p-3"
      data-testid={testId}
      data-verb={outcome.verb}
      data-outcome={outcome.outcome}
    >
      <p className={failedRoll ? "font-semibold text-warn" : "font-semibold text-text"}>
        {outcome.verb} — {outcome.outcome}
        {outcome.replayed ? " (already done — this is the recorded result)" : ""}
      </p>

      {failedRoll ? (
        <p className="mt-0.5 text-xs text-muted">
          It did not take. The materials are spent either way, and the pity counter moved.
        </p>
      ) : null}

      <div className="mt-2 flex flex-col gap-2">
        <CostLines title="Spent" lines={outcome.spent} testId={`${testId}-spent`} />
        <CostLines title="Gained" lines={outcome.granted} testId={`${testId}-granted`} />

        {outcome.verb === "enhance" ? (
          <p className="text-sm text-muted" data-testid={`${testId}-enhance`}>
            Now at +{formatMagnitude(outcome.enhanceLevel)} · pity{" "}
            {formatMagnitude(outcome.pityCounter)}
            {outcome.successChance ? ` · rolled at ${formatMagnitude(outcome.successChance)}` : ""}
          </p>
        ) : null}

        {outcome.sockets.length > 0 ? (
          <div data-testid={`${testId}-sockets`}>
            <p className="text-2xs font-bold uppercase tracking-wide text-muted">Sockets now</p>
            <div className="mt-1 flex flex-wrap gap-1">
              {outcome.sockets.map((socket) => (
                <span
                  key={socket.index}
                  className={
                    socket.insertContainerId
                      ? "inline-flex min-w-[64px] flex-col items-center rounded-sm border border-lawn-hot px-2 py-1 text-xs text-text"
                      : "inline-flex min-w-[64px] flex-col items-center rounded-sm border border-dashed border-border px-2 py-1 text-xs text-muted"
                  }
                  data-testid={`${testId}-socket-${socket.index}`}
                >
                  <span>{socket.insertContainerId ?? "empty"}</span>
                  {socket.affinity ? <span className="text-2xs text-muted">{socket.affinity}</span> : null}
                </span>
              ))}
            </div>
          </div>
        ) : null}
      </div>
    </div>
  );
}

/** A refusal, printed as the server named it. */
export function WorkbenchRefusal({ error, testId }: { error: Error; testId: string }) {
  return (
    <Banner tone="error" data-testid={testId}>
      {error.message}
    </Banner>
  );
}

/**
 * The last thing the bench did, whichever verb did it.
 *
 * A bench runs several verbs against one item, and each mutation hook keeps its own `data`. Reading
 * them in a fixed order would leave a stale enhance result on screen after a salvage — so the
 * result is held once, here, and the per-call handlers replace it. Both slots clear on the way in,
 * so a refusal never lingers beside the reply that followed it.
 */
export function useWorkbenchFeedback() {
  const [last, setLast] = useState<WorkbenchOutcomeDto | null>(null);
  const [failure, setFailure] = useState<Error | null>(null);

  return {
    last,
    failure,
    /** Spread into `mutate(req, handlers())` — clears the previous pair first. */
    handlers() {
      setLast(null);
      setFailure(null);
      return {
        onSuccess: (dto: WorkbenchOutcomeDto) => setLast(dto),
        onError: (error: Error) => setFailure(error)
      };
    }
  };
}

/**
 * Verbs the item program designed and the server does not serve, each with the real reason. They
 * are listed rather than drawn as controls: a button that can only fail is worse than an absent
 * one, because it looks wired.
 */
const UNAVAILABLE_VERBS: { verb: string; reason: string }[] = [
  {
    verb: "Forge",
    reason:
      "Forging mints a new item, and nothing has authored an effect container for a base type yet — there is nothing for a forge recipe to produce."
  },
  {
    verb: "Reroll",
    reason: "Rerolling an item's rolled bonuses has no resolver behind it yet."
  },
  {
    verb: "Transfer",
    reason: "Moving a bonus between two items needs an ask-first operation the workbench does not carry yet."
  }
];

export function UnavailableVerbs({ testId = "workbench-unavailable" }: { testId?: string }) {
  return (
    <div data-testid={testId}>
      <p className="text-2xs font-bold uppercase tracking-wide text-muted">Not available yet</p>
      <ul className="mt-1 flex flex-col gap-1">
        {UNAVAILABLE_VERBS.map((row) => (
          <li key={row.verb} className="text-xs text-muted" data-testid={`${testId}-${row.verb.toLowerCase()}`}>
            <b className="text-text">{row.verb}</b> — {row.reason}
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * The craft bench dialog: salvage the item, strengthen it, or refine loose materials.
 *
 * `instanceId` is the selected armoury row's, so every verb here acts on the item the player is
 * already looking at — the upcycle verb is the one exception and touches no item at all.
 */
export function CraftBench({
  open,
  onOpenChange,
  instanceId,
  itemName,
  playerId
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  instanceId: string;
  itemName: string;
  playerId: number;
}) {
  const [enhanceRecipe, setEnhanceRecipe] = useState("");
  const [upcycleRecipe, setUpcycleRecipe] = useState("");
  const [wardLoaded, setWardLoaded] = useState(false);

  const salvage = useSalvageItem();
  const enhance = useEnhanceItem();
  const upcycle = useUpcycleMaterials();
  const { last, failure, handlers } = useWorkbenchFeedback();

  const busy = salvage.isPending || enhance.isPending || upcycle.isPending;

  return (
    <DialogShell
      open={open}
      onOpenChange={onOpenChange}
      title="Craft bench"
      subtitle={itemName}
      testId="craft-bench"
      footer={
        <Button size="sm" variant="ghost" onClick={() => onOpenChange(false)}>
          Close
        </Button>
      }
    >
      <div className="flex flex-col gap-4">
        {busy ? (
          <p className="text-sm text-muted" data-testid="craft-bench-busy" aria-busy="true">
            Working the bench…
          </p>
        ) : null}

        {failure ? <WorkbenchRefusal error={failure} testId="craft-bench-error" /> : null}

        {last ? <WorkbenchResult outcome={adaptWorkbenchOutcome(last)} testId="craft-bench-result" /> : null}

        {/* Strengthen — module 15. A failed attempt still spends, and the result panel says so. */}
        <div className="flex flex-col gap-1">
          <p className="text-2xs font-bold uppercase tracking-wide text-muted">Strengthen</p>
          <RecipeField
            label="Temper recipe"
            hint="No route lists craft recipes yet, so name the one you want — a wrong id comes back named."
            value={enhanceRecipe}
            onChange={setEnhanceRecipe}
            testId="craft-enhance-recipe"
          />
          <Checkbox
            label="Spend a ward to soften a failure"
            data-testid="craft-enhance-ward"
            checked={wardLoaded}
            onChange={(e) => setWardLoaded(e.target.checked)}
          />
          <div>
            <Button
              size="sm"
              data-testid="craft-enhance-btn"
              disabled={busy || enhanceRecipe.trim().length === 0}
              title={
                busy
                  ? "The bench is busy"
                  : enhanceRecipe.trim().length === 0
                    ? "Name a temper recipe first"
                    : undefined
              }
              onClick={() =>
                enhance.mutate(
                  {
                    playerId,
                    instanceId,
                    recipeId: enhanceRecipe.trim(),
                    correlationId: newCorrelationId(),
                    wardLoaded
                  },
                  handlers()
                )
              }
            >
              Strengthen
            </Button>
          </div>
        </div>

        {/* Refine — module 14's material-to-material verb. Touches no item. */}
        <div className="flex flex-col gap-1">
          <p className="text-2xs font-bold uppercase tracking-wide text-muted">Refine materials</p>
          <RecipeField
            label="Upcycle recipe"
            hint="Turns several of one grade into one of the next. It uses no item."
            value={upcycleRecipe}
            onChange={setUpcycleRecipe}
            testId="craft-upcycle-recipe"
          />
          <div>
            <Button
              size="sm"
              data-testid="craft-upcycle-btn"
              disabled={busy || upcycleRecipe.trim().length === 0}
              title={
                busy
                  ? "The bench is busy"
                  : upcycleRecipe.trim().length === 0
                    ? "Name an upcycle recipe first"
                    : undefined
              }
              onClick={() =>
                upcycle.mutate(
                  { playerId, recipeId: upcycleRecipe.trim(), correlationId: newCorrelationId() },
                  handlers()
                )
              }
            >
              Refine
            </Button>
          </div>
        </div>

        {/* Break down — module 14. No recipe and no debit: the yield is a credit. */}
        <div className="flex flex-col gap-1">
          <p className="text-2xs font-bold uppercase tracking-wide text-muted">Break down</p>
          <p className="text-xs text-muted">
            Turns this one back into materials. It cannot be undone, and a locked item is refused.
          </p>
          <div>
            <Button
              size="sm"
              variant="danger"
              data-testid="craft-salvage-btn"
              disabled={busy}
              title={busy ? "The bench is busy" : undefined}
              onClick={() => salvage.mutate({ playerId, instanceId }, handlers())}
            >
              Break down
            </Button>
          </div>
        </div>

        <UnavailableVerbs />
      </div>
    </DialogShell>
  );
}
