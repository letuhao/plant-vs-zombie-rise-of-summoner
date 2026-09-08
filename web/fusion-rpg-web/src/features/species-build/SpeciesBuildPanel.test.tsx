import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { SpeciesBuildPanel } from "./SpeciesBuildPanel";

const respecMutateAsync = vi.fn();
// G6: a stable reference (not a fresh `vi.fn()` per render) so a test can assert retry actually
// called it, the same way `respecMutateAsync` is already asserted on below.
const speciesRefetch = vi.fn();

const baseline = { Might: 500, Vigor: 300, Fortitude: 200 };

let speciesData:
  | {
      speciesId: string;
      level: number;
      budget: number;
      spent: number;
      withinBudget: boolean;
      hasOverride: boolean;
      shares: Record<string, number>;
      baseline: Record<string, number>;
    }
  | undefined;
// G6: lets a test put the species query into its OWN error state independently of `speciesData`
// being undefined -- a failed query has no data either, and the panel must tell the two apart.
let speciesIsError = false;

let priceData: { speciesId: string; respecCount: number; priceResource: string; priceAmount: number; everRespecced: boolean } | undefined;
// G7: same idea for the respec price preview -- pending and failed both leave `data` undefined,
// so a test needs to say which one it means.
let priceIsLoading = false;
let priceIsError = false;

vi.mock("@/lib/bus", () => ({
  useSpeciesAptitudes: () => ({
    data: speciesData,
    isLoading: speciesData === undefined && !speciesIsError,
    isError: speciesIsError,
    refetch: speciesRefetch
  }),
  useSpeciesRespecPrice: () => ({ data: priceData, isLoading: priceIsLoading, isError: priceIsError }),
  useRespecSpecies: () => ({ mutateAsync: respecMutateAsync, isPending: false })
}));

vi.mock("@/lib/bus/demons", () => ({
  newCorrelationId: () => "corr-fixed"
}));

function freshState(overrides?: Partial<NonNullable<typeof speciesData>>) {
  return {
    speciesId: "fumeshroom",
    level: 21,
    budget: 1000,
    spent: 1000,
    withinBudget: true,
    hasOverride: false,
    shares: { ...baseline },
    baseline: { ...baseline },
    ...overrides
  };
}

