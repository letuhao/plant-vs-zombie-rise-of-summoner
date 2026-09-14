import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/render";
import { RiftPrologueDialog } from "./RiftPrologueDialog";

const mutateAsync = vi.fn();

vi.mock("@/lib/bus", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/bus")>();
  return { ...actual, useAcknowledgeOnboardingStory: () => ({ mutateAsync, isPending: false }) };
});

beforeEach(() => {
  mutateAsync.mockReset();
  mutateAsync.mockResolvedValue({ ok: true });
});

describe("RiftPrologueDialog", () => {
  it("walks the four beats and acknowledges completed exactly once", async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    const onCue = vi.fn();
    const onContinueToLawn = vi.fn();
    renderWithProviders(
      <RiftPrologueDialog open playerId={1} onClose={onClose} onCue={onCue} onContinueToLawn={onContinueToLawn} />
    );

    expect(screen.getByLabelText("Beat 1 of 4")).toBeInTheDocument();
    expect(onCue).toHaveBeenLastCalledWith("rift.portal.open");
    const next = screen.getByRole("button", { name: "Next" });
    await user.click(next);
    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(screen.getByRole("button", { name: "Next" }));
    expect(screen.getByRole("button", { name: "Anchor the lawn" })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Anchor the lawn" }));
    await user.click(screen.getByRole("button", { name: "Anchor the lawn" }));

    expect(mutateAsync).toHaveBeenCalledTimes(1);
    expect(mutateAsync).toHaveBeenCalledWith({ storyId: "rift-prologue", version: 1, outcome: "completed" });
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(onContinueToLawn).toHaveBeenCalledTimes(1);
    expect(onCue).toHaveBeenCalledTimes(4);
  });

  it("supports skip and keeps a failed acknowledgement bypassable", async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    const onContinueToLawn = vi.fn();
    mutateAsync.mockRejectedValueOnce(new Error("offline"));
    renderWithProviders(
      <RiftPrologueDialog open playerId={1} onClose={onClose} onContinueToLawn={onContinueToLawn} />
    );

    await user.click(screen.getByRole("button", { name: "Skip intro" }));
    expect(await screen.findByRole("status")).toHaveTextContent("try again next time");
    expect(screen.getByRole("button", { name: "Continue to lawn" })).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Continue to lawn" }));
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(onContinueToLawn).toHaveBeenCalledTimes(1);
  });
});
