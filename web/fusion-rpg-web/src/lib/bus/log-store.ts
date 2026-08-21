import type { EventEnvelope } from "./types";

const CAP = 800;
const HIT_KINDS = new Set(["combat.hit", "combat.hitland"]);

type Listener = () => void;

let events: EventEnvelope[] = [];
let membership: EventEnvelope[] = [];
let membershipSig = "";
let lastHitEvent: EventEnvelope | null = null;

const listeners = new Set<Listener>();
const lastHitListeners = new Set<Listener>();

function isHit(kind: string | undefined): boolean {
  return HIT_KINDS.has(kind ?? "");
}

/**
 * The live log + lawn rings are CAPTURE surfaces. Web-mode battles (game `webrpg-1`) resolve in
 * milliseconds and would render as a capture burst — they stay on their own runs/battle surfaces
 * (spec-match-source-core precondition 8). Unstamped events are legacy capture traffic.
 */
export function isCaptureGame(game: string | undefined | null): boolean {
  return game == null || game === "" || game.startsWith("pvzrh");
}

function emit() {
  for (const l of listeners) l();
}

function emitLastHit() {
  for (const l of lastHitListeners) l();
}

function membershipSignature(list: readonly EventEnvelope[]): string {
  return list.map((e) => `${e.id ?? ""}:${e.kind}`).join("\n");
}

function rebuildMembership(): void {
  const next = events.filter((e) => !isHit(e.kind));
  const sig = membershipSignature(next);
  if (sig === membershipSig) return;
  membershipSig = sig;
  membership = next;
}

function noteHits(newestFirst: readonly EventEnvelope[]): boolean {
  for (const e of newestFirst) {
    if (!isHit(e.kind)) continue;
    lastHitEvent = e;
    return true;
  }
  return false;
}

export function getLogEvents(): EventEnvelope[] {
  return events;
}

/** Hub ring without combat hits — same array identity when only hits append. */
export function getLawnMembershipRing(): EventEnvelope[] {
  return membership;
}

export function getLastHitEvent(): EventEnvelope | null {
  return lastHitEvent;
}

export function appendLogEvents(items: EventEnvelope[]): void {
  const capture = items.filter((e) => isCaptureGame(e.game));
  if (!capture.length) return;
  const newestFirst = [...capture].reverse();
  events = newestFirst.concat(events).slice(0, CAP);
  const hitChanged = noteHits(newestFirst);
  rebuildMembership();
  emit();
  if (hitChanged) emitLastHit();
}

export function appendLogEvent(item: EventEnvelope): void {
  if (!isCaptureGame(item.game)) return;
  events = [item, ...events].slice(0, CAP);
  const hitChanged = noteHits([item]);
  rebuildMembership();
  emit();
  if (hitChanged) emitLastHit();
}

export function clearLogEvents(): void {
  events = [];
  membership = [];
  membershipSig = "";
  const hadHit = lastHitEvent != null;
  lastHitEvent = null;
  emit();
  if (hadHit) emitLastHit();
}

export function subscribeLog(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function subscribeLastHit(listener: Listener): () => void {
  lastHitListeners.add(listener);
  return () => {
    lastHitListeners.delete(listener);
  };
}
