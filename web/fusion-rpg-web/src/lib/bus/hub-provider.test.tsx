import { afterEach, describe, expect, it, vi } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import type { ReactNode } from "react";
import { HubProvider } from "./hub-provider";
import { queryKeys } from "./keys";

const handlers: Record<string, (...args: unknown[]) => void> = {};

vi.mock("./hub", () => ({
  getHubConnection: () => ({
    on: (ev: string, fn: (...args: unknown[]) => void) => {
      handlers[ev] = fn;
    },
    off: () => undefined,
    start: async () => undefined,
    stop: async () => undefined,
    invoke: async () => undefined,
    onreconnecting: () => undefined,
    onreconnected: () => undefined,
    onclose: () => undefined
  })
}));

function wrap(client: QueryClient) {
  return function W({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

afterEach(() => {
  vi.clearAllMocks();
  for (const k of Object.keys(handlers)) delete handlers[k];
});

describe("hub-provider PvzStats", () => {
  it("PvzStatsUpdated_invalidates_pvzStats_and_channel_keys", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, "invalidateQueries");
    render(
      <HubProvider>
        <div />
      </HubProvider>,
      { wrapper: wrap(client) }
    );
    await Promise.resolve();
    expect(handlers.PvzStatsUpdated).toBeTypeOf("function");
    handlers.PvzStatsUpdated!({ playerId: 7 });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: queryKeys.pvzStats(7) });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["pvzStats"] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["pvzStatsChannel"] });
  });

  it("PvzActivityUpdated_invalidates_activity_keys", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, "invalidateQueries");
    render(
      <HubProvider>
        <div />
      </HubProvider>,
      { wrapper: wrap(client) }
    );
    await Promise.resolve();
    handlers.PvzActivityUpdated!({ playerId: 4 });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: queryKeys.pvzActivity(4) });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["pvzActivity"] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["pvzActivityFacts"] });
  });

  it("RpgProgressionUpdated_invalidates_progression_keys", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, "invalidateQueries");
    render(
      <HubProvider>
        <div />
      </HubProvider>,
      { wrapper: wrap(client) }
    );
    await Promise.resolve();
    handlers.RpgProgressionUpdated!({ playerId: 9 });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: queryKeys.rpgProgressionSummary(9) });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: queryKeys.rpgProgressionStats(9) });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["rpgProgressionLedger", 9] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["rpgProgressionActors", 9] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["rpgProgressionActor", 9] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["rpgProgressionSummary"] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["rpgProgressionStats"] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["rpgProgressionActors"] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["rpgProgressionLedger"] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["rpgProgressionActor"] });
  });

  it("CommandersUpdated_invalidates_commanders_keys", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, "invalidateQueries");
    render(
      <HubProvider>
        <div />
      </HubProvider>,
      { wrapper: wrap(client) }
    );
    await Promise.resolve();
    expect(handlers.CommandersUpdated).toBeTypeOf("function");
    handlers.CommandersUpdated!({ playerId: 3 });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: queryKeys.commanders(3) });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["commanders"] });
  });

  it("AlmanacTextUpdated_invalidates_almanac_progression_and_types", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, "invalidateQueries");
    render(
      <HubProvider>
        <div />
      </HubProvider>,
      { wrapper: wrap(client) }
    );
    await Promise.resolve();
    expect(handlers.AlmanacTextUpdated).toBeTypeOf("function");
    handlers.AlmanacTextUpdated!({ side: "plant", typeId: 0, created: true, fieldCount: 3 });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["almanacDumps"] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["rpgProgressionActors"] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["rpgProgressionActor"] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["rpgProgressionSummary"] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["types"] });
  });
});
