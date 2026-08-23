import { beforeEach, describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { Toasts } from "./Toasts";
import { useToastStack } from "./toastStack";

beforeEach(() => {
  useToastStack.getState().clear();
});

describe("Toasts", () => {
  it("renders nothing when the stack is empty", () => {
    render(<Toasts />);
    expect(screen.getByTestId("toast-stack")).toBeEmptyDOMElement();
  });

  it("renders a pushed toast's title and message", () => {
    useToastStack.getState().push({ tone: "bad", title: "Creature update failed", message: "Nothing changed." });
    render(<Toasts />);
    expect(screen.getByTestId("toast-title")).toHaveTextContent("Creature update failed");
    expect(screen.getByTestId("toast-message")).toHaveTextContent("Nothing changed.");
  });

  it("the stack container never blocks input, only the toasts themselves do", () => {
    useToastStack.getState().push({ tone: "ok", title: "Souls updated" });
    render(<Toasts />);
    expect(screen.getByTestId("toast-stack").className).toContain("pointer-events-none");
    const toastId = useToastStack.getState().toasts[0]!.id;
    expect(screen.getByTestId(`toast-${toastId}`).className).toContain("pointer-events-auto");
  });

  it("is band-toast, never a bespoke z-index", () => {
    render(<Toasts />);
    expect(screen.getByTestId("toast-stack").className).toContain("band-toast");
  });
});
