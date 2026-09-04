using System.Text.Json;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Items.Drops;

/// <summary>
/// One loot event, as the SERVER sees it.
///
/// <para>⛔ <b>There is no correlation-id field, and that is the design.</b> §4.4: the correlation id
/// is derived from the source record on the server, never supplied by the client — a client that can
/// pick its own loot correlation can mint on demand. This is a deliberate difference from summon and
/// expedition dispatch, which DO take a client correlation id: those are player-initiated commands, a
/// loot event is a consequence of a recorded fact.</para>
///
/// <para><see cref="ThetaActor"/> is read by the caller through
/// <c>IPowerIndexProvider.ActorIndex(ctx)</c> and passed in, so this file declares no
/// <c>f(level)</c> of any kind (D18).</para>
/// </summary>
public sealed record LootRequest(
    string PlayerId,
    string SourceKind,
    string SourceId,
    ulong SourceSeed,
    int ThetaActor,
    long CatalogRevision = 0,
    long DropTableRevision = 0,
    IReadOnlyDictionary<string, int>? SquadFrameMilli = null);

/// <summary>One thing the event granted. Equipment carries the whole step 6…10 decision record.</summary>
public sealed record LootGrant(
    int Index,
    DropEntryKind Kind,
    string RefId,
    long Count,
    string AffixChannel,
    string? BaseTypeId = null,
    string? Frame = null,
    string? Role = null,
    string? RarityId = null,
    int RarityOrdinal = 0,
    int ItemLevel = 0,
    int MinTier = 0,
    int MaxTier = 0,
    int PrefixRolls = 0,
    int SuffixRolls = 0,
    int SocketCount = 0,
    ulong RollSeed = 0,
    bool EnvelopeNarrowed = false,
    bool PityForced = false,
    string? InstanceId = null);

/// <summary>The sealed outcome. Step 12 (REVEAL) is presentation only — this was decided at step 2.</summary>
public sealed record LootManifest(
    string CorrelationId,
    string TableId,
    ulong LootSeed,
    int ItemLevel,
    IReadOnlyList<LootGrant> Grants,
    IReadOnlyList<string> Notes,
    string ContextJson,
    LootPityState PityIn,
    LootPityState PityOut,
    string? FirstClearGrant,
    bool Replayed,
    string? ReplayedResultJson);

/// <summary>Step 9's mint, injected so this file stays pure and free of any store.</summary>
public readonly record struct LootMintResult(AtomRejection Rejection, string? InstanceId);

/// <summary>Everything the pipeline reads, resolved by the host. No I/O in this file.</summary>
public sealed record LootContentView(
    IReadOnlyDictionary<string, LootSourceRow> Sources,
    IReadOnlyDictionary<string, DropTableRow> Tables,
    IReadOnlyList<RarityRung> Ladder,
    Func<string, string, IReadOnlyList<string>> BaseTypesFor,
    Func<string, string, string, bool>? FirstClearAlreadyGranted = null,
    Func<string, string, string?>? RecordedManifestFor = null,
    Func<string, int, int, int>? DrawableAffixGroups = null,
    Func<LootGrant, LootMintResult>? Mint = null);

/// <summary>Server-derived correlation ids (§4.4). One shape per source kind, none client-reachable.</summary>
public static class LootCorrelation
{
    public static string Derive(string sourceKind, string sourceId) => sourceKind switch
    {
        "web-wave" => $"loot:{sourceId}",
        "expedition-tier" => $"loot:exp:{sourceId}",
        "world-sector" => $"loot:sector:{sourceId}",
        DropTableValidator.UndesignedSourceKind => $"loot:pvz:{sourceId}",
        _ => throw new ArgumentException($"no correlation shape for source kind '{sourceKind}'", nameof(sourceKind)),
    };
}

