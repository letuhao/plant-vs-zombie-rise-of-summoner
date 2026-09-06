using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Board;
using FusionRpg.Core.Battle.Siege;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Demons;
using FusionRpg.Core.World.District;

namespace FusionRpg.Core.World.Turn;

/// <summary>
/// base-defense `siege-resolver` (module 15, spec-siege-resolver.md): the `IBattleResolver`
/// implementation for <see cref="BattleKinds.District"/> — the world/battle join. Builds a real board
/// from <see cref="DistrictLayout"/>, places combatants (and any structures standing on the district's
/// slots) on it, runs a real <see cref="BattleEngine.Resolve"/>, evaluates <see cref="SiegeObjective"/>,
/// and translates the result back into a <see cref="BattleOutcome"/>. Delegates every non-district kind,
/// and every district request with no board projected, straight to
/// <see cref="PlaceholderBattleResolver"/> — the early return IS the feature-absence guarantee,
/// provable by construction rather than by a golden diff.
///
/// <para><b>No `IIntentSource` is ever constructed here.</b> `BattleEngine.Resolve`'s own internal
/// fallback (`intentSource ?? new StubIntentSource(view, state.Cooldowns, ...)`, confirmed by reading
/// `TimelineDispatch.cs`/`BasicAttack.cs`) already drives every actor with the shipped nearest-enemy
/// stub AI the moment the caller passes no `intentSource` at all — which is exactly what "a siege
/// resolves and is playable with no FE" (this module's own success criterion) needs. Wiring a played
/// side through `Battle/Siege/SiegeAi.cs`'s `SiegeIntentSource` is real, deferred work for whichever
/// module first has a live human-input channel to plug into it (`siege-stage`), not this one.</para>
///
/// <para><b>Two things this pass deliberately does NOT solve</b> — named once, in `tasks/base-defense-todo.md`
/// rather than guessed through under time pressure, and restated here so the code and the task list
/// agree: (1) a structure's `BattleActorSetup.Side` is a single, reversible convention (every structure
/// enters on the DEFENDER's side, so an attacker may destroy it and a defender never targets its own
/// wall) rather than a deep rule; (2) <see cref="SiegeObjective.SiegeCombatant.InCore"/> cannot be read
/// from a real final board position — `BattleReport`/`BattleActorResult` carry none, for any battle kind
/// (verified directly) — so every living combatant is passed as `InCore: true`, which only actually
/// matters for the DEFENDER side (`SiegeObjective.Evaluate` never reads the attacker's own `InCore`):
/// a defender who survives the fight is treated as still holding the Core regardless of where the round
/// loop actually left them standing.</para>
/// </summary>
public sealed class DistrictAssaultResolver : IBattleResolver
{
    public static readonly DistrictAssaultResolver Instance = new();

    const string AttackerSide = "squad";
    const string DefenderSide = "wave";

