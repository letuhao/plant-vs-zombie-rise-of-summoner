using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Sockets;
using FusionRpg.Core.Items.Surfaces;
using FusionRpg.Data;

namespace FusionRpg.Server;

/// <summary>
/// item module 20 (`item-surfaces`) — the <b>read-only</b> server half of the six player surfaces.
///
/// <para>⛔ <b>Every route here reads. None writes, and none reaches generation.</b> D26 puts drop
/// volume and pacing out of the item program's scope, and the loot filter is the interface answer to
/// review pressure, never the metering one — so the armoury route hands the client rows and counts
/// and lets <see cref="LootFilterView"/> decide what is drawn. There is no <c>MapPost</c> in this
/// file, deliberately: equipping, socketing and salvaging already have owners (modules 4, 16, 14) and
/// a second write path through the presentation layer is exactly the "second surface" this module
/// exists to prevent.</para>
///
/// <para>⚠ <b>Honestly scoped, and the gap is named rather than filled with a guess.</b> An armoury
/// row's <c>role</c> and <c>frame</c> come from the item's BASE TYPE, and module 6 shipped the 740-row
/// corpus and the Core readers but <b>not a table</b> — the same wiring gap modules 17 and 19 already
/// recorded for <c>item_unique.derived_from</c> and <c>item_granted_action.container_id</c>. So those
/// two fields come back empty here and role/frame filtering is not offered, rather than being
/// answered from the container's <c>slot</c>, which is a different axis and would be a plausible
/// wrong answer. <b>Owner: module 6</b>, and the field is ready the day the table exists.</para>
/// </summary>
public static class ItemSurfaceEndpoints
{
    public sealed record SurfaceStatusDto(string Surface, string State, string UnlockKey);

    public sealed record ArmouryRowDto(
        string InstanceId, string ContainerId, string Rarity, int RarityOrdinal,
        bool Assigned, bool Locked, bool Unseen, bool Stale, string AcquiredUtc);

    public sealed record ArmouryPageDto(
        int Total, int Unseen, bool OverReviewPressure, string RenderStrategy, IReadOnlyList<ArmouryRowDto> Rows);

    public sealed record CombinationRowDto(
        string ComboId, string Shape, string State, int? Distance,
        IReadOnlyList<string> MissingFamilies, IReadOnlyList<string> MissingElements, int GrantedTier);

