import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode
} from "react";
import { useQueryClient } from "@tanstack/react-query";
import { getHubConnection } from "./hub";
import { keysForEventKind } from "./invalidate";
import { appendLogEvent, appendLogEvents } from "./log-store";
import { queryKeys } from "./keys";
import type { EventEnvelope, HealthDto, HubStatus } from "./types";
import { mergeCheatsPreservingDirty } from "./cheat-dirty";
import { bumpIconEpoch } from "./icon-epoch";
import type { CheatSnapshot } from "./types";

const HubStatusContext = createContext<HubStatus>("off");

export function useHubStatus(): HubStatus {
  return useContext(HubStatusContext);
}

export function HubProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<HubStatus>("off");
  const qc = useQueryClient();

  useEffect(() => {
    const c = getHubConnection();

    const onEvent = (e: EventEnvelope) => {
      appendLogEvent(e);
      for (const key of keysForEventKind(e.kind)) {
        void qc.invalidateQueries({ queryKey: key });
      }
    };

    const onBatch = (batch: { events?: EventEnvelope[] }) => {
      const items = batch?.events ?? [];
      if (!items.length) return;
      appendLogEvents(items);
      const seen = new Set<string>();
      for (const e of items) {
        for (const key of keysForEventKind(e.kind)) {
          const id = JSON.stringify(key);
          if (seen.has(id)) continue;
          seen.add(id);
          void qc.invalidateQueries({ queryKey: key });
        }
      }
    };

    const onHealth = (h: HealthDto) => {
      qc.setQueryData(queryKeys.health, h);
    };

    const onCheatsUpdated = (snap: unknown) => {
      if (!snap || typeof snap !== "object") return;
      const local = qc.getQueryData<CheatSnapshot>(queryKeys.cheats);
      qc.setQueryData(
        queryKeys.cheats,
        mergeCheatsPreservingDirty(snap as Record<string, unknown>, local)
      );
    };

    const onPvzStatsUpdated = (sheet: { playerId?: number } | null) => {
      if (sheet?.playerId != null) {
        void qc.invalidateQueries({ queryKey: queryKeys.pvzStats(sheet.playerId) });
      }
      void qc.invalidateQueries({ queryKey: ["pvzStats"] });
      void qc.invalidateQueries({ queryKey: ["pvzStatsChannel"] });
    };

    const onPvzActivityUpdated = (rollup: { playerId?: number } | null) => {
      if (rollup?.playerId != null) {
        void qc.invalidateQueries({ queryKey: queryKeys.pvzActivity(rollup.playerId) });
        void qc.invalidateQueries({ queryKey: queryKeys.pvzActivityFacts(rollup.playerId) });
      }
      void qc.invalidateQueries({ queryKey: ["pvzActivity"] });
      void qc.invalidateQueries({ queryKey: ["pvzActivityFacts"] });
    };

    const onRpgProgressionUpdated = (msg: { playerId?: number } | null) => {
      if (msg?.playerId != null) {
        void qc.invalidateQueries({ queryKey: queryKeys.rpgProgressionSummary(msg.playerId) });
        void qc.invalidateQueries({ queryKey: queryKeys.rpgProgressionStats(msg.playerId) });
        void qc.invalidateQueries({ queryKey: ["rpgProgressionLedger", msg.playerId] });
        void qc.invalidateQueries({ queryKey: ["rpgProgressionActors", msg.playerId] });
        void qc.invalidateQueries({ queryKey: ["rpgProgressionActor", msg.playerId] });
      }
      void qc.invalidateQueries({ queryKey: ["rpgProgressionSummary"] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionStats"] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionActors"] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionLedger"] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionActor"] });
    };

    const onTypeIconUpdated = () => {
      bumpIconEpoch();
      void qc.invalidateQueries({ queryKey: ["iconDumps"] });
    };

    const onAlmanacTextUpdated = () => {
      void qc.invalidateQueries({ queryKey: ["almanacDumps"] });
      // Promoted fields land on progression actor DTOs + types catalog.
      void qc.invalidateQueries({ queryKey: ["rpgProgressionActors"] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionActor"] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionSummary"] });
      void qc.invalidateQueries({ queryKey: ["types"] });
    };

    c.on("Event", onEvent);
    c.on("EventBatch", onBatch);
    c.on("Health", onHealth);
    c.on("CheatsUpdated", onCheatsUpdated);
    c.on("PvzStatsUpdated", onPvzStatsUpdated);
    c.on("PvzActivityUpdated", onPvzActivityUpdated);
    c.on("RpgProgressionUpdated", onRpgProgressionUpdated);
    c.on("TypeIconUpdated", onTypeIconUpdated);
    c.on("AlmanacTextUpdated", onAlmanacTextUpdated);

    c.onreconnecting(() => setStatus("off"));
    c.onreconnected(() => {
      setStatus("on");
      void c.invoke("Join", "web");
    });
    c.onclose(() => setStatus("err"));

    let cancelled = false;
    c.start()
      .then(() => {
        if (cancelled) return;
        setStatus("on");
        return c.invoke("Join", "web");
      })
      .catch(() => {
        if (!cancelled) setStatus("err");
      });

    return () => {
      cancelled = true;
      c.off("Event", onEvent);
      c.off("EventBatch", onBatch);
      c.off("Health", onHealth);
      c.off("CheatsUpdated", onCheatsUpdated);
      c.off("PvzStatsUpdated", onPvzStatsUpdated);
      c.off("PvzActivityUpdated", onPvzActivityUpdated);
      c.off("RpgProgressionUpdated", onRpgProgressionUpdated);
      c.off("TypeIconUpdated", onTypeIconUpdated);
      c.off("AlmanacTextUpdated", onAlmanacTextUpdated);
      void c.stop();
    };
  }, [qc]);

  return <HubStatusContext.Provider value={status}>{children}</HubStatusContext.Provider>;
}