/// <summary>
/// I12's twelve ordered steps, plus module 11's <b>5a</b>.
///
/// <code>
///  0  LOOT EVENT           server-side fact; correlation id derived FROM the source record
///  1  IDEMPOTENCY GATE     hit on (player_id, correlation_id) → return the manifest, mint nothing
///  2  SEAL THE SEED        loot_seed = DeriveStream(sourceSeed, "loot:"+correlationId).NextULong()
///  3  ITEM LEVEL           content level + jitter                      [item.ilvl]
///  4  DROP TABLE           loot_source → table_id; reject if its ilvl band excludes it
///  5a VOLUME SCALE     ⭐  rollsEffective per group from Θ_actor        [item.volume.{table}.{group}]
///  5  GROUP DRAWS          each group draws rollsEffective times       [item.table.{t}.{g}]
///  6  BASE TYPE            frame → role → base type                    [item.base.{i}]
///  7  RARITY               weighted ladder draw, shifted, floored, pity-checked   [item.rarity.{i}]
///  8  ENVELOPE             (rolls, min_tier, max_tier) = band ∩ ilvl cap          [item.rolls.{i}]
///  9  AFFIX DRAW + FREEZE  Instantiator.TryInstantiate(...) carrying `channel`
/// 10  SOCKETS              I4's count rule, last so it can never shift an affix   [item.socket]
/// 11  PERSIST              ONE transaction (the STORE's job — this returns the manifest to persist)
/// 12  REVEAL               presentation only — the outcome was sealed at step 2
/// </code>
///
/// <para>The three contested orderings and why they hold: <b>level before table</b> (the band is a
/// filter, not a post-hoc correction); <b>base before rarity</b> (uniques and set pieces are defined
/// ON a base type); <b>sockets last</b> (a socket count consuming a stream earlier would move every
/// affix roll at that band with no content-hash change).</para>
/// </summary>
public static class LootPipeline
{
    static LootPipeline() => ContentRuleNamespaces.Register("drop");

    /// <summary>Notes written to <c>item_drop_log.notes</c> — the vocabulary, in one place.</summary>
    public const string NoteEnvelopeNarrowed = "envelope_narrowed";
    public const string NotePityForced = "pity_forced";

    public static AtomRejection Resolve(
        LootRequest request,
        LootContentView view,
        DropVolumeTuning tuning,
        LootPityState pity,
        out LootManifest? manifest)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (view is null) throw new ArgumentNullException(nameof(view));
        manifest = null;

        // ---- 0  LOOT EVENT ------------------------------------------------------------------------
        var correlationId = LootCorrelation.Derive(request.SourceKind, request.SourceId);

        // ---- 1  IDEMPOTENCY GATE -----------------------------------------------------------------
        // A retry mints NOTHING. It returns the recorded manifest, advances no counter, writes no
        // ledger row. The store's UNIQUE(player_id, correlation_id) is the second net under this one.
        if (view.RecordedManifestFor?.Invoke(request.PlayerId, correlationId) is { } recorded)
        {
            manifest = new LootManifest(correlationId, "", 0, 0, Array.Empty<LootGrant>(),
                Array.Empty<string>(), "{}", pity, pity, null, Replayed: true, recorded);
            return AtomRejection.Ok;
        }

        if (!view.Sources.TryGetValue(request.SourceKind + ":" + request.SourceId, out var source))
            return AtomRejection.ContentRule("drop.unknown-loot-source",
                $"no loot_source row for '{request.SourceKind}:{request.SourceId}'");

        // ⛔ Refused BY NAME, never defaulted to 1 (§4.1, §11 Q8).
        if (string.Equals(source.SourceKind, DropTableValidator.UndesignedSourceKind, StringComparison.Ordinal))
            return AtomRejection.ContentRule("drop.source-kind-undesigned",
                $"loot_source '{source.Key}' is of kind '{DropTableValidator.UndesignedSourceKind}' and no "
                + "contentLevel source exists for it; refused rather than defaulted");

        // ---- 2  SEAL THE SEED --------------------------------------------------------------------
        var lootSeed = SeededRng.DeriveStream(request.SourceSeed, LootStreams.LootSeed(correlationId)).NextULong();

        // ---- 3  ITEM LEVEL -----------------------------------------------------------------------
        // ⛔ Reads the CONTENT and nothing else. `request` supplies no level to this step, by design:
        // the moment item level tracks player level, every piece of content yields the same gear and
        // the map flattens (§4.1).
        var itemLevel = ItemLevel(source.ContentLevel, lootSeed, tuning);

