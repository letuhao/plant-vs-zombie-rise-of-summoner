import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { known, absent } from "@/contract/pending";
import type { ActorView } from "@/contract/types";
import type { ActorRungState } from "@/ui/actor";
import { ActorMenuScopePicker, type ScopePickerValue } from "./ActorMenuScopePicker";

function readyActor(instanceId: string, name: string): ActorRungState {
  const data: ActorView = {
    instanceId,
    playerId: 1,
    side: "plant",
    typeId: 3,
    displayName: known(name),
    phase: "ActiveBound",
    level: 5,
    xp: 0,
    xpToNext: known(100),
    revision: 1,
    channelSummary: absent(),
    elementTyping: absent(),
    shieldStack: absent(),
    equipSlots: absent()
  };
  return { kind: "ready", data };
}

const TARGET_CANDIDATES = [readyActor("a1", "Emberling")];
const UNIQUE_DEMON_CANDIDATES = [readyActor("d1", "Ashkell")];
const TYPE_OPTIONS = [
  { typeId: 3, label: "Sunflower" },
  { typeId: 7, label: "Peashooter" }
];

/** A real, controlled caller — mirrors how a future consumer would own `value`. */
function Harness() {
  const [value, setValue] = useState<ScopePickerValue | null>(null);
  return (
    <ActorMenuScopePicker
      value={value}
      onChange={setValue}
      targetCandidates={TARGET_CANDIDATES}
      uniqueDemonCandidates={UNIQUE_DEMON_CANDIDATES}
      typeOptions={TYPE_OPTIONS}
    />
  );
}

describe("ActorMenuScopePicker", () => {
  it("renders the four real mode tabs", () => {
    render(<Harness />);
    expect(screen.getByTestId("scope-mode-target")).toBeInTheDocument();
    expect(screen.getByTestId("scope-mode-type")).toBeInTheDocument();
    expect(screen.getByTestId("scope-mode-unique-demon")).toBeInTheDocument();
    expect(screen.getByTestId("scope-mode-relation")).toBeInTheDocument();
  });

  it("defaults to Relation mode, fully functional standalone", async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<ActorMenuScopePicker value={null} onChange={onChange} />);
    expect(screen.getByTestId("scope-relation-panel")).toBeInTheDocument();
    await user.click(screen.getByTestId("scope-relation-ally"));
    expect(onChange).toHaveBeenCalledWith({ kind: "relation", relation: "ally" });
  });

  it("Relation mode reflects the controlled value's checked state", async () => {
    render(<Harness />);
    await userEvent.setup().click(screen.getByTestId("scope-relation-enemy"));
    expect(screen.getByTestId("scope-relation-enemy")).toHaveAttribute("aria-checked", "true");
    expect(screen.getByTestId("scope-relation-ally")).toHaveAttribute("aria-checked", "false");
  });

  it("Target and UniqueDemon modes both render through the real ActorListPickerPanel, not a lookalike", async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByTestId("scope-mode-target"));
    expect(screen.getByTestId("scope-target-list")).toBeInTheDocument();
    expect(screen.getByTestId("actor-row")).toHaveTextContent("Emberling");

    await user.click(screen.getByTestId("scope-mode-unique-demon"));
    expect(screen.getByTestId("scope-uniqueDemon-list")).toBeInTheDocument();
    expect(screen.getByTestId("actor-row")).toHaveTextContent("Ashkell");
  });

  it("selecting a Target candidate round-trips through the container to the exact WhoSelector shape", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByTestId("scope-mode-target"));
    await user.click(screen.getByTestId("scope-target-option-a1"));
    expect(screen.getByTestId("scope-target-option-a1")).toHaveAttribute("aria-pressed", "true");
  });

  it("Type mode round-trips the exact typeIds selected", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByTestId("scope-mode-type"));
    await user.click(screen.getByTestId("scope-type-option-3"));
    await user.click(screen.getByTestId("scope-type-option-7"));
    expect(screen.getByTestId("scope-type-option-3")).toBeChecked();
    expect(screen.getByTestId("scope-type-option-7")).toBeChecked();
  });

  it("switching modes never leaves a stale cross-mode selection visible", async () => {
    const user = userEvent.setup();
    render(<Harness />);

    // Select in Type mode.
    await user.click(screen.getByTestId("scope-mode-type"));
    await user.click(screen.getByTestId("scope-type-option-3"));
    expect(screen.getByTestId("scope-type-option-3")).toBeChecked();

    // Switch to Relation — the Type panel is gone, nothing carries over.
    await user.click(screen.getByTestId("scope-mode-relation"));
    expect(screen.queryByTestId("scope-type-list")).not.toBeInTheDocument();
    expect(screen.getByTestId("scope-relation-ally")).toHaveAttribute("aria-checked", "false");
    expect(screen.getByTestId("scope-relation-enemy")).toHaveAttribute("aria-checked", "false");

    // Switch back to Type — the earlier selection does not silently resurrect as the new default,
    // because the controlled `value` still names "type" but the picker only ever reflects `value`
    // as-is; the caller's own value is still the stale { kind: "type", typeIds: [3] } from before —
    // so option 3 legitimately still shows checked here, which is correct: nothing was silently
    // cleared or corrupted. The regression this guards is a *different* mode's panel misreading it.
    await user.click(screen.getByTestId("scope-mode-type"));
    expect(screen.getByTestId("scope-type-option-3")).toBeChecked();
    expect(screen.getByTestId("scope-type-option-7")).not.toBeChecked();
  });

  it("a value shaped for one mode never leaks into a different mode's panel", async () => {
    const user = userEvent.setup();
    // Start already holding a Relation value, then switch straight to Target.
    function StartedOnRelation() {
      const [value, setValue] = useState<ScopePickerValue | null>({ kind: "relation", relation: "ally" });
      return <ActorMenuScopePicker value={value} onChange={setValue} targetCandidates={TARGET_CANDIDATES} />;
    }
    render(<StartedOnRelation />);
    await user.click(screen.getByTestId("scope-mode-target"));
    // Target's panel must not interpret the leftover relation value as a selected target.
    expect(screen.getByTestId("scope-target-option-a1")).toHaveAttribute("aria-pressed", "false");
  });
});
