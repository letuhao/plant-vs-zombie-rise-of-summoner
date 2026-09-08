import type { NotifyCategory, NotifyChannel } from "./categories";
import { CATEGORY_DEFAULT_CHANNEL } from "./categories";

/**
 * world-stage W88 (spec-world-notify.md §6) — the persisted category→channel map. Player settings,
 * not tunables, following the same shape `layers/system/keybindings.ts` already established:
 * localStorage behind a try/catch (preferences degrade to session-only rather than throwing), and a
 * change event so the on-notification control and the settings list — two separate mounted
 * components reading the same category — can never drift apart, live, without a reload.
 */
const STORAGE_KEY = "fusionrpg.world-notify.channels.v1";
export const CHANNEL_SETTINGS_CHANGED_EVENT = "fusionrpg:world-notify-channels-changed";

function notifyChanged(): void {
  window.dispatchEvent(new Event(CHANNEL_SETTINGS_CHANGED_EVENT));
}

function readOverrides(): Partial<Record<NotifyCategory, NotifyChannel>> {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as unknown;
    if (typeof parsed !== "object" || parsed === null) return {};
    return parsed as Partial<Record<NotifyCategory, NotifyChannel>>;
  } catch {
    return {};
  }
}

function writeOverrides(overrides: Partial<Record<NotifyCategory, NotifyChannel>>): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(overrides));
  } catch {
    // Preferences degrade to session-only if storage is unavailable — never throw.
  }
}

/** The full, current category → channel table (defaults merged with any saved overrides). */
export function currentChannels(): Record<NotifyCategory, NotifyChannel> {
  return { ...CATEGORY_DEFAULT_CHANNEL, ...readOverrides() };
}

export function channelFor(category: NotifyCategory): NotifyChannel {
  return currentChannels()[category];
}

/** Applied to the category, never to one message — the sentence naming the category is what keeps
 * the scope of the change in no doubt (spec §6). */
export function setChannel(category: NotifyCategory, channel: NotifyChannel): Record<NotifyCategory, NotifyChannel> {
  const next = { ...readOverrides(), [category]: channel };
  writeOverrides(next);
  notifyChanged();
  return { ...CATEGORY_DEFAULT_CHANNEL, ...next };
}

/** Test-only: clears the persisted table without going through `setChannel`'s own change event. */
export function clearChannelSettingsForTests(): void {
  try {
    window.localStorage.removeItem(STORAGE_KEY);
  } catch {
    // ignore
  }
}
