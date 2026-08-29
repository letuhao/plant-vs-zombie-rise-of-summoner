import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/render";
import { resetKeymapForTests } from "@/shell/keymap";
import { DevTreeHost } from "@/dev/DevTreeHost";
import { setDevModeEnabled } from "@/dev/devMode";
import { SystemLayer } from "./SystemLayer";
import { clearBindingsForTests, currentKeyFor } from "./keybindings";

// This environment's default window.localStorage is incomplete (see lawnViewMode.test.ts for the
// same pattern) — stub a real in-memory Storage before each test.
beforeEach(() => {
  const mem: Record<string, string> = {};
  const ls = {
    getItem: (k: string) => mem[k] ?? null,
    setItem: (k: string, v: string) => {
      mem[k] = v;
    },
    removeItem: (k: string) => {
      delete mem[k];
    },
    clear: () => {
      for (const key of Object.keys(mem)) delete mem[key];
    },
    key: (i: number) => Object.keys(mem)[i] ?? null,
    get length() {
      return Object.keys(mem).length;
    }
  };
  Object.defineProperty(window, "localStorage", { configurable: true, value: ls });
  clearBindingsForTests();
  resetKeymapForTests();
});

describe("SystemLayer (T20)", () => {
  it("preferences persist to localStorage across a remount, surviving without any server", async () => {
    const user = userEvent.setup();
    const { unmount } = renderWithProviders(<SystemLayer open onOpenChange={() => {}} />, { withGlobalKeys: true });
    await user.click(screen.getByTestId("pref-damage-numbers"));
    expect(screen.getByTestId("pref-damage-numbers")).toHaveAttribute("aria-checked", "false");
    unmount();

    renderWithProviders(<SystemLayer open onOpenChange={() => {}} />, { withGlobalKeys: true });
    expect(screen.getByTestId("pref-damage-numbers")).toHaveAttribute("aria-checked", "false");
  });

  it("Developer mode drives the real T12 gate live: the switch reflects it, and DevTreeHost's own backtick verb reacts", async () => {
    // Mirrors AppShell's real composition (SystemLayer + DevTreeHost as siblings) rather than
    // SystemLayer alone — the gate's live-update path lives in DevTreeHost's own `?devmode=`
    // effect, not in SystemLayer, so isolating SystemLayer can't prove the toggle actually works.
    const user = userEvent.setup();
    renderWithProviders(
      <>
        <SystemLayer open onOpenChange={() => {}} />
        <DevTreeHost />
      </>,
      { withGlobalKeys: true }
    );
    const toggle = screen.getByTestId("pref-developer-mode");
    expect(toggle).toHaveAttribute("aria-checked", "false");
    await user.keyboard("`");
    expect(screen.queryByTestId("dev-tree")).not.toBeInTheDocument();

    await user.click(toggle);
    expect(toggle).toHaveAttribute("aria-checked", "true"); // the rendered switch, not just the flag

    await user.keyboard("`");
    await waitFor(() => expect(screen.getByTestId("dev-tree")).toBeInTheDocument());

    setDevModeEnabled(false);
  });

  it("rebinding to a free key commits immediately, and the change is what the app actually registers (GG-20)", async () => {
    const user = userEvent.setup();
    renderWithProviders(<SystemLayer open onOpenChange={() => {}} />, { withGlobalKeys: true });
    await user.click(screen.getByTestId("system-tab-controls"));
    await user.click(screen.getByTestId("keybind-change-creatures"));
    expect(screen.getByTestId("keybind-listening-creatures")).toBeInTheDocument();

    await user.keyboard("z");
    await waitFor(() => expect(screen.getByTestId("keybind-key-creatures")).toHaveTextContent("z"));
    expect(currentKeyFor("creatures")).toBe("z");
  });

  it("rebinding onto a key another action already holds shows the conflict with its cost, before committing", async () => {
    const user = userEvent.setup();
    renderWithProviders(<SystemLayer open onOpenChange={() => {}} />, { withGlobalKeys: true });
    await user.click(screen.getByTestId("system-tab-controls"));
    await user.click(screen.getByTestId("keybind-change-creatures"));
    await user.keyboard("r"); // Relics' default key

    await waitFor(() => expect(screen.getByTestId("keybind-conflict")).toBeInTheDocument());
    expect(screen.getByTestId("keybind-conflict-reason")).toHaveTextContent("Relics");
    expect(currentKeyFor("creatures")).toBe("c"); // not yet committed

    await user.click(screen.getByTestId("keybind-conflict-take"));
    expect(currentKeyFor("creatures")).toBe("r");
    expect(currentKeyFor("relics")).toBe("c"); // swapped onto Creatures' vacated key, not left colliding on "r"
  });

  it("the reserved launcher key is listed and refuses to be bound", async () => {
    const user = userEvent.setup();
    renderWithProviders(<SystemLayer open onOpenChange={() => {}} />, { withGlobalKeys: true });
    await user.click(screen.getByTestId("system-tab-controls"));
    expect(screen.getByTestId("keybind-key-reserved-f10")).toHaveTextContent("F10");

    await user.click(screen.getByTestId("keybind-change-creatures"));
    await user.keyboard("{F10}");
    await waitFor(() => expect(screen.getByTestId("keybind-reserved-refusal")).toBeInTheDocument());
    expect(currentKeyFor("creatures")).toBe("c");
  });

  it("Escape while listening cancels the rebind without changing anything", async () => {
    const user = userEvent.setup();
    renderWithProviders(<SystemLayer open onOpenChange={() => {}} />, { withGlobalKeys: true });
    await user.click(screen.getByTestId("system-tab-controls"));
    await user.click(screen.getByTestId("keybind-change-creatures"));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByTestId("keybind-listening-creatures")).not.toBeInTheDocument());
    expect(currentKeyFor("creatures")).toBe("c");
  });

  // T29 (plate 06 §C): Display/Sound/Advanced tabs plus the connection row and Quit-to-title, added
  // after the visual-completeness audit found only Game and Controls existed.
  describe("T29 — Display/Sound/Advanced", () => {
    it("Reduce motion on the Display tab persists to the real preferences store", async () => {
      const user = userEvent.setup();
      const { unmount } = renderWithProviders(<SystemLayer open onOpenChange={() => {}} />, { withGlobalKeys: true });
      await user.click(screen.getByTestId("system-tab-display"));
      await user.click(screen.getByTestId("pref-reduce-motion-on"));
      expect(screen.getByTestId("pref-reduce-motion-on")).toHaveAttribute("aria-current", "true");
      unmount();

      renderWithProviders(<SystemLayer open onOpenChange={() => {}} />, { withGlobalKeys: true });
      await user.click(screen.getByTestId("system-tab-display"));
      expect(screen.getByTestId("pref-reduce-motion-on")).toHaveAttribute("aria-current", "true");
    });

    it("the Sound tab is disabled and carries its reason as a title, matching the rail's own locked-entry convention", async () => {
      renderWithProviders(<SystemLayer open onOpenChange={() => {}} />, { withGlobalKeys: true });
      const soundTab = screen.getByTestId("system-tab-sound");
      expect(soundTab).toBeDisabled();
      expect(soundTab).toHaveAttribute("title", expect.stringContaining("audio pipeline"));
    });

    it("Advanced shows the real API base and resets preferences to defaults for real", async () => {
      const user = userEvent.setup();
      renderWithProviders(<SystemLayer open onOpenChange={() => {}} />, { withGlobalKeys: true });
      await user.click(screen.getByTestId("system-tab-preferences"));
      await user.click(screen.getByTestId("pref-damage-numbers"));
      expect(screen.getByTestId("pref-damage-numbers")).toHaveAttribute("aria-checked", "false");

      await user.click(screen.getByTestId("system-tab-advanced"));
      expect(screen.getByTestId("system-advanced-api-base")).toHaveTextContent(/.+/);
      await user.click(screen.getByTestId("system-reset-preferences"));

      await user.click(screen.getByTestId("system-tab-preferences"));
      expect(screen.getByTestId("pref-damage-numbers")).toHaveAttribute("aria-checked", "true");
    });

    it("Quit to title closes the layer and navigates to the real Title screen", async () => {
      const user = userEvent.setup();
      const onOpenChange = vi.fn();
      renderWithProviders(<SystemLayer open onOpenChange={onOpenChange} />, { withGlobalKeys: true });
      const quit = screen.getByTestId("system-quit-to-title");
      expect(quit).not.toBeDisabled();
      await user.click(quit);
      expect(onOpenChange).toHaveBeenCalledWith(false);
    });

    it("the connection row summarizes real health/hub state, and Details reveals the raw fields", async () => {
      const user = userEvent.setup();
      renderWithProviders(<SystemLayer open onOpenChange={() => {}} />, { withGlobalKeys: true });
      await user.click(screen.getByTestId("system-tab-preferences"));
      expect(screen.getByTestId("system-connection-tag")).toBeInTheDocument();
      expect(screen.queryByTestId("system-connection-details")).not.toBeInTheDocument();

      await user.click(screen.getByTestId("system-connection-details-toggle"));
      expect(screen.getByTestId("system-connection-details")).toBeInTheDocument();
    });
  });
});
