import { Button } from "@/ui";

/**
 * Plate 01 §D's authored first-run beat (GG-43/GG-44): a written beginning, not an empty box.
 * No name input here — `CreaturesLayer.tsx`'s own comment confirms display-name resolution isn't
 * wired anywhere in the FE yet, so a text field would be a non-functional control (fe-essentials
 * spec-onboarding-first-run.md Assumption 1). "Bind" reaches the same real destination the old
 * CTA already reached — this is a reskin of the entry point, not a new mechanism.
 */
export function FirstRunReveal({ onBind }: { onBind: () => void }) {
  const handleBind = () => {
    console.debug("[fe-essentials] first-run reveal: bind clicked", { testId: "focus-card-cta" });
    onBind();
  };

  return (
    <div
      className="grid place-items-center gap-4 rounded-md border border-border-control bg-panel p-8 text-center"
      data-testid="focus-card-first-run"
    >
      <span className="text-6xl leading-none" aria-hidden="true">
        🌻
      </span>
      <div>
        <h3 className="font-display text-lg text-text">This one answered</h3>
        <p className="mt-1 max-w-xs text-sm text-muted">
          A sunflower has bound itself to you. It will remember what it learns, and it will come back
          after every night.
        </p>
      </div>
      <Button data-testid="focus-card-cta" onClick={handleBind}>
        Bind
      </Button>
    </div>
  );
}
