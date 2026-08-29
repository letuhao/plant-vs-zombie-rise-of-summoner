import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { known, absent } from "@/contract/pending";
import type { ActorView } from "@/contract/types";
import type { ActorRungState } from "@/ui/actor";
import { ActorListPickerPanel } from "./ActorListPickerPanel";

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

describe("ActorListPickerPanel", () => {
  it("renders candidates through the real ActorRow, not a lookalike", () => {
    render(
      <ActorListPickerPanel kind="target" candidates={[readyActor("a1", "Emberling")]} value={null} onChange={vi.fn()} />
    );
    expect(screen.getByTestId("actor-row")).toBeInTheDocument();
    expect(screen.getByTestId("actor-name")).toHaveTextContent("Emberling");
  });

  it("emits a target-kind value with the candidate's instanceId when in target mode", async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<ActorListPickerPanel kind="target" candidates={[readyActor("a1", "Emberling")]} value={null} onChange={onChange} />);
    await user.click(screen.getByTestId("scope-target-option-a1"));
    expect(onChange).toHaveBeenCalledWith({ kind: "target", targetPtr: "a1" });
  });

  it("emits a uniqueDemon-kind value with the candidate's instanceId when in uniqueDemon mode", async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<ActorListPickerPanel kind="uniqueDemon" candidates={[readyActor("d1", "Ashkell")]} value={null} onChange={onChange} />);
    await user.click(screen.getByTestId("scope-uniqueDemon-option-d1"));
    expect(onChange).toHaveBeenCalledWith({ kind: "uniqueDemon", instanceId: "d1" });
  });

  it("marks the currently selected candidate as pressed", () => {
    render(
      <ActorListPickerPanel
        kind="target"
        candidates={[readyActor("a1", "Emberling")]}
        value={{ kind: "target", targetPtr: "a1" }}
        onChange={vi.fn()}
      />
    );
    expect(screen.getByTestId("scope-target-option-a1")).toHaveAttribute("aria-pressed", "true");
  });

  it("renders a clear empty state with no candidates", () => {
    render(<ActorListPickerPanel kind="target" candidates={[]} value={null} onChange={vi.fn()} />);
    expect(screen.getByTestId("scope-target-empty")).toBeInTheDocument();
  });

  it("renders non-ready states (loading/error) via ActorRow's own fallback, not selectable", () => {
    const candidates: ActorRungState[] = [{ kind: "loading" }, { kind: "error", message: "boom" }];
    render(<ActorListPickerPanel kind="target" candidates={candidates} value={null} onChange={vi.fn()} />);
    expect(screen.getByTestId("actor-row-loading")).toBeInTheDocument();
    expect(screen.getByTestId("actor-row-error")).toBeInTheDocument();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});
