using FusionRpg.Contracts;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Creatures;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Data;

public sealed partial class RpgStore
{
    /// <summary>The one stable display name a Zomboss-owned player row is found or created under —
    /// never a magic id/offset convention, since `players.id` carries no schema-level meaning beyond
    /// autoincrement (confirmed: no FOREIGN KEY constraints reference it anywhere in this schema).</summary>
    public const string ZombossPlayerName = "Zomboss";

    /// <summary>
    /// zomboss-deploy-ai T3.4 — find-or-create the dedicated player row Zomboss's own unique-actor
    /// mints are owned under, so <see cref="Battle.SpecimenOwnershipOracle"/>'s existing "which player
    /// deployed it" model needs ZERO changes to correctly tell a Zomboss-deployed specimen apart from
    /// the human player's own: they simply carry different `player_id` values, exactly like any other
    /// two real players already would. No schema change, no new table — `CreatePlayer` is already a
    /// real, unrestricted, existing call (`RpgStore.cs`); this only adds the idempotent lookup so a
    /// second Zomboss deploy in a later match reuses the SAME row instead of minting a new "Zomboss"
    /// player every time.
    /// </summary>
    public PlayerDto EnsureZombossPlayer()
    {
        var existing = ListPlayers().FirstOrDefault(p => string.Equals(p.Name, ZombossPlayerName, StringComparison.Ordinal));
        return existing ?? CreatePlayer(ZombossPlayerName);
    }

    /// <summary>
    /// zomboss-deploy-ai T3.4 — mints a fresh specimen of <paramref name="speciesId"/> under Zomboss's
    /// own player row (never the human player's), reusing the EXACT same mint mapping `ExecuteSummon`
    /// already establishes for a chosen species (`RpgStore.Summons.cs`: `Side`/`GameTypeId`/
    /// `ElementPrimary`/`ElementSecondary` straight off the catalog def, `Rarity` = the species' own
    /// `BaseRarity`, `TraitIds` via `SummonRoller.RollTraits` — the same shared trait-roll summons and
    /// wild joins already use, not a third trait-rolling scheme invented here) — never a bare literal,
    /// never a hand-built row. `origin: "zomboss"` is the one field genuinely new to this call site, so
    /// a specimen born this way is distinguishable in its own history from a player summon/fusion/quest
    /// mint without needing a separate table.
    /// </summary>
    public CreatureSpecimenDto MintForZomboss(string speciesId, ulong seed)
    {
        var zomboss = EnsureZombossPlayer();
        var species = Core.Creatures.CreatureSpeciesCatalog.Get(speciesId);
        var traits = SummonRoller.RollTraits(species, species.BaseRarity, SeededRng.DeriveStream(seed, "zomboss-deploy-ai:mint"));
        var (specimen, _) = MintCreature(zomboss.Id, new CreatureMintSpec
        {
            SpeciesId = species.SpeciesId,
            Side = species.Side,
            GameTypeId = species.GameTypeId,
            Rarity = species.BaseRarity.ToId(),
            Variant = "normal",
            ElementPrimary = species.ElementPrimary.ToElementId(),
            ElementSecondary = species.ElementSecondary?.ToElementId(),
            TraitIds = traits.ToList(),
            Origin = "zomboss",
        });
        return specimen;
    }
}