    public static void MapItemSurfaces(this WebApplication app, ItemSurfaceTuning surfaceTuning, SocketTuning socketTuning)
    {
        if (surfaceTuning is null) throw new ArgumentNullException(nameof(surfaceTuning));
        if (socketTuning is null) throw new ArgumentNullException(nameof(socketTuning));

        // GG-17 / GG-44 — which of the four designed states each of the six surfaces is in, and what
        // unlocks the locked ones. Derived from the player's own state, never from a constant list.
        app.MapGet("/api/items/surfaces/{playerId}", (string playerId, RpgStore store) =>
        {
            var owned = store.ListItemsByPlayer(playerId);
            var satisfied = new HashSet<string>(StringComparer.Ordinal);
            if (owned.Count > 0)
            {
                satisfied.Add("first-container-acquired");
                // ⚠ The honest condition is "a second candidate for a role you have filled", and the
                // role half needs module 6's base-type table. The key is the weaker, TRUE one.
                if (owned.Count > 1) satisfied.Add("second-container-owned");
                if (owned.Any(i => store.GetSockets(i.InstanceId).Count > 0))
                    satisfied.Add("first-socketed-item");
            }

            return Results.Ok(SurfaceCatalog.All
                .Select(s => SurfaceCatalog.Resolve(s, surfaceTuning, satisfied, loading: false, errored: false,
                    rowCount: s == ItemSurface.Compendium ? 1 : owned.Count))
                .Select(st => new SurfaceStatusDto(SurfaceCatalog.Id(st.Surface), st.State.ToString(), st.UnlockKey))
                .ToList());
        });

        // The armoury page. The inbox count is over the WHOLE armoury, never the page — an inbox you
        // can empty by paging past it is not an inbox.
        app.MapGet("/api/items/armoury/{playerId}", (string playerId, RpgStore store, int? limit, string? after) =>
        {
            var ordinals = store.ListRarities().ToDictionary(r => r.RarityId, r => r.Ordinal, StringComparer.Ordinal);

            var entries = store.ListItemsByPlayer(playerId).Select(item =>
            {
                var instance = store.GetInstance(item.InstanceId);
                var container = instance is null ? null : store.GetContainer(instance.ContainerId);
                var rarity = container?.Rarity ?? "";
                return (
                    Row: new ArmouryRowDto(
                        item.InstanceId, instance?.ContainerId ?? "", rarity,
                        rarity.Length > 0 && ordinals.TryGetValue(rarity, out var ord) ? ord : 0,
                        Assigned: false, item.Locked, Unseen: !item.Seen, item.Stale, item.AcquiredUtc),
                    Entry: new ArmouryEntry(
                        item.InstanceId, instance?.ContainerId ?? "", Role: "", Frame: "",
                        rarity.Length > 0 && ordinals.TryGetValue(rarity, out var o2) ? o2 : 0,
                        item.AcquiredUtc, Assigned: false, item.Locked, Unseen: !item.Seen, item.Stale,
                        RollQualityMilli: 0));
            }).ToList();

            var inbox = LootFilterView.Inbox(entries.Select(e => e.Entry).ToList(), surfaceTuning);
            var strategy = CollectionStrategy.For(entries.Count, surfaceTuning);

            var sorted = ArmouryQuery.ApplySort(entries.Select(e => e.Entry), ArmourySortKey.Acquired).ToList();
            var page = ArmouryQuery.ApplyPage(sorted, new ArmouryPageRequest(limit ?? 50, after));
            var byId = entries.ToDictionary(e => e.Entry.InstanceId, e => e.Row, StringComparer.Ordinal);

            return Results.Ok(new ArmouryPageDto(
                entries.Count, inbox.Unseen, inbox.OverReviewPressure, strategy.ToString(),
                page.Items.Select(i => byId[i.InstanceId]).ToList()));
        });

        // The socket bench's preview and the compendium's four states for one item — the ONE
        // combination read, so a preview and a result can never come from two functions.
        app.MapGet("/api/items/{instanceId}/combinations", (string instanceId, RpgStore store, string? playerId) =>
        {
            var instance = store.GetInstance(instanceId);
            if (instance is null) return Results.NotFound(new { error = "unknown instance", instanceId });

            var slots = store.GetSockets(instanceId);
            var catalog = store.GetComboRecipes();
            var host = new SocketHost(instance.ContainerId, ItemRole.ArmamentPrimary, "", slots.Count);

            // Only filled sockets reach the evaluator; an empty one is room, not an ingredient.
            var fill = slots
                .Where(s => !s.IsEmpty)
                .Select(s => new SocketFill(s.Index, s.Affinity,
                    new InsertDef(s.InsertContainerId ?? "", s.InsertContainerId ?? "", "", 1)))
                .ToList();

            var rows = CombinationDistance.Evaluate(host, fill, catalog, socketTuning, surfaceTuning, out _);

            // The held ledger is what the player has EVER held; stock is the honest approximation the
            // shipped schema supports today (there is no ever-held ledger table — named in module 20's
            // build log as owed to inventory, not invented here).
            var held = playerId is { Length: > 0 }
                ? HeldLedger.From(store.ListStock(playerId)
                    .Where(s => s.ContainerId.StartsWith("gem.", StringComparison.Ordinal))
                    .Select(s => new InsertDef(s.ContainerId, s.ContainerId, "", 1)))
                : HeldLedger.Empty;

            var rendered = CompendiumReveal.Render(rows, catalog, held, socketTuning, surfaceTuning);

            return Results.Ok(rendered.Select(r => new CombinationRowDto(
                r.ComboId, ComboShapes.Id(r.Shape), r.State.ToString(), r.Distance,
                r.Missing.Select(m => m.FamilyId).ToList(), r.MissingElements, r.GrantedTier)).ToList());
        });
    }
}