    public BattleOutcome Resolve(BattleRequest request, IReadOnlyList<WorldEntity> combatants, ulong seed)
    {
        if (request.Kind != BattleKinds.District || request.Board is null)
            return PlaceholderBattleResolver.Instance.Resolve(request, combatants, seed);

        var attacker = combatants.FirstOrDefault(e => string.Equals(e.EntityId, request.AttackerEntityId, StringComparison.Ordinal));
        if (attacker is null)
            return PlaceholderBattleResolver.Instance.Resolve(request, combatants, seed);

        var defender = request.DefenderEntityId is { } defenderId
            ? combatants.FirstOrDefault(e => string.Equals(e.EntityId, defenderId, StringComparison.Ordinal))
            : null;

        var board = request.Board;

        // Two assaults resolved inside the SAME turn share the SAME raw `seed` (verified directly in
        // TurnEngine.Step: one ulong, threaded unchanged through every phase) -- mixing it with this
        // battle's own id is this resolver's own job, matching DistrictLayout.DistrictSeed's own
        // established mixing pattern rather than inventing a second one.
        var battleSeed = SeededRng.DeriveStream(seed, request.BattleId).NextULong();

        var syntheticSector = new WorldSector
        {
            SectorId = board.SectorId,
            TypeId = board.SectorTypeId,
            DevelopmentLevel = board.DevelopmentLevel,
            Slots = board.Slots.Select(s => new WorldSlot { SlotIndex = s.SlotIndex, State = s.State }).ToList(),
        };
        var spec = DistrictLayout.Build(syntheticSector, board.WorldSeed, board.AttackerEdge);
        var boardState = new BoardState(spec);
        var districtSeed = DistrictLayout.DistrictSeed(board.WorldSeed, board.SectorId);
        var coreCenter = new GridPos(spec.Rows / 2, spec.Rows / 2);
        var coreSideCells = DistrictLayout.CoreSideCells(spec.Rows, SiegeTuningPolicy.District.CoreSideMilli);

        var structureSetups = PlaceStructures(board, spec, boardState, districtSeed, coreCenter, coreSideCells);

        // base-defense `siege-construction` (decision 27, 2026-09-06): the SAME deterministic
        // slot-to-cell mapping PlaceStructures just used, inverted, plus each slot's own SlotKind --
        // closes ConstructionPlacement.CanPlace's own named gap ("the tactical board has no mapping
        // from a GridPos cell to a world-layer SlotKind today"). An unrecognised SlotTypeId is skipped
        // rather than thrown on -- "cannot verify what may be built here" is refused construction, not
        // a crash, matching this file's own established "fall back rather than throw mid-turn" posture.
        var slotByCell = new Dictionary<GridPos, (int SlotIndex, SlotKind Kind)>();
        foreach (var slot in board.Slots)
        {
            if (!SlotTypeCatalog.IsKnown(slot.SlotTypeId)) continue;
            var cell = DistrictLayout.CellForSlot(districtSeed, slot.SlotIndex, spec, coreCenter, coreSideCells);
            slotByCell[cell] = (slot.SlotIndex, SlotTypeCatalog.Get(slot.SlotTypeId).Kind);
        }
        var constructionBoard = new ConstructionBoardContext(
            boardState, spec.Rows, SiegeTuningPolicy.District.CoreSideMilli,
            SiegeTuningPolicy.District.RampartThickness, slotByCell);

        var attackerKeys = new List<string>();
        var attackerSetups = BuildAnimateSetups(attacker, AttackerSide, attackerKeys);
        if (attackerSetups.Count == 0)
            return PlaceholderBattleResolver.Instance.Resolve(request, combatants, seed);

        var defenderKeys = new List<string>();
        var defenderSetups = defender is null ? new List<BattleActorSetup>() : BuildAnimateSetups(defender, DefenderSide, defenderKeys);

        var approachCells = OpenCellsInZone(spec, boardState, DistrictZone.Approach);
        var coreCells = OpenCellsInZone(spec, boardState, DistrictZone.Core);

        // A board too small for the forces standing on it falls back rather than throwing mid-turn --
        // the placeholder's own crude weight comparison is a better answer than an aborted turn.
        if (attackerKeys.Count > approachCells.Count || defenderKeys.Count > coreCells.Count)
            return PlaceholderBattleResolver.Instance.Resolve(request, combatants, seed);

        Placement.PlaceActors(boardState, attackerKeys, approachCells);
        if (defenderKeys.Count > 0) Placement.PlaceActors(boardState, defenderKeys, coreCells);

        var defenderSideSetups = defenderSetups.Concat(structureSetups).ToList();

        // Unopposed: nothing stands against the attacker at all (no defender entity, or one with no
        // living members and no structures on the board). BattleEngine.Resolve throws on an empty
        // Wave, and there is nothing to simulate -- SiegeObjective.Evaluate below reads an empty
        // defender combatant list and resolves CoreTaken on its own, with no round loop needed.
        BattleReport? report = null;
        if (defenderSideSetups.Count > 0)
        {
            var setup = new BattleSetup
            {
                WaveId = "district:" + request.LocationId,
                Squad = attackerSetups,
                Wave = defenderSideSetups,
            };
            report = BattleEngine.Resolve(setup, battleSeed,
                profile: BattleModeProfileCatalog.Resolve(BattleModeProfileCatalog.SiegeId),
                board: boardState,
                containerResolver: ConstructionActions.ContainerResolver,
                onEffectHostReady: host =>
                {
                    host.ConstructionBoard = constructionBoard;
                    // 15.3b (2026-09-06): the four construction effects, upserted BEFORE BindContainers
                    // runs later in this same BattleRunState constructor (onEffectHostReady fires at
                    // BattleRunState.cs:330, BindContainers at :473 -- confirmed by reading the file, not
                    // assumed) so a builder actor's own container grant resolves correctly. No actor
                    // holds a construction action yet -- that is `siege-ai`'s own live intent source's
                    // job (this module's own doc comment above), not this resolver's.
                    if (host.Bag.Catalog is Effects.InMemoryEffectCatalog catalog)
                        foreach (var def in ConstructionActions.CompiledEffects) catalog.Upsert(def);
                });
        }

        var resultByKey = report?.Actors.ToDictionary(a => a.Key, StringComparer.Ordinal)
            ?? new Dictionary<string, BattleActorResult>(StringComparer.Ordinal);

        var siegeCombatants = new List<SiegeCombatant>();
        AddSiegeCombatants(siegeCombatants, attackerKeys, AttackerSide, resultByKey);
        if (defender is not null) AddSiegeCombatants(siegeCombatants, defenderKeys, DefenderSide, resultByKey);

        var objective = SiegeObjective.Evaluate(siegeCombatants, DefenderSide, AttackerSide);

        var sides = new List<BattleSideOutcome>
        {
            BuildSideOutcome(attacker, attackerKeys, resultByKey, routed: objective == SiegeOutcomeKind.AssaultBroken),
        };
        if (defender is not null)
            sides.Add(BuildSideOutcome(defender, defenderKeys, resultByKey, routed: objective == SiegeOutcomeKind.CoreTaken));

        var winnerEntityId = objective switch
        {
            SiegeOutcomeKind.CoreTaken => attacker.EntityId,
            SiegeOutcomeKind.AssaultBroken => defender?.EntityId,
            _ => null,
        };

        return new BattleOutcome
        {
            BattleId = request.BattleId,
            WinnerEntityId = winnerEntityId,
            Sides = sides.OrderBy(s => s.EntityId, StringComparer.Ordinal).ToList(),
            SlotResults = BuildSlotResults(board, resultByKey, constructionBoard.Placed),
            EngineVersion = BattleRuleset.EngineVersion,
            RulesetVersion = BattleRuleset.RulesetVersion,
            Seed = battleSeed,
            // base-defense `siege-engagement` (module 20, decision 24): an Inconclusive objective is
            // Spent -- the everyday case, the siege continues next turn (assuming a later fix teaches
            // the movement/contact phase to keep re-issuing a district request; see this resolver's
            // own module notes for that named, un-started gap).
            Exit = SiegeEngagement.ExitFor(objective, sides, attacker.EntityId),
        };
    }