describe("SpeciesBuildPanel", () => {
  beforeEach(() => {
    respecMutateAsync.mockReset();
    speciesRefetch.mockReset();
    speciesData = freshState();
    speciesIsError = false;
    priceData = { speciesId: "fumeshroom", respecCount: 0, priceResource: "Soul", priceAmount: 50, everRespecced: false };
    priceIsLoading = false;
    priceIsError = false;
    // The real bug the E2E round trip caught: the panel must seed its draft from the MUTATION's own
    // response, never by racing the query cache's refetch. Resolving with the posted `shares` here
    // (matching what the real server's respec endpoint actually echoes back) is what makes that path
    // exercised at all -- an unconfigured `vi.fn()` resolving to `undefined` would let `commit()`'s
    // `result.shares` throw silently into the try/catch without any test noticing.
    respecMutateAsync.mockImplementation(async (vars: { shares: Record<string, number> }) => ({
      speciesId: "fumeshroom",
      level: 21,
      priced: false,
      priceAmount: 0,
      respecCount: 0,
      soulBalance: 500,
      replay: false,
      shares: vars.shares
    }));
  });

  it("shows a loading state before the species data arrives", () => {
    speciesData = undefined;
    render(<SpeciesBuildPanel playerId={1} speciesId="fumeshroom" />);
    expect(screen.getByTestId("species-build-loading")).toBeInTheDocument();
  });

  it("G6: a failed species query renders the error state (with retry), never a permanent loading spinner", () => {
    speciesData = undefined;
    speciesIsError = true;
    render(<SpeciesBuildPanel playerId={1} speciesId="fumeshroom" />);

    expect(screen.getByTestId("species-build-error")).toBeInTheDocument();
    expect(screen.queryByTestId("species-build-loading")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(speciesRefetch).toHaveBeenCalledTimes(1);
  });

  it("renders the shipped baseline without an override", () => {
    render(<SpeciesBuildPanel playerId={1} speciesId="fumeshroom" />);
    expect(screen.getByTestId("species-build-status")).toHaveTextContent("shipped build");
    expect((screen.getByTestId("species-build-input-Might") as HTMLInputElement).value).toBe("500");
    // No deviation shown when the draft equals the baseline.
    expect(screen.queryByTestId("species-build-deviation-Might")).not.toBeInTheDocument();
  });

  it("renders an override as a deviation FROM the baseline, not as a standalone build", () => {
    speciesData = freshState({ hasOverride: true, shares: { Might: 0, Vigor: 0, Ferocity: 1000 } as Record<string, number>, baseline });
    // baseline still carries the shipped keys; shares reflects the override for whichever keys it set.
    speciesData!.baseline = baseline;
    render(<SpeciesBuildPanel playerId={1} speciesId="fumeshroom" />);
    expect(screen.getByTestId("species-build-status")).toHaveTextContent("overridden");
    expect((screen.getByTestId("species-build-input-Might") as HTMLInputElement).value).toBe("0");
    expect(screen.getByTestId("species-build-deviation-Might")).toHaveTextContent("vs shipped");
  });

  it("G5: a species with no build yet (budget 0, no override) renders honest empty-build copy, not the shipped-build line", () => {
    speciesData = freshState({
      level: 1,
      budget: 0,
      spent: 0,
      withinBudget: true,
      hasOverride: false,
      shares: { Might: 0, Vigor: 0, Fortitude: 0 },
      baseline: { Might: 0, Vigor: 0, Fortitude: 0 }
    });
    render(<SpeciesBuildPanel playerId={1} speciesId="fumeshroom" />);

    const status = screen.getByTestId("species-build-status");
    expect(status).toHaveTextContent(/hasn't grown a build yet/i);
    expect(status).not.toHaveTextContent("shipped build");

    // Save is disabled (nothing to save), and the reason is real rendered text -- not ONLY a
    // `title` attribute nobody hovers over.
    expect(screen.getByTestId("species-build-save")).toBeDisabled();
    const reason = screen.getByTestId("species-build-save-reason");
    expect(reason.textContent).toBeTruthy();
    expect(reason).toHaveTextContent(/hasn't earned any aptitude points yet/i);
  });

  it("budget refusal disables save, scope-locally, without clamping the input", () => {
    render(<SpeciesBuildPanel playerId={1} speciesId="fumeshroom" />);
    const might = screen.getByTestId("species-build-input-Might");
    fireEvent.change(might, { target: { value: "1500" } }); // over the 1000 budget
    expect(screen.getByTestId("species-build-save")).toBeDisabled();
    expect((might as HTMLInputElement).value).toBe("1500"); // never silently clamped (PS-8)
  });

  it("a revert to baseline (all-zero draft) saves immediately, without a confirm dialog, and the input reflects the server's own reply", async () => {
    speciesData = freshState({ hasOverride: true });
    render(<SpeciesBuildPanel playerId={1} speciesId="fumeshroom" />);
    for (const id of Object.keys(baseline)) {
      fireEvent.change(screen.getByTestId(`species-build-input-${id}`), { target: { value: "0" } });
    }
    fireEvent.click(screen.getByTestId("species-build-save"));

    expect(respecMutateAsync).toHaveBeenCalledWith(
      expect.objectContaining({ playerId: 1, speciesId: "fumeshroom", shares: expect.objectContaining({ Might: 0 }) })
    );
    expect(screen.queryByTestId("species-build-respec-confirm")).not.toBeInTheDocument();
    // The real bug the E2E test caught: this must reflect the MUTATION's response, not silently
    // stay at whatever was last typed nor jump back to a stale cached value.
    await waitFor(() => expect((screen.getByTestId("species-build-input-Might") as HTMLInputElement).value).toBe("0"));
  });

  it("a first override (never respecced) saves immediately, without a confirm dialog, and the input reflects the server's own reply", async () => {
    priceData!.everRespecced = false;
    render(<SpeciesBuildPanel playerId={1} speciesId="fumeshroom" />);
    // Redistribute within the same 1000 budget: Might down, Vigor up by the same amount.
    fireEvent.change(screen.getByTestId("species-build-input-Might"), { target: { value: "400" } });
    fireEvent.change(screen.getByTestId("species-build-input-Vigor"), { target: { value: "400" } });
    fireEvent.click(screen.getByTestId("species-build-save"));

    expect(respecMutateAsync).toHaveBeenCalled();
    expect(screen.queryByTestId("species-build-respec-confirm")).not.toBeInTheDocument();
    await waitFor(() => expect((screen.getByTestId("species-build-input-Might") as HTMLInputElement).value).toBe("400"));
  });

  it("a priced change shows the price BEFORE the confirm — the save button does not spend directly", () => {
    speciesData = freshState({ hasOverride: true });
    priceData = { speciesId: "fumeshroom", respecCount: 1, priceResource: "Soul", priceAmount: 75, everRespecced: true };
    render(<SpeciesBuildPanel playerId={1} speciesId="fumeshroom" />);

    // Redistribute within the same 1000 budget: Might down, Vigor up by the same amount.
    fireEvent.change(screen.getByTestId("species-build-input-Might"), { target: { value: "400" } });
    fireEvent.change(screen.getByTestId("species-build-input-Vigor"), { target: { value: "400" } });
    fireEvent.click(screen.getByTestId("species-build-save"));

    expect(respecMutateAsync).not.toHaveBeenCalled(); // not spent yet
    const dialog = screen.getByTestId("species-build-respec-confirm");
    expect(dialog).toHaveTextContent("75");
    expect(dialog).toHaveTextContent("soul");

    fireEvent.click(screen.getByTestId("species-build-respec-confirm-confirm"));
    expect(respecMutateAsync).toHaveBeenCalledWith(
      expect.objectContaining({ playerId: 1, speciesId: "fumeshroom" })
    );
  });

  it("cancelling the confirm dialog never spends", () => {
    speciesData = freshState({ hasOverride: true });
    priceData = { speciesId: "fumeshroom", respecCount: 1, priceResource: "Soul", priceAmount: 75, everRespecced: true };
    render(<SpeciesBuildPanel playerId={1} speciesId="fumeshroom" />);

    // Redistribute within the same 1000 budget: Might down, Vigor up by the same amount.
    fireEvent.change(screen.getByTestId("species-build-input-Might"), { target: { value: "400" } });
    fireEvent.change(screen.getByTestId("species-build-input-Vigor"), { target: { value: "400" } });
    fireEvent.click(screen.getByTestId("species-build-save"));
    fireEvent.click(screen.getByTestId("species-build-respec-confirm-cancel"));

    expect(respecMutateAsync).not.toHaveBeenCalled();
    expect(screen.queryByTestId("species-build-respec-confirm")).not.toBeInTheDocument();
  });

  it("G7: a pending price never lets Save spend silently -- it disables Save instead of defaulting to free", () => {
    speciesData = freshState({ hasOverride: true });
    priceData = undefined;
    priceIsLoading = true;
    render(<SpeciesBuildPanel playerId={1} speciesId="fumeshroom" />);

    // Redistribute within the same 1000 budget: Might down, Vigor up by the same amount. Not a
    // revert (not all-zero), so this would previously have been misread as `isFree`.
    fireEvent.change(screen.getByTestId("species-build-input-Might"), { target: { value: "400" } });
    fireEvent.change(screen.getByTestId("species-build-input-Vigor"), { target: { value: "400" } });

    expect(screen.getByTestId("species-build-save")).toBeDisabled();
    fireEvent.click(screen.getByTestId("species-build-save"));

    expect(respecMutateAsync).not.toHaveBeenCalled();
    expect(screen.queryByTestId("species-build-respec-confirm")).not.toBeInTheDocument();
  });

  it("G7: an errored price also never lets Save spend silently", () => {
    speciesData = freshState({ hasOverride: true });
    priceData = undefined;
    priceIsError = true;
    render(<SpeciesBuildPanel playerId={1} speciesId="fumeshroom" />);

    fireEvent.change(screen.getByTestId("species-build-input-Might"), { target: { value: "400" } });
    fireEvent.change(screen.getByTestId("species-build-input-Vigor"), { target: { value: "400" } });

    expect(screen.getByTestId("species-build-save")).toBeDisabled();
    fireEvent.click(screen.getByTestId("species-build-save"));

    expect(respecMutateAsync).not.toHaveBeenCalled();
    expect(screen.queryByTestId("species-build-respec-confirm")).not.toBeInTheDocument();
  });

  it("no engine vocabulary appears in the rendered copy", () => {
    speciesData = freshState({ hasOverride: true });
    render(<SpeciesBuildPanel playerId={1} speciesId="fumeshroom" />);
    const text = document.body.textContent ?? "";
    for (const forbidden of ["typeId", "scope_key", "AllocationScope", "DemonType"]) {
      expect(text).not.toContain(forbidden);
    }
  });
});
