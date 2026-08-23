import { useState } from "react";
import {
  apiBase,
  useCreatePlayer,
  useHealth,
  useHubStatus,
  usePlayers,
  useSelectPlayer
} from "@/lib/bus";
import { Button, Select, StatusDot, TextInput } from "@/ui";

export function HudBar() {
  const health = useHealth();
  const players = usePlayers();
  const hub = useHubStatus();
  const selectPlayer = useSelectPlayer();
  const createPlayer = useCreatePlayer();
  const [newName, setNewName] = useState("");

  const currentId = players.data?.currentPlayerId ?? 1;
  const items = players.data?.items ?? [];

  return (
    <header
      className="band-hud relative flex flex-wrap items-center gap-4 border-b border-border bg-soil-raised px-5 py-3"
      data-testid="hud-bar"
    >
      <h1 className="font-display text-xl text-text" data-testid="hud-title">
        Rise of Summoner
      </h1>

      <label className="flex items-center gap-2 text-sm text-muted" data-testid="player-picker">
        Player
        <Select
          data-testid="player-select"
          value={currentId}
          onChange={(e) => void selectPlayer.mutateAsync(Number(e.target.value))}
        >
          {items.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </Select>
      </label>

      <span className="flex items-center gap-2" data-testid="player-create">
        <TextInput
          data-testid="player-create-input"
          value={newName}
          placeholder="New save"
          className="w-36"
          onChange={(e) => setNewName(e.target.value)}
        />
        <Button
          data-testid="player-create-btn"
          size="sm"
          onClick={() => {
            const name = newName.trim();
            if (!name) return;
            void createPlayer.mutateAsync(name).then(() => setNewName(""));
          }}
        >
          Create
        </Button>
      </span>

      <StatusDot
        data-testid="status-injector"
        status={health.data?.injectorConnected ? "on" : "off"}
        label={`injector ${health.data?.injectorConnected ? "on" : "off"}`}
      />
      <StatusDot data-testid="status-hub" status={hub} label={`live ${hub}`} />
      <span className="text-xs text-muted" data-testid="api-base">
        {apiBase() || window.location.origin}
      </span>
    </header>
  );
}