    /// <summary>
    /// base-defense `siege-construction`: the pre-existing structure-damage half of the seam, found
    /// unwired while building the new structure-PLACEMENT half (`structure.place`) alongside it — this
    /// resolver has always built a `resultByKey` entry for every `"slot:{index}"` structure key
    /// (<see cref="AddSiegeCombatants"/>'s own key format matches <see cref="PlaceStructures"/>'s), but
    /// nothing ever read it back into <see cref="BattleOutcome.SlotResults"/>, so a structure's HP
    /// damage from a real district fight was silently discarded on every return from this method until
    /// now. Scoped deliberately to HP/destruction only: <see cref="SlotOutcome.HeldByFactionId"/> is
    /// passed straight through from <paramref name="board"/>'s own projected ownership (a no-op write)
    /// rather than computed from post-battle occupation — capture-based ownership transfer for an
    /// EXISTING structure's own slot is a separate, unscoped question this fix does not also attempt
    /// (F11's capture-transfers-the-stockpile rule belongs to `siege-economy`'s own `SiegeDepot`, not
    /// this field). A slot with no `resultByKey` entry (never fielded — the whole fight was unopposed
    /// with no structures at all, so `BattleEngine.Resolve` never ran) contributes nothing, matching
    /// every existing caller's exact current behaviour for that case.
    /// </summary>
    internal static IReadOnlyList<SlotOutcome> BuildSlotResults(
        BoardProjection board, IReadOnlyDictionary<string, BattleActorResult> resultByKey,
        IReadOnlyList<StructurePlacementRecord>? placed = null)
    {
        var outcomes = new Dictionary<int, SlotOutcome>();
        foreach (var slot in board.Slots)
        {
            if (slot.StructureId is null) continue;
            if (!resultByKey.TryGetValue($"slot:{slot.SlotIndex}", out var result)) continue;

            outcomes[slot.SlotIndex] = new SlotOutcome
            {
                SlotIndex = slot.SlotIndex,
                StructureHp = result.HpRemaining,
                StructureDestroyed = !result.Survived,
                HeldByFactionId = slot.OwnerFactionId,
            };
        }

        // base-defense `siege-construction`: a structure PLACED this battle (any of the four
        // acquisition paths, all resolved through `structure.place`) wins over anything computed
        // above for the same slot. Not really a merge conflict -- the placement gate itself refuses
        // building on an occupied cell, so a slot cannot be both "an existing structure that fought"
        // and "freshly placed" in the same battle; overwriting only guards a hypothetical double-count.
        foreach (var record in placed ?? Array.Empty<StructurePlacementRecord>())
        {
            var def = StructureCatalog.Get(record.StructureId);
            outcomes[record.SlotIndex] = new SlotOutcome
            {
                SlotIndex = record.SlotIndex,
                StructurePlaced = record.StructureId,
                PlacedConstructionTurnsRemaining = record.Instant || def.BuildTurns <= 0 ? null : def.BuildTurns,
            };
        }

        return outcomes.Values.OrderBy(o => o.SlotIndex).ToList();
    }