        // ---- 4  DROP TABLE -----------------------------------------------------------------------
        if (!view.Tables.TryGetValue(source.TableId, out var table))
            return AtomRejection.ContentRule("drop.unknown-drop-table",
                $"loot_source '{source.Key}' points at table '{source.TableId}', which is not loaded");
        if (!table.Enabled)
            return AtomRejection.ContentRule("drop.table-disabled",
                $"table '{table.TableId}' is disabled; the row is kept and never draws (E5's rule)");
        if (table.MinIlvl is { } lo && itemLevel < lo)
            return AtomRejection.ContentRule("drop.ilvl-band-excludes",
                $"item level {itemLevel} is below table '{table.TableId}'s min_ilvl {lo}");
        if (table.MaxIlvl is { } hi && itemLevel > hi)
            return AtomRejection.ContentRule("drop.ilvl-band-excludes",
                $"item level {itemLevel} is above table '{table.TableId}'s max_ilvl {hi}");

        // ---- 5a  VOLUME SCALE --------------------------------------------------------------------
        var scaleMilli = DropVolume.VolumeScaleMilli(request.ThetaActor, tuning);

        var grants = new List<LootGrant>();
        var notes = new List<string>();
        var index = 0;
        var pityCursor = pity;

        var rejection = DrawTable(table, depth: 0);
        if (!rejection.IsOk) return rejection;

        // ---- first-clear grant (§3.5) — deterministic, fires once, never rolls ---------------------
        string? firstClear = null;
        if (source.FirstClearGrant is { Length: > 0 } grant
            && view.FirstClearAlreadyGranted?.Invoke(request.PlayerId, source.SourceKind, source.SourceId) != true)
        {
            firstClear = grant;
            grants.Add(new LootGrant(index++, DropEntryKind.Equipment, grant, 1, AffixChannels.Drop,
                ItemLevel: itemLevel, PrefixRolls: 0, SuffixRolls: 0));
        }

        manifest = new LootManifest(
            correlationId, table.TableId, lootSeed, itemLevel, grants, notes,
            ContextJson(request, scaleMilli), pity, pityCursor, firstClear,
            Replayed: false, ReplayedResultJson: null);
        return AtomRejection.Ok;

        AtomRejection DrawTable(DropTableRow t, int depth)
        {
            if (depth > tuning.MaxNestingDepth)
                return AtomRejection.ContentRule("drop.depth-exceeded",
                    $"table '{t.TableId}' nests past depth {tuning.MaxNestingDepth}");

            foreach (var group in t.Groups.OrderBy(g => g.Seq))
            {
                // ---- 5a per group: the fractional remainder is a Bernoulli on its OWN named stream.
                var volumeRng = new AtomRandom(lootSeed, LootStreams.Volume(t.TableId, group.GroupKey));
                // ⭐ The volume term applies at the TOP LEVEL only. A nested table draws its own
                // authored rolls: compounding Θ once per nesting level would reintroduce exactly the
                // quadratic shape D18 exists to refuse, and §5.3 property 5 is explicit that nesting
                // is for REUSE, not for depth.
                var draws = DropVolume.RollsEffective(group.Rolls, depth == 0 ? scaleMilli : 1000, volumeRng);

                var drawRng = new AtomRandom(lootSeed, depth == 0
                    ? LootStreams.GroupDraw(t.TableId, group.GroupKey)
                    : LootStreams.NestedGroupDraw(t.TableId, group.GroupKey, depth));

                var guaranteed = group.Entries.All(e => e.Kind != DropEntryKind.Nothing);

                for (long d = 0; d < draws; d++)
                {
                    // ---- 5  GROUP DRAWS ----------------------------------------------------------
                    var drawn = DropTableDraw.Draw(group.Entries, itemLevel, drawRng);
                    if (drawn.Entry is null)
                    {
                        // A group that was GUARANTEED and lost everything is a rejection, not a
                        // silent nothing: "silently under-filling is the failure this program exists
                        // to remove". A group with a `nothing` row simply yields nothing.
                        if (guaranteed)
                            return AtomRejection.Fail(AtomRejectionReason.UnsatisfiablePool,
                                $"table '{t.TableId}' group '{group.GroupKey}' is guaranteed and has no drawable entry");
                        continue;
                    }

                    var entry = drawn.Entry;
                    if (entry.Kind == DropEntryKind.Nothing) continue;

                    if (!DropTableDraw.IsAvailable(entry.Kind))
                        return AtomRejection.ContentRule("drop.entry-kind-unavailable",
                            $"table '{t.TableId}' group '{group.GroupKey}' seq {entry.Seq} drew "
                            + $"'{entry.Kind.ToString().ToLowerInvariant()}' — "
                            + DropTableDraw.UnavailableKinds[entry.Kind]);

                    if (entry.Kind == DropEntryKind.Table)
                    {
                        if (!view.Tables.TryGetValue(entry.RefId, out var nested))
                            return AtomRejection.ContentRule("drop.unknown-drop-table",
                                $"table '{t.TableId}' nests '{entry.RefId}', which is not loaded");
                        var nestedResult = DrawTable(nested, depth + 1);
                        if (!nestedResult.IsOk) return nestedResult;
                        continue;
                    }

                    if (entry.Kind != DropEntryKind.Equipment)
                    {
                        // Kind is drawn; quantity is rolled; nothing is scaled. Inclusive integers.
                        var qtyRng = new AtomRandom(lootSeed, LootStreams.Quantity(index));
                        long count = qtyRng.NextInclusive(entry.MinCount, entry.MaxCount);
                        grants.Add(new LootGrant(index++, entry.Kind, entry.RefId, count, entry.AffixChannel));
                        continue;
                    }

                    var equipment = MintEquipment(entry, out var equipmentRejection);
                    if (!equipmentRejection.IsOk) return equipmentRejection;
                    grants.Add(equipment!);
                }
            }

            return AtomRejection.Ok;
        }

