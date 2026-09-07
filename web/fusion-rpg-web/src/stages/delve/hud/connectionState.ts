/**
 * The live-session connection state (D5.6, spec-delve-stage.md §7's band-1 HUD row: "...the initiative
 * rail during a fight, connection state"). §9's own event table names the real states a live SignalR
 * session moves through — subscribed and live; reconnecting after a drop; **frozen**, after three
 * consecutive dwell timeouts, which the table says must "say so": *"Your band is waiting."* (§9's own
 * literal quoted copy, reused here verbatim, not reworded) — and a resume that "replays the recorded
 * prefix, then goes live" again.
 *
 * **This module is deliberately NOT `Pending<T>`-shaped**, unlike every other real-but-unproduced field
 * in this program (`FightView`, `MemberView.nerveStage`, `DelveView.quests`, …). `Pending` means "a real
 * wire field with no adapter wired to it yet" — every one of those types is declared in
 * `contract/types.ts` because a concrete C# producer exists somewhere, even if no route serves it.
 * Connection state has no such producer anywhere in this codebase: `D2.16` (`tasks/party-dungeon-
 * todo.md`) found the concurrency primitive a live mid-battle pause/resume would need "has never been
 * built ANYWHERE in this codebase," not just unwired for party-dungeon, and `D5.11` (the SignalR client
 * itself) is correctly, honestly still blocked on that same finding. Modelling this as a wire-shaped
 * `Pending<ConnectionStateView>` would misstate the gap as "the server has this, nobody read it yet" —
 * it does not have it yet, at any layer. A plain, local, presentation-only status type is the honest
 * shape: a real, tested display component that any future live-session client can feed a value into,
 * with no contract-layer claim about a producer that does not exist.
 */
export type DelveConnectionStatus = "live" | "reconnecting" | "frozen" | "offline";

/**
 * Every branch here is real player copy, not a placeholder — `"frozen"` reuses spec-delve-stage.md
 * §9's own quoted sentence exactly. `"offline"` is today's actual, honest default (see
 * `DelveHud.tsx`'s own doc comment): no live session has ever been opened, so nothing here is lying by
 * calling itself "Live" when no subscription exists.
 */
export function connectionStatusLabel(status: DelveConnectionStatus): string {
  switch (status) {
    case "live":
      return "Live";
    case "reconnecting":
      return "Reconnecting…";
    case "frozen":
      return "Your band is waiting.";
    case "offline":
      return "Not connected";
    default: {
      const exhaustive: never = status;
      throw new Error(`connectionStatusLabel: unhandled status ${String(exhaustive)}`);
    }
  }
}