    /// <summary>
    /// Places every structure standing on the district's slots at the SAME cell
    /// <see cref="DistrictLayout.Build"/> itself derives for that slot index — never through
    /// <see cref="ConstructionPlacement"/>, which gates NEW construction, not a structure the world
    /// already recorded standing. Returns the (Structure-kind) setups; the caller is responsible for
    /// excluding these cells from animate placement (<see cref="OpenCellsInZone"/> already does, by
    /// reading live board occupancy after this runs).
    /// </summary>
    static List<BattleActorSetup> PlaceStructures(
        BoardProjection board, GridSpec spec, BoardState boardState,
        ulong districtSeed, GridPos coreCenter, int coreSideCells)
    {
        var setups = new List<BattleActorSetup>();
        foreach (var slot in board.Slots)
        {
            if (slot.StructureId is not { } structureId || !StructureCatalog.IsKnown(structureId)) continue;

            var def = StructureCatalog.Get(structureId);
            var maxHp = StructureDef.MaxHpOf(def, board.DevelopmentLevel);
            var key = $"slot:{slot.SlotIndex}";

            setups.Add(new BattleActorSetup
            {
                Key = key,
                // Decision 4: a structure has no ownership. One fixed, reversible convention: every
                // structure enters on the DEFENDER's side, so the attacker's own units may target and
                // destroy it while the defender's own units never attack their own wall. See this
                // class's own top comment for why this is named as a convention, not a deep rule.
                Side = DefenderSide,
                SpeciesId = structureId,
                Level = 0,
                MaxHp = slot.StructureHp ?? maxHp,
                Atk = 0,
                Defense = 0,
                Kind = CombatantKind.Structure,
            });

            var cell = DistrictLayout.CellForSlot(districtSeed, slot.SlotIndex, spec, coreCenter, coreSideCells);
            boardState.Place(key, cell);
        }

        return setups;
    }

    /// <summary>
    /// One `BattleActorSetup` per LIVING member (`Math.Max(0, member.Hp - member.Wounds) > 0` — the
    /// same effective-HP formula <see cref="PlaceholderBattleResolver.Strength"/> already establishes
    /// for this program), reusing the real, shipped, Core-only pattern
    /// <see cref="Battle.WaveCatalog"/> already uses for AI-side content: species-derived
    /// Element/Traits/AttackInterval, magnitudes from <see cref="BattleRuleset.BaseHp"/>/
    /// <see cref="BattleRuleset.BaseAtk"/>/<see cref="BattleRuleset.BaseDefense"/>. Appends each built
    /// key to <paramref name="keys"/> in the same order, so the caller can place and later read results
    /// back by the exact same key list.
    ///
    /// <para><b>Deferred, real gap</b>: a player-owned specimen's real loadout/aptitude/equipment
    /// bonuses (<c>WebMatchService.BuildSquad</c>'s own, richer path) are NOT read here — that
    /// mechanism lives in `FusionRpg.Server` and needs a live `RpgStore`, which this Core-only,
    /// statics-constructible resolver cannot reach. Every legion member fights with the same flat,
    /// level-derived stats a wave enemy would, whether or not `WorldEntityMember.InstanceId` is set.
    /// </para>
    /// </summary>
    static List<BattleActorSetup> BuildAnimateSetups(WorldEntity entity, string side, List<string> keys)
    {
        var setups = new List<BattleActorSetup>();
        for (var i = 0; i < entity.Members.Count; i++)
        {
            var member = entity.Members[i];
            var effectiveHp = Math.Max(0, member.Hp - member.Wounds);
            if (effectiveHp <= 0) continue; // already gone -- never fielded

            var species = DemonSpeciesCatalog.Get(member.SpeciesId);
            var key = $"{entity.EntityId}:{i}";
            keys.Add(key);

            setups.Add(new BattleActorSetup
            {
                Key = key,
                Side = side,
                SpeciesId = member.SpeciesId,
                TypeId = species.DemonTypeId,
                Level = member.Level,
                ElementPrimary = species.ElementPrimary,
                ElementSecondary = species.ElementSecondary,
                TraitIds = species.TraitPool,
                MaxHp = effectiveHp,
                Atk = BattleRuleset.BaseAtk(member.Level),
                Defense = BattleRuleset.BaseDefense(member.Level),
                AttackIntervalMs = species.AttackIntervalMs,
            });
        }

        return setups;
    }

