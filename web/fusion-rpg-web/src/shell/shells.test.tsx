import { useState } from "react";
import { describe, expect, it } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { DialogShell } from "./DialogShell";
import { PanelShell } from "./PanelShell";
import { useLayerStack } from "./layerStack";
import { resetKeymapForTests } from "./keymap";
import { useGlobalKeys } from "./useGlobalKeys";

function OpenerHarness({ Shell, band }: { Shell: typeof PanelShell; band: "panel" | "dialog" }) {
  useGlobalKeys();
  const [open, setOpen] = useState(false);
  return (
    <div>
      <button data-testid="opener" onClick={() => setOpen(true)}>
        Open
      </button>
      <Shell
        open={open}
        onOpenChange={setOpen}
        title={band === "panel" ? "Roster" : "Confirm release"}
        testId="shell-under-test"
        footer={<button data-testid="footer-btn">Footer action</button>}
      >
        <button data-testid="body-btn-1">First</button>
        <button data-testid="body-btn-2">Second</button>
      </Shell>
    </div>
  );
}

describe.each([
  { name: "PanelShell", Shell: PanelShell, band: "panel" as const },
  { name: "DialogShell", Shell: DialogShell, band: "dialog" as const }
])("$name", ({ Shell, band }) => {
  beforeEach(() => {
    useLayerStack.setState({ layers: [] });
    resetKeymapForTests();
  });

  it("renders title, body and footer when open", () => {
    render(<Shell open onOpenChange={() => {}} title="Roster" testId="t">Body content</Shell>);
    expect(screen.getByTestId("t")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Roster" })).toBeInTheDocument();
    expect(screen.getByTestId("t-body")).toHaveTextContent("Body content");
  });

  it("renders nothing when closed", () => {
    render(<Shell open={false} onOpenChange={() => {}} title="Roster" testId="t">Body content</Shell>);
    expect(screen.queryByTestId("t")).not.toBeInTheDocument();
  });

  it("registers itself on the shared LayerStack in the right band while open, and clears on close", async () => {
    const user = userEvent.setup();
    render(<OpenerHarness Shell={Shell} band={band} />);
    expect(useLayerStack.getState().layers).toEqual([]);

    await user.click(screen.getByTestId("opener"));
    await waitFor(() => expect(screen.getByTestId("shell-under-test")).toBeInTheDocument());
    expect(useLayerStack.getState().layers.map((l) => l.band)).toEqual([band]);

    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByTestId("shell-under-test")).not.toBeInTheDocument());
    expect(useLayerStack.getState().layers).toEqual([]);
  });

  it("traps Tab focus inside the shell's own controls", async () => {
    const user = userEvent.setup();
    render(
      <div>
        <button data-testid="outside-sentinel">Outside</button>
        <OpenerHarness Shell={Shell} band={band} />
      </div>
    );
    await user.click(screen.getByTestId("opener"));
    const shell = await screen.findByTestId("shell-under-test");

    for (let i = 0; i < 8; i += 1) {
      await user.tab();
      expect(shell.contains(document.activeElement)).toBe(true);
    }
  });

  it("restores focus to the opener after closing", async () => {
    const user = userEvent.setup();
    render(<OpenerHarness Shell={Shell} band={band} />);
    const opener = screen.getByTestId("opener");

    opener.focus();
    await user.click(opener);
    await screen.findByTestId("shell-under-test");

    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByTestId("shell-under-test")).not.toBeInTheDocument());
    await waitFor(() => expect(document.activeElement).toBe(opener));
  });
});
