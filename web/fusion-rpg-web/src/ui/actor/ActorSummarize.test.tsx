import { describe, expect, it } from "vitest";
import { known } from "@/contract/pending";
import { ActorSummarize } from "./ActorSummarize";
import { render, screen } from "@testing-library/react";

describe("ActorSummarize", () => {
  it("shows name and Lv · role only — no species/element chips on the rail", () => {
    render(
      <ActorSummarize
        displayName={known("Emberling")}
        instanceId="a1"
        level={14}
        roleLabel="Plant"
        side="plant"
        collapsed={false}
        sheet={{
          instanceId: "a1",
          playerId: 1,
          side: "plant",
          typeId: 3,
          displayName: "Emberling",
          speciesId: "ember-sprout",
          speciesName: "Ember Sprout",
          phase: "ActiveBound",
          roleLabel: "Specimen",
          level: 14,
          xp: 2140,
          xpToNext: 3400,
          elementTyping: { primary: "fire", secondary: "earth" },
          derived: [],
          primary: []
        }}
      />
    );
    expect(screen.getByTestId("actor-summarize-meta")).toHaveTextContent("Lv 14 · Specimen");
    expect(screen.queryByText("Ember Sprout")).not.toBeInTheDocument();
    expect(screen.queryByTestId("actor-summarize-element-fire")).not.toBeInTheDocument();
  });
});