    /// <summary>Every `Open` cell of the given zone, currently unoccupied, ordinal (row, then column)
    /// for determinism — a plain deterministic order rather than "nearest the entry edge," which is a
    /// deferred realism polish, not a correctness requirement for `Placement.PlaceActors` (any valid,
    /// deterministic cell list works).</summary>
    static List<GridPos> OpenCellsInZone(GridSpec spec, BoardState boardState, DistrictZone zone)
    {
        var district = SiegeTuningPolicy.District;
        var cells = new List<GridPos>();
        for (var r = 0; r < spec.Rows; r++)
        for (var c = 0; c < spec.Cols; c++)
        {
            var p = new GridPos(r, c);
            if (spec.TerrainAt(p) != CellTerrain.Open) continue;
            if (DistrictLayout.ZoneOf(p, spec.Rows, district.CoreSideMilli, district.RampartThickness) != zone) continue;
            if (boardState.OccupantAt(p) is not null) continue;
            cells.Add(p);
        }

        return cells;
    }

    static void AddSiegeCombatants(
        List<SiegeCombatant> combatants, IReadOnlyList<string> keys, string side,
        IReadOnlyDictionary<string, BattleActorResult> resultByKey)
    {
        foreach (var key in keys)
        {
            // A key with no result (never reached, e.g. an unopposed fight that never called
            // BattleEngine.Resolve) is still alive -- it never fought at all.
            var alive = !resultByKey.TryGetValue(key, out var result) || result.Survived;
            combatants.Add(new SiegeCombatant(key, side, Alive: alive, Withdrawn: false, InCore: true, Kind: CombatantKind.Animate));
        }
    }

    /// <summary>Translates this side's battle result back into world state — the same
    /// entering-effective-hp / new-total-wounds composition <see cref="PlaceholderBattleResolver.Wounded"/>
    /// already establishes: a member entered with `member.Hp - member.Wounds` effective HP; the battle
    /// leaves it with `HpRemaining` of THAT; the new total wounds relative to the member's own full
    /// `Hp` is `member.Hp - HpRemaining`.</summary>
    static BattleSideOutcome BuildSideOutcome(
        WorldEntity entity, IReadOnlyList<string> keys,
        IReadOnlyDictionary<string, BattleActorResult> resultByKey, bool routed)
    {
        var survivors = new List<WorldEntityMember>(entity.Members.Count);
        for (var i = 0; i < entity.Members.Count; i++)
        {
            var member = entity.Members[i];
            var key = $"{entity.EntityId}:{i}";
            if (!keys.Contains(key))
            {
                // Never fielded (already at zero effective HP before this fight) -- carries forward
                // unchanged rather than being silently dropped from the roster.
                survivors.Add(member);
                continue;
            }

            if (!resultByKey.TryGetValue(key, out var result))
            {
                // Fielded, but the fight never actually ran (unopposed) -- unchanged.
                survivors.Add(member);
                continue;
            }

            var newWounds = checked(member.Hp - result.HpRemaining);
            if (newWounds < member.Hp) survivors.Add(member with { Wounds = checked((int)newWounds) });
        }

        return new BattleSideOutcome
        {
            EntityId = entity.EntityId,
            Survivors = survivors,
            Destroyed = survivors.Count == 0,
            Routed = routed && survivors.Count > 0,
        };
    }
}
