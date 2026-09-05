import { useEffect, useState } from "react";
import type { NotifyCategory, NotifyChannel } from "./categories";
import { CHANNEL_SETTINGS_CHANGED_EVENT, channelFor, setChannel } from "./channelSettings";

export type ChannelControlProps = {
  category: NotifyCategory;
  /** True for a blocking rail item — visible but locked, never hidden (GG-55: the player learns the
   * rule instead of wondering why the switch did nothing). */
  locked?: boolean;
};

const CHANNELS: readonly { value: NotifyChannel; label: string }[] = [
  { value: "toast", label: "Toast" },
  { value: "rail", label: "Rail" },
  { value: "off", label: "Off" }
];

/**
 * world-stage W88 (spec-world-notify.md §6) — "Show ⟨category⟩ as… Toast · Rail · Off", applied to
 * the category and never to just this one message. Mounted both on a notification and in settings;
 * every mounted instance reads `channelSettings.ts`'s own shared store and re-syncs on its change
 * event, so two instances showing the same category can never disagree.
 */
export function ChannelControl({ category, locked = false }: ChannelControlProps) {
  const [channel, setChannelState] = useState<NotifyChannel>(() => channelFor(category));

  useEffect(() => {
    setChannelState(channelFor(category));
    const sync = () => setChannelState(channelFor(category));
    window.addEventListener(CHANNEL_SETTINGS_CHANGED_EVENT, sync);
    return () => window.removeEventListener(CHANNEL_SETTINGS_CHANGED_EVENT, sync);
  }, [category]);

  return (
    <div role="group" aria-label={`Show ${category} as`} data-testid={`channel-control-${category}`}>
      {CHANNELS.map((c) => (
        <button
          key={c.value}
          type="button"
          aria-pressed={channel === c.value}
          disabled={locked}
          title={locked ? "Locked while this item blocks the turn" : undefined}
          onClick={() => setChannel(category, c.value)}
        >
          {c.label}
        </button>
      ))}
    </div>
  );
}
