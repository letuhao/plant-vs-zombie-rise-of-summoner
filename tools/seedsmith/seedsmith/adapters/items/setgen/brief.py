"""seedsmith.adapters.items.setgen.brief — theme + aptitude + archetype → a brief.

**Motifs inline, no citation text.** `briefkit`'s own discipline: a brief carries the closed
vocabularies the model must choose from and the theme's own words, and nothing else — a citation
teaches the model to write about the document instead of the content.

Two things this brief deliberately does NOT contain, each because putting it there would break P1:

- **any number** — no tier, no magnitude, no per-mille, no AE budget. The model is not being asked
  to respect a budget; the distributor enforces it afterwards. Telling the model the budget invites
  it to reason numerically, which is the exact habit `audit_schema` exists to prevent.
- **any role outside the twelve** — the cap is applied here, in the brief, by construction. The
  model is never shown `head-guard`, so `SetRoleNotUniversal` is unproducible from a well-formed
  answer (ssot-sets §3.7 fires at LOAD; ~1,000 sets checked after the fact is a re-run).
"""
from __future__ import annotations

from .roles import HYBRID_CORE_ROLES
from .themes import Theme
from .tuning import SetCharmGenTuning
from .vocab import FamilyPick, Vocabulary

PROMPT_VERSION = "set-charm-gen/1"


def _core_roles(pick: FamilyPick) -> "tuple[str, ...]":
    """A family's roles, narrowed to the twelve before they reach the brief.

    ⚠ Found by a test: the shipped families list their legality on `head-guard` / `sense` /
    `ward-array` too, and printing that verbatim would put a dropped role in front of the model in
    the same document that tells it those roles do not exist. A family's legality on a role a set
    may never claim is not information the model can act on.
    """
    return tuple(r for r in pick.roles if r in HYBRID_CORE_ROLES)


def _pick_lines(picks: "tuple[FamilyPick, ...]", limit: int) -> str:
    shown = picks[:limit]
    lines = []
    for p in shown:
        core = _core_roles(p)
        lines.append(f"  - {p.pick_id}" + (f"  (roles: {', '.join(core)})" if core else ""))
    if len(picks) > limit:
        lines.append(f"  - ...and {len(picks) - limit} more")
    return "\n".join(lines)


def build_set_brief(theme: Theme, tuning: SetCharmGenTuning, vocabulary: Vocabulary, *,
                    member_count: "int | None" = None, capability_limit: int = 40,
                    stat_limit: int = 60) -> str:
    """One set, one theme. `member_count` defaults to the typical size; a grand set is the exception,
    not the pattern (ssot-sets §3.4), so it is always an explicit ask."""
    members = member_count or tuning.typical_members
    capability_pool = vocabulary.capability
    stat_pool = vocabulary.stat
    identity = (f"the demon species '{theme.display_name}'" if theme.population == "species"
                else f"the build '{theme.display_name}' ({theme.aptitude} / {theme.archetype})")
    anti = (f"\nAvoid entirely: {', '.join(theme.anti_motifs)}." if theme.anti_motifs else "")
    return f"""Author ONE equipment set for {identity}.

Motifs to express: {', '.join(theme.motifs)}.{anti}
How this theme expresses itself in an item: {theme.expression_item}

Choose, and nothing else:
1. `capability` — exactly ONE family from the list below. It is the set's identity and it sits at
   the set's LOWEST threshold, so a two-piece splash already gives the player the thing an ordinary
   item cannot roll.
2. `members` — {members} entries, each a (role, frame) pair. The role list below is closed and
   complete; at most one of armament-primary / armament-secondary, and no role twice.
3. `thresholds` — the piece counts, and for every threshold ABOVE the lowest, one to three stat
   families from the second list. The lowest threshold takes no families; it carries the capability.
4. `name`, `nameKey`, `flavor`.

Never choose a number, a strength, a duration or a tier. Those are resolved after you answer.

Legal member roles ({len(HYBRID_CORE_ROLES)}):
{chr(10).join('  - ' + r for r in HYBRID_CORE_ROLES)}

Capability families ({len(capability_pool)} picks):
{_pick_lines(capability_pool, capability_limit)}

Stat families ({len(stat_pool)} picks):
{_pick_lines(stat_pool, stat_limit)}

If this theme cannot carry a set you would be happy to ship, set `blocked` and say why."""


def build_charm_brief(theme: Theme, tuning: SetCharmGenTuning, vocabulary: Vocabulary, *,
                      stat_limit: int = 60) -> str:
    """One charm, one theme. Charms are the always-on, side-wide layer — the family split against
    `jewel-minor` (ssot-charms §3.6) is stated as a rule, not left to be inferred from examples."""
    anti = (f"\nAvoid entirely: {', '.join(theme.anti_motifs)}." if theme.anti_motifs else "")
    classes = ", ".join(c.id for c in tuning.charm_classes)
    return f"""Author ONE charm for the demon species '{theme.display_name}'.

Motifs to express: {', '.join(theme.motifs)}.{anti}
How this theme expresses itself in an item: {theme.expression_item}

A charm is carried by the commander, not worn by one actor: it is always on, it applies to every
deployed actor, and it buys that breadth with depth. So it carries FLAT effects only — never a
percentage, never a multiplier.

Choose, and nothing else:
1. `charmClass` — one of: {classes}. A signet is named, carries a drawback, and rolls nothing.
2. `axis` — one of offense, survivability, control, utility, economy.
3. `frameHint`, `families` (one or two always-on families from the list below), `name`, `nameKey`,
   `flavor`. A signet also names its `drawback` family.

Never choose a number, a cost, a strength or a tier. Those are resolved after you answer.

Always-on families ({len(vocabulary.stat)} picks):
{_pick_lines(vocabulary.stat, stat_limit)}

If this theme cannot carry a charm you would be happy to ship, set `blocked` and say why."""