        LootGrant? MintEquipment(DropTableEntryRow entry, out AtomRejection rejected)
        {
            var i = index++;

            // ---- 6  BASE TYPE ---------------------------------------------------------------------
            // ⏸ UNIFORM over the legal set, and this is a DEFERRAL with a reason, not a default.
            // I12 §3.3's smart loot is frame-weighted, and `frameWeight(f) = 250 + 750 ×
            // squadShareMilli(f)/1000` reads the deployed squad's FRAME MIX — `frame` exists on no
            // species type today (X1 `frame-classify`, resolved 2026-09-03 and UNBUILT), so a
            // frame-weighted draw over an unclassified roster is a uniform draw with extra code. It is
            // also the one bias that can break step 6, and step 6 feeds step 9's `affix_channel`,
            // which X4 weights composition off — landing a bias here before X4's weights exist means
            // the two get tuned against each other later, from opposite sides.
            // Trigger to revisit: X1 built AND X4 landed, whichever is later. Owner: this module.
            var legal = view.BaseTypesFor(entry.Frame!, entry.Role!);
            if (legal.Count == 0)
            {
                rejected = AtomRejection.ContentRule("drop.no-legal-base-type",
                    $"no base type exists for frame '{entry.Frame}' role '{entry.Role}'");
                return null;
            }

            var baseRng = new AtomRandom(lootSeed, LootStreams.BaseType(i));
            var baseTypeId = legal[baseRng.NextInclusive(0, legal.Count - 1)];

            // ---- 7  RARITY ------------------------------------------------------------------------
            var rarityRng = new AtomRandom(lootSeed, LootStreams.Rarity(i));
            var rarityResult = RarityDraw.Draw(view.Ladder, entry, pityCursor, tuning, rarityRng, out var pityOutcome);
            if (!rarityResult.IsOk) { rejected = rarityResult; return null; }
            pityCursor = pityOutcome.Next;
            if (pityOutcome.Forced && !notes.Contains(NotePityForced)) notes.Add(NotePityForced);

            var rung = view.Ladder.First(r => string.Equals(r.RarityId, pityOutcome.RarityId, StringComparison.Ordinal));

            // ---- 8  ENVELOPE ----------------------------------------------------------------------
            var rollsRng = new AtomRandom(lootSeed, LootStreams.Rolls(i));
            var envelope = DropEnvelope.Resolve(rung, itemLevel, rollsRng,
                view.DrawableAffixGroups is { } groups
                    ? (min, max) => groups(baseTypeId, min, max)
                    : null);
            if (envelope.Narrowed && !notes.Contains(NoteEnvelopeNarrowed)) notes.Add(NoteEnvelopeNarrowed);

            // ---- 9  AFFIX DRAW + FREEZE -------------------------------------------------------------
            // The roll seed is itself derived, so BOTH reproduction contracts hold at once: the atom
            // layer's (same container/revision/roll_seed ⇒ byte-identical instance) and this lane's
            // (same loot_seed/revisions ⇒ identical manifest).
            var rollSeed = SeededRng.DeriveStream(lootSeed, LootStreams.RollSeed(i)).NextULong();

            var grant = new LootGrant(
                i, DropEntryKind.Equipment, entry.RefId, 1, entry.AffixChannel,
                baseTypeId, entry.Frame, entry.Role, rung.RarityId, rung.Ordinal, itemLevel,
                envelope.MinTier, envelope.MaxTier, envelope.PrefixRolls, envelope.SuffixRolls,
                SocketCount: Sockets(rollSeed), RollSeed: rollSeed,
                EnvelopeNarrowed: envelope.Narrowed, PityForced: pityOutcome.Forced);

            if (view.Mint is { } mint)
            {
                var minted = mint(grant);
                if (!minted.Rejection.IsOk) { rejected = minted.Rejection; return null; }
                grant = grant with { InstanceId = minted.InstanceId };
            }

            rejected = AtomRejection.Ok;
            return grant;
        }
    }

    /// <summary>
    /// Step 3. <c>itemLevel = max(1, contentLevel + j)</c>, with <c>j ∈ {−1, 0, +1}</c> on
    /// <c>item.ilvl</c>. Takes no actor input, and that absence is the rule §4.1 states.
    /// </summary>
    public static int ItemLevel(int contentLevel, ulong lootSeed, DropVolumeTuning tuning)
    {
        var rng = new AtomRandom(lootSeed, LootStreams.ItemLevel);
        var roll = rng.NextPerMille();
        var jitter = roll < tuning.JitterDownBelowPerMille ? -1
            : roll < tuning.JitterFlatBelowPerMille ? 0
            : 1;
        return Math.Max(1, contentLevel + jitter);
    }

    /// <summary><c>level_req = max(1, itemLevel − 2)</c> — you should always be able to wear what the
    /// content you just beat dropped. The gate itself is shipped and fires at bind.</summary>
    public static int LevelReq(int itemLevel) => Math.Max(1, itemLevel - 2);

    /// <summary>
    /// ⏸ <b>Step 10 — a DOCUMENTED NO-OP that reserves its stream, and that is the whole point.</b>
    ///
    /// <para>Module 16 owns the count rule and D2 §6 makes <c>item_socket</c> its SSOT. Until module 7
    /// seeds <c>rarity.socket_min</c>/<c>socket_max</c> (SC7 refuses them today — no decided shape)
    /// and module 16 lands the rule, this resolves to <b>0 sockets</b>. It still DERIVES and ADVANCES
    /// its stream, so landing the real count later changes no other draw. A step inserted after the
    /// corpus ships is a migration; a no-op that reserves its stream is cheap.</para>
    /// </summary>
    static int Sockets(ulong rollSeed)
    {
        var socketRng = SeededRng.DeriveStream(rollSeed, LootStreams.Sockets);
        _ = socketRng.NextULong();
        return 0;
    }

    /// <summary>
    /// §4.3's replay input, written from the FIRST drop rather than retrofitted later.
    ///
    /// <para><c>smartLoot</c> and <c>squadFrameMix</c> are the two keys I12 §3.3's smart loot will
    /// write. They are reserved and written NOW — <c>smartLoot: false</c> and whatever mix the caller
    /// can observe — so §4.3's rule that "a settings change must not alter an already-sealed result"
    /// is true from the first drop, instead of a replay input being bolted onto a log that never had
    /// one.</para>
    /// </summary>
    public static string ContextJson(LootRequest request, long volumeScaleMilli)
    {
        var mix = new Dictionary<string, int>(StringComparer.Ordinal);
        if (request.SquadFrameMilli is not null)
            foreach (var kv in request.SquadFrameMilli.OrderBy(k => k.Key, StringComparer.Ordinal))
                mix[kv.Key] = kv.Value;

        return JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["smartLoot"] = false,
            ["squadFrameMix"] = mix,
            ["thetaActor"] = request.ThetaActor,
            ["volumeScaleMilli"] = volumeScaleMilli,
            ["catalogRevision"] = request.CatalogRevision,
            ["dropTableRevision"] = request.DropTableRevision,
        });
    }
}
