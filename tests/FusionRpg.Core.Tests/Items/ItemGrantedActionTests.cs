using System.Text.Json;
using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Grants;
using FusionRpg.Core.Actions.Loadout;
using FusionRpg.Core.Actions.Rungs;
using FusionRpg.Core.Actions.Unlock;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Grants;
using FusionRpg.Core.Items.Power;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>
/// `granted-actions` (item module 19) — the item side of the grant seam, against the SHIPPED action
/// runtime (`ActionSetAssembler`, `CapPolicy`, `CooldownLedger`, `TurnState`, `InterruptCause`) and
/// the SHIPPED base-type corpus (`data/seed/items/base-types/**`, 740 rows).
///
/// <para>Nothing here rebuilds the assembler, the cap or the freeze. Every merge assertion runs
/// through <see cref="ActionSetAssembler"/> itself, which is what "the item layer must not implement
/// this" means as a test rather than as a sentence.</para>
/// </summary>
public class ItemGrantedActionTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }

    static ItemPowerTuning LoadPowerTuning() =>
        ItemPowerTuningLoader.Parse(File.ReadAllText(
            Path.Combine(RepoRoot(), "data", "tuning", "item-power.v1.json")));

    static IReadOnlyDictionary<string, ItemRarityRungTuning> LoadRarity() =>
        ItemRarityTuning.Parse(File.ReadAllText(
            Path.Combine(RepoRoot(), "data", "tuning", "item-rarity.v1.json")));

    static RungTable LoadRungs() =>
        RungTableLoader.Parse(File.ReadAllText(
            Path.Combine(RepoRoot(), "data", "tuning", "action-rungs.v2.json")));

    static ActionRow Skill(string id, bool grantable = true, bool defaultAttackEligible = false,
        bool enabled = true, int rung = 3) => new()
    {
        ActionId = id,
        Name = id,
        Kind = ActionKind.Skill,
        ContainerId = "skill.test",
        Grantable = grantable,
        DefaultAttackEligible = defaultAttackEligible,
        Enabled = enabled,
        Rung = rung,
    };

    static ItemGrantBaseTypeFacts Primary =>
        new(ContainerKind.Item, ItemGrantLimits.DefaultAttackRoleId);

    static ItemGrantedActionRow Row(string actionId, ItemGrantRole role = ItemGrantRole.Granted, int seq = 0,
        string containerId = "item.brass-nozzle") => new(containerId, seq, actionId, role);

    static string Rules(IEnumerable<AtomRejection> fails) => string.Join(" | ", fails.Select(f => f.Detail));

    /// <summary>
    /// A file's code with comment lines removed. Every "this name appears nowhere" assertion below
    /// runs over this rather than the raw text: the whole point of these rules is that they are
    /// DISCUSSED by name in the doc comments — a grep that could not tell a paragraph from a
    /// declaration would forbid explaining the rule it enforces.
    /// </summary>
    static string CodeOnly(string path) =>
        string.Join("\n", File.ReadAllLines(path).Where(l =>
        {
            var t = l.TrimStart();
            return !t.StartsWith("//", StringComparison.Ordinal)
                && !t.StartsWith("///", StringComparison.Ordinal)
                && !t.StartsWith("--", StringComparison.Ordinal)
                && !t.StartsWith("*", StringComparison.Ordinal);
        }));

    /// <summary>Code with comment lines AND string-literal contents removed — so a refusal MESSAGE
    /// that names a symbol is not mistaken for a use of it. Only safe on files with no raw
    /// (<c>"""</c>) string, which is every file under <c>Items/Grants</c>.</summary>
    static string CodeNoText(string path) =>
        System.Text.RegularExpressions.Regex.Replace(CodeOnly(path), @"""(?:[^""\\]|\\.)*""", "\"\"");

    static bool Raised(IEnumerable<AtomRejection> fails, string ruleId) =>
        fails.Any(f => f.Reason == AtomRejectionReason.ContentRuleViolated
                    && f.Detail.StartsWith(ruleId + ":", StringComparison.Ordinal));

    // ---- the six columns and §5.3's Never list -------------------------------------------------

    [Fact]
    public void The_row_carries_exactly_six_properties_and_none_of_them_is_on_the_never_list()
    {
        var props = typeof(ItemGrantedActionRow)
            .GetProperties()
            .Select(p => p.Name)
            .Where(n => n != "RoleWire" && n != "EqualityContract")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "ActionId", "ContainerId", "Enabled", "Revision", "Role", "Seq" }, props);
    }

    /// <summary>
    /// §5.3 as a source test, not a review note: every column someone will propose, checked against
    /// the whole module's source. The list is the lane's own, verbatim.
    /// </summary>
    [Fact]
    public void No_source_file_in_the_module_declares_a_forbidden_column()
    {
        string[] forbidden =
        {
            "cooldown_ticks", "cooldown_class", "cooldown_key", "starts_at", "interrupt_cooldown_milli",
            "time_cost_ticks", "windup_ticks", "recovery_ticks", "resolve_offsets_json", "speed_channel",
            "priority_band", "slot_consuming", "commitment", "interruptible", "interrupt_refund_milli",
            "resource_id", "target_spec_json", "min_range", "max_range", "range_channel",
            "anchor_source", "requires_line_of_sight", "conditions_json", "overrides_json",
            "charges", "uses_per_battle", "uses_per_rest",
        };

        var files = Directory.EnumerateFiles(
            Path.Combine(RepoRoot(), "src", "FusionRpg.Core", "Items", "Grants"), "*.cs")
            .Append(Path.Combine(RepoRoot(), "src", "FusionRpg.Data", "Sqlite", "RpgStore.ItemGrants.cs"));

        foreach (var file in files)
        {
            var code = CodeOnly(file);
            foreach (var name in forbidden)
                Assert.False(code.Contains(name, StringComparison.OrdinalIgnoreCase),
                    $"{Path.GetFileName(file)} declares '{name}', which §5.3 puts on the item side's Never list");
        }
    }

    // ---- import validation ---------------------------------------------------------------------

    [Fact]
    public void A_grant_naming_a_non_grantable_action_is_refused_at_import()
    {
        var fails = ItemGrantValidator.ValidateRow(
            Row("skill.pass"), Primary, Skill("skill.pass", grantable: false), default);

        Assert.True(Raised(fails, ItemGrantRules.NotGrantable), Rules(fails));
    }

    [Fact]
    public void A_grant_naming_an_unknown_or_disabled_action_is_refused_under_one_rule()
    {
        var unknown = ItemGrantValidator.ValidateRow(Row("skill.ghost"), Primary, action: null, default);
        Assert.True(Raised(unknown, ItemGrantRules.UnknownAction), Rules(unknown));

        var disabled = ItemGrantValidator.ValidateRow(
            Row("skill.retired"), Primary, Skill("skill.retired", enabled: false), default);
        Assert.True(Raised(disabled, ItemGrantRules.UnknownAction), Rules(disabled));
    }

    [Fact]
    public void A_grant_naming_a_basic_is_refused_because_every_actor_already_has_it()
    {
        var basic = Skill("act.attack") with { Kind = ActionKind.Basic };
        var fails = ItemGrantValidator.ValidateRow(Row("act.attack"), Primary, basic, default);
        Assert.True(Raised(fails, ItemGrantRules.BasicCollision), Rules(fails));
    }

    [Fact]
    public void A_default_attack_grant_on_an_ineligible_action_is_refused()
    {
        var fails = ItemGrantValidator.ValidateRow(
            Row("skill.cleave", ItemGrantRole.DefaultAttack), Primary,
            Skill("skill.cleave", defaultAttackEligible: false), default);

        Assert.True(Raised(fails, ItemGrantRules.DefaultAttackNotAllowed), Rules(fails));
    }

    [Theory]
    [InlineData("armament-secondary")]
    [InlineData("jewel-major")]
    [InlineData("girdle")]
    public void Default_attack_is_refused_on_any_role_but_armament_primary(string roleId)
    {
        var facts = new ItemGrantBaseTypeFacts(ContainerKind.Item, roleId);
        var fails = ItemGrantValidator.ValidateRow(
            Row("skill.cleave", ItemGrantRole.DefaultAttack), facts,
            Skill("skill.cleave", defaultAttackEligible: true), default);

        Assert.True(Raised(fails, ItemGrantRules.DefaultAttackNotAllowed), Rules(fails));

        // ...and the same item may still grant an EXTRA action from that role.
        var granted = ItemGrantValidator.ValidateRow(
            Row("skill.cleave"), facts, Skill("skill.cleave", defaultAttackEligible: true), default);
        Assert.Empty(granted);
    }

    [Fact]
    public void A_grant_on_a_non_item_container_is_refused()
    {
        var fails = ItemGrantValidator.ValidateRow(
            Row("skill.cleave"), new ItemGrantBaseTypeFacts(ContainerKind.Skill, "armament-primary"),
            Skill("skill.cleave"), default);

        Assert.True(Raised(fails, ItemGrantRules.UnknownContainer), Rules(fails));
    }

    [Fact]
    public void A_malformed_container_id_or_negative_seq_is_refused()
    {
        var badId = ItemGrantValidator.ValidateShape(
            new ItemGrantedActionRow("skill.not-an-item", 0, "skill.cleave", ItemGrantRole.Granted), Primary);
        Assert.True(Raised(badId, ItemGrantRules.UnknownContainer), Rules(badId));

        var badSeq = ItemGrantValidator.ValidateShape(Row("skill.cleave", seq: -1), Primary);
        Assert.True(Raised(badSeq, ItemGrantRules.BadValue), Rules(badSeq));
    }

    [Fact]
    public void A_clean_row_raises_nothing()
    {
        var fails = ItemGrantValidator.ValidateRow(
            Row("skill.cleave", ItemGrantRole.DefaultAttack), Primary,
            Skill("skill.cleave", defaultAttackEligible: true), default);

        Assert.Empty(fails);
    }

    // ---- cross-row, per container --------------------------------------------------------------

    [Fact]
    public void At_most_one_default_attack_per_container()
    {
        var rows = new[]
        {
            Row("skill.a", ItemGrantRole.DefaultAttack, seq: 0),
            Row("skill.b", ItemGrantRole.DefaultAttack, seq: 1),
        };

        var fails = ItemGrantValidator.ValidateContainer("item.brass-nozzle", rows);
        Assert.True(Raised(fails, ItemGrantRules.DefaultAttackNotAllowed), Rules(fails));

        // One is fine, and a second GRANTED row alongside it is the unique's own shape (§7.2).
        Assert.Empty(ItemGrantValidator.ValidateContainer("item.thornbind-lash", new[]
        {
            Row("skill.lash-strike", ItemGrantRole.DefaultAttack, seq: 0),
            Row("skill.thornbind", ItemGrantRole.Granted, seq: 1),
        }));
    }

    [Fact]
    public void Duplicate_seq_and_duplicate_action_on_one_container_are_each_refused()
    {
        var dupSeq = ItemGrantValidator.ValidateContainer("item.x", new[]
        {
            Row("skill.a", seq: 0), Row("skill.b", seq: 0),
        });
        Assert.True(Raised(dupSeq, ItemGrantRules.DuplicateSeq), Rules(dupSeq));

        var dupAction = ItemGrantValidator.ValidateContainer("item.x", new[]
        {
            Row("skill.a", seq: 0), Row("skill.a", seq: 1),
        });
        Assert.True(Raised(dupAction, ItemGrantRules.DuplicateAction), Rules(dupAction));
    }

    [Fact]
    public void Display_order_is_role_ordinal_then_seq_then_action_id()
    {
        var rows = new[]
        {
            new ItemGrantedActionRow("item.ring", 0, "skill.zeta", ItemGrantRole.Granted),
            new ItemGrantedActionRow("item.nozzle", 1, "skill.alpha", ItemGrantRole.Granted),
            new ItemGrantedActionRow("item.nozzle", 0, "skill.omega", ItemGrantRole.Granted),
        };

        int Ordinal(string containerId) => containerId == "item.nozzle" ? 0 : 5;

        var ordered = ItemGrantValidator.InDisplayOrder(rows, Ordinal);
        Assert.Equal(new[] { "skill.omega", "skill.alpha", "skill.zeta" },
            ordered.Select(r => r.ActionId).ToArray());
    }

    // ---- ⭐ R2, enforced rather than reported ---------------------------------------------------

    [Fact]
    public void An_action_with_no_resolvable_rung_is_refused_as_unpriced_never_zero()
    {
        var tuning = LoadPowerTuning();
        var fails = ItemGrantValidator.ValidateBudget(
            Row("skill.cleave"), new ItemGrantPriceInputs(QPowerMilli: null, RarityCeilingMilli: 1000), tuning);

        Assert.True(Raised(fails, ItemGrantRules.Unpriced), Rules(fails));
        Assert.Contains("never read as 0", Rules(fails), StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠ The other unpriced arm is a CALLER gap, not a content defect, and is deliberately NOT
    /// refused: `chaff`'s shipped `powerCeilingShareMilli` is 0, so a real rung in the real ladder
    /// reaches this branch. Refusing an authored row for the harness's missing ceiling would blame
    /// the content.
    /// </summary>
    [Fact]
    public void No_seeded_ceiling_is_reported_not_refused_and_chaff_is_the_real_case()
    {
        Assert.Equal(0, LoadRarity()["chaff"].PowerCeilingShareMilli);

        var fails = ItemGrantValidator.ValidateBudget(
            Row("skill.cleave"), new ItemGrantPriceInputs(QPowerMilli: 1000, RarityCeilingMilli: 0),
            LoadPowerTuning());

        Assert.Empty(fails);
    }

    [Fact]
    public void A_granted_action_over_its_rarity_ceiling_is_refused_at_import()
    {
        // A rung-10 action priced against `sprout`'s ceiling: the largest thing an item can do,
        // offered on nearly the cheapest item there is.
        var rungs = LoadRungs();
        Assert.True(rungs.TryResolve(10, out var top));
        var sprout = LoadRarity()["sprout"].PowerCeilingShareMilli;

        var fails = ItemGrantValidator.ValidateBudget(
            Row("skill.apocalypse"),
            new ItemGrantPriceInputs(top.QPowerMilli, sprout),
            LoadPowerTuning());

        Assert.True(Raised(fails, ItemGrantRules.OverBudget), Rules(fails));
    }

    [Fact]
    public void The_effective_cap_is_module_9s_tunable_when_set_and_the_whole_ceiling_otherwise()
    {
        var shipped = LoadPowerTuning();
        Assert.Null(shipped.GrantedActionShareCapMilli); // no number is invented by this module
        Assert.Equal(ItemGrantLimits.WholeCeilingShareMilli, ItemGrantValidator.EffectiveCapMilli(shipped));

        var tightened = shipped with { GrantedActionShareCapMilli = 300 };
        Assert.Equal(300, ItemGrantValidator.EffectiveCapMilli(tightened));

        // ...and tightening it is a file save, not a code change: the same price now refuses.
        var price = new ItemGrantPriceInputs(QPowerMilli: 500, RarityCeilingMilli: 1000);
        Assert.Empty(ItemGrantValidator.ValidateBudget(Row("skill.small"), price, shipped));
        Assert.True(Raised(ItemGrantValidator.ValidateBudget(Row("skill.small"), price, tightened),
            ItemGrantRules.OverBudget));
    }

    [Fact]
    public void R2_is_reportable_without_tuning_and_gating_with_it()
    {
        var price = new ItemGrantPriceInputs(QPowerMilli: 9_000_000, RarityCeilingMilli: 1);

        var reportable = ItemPowerReads.GrantedActionPrice(price.QPowerMilli, price.RarityCeilingMilli);
        Assert.False(reportable.Over);           // every pre-module-19 caller's behaviour, unchanged
        Assert.NotNull(reportable.ShareMilli);

        var gating = ItemPowerReads.GrantedActionPrice(
            price.QPowerMilli, price.RarityCeilingMilli, LoadPowerTuning());
        Assert.True(gating.Over);
        Assert.True(gating.CoefficientSensitive); // cross-shape: report with a band, never a threshold
    }

    /// <summary>The share is a `long` and widens before multiplying — a rung price that would overflow
    /// `int` at the intermediate step still resolves exactly.</summary>
    [Fact]
    public void The_r2_share_is_a_long_and_does_not_overflow_at_the_intermediate()
    {
        var read = ItemPowerReads.GrantedActionPrice(int.MaxValue, rarityCeiling: 1);
        Assert.NotNull(read.ShareMilli);
        Assert.True(read.ShareMilli > int.MaxValue,
            "a per-mille share of a max-int price must exceed int range, which is why it is a long");
    }

    // ---- the assembler is the SHIPPED one, never re-implemented --------------------------------

    static SpeciesBasicsRow Basics =>
        new("species.pea", "act.attack", "act.guard", "act.move", InnateActionId: null);

    [Fact]
    public void Two_items_granting_one_action_produce_one_set_entry()
    {
        var grants = new[]
        {
            new ActionGrantRow(OwnerKind.Entity, "spec-1", "skill.emberburst", "item.ring-a"),
            new ActionGrantRow(OwnerKind.Entity, "spec-1", "skill.emberburst", "item.ring-b"),
        };

        var set = ActionSetAssembler.Assemble(Basics, grants, _ => true);
        var entry = Assert.Single(set.Actions.Where(a => a.ActionId == "skill.emberburst"));
        Assert.Equal(2, entry.Sources.Count);
    }

    [Fact]
    public void Removing_one_of_two_sources_leaves_the_action()
    {
        var remaining = new[]
        {
            new ActionGrantRow(OwnerKind.Entity, "spec-1", "skill.emberburst", "item.ring-b"),
        };

        var set = ActionSetAssembler.Assemble(Basics, remaining, _ => true);
        var entry = Assert.Single(set.Actions.Where(a => a.ActionId == "skill.emberburst"));
        Assert.Equal(new[] { "item.ring-b" }, entry.Sources.ToArray());
    }

    [Fact]
    public void An_item_granting_an_action_the_species_already_has_is_reported_not_swallowed()
    {
        var grants = new[]
        {
            new ActionGrantRow(OwnerKind.Entity, "spec-1", "act.guard", "item.shield"),
        };

        var set = ActionSetAssembler.Assemble(Basics, grants, _ => true);
        Assert.Single(set.Actions.Where(a => a.ActionId == "act.guard"));
        var report = Assert.Single(set.RedundantGrants);
        Assert.Equal("act.guard", report.ActionId);
        Assert.Equal("item.shield", report.Source);
    }

    [Fact]
    public void Default_attack_replaces_the_species_intrinsic()
    {
        Assert.Equal("act.attack", ActionSetAssembler.Assemble(Basics, Array.Empty<ActionGrantRow>(), _ => true)
            .DefaultAttackActionId);

        var grants = new[]
        {
            new ActionGrantRow(OwnerKind.Entity, "spec-1", "skill.spray-cone", "item.brass-nozzle",
                ActionGrantRoles.DefaultAttack),
        };
        Assert.Equal("skill.spray-cone",
            ActionSetAssembler.Assemble(Basics, grants, _ => true).DefaultAttackActionId);
    }

    /// <summary>The item side's wire spelling IS the assembler's constant — one string, one concept.</summary>
    [Fact]
    public void The_default_attack_wire_value_is_the_shipped_assembler_constant()
    {
        Assert.Equal(ActionGrantRoles.DefaultAttack, ItemGrantRoles.Wire(ItemGrantRole.DefaultAttack));
        Assert.True(ItemGrantRoles.TryParse(ActionGrantRoles.DefaultAttack, out var parsed));
        Assert.Equal(ItemGrantRole.DefaultAttack, parsed);
        Assert.False(ItemGrantRoles.TryParse("on-use", out _)); // G2's proposed third role is not ours
    }

    [Fact]
    public void This_module_implements_no_action_set_merge_of_its_own()
    {
        var dir = Path.Combine(RepoRoot(), "src", "FusionRpg.Core", "Items", "Grants");
        foreach (var file in Directory.EnumerateFiles(dir, "*.cs"))
        {
            var code = CodeOnly(file);
            Assert.DoesNotContain("class ActionSetAssembler", code, StringComparison.Ordinal);
            Assert.DoesNotContain("DefaultAttackActionId =", code, StringComparison.Ordinal);
        }
    }

    // ---- the projection (wiring gap b), pure half ----------------------------------------------

    static EquipAssignment Assignment(string specimenId, ItemRole role, string refId) =>
        new(specimenId, role, "stock", refId, "2026-09-05T00:00:00.0000000Z");

    /// <summary>A real specimen id: <c>RpgStore.CreateUniqueActor</c> mints
    /// <c>Guid.NewGuid().ToString("N")</c>, 32 lowercase hex characters.</summary>
    const string RealSpecimenId = "0a1b2c3d4e5f60718293a4b5c6d7e8f9";

    [Fact]
    public void The_grant_scope_matches_what_WebMatchService_reads()
    {
        var row = Row("skill.cleave");
        var grant = EquippedGrantProjection.GrantFor(
            Assignment(RealSpecimenId, ItemRole.ArmamentPrimary, "item.brass-nozzle"), "item.brass-nozzle", row);

        // WebMatchService.EquippedActionIdsFor builds exactly this scope before calling ListGrants.
        Assert.Equal(OwnerKind.Entity, grant.OwnerKind);
        Assert.Equal(RealSpecimenId, grant.OwnerKey);
        Assert.Equal("item.brass-nozzle", grant.Source);
        Assert.Equal("skill.cleave", grant.ActionId);

        // ...and the key is LEGAL at that kind, which is not free: OwnerScope.Validate requires
        // lowercase hex for `entity`, and a kebab placeholder is BadOwnerKey before a row is written.
        Assert.True(OwnerScope.Validate(OwnerKind.Entity, RealSpecimenId, out _).IsOk);
        Assert.False(OwnerScope.Validate(OwnerKind.Entity, "spec-1", out _).IsOk);
    }

    /// <summary>
    /// ⛔ <b>Found, not fixed — a durability mismatch on the scope this seam is required to use.</b>
    /// <c>OwnerScope.IsSessionScoped</c> is true for exactly <see cref="OwnerKind.Entity"/>, and its
    /// own doc says why: "`entity:` bindings are session-scoped and never durable — the pointer is
    /// reused." <c>rpg_action_grant</c> is a DURABLE table, and the owner approved
    /// <see cref="OwnerKind.UniqueActor"/> on 2026-09-02 for durable per-specimen state
    /// "specifically because <c>OwnerKind.Entity</c> is session-scoped and would silently drop
    /// equipped-item bonuses on the next session boundary"
    /// (<c>RpgStore.UniqueActors.cs</c>, <c>ReconcileUniqueEquipmentAtomBindings</c>).
    ///
    /// <para>It works today only because a specimen id is a <c>Guid("N")</c>, which is coincidentally
    /// valid hex — so the grammar passes and the mismatch is invisible. This module writes where the
    /// shipped reader reads (spec's Boundaries, and writing elsewhere would produce rows nothing
    /// sees); the scope's OWNER is the server/loadout lane, and the same question applies to
    /// <c>rpg_actor_loadout</c>, which is read at the identical scope. Pinned here so the conflict has
    /// a test rather than a memory.</para>
    /// </summary>
    [Fact]
    public void The_grant_scope_is_the_session_scoped_one_and_that_conflicts_with_a_durable_table()
    {
        Assert.True(new OwnerScope(OwnerKind.Entity, RealSpecimenId).IsSessionScoped);
        Assert.False(new OwnerScope(OwnerKind.UniqueActor, RealSpecimenId).IsSessionScoped);

        // Both grammars accept a real specimen id, which is exactly why the mismatch went unnoticed.
        Assert.True(OwnerScope.Validate(OwnerKind.UniqueActor, RealSpecimenId, out _).IsOk);
    }

    [Fact]
    public void A_disabled_grant_row_produces_no_grant_and_is_not_deleted()
    {
        var rows = new[]
        {
            Row("skill.live", seq: 0),
            Row("skill.retired", seq: 1) with { Enabled = false },
        };

        var grants = EquippedGrantProjection.GrantsFor(
            Assignment("spec-1", ItemRole.ArmamentPrimary, "item.brass-nozzle"),
            _ => "item.brass-nozzle", _ => rows);

        Assert.Equal(new[] { "skill.live" }, grants.Select(g => g.ActionId).ToArray());
    }

    [Fact]
    public void An_assignment_that_resolves_to_no_base_type_yields_no_grants_rather_than_a_guess()
    {
        var grants = EquippedGrantProjection.GrantsFor(
            Assignment("spec-1", ItemRole.ArmamentPrimary, "instance-42"),
            _ => null, _ => new[] { Row("skill.cleave") });

        Assert.Empty(grants);
    }

    [Fact]
    public void The_grant_id_is_derived_and_stable_so_a_rebuild_upserts_rather_than_duplicates()
    {
        var a = EquippedGrantProjection.GrantIdFor("spec-1", "item.nozzle", "skill.cleave");
        var b = EquippedGrantProjection.GrantIdFor("spec-1", "item.nozzle", "skill.cleave");
        Assert.Equal(a, b);
        Assert.NotEqual(a, EquippedGrantProjection.GrantIdFor("spec-2", "item.nozzle", "skill.cleave"));

        var dir = Path.Combine(RepoRoot(), "src", "FusionRpg.Core", "Items", "Grants");
        foreach (var file in Directory.EnumerateFiles(dir, "*.cs"))
            Assert.DoesNotContain("Guid.NewGuid", CodeOnly(file), StringComparison.Ordinal);
    }

    [Fact]
    public void A_specimens_projection_is_ordinal_and_lists_every_source_to_withdraw()
    {
        var assignments = new[]
        {
            Assignment("spec-1", ItemRole.ArmamentPrimary, "item.nozzle"),
            Assignment("spec-1", ItemRole.JewelMajor, "item.ring"),
            Assignment("spec-2", ItemRole.ArmamentPrimary, "item.other"), // another specimen — ignored
        };

        IReadOnlyList<ItemGrantedActionRow> GrantsOf(string containerId) => containerId switch
        {
            "item.nozzle" => new[] { Row("skill.spray", containerId: containerId) },
            "item.ring" => new[] { Row("skill.ember", containerId: containerId) },
            _ => Array.Empty<ItemGrantedActionRow>(),
        };

        var (grants, sources) = EquippedGrantProjection.ForSpecimen(
            "spec-1", assignments, a => a.RefId, GrantsOf);

        Assert.Equal(new[] { "item.nozzle", "item.ring" }, sources.ToArray());
        Assert.Equal(new[] { "skill.spray", "skill.ember" }, grants.Select(g => g.ActionId).ToArray());
        Assert.All(grants, g => Assert.Equal("spec-1", g.OwnerKey));
    }

    // ---- ⭐ handshake item 7: the removal table, skipped against the flag -----------------------

    [Fact]
    public void Mid_run_equip_is_unlanded_and_the_removal_table_is_written_anyway()
    {
        Assert.False(ItemGrantLandedFlags.MidRunEquipLanded);
        // Written now because it is free now and expensive after someone builds mid-match equip:
        // every state has a rule, so the table cannot be partially applicable.
        foreach (TurnState state in Enum.GetValues<TurnState>())
            _ = GrantRemovalPolicy.EffectIn(state);
    }

    [Fact]
    public void Removal_in_charging_or_ready_drops_the_action_from_the_selectable_set()
    {
        if (ItemGrantLandedFlags.MidRunEquipLanded) return; // the real behaviour test lands with the feature
        Assert.True(GrantRemovalPolicy.AppliesImmediately(TurnState.Charging));
        Assert.True(GrantRemovalPolicy.AppliesImmediately(TurnState.Ready));
    }

    [Fact]
    public void Removal_in_committed_or_resolving_lets_the_action_complete()
    {
        if (ItemGrantLandedFlags.MidRunEquipLanded) return;
        Assert.Equal(GrantRemovalEffect.LetTheRunComplete, GrantRemovalPolicy.EffectIn(TurnState.Committed));
        Assert.Equal(GrantRemovalEffect.LetTheRunComplete, GrantRemovalPolicy.EffectIn(TurnState.Resolving));
        Assert.False(GrantRemovalPolicy.CancelsACommittedAction(TurnState.Committed));
    }

    [Fact]
    public void Removal_in_recovering_applies_at_the_transition_to_charging()
    {
        if (ItemGrantLandedFlags.MidRunEquipLanded) return;
        Assert.Equal(GrantRemovalEffect.AtNextCharging, GrantRemovalPolicy.EffectIn(TurnState.Recovering));
        // Asserted against the shipped table, not against the doc: Recovering has exactly one edge out.
        Assert.True(TurnTransitions.IsLegal(TurnState.Recovering, TurnState.Charging));
    }

    [Fact]
    public void Removal_while_downed_is_recorded_and_survives_a_revive()
    {
        if (ItemGrantLandedFlags.MidRunEquipLanded) return;
        Assert.Equal(GrantRemovalEffect.RecordedAndSurvivesRevive, GrantRemovalPolicy.EffectIn(TurnState.Downed));
        // Downed → Charging is legal, which is exactly why "recorded" is not the same as "forgotten".
        Assert.True(TurnTransitions.IsLegal(TurnState.Downed, TurnState.Charging));
    }

    /// <summary>
    /// ⛔ Invariant 3, as a guard on the kernel's own enum. ⚠ The lane says the enum is
    /// "`CrowdControl` and `Damage`"; it has THREE members today (`ResourceExhausted` landed with the
    /// per-tick cost model). So the assertion is the INVARIANT — no inventory-shaped cause — and never
    /// the count, which would go red for a reason that is not this rule.
    /// </summary>
    [Fact]
    public void An_inventory_event_never_becomes_an_InterruptCause()
    {
        var names = Enum.GetNames<InterruptCause>();
        string[] inventoryWords = { "item", "equip", "unequip", "grant", "inventory", "gear" };

        foreach (var name in names)
            foreach (var word in inventoryWords)
                Assert.False(name.Contains(word, StringComparison.OrdinalIgnoreCase),
                    $"InterruptCause.{name} names an inventory concept; §3.5 refuses a third cause for " +
                    "'the item left' — it would put an item concern inside the kernel's slot accounting");

        Assert.False(GrantRemovalPolicy.InventoryEventMayBeAnInterruptCause);
    }

    /// <summary>The swap exploit is closed for free by a key shape that shipped for another reason.</summary>
    [Fact]
    public void Cooldown_survives_unequip_and_re_equip()
    {
        var slot = new CooldownSlot("actor-1", "cd.emberburst");
        Assert.Equal(slot, new CooldownSlot("actor-1", "cd.emberburst"));

        var fields = typeof(CooldownSlot).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(new[] { "ActorKey", "Slot" }, fields);
        Assert.False(GrantRemovalPolicy.CooldownIsKeyedOnTheItem);
    }

    [Fact]
    public void A_granted_action_creates_no_binding_so_nothing_needs_reverting()
    {
        var dir = Path.Combine(RepoRoot(), "src", "FusionRpg.Core", "Items", "Grants");
        foreach (var file in Directory.EnumerateFiles(dir, "*.cs"))
        {
            var code = CodeOnly(file);
            Assert.DoesNotContain("effect_binding", code, StringComparison.Ordinal);
            Assert.DoesNotContain("BindingRow", code, StringComparison.Ordinal);
        }
    }

    // ---- ⭐ handshake item 8: CLOSED, and the answer is "uncapped by design" --------------------

    /// <summary>
    /// ⭐ The spec's own handshake table marks item 8 <b>open</b> ("nothing enforces one"). It is
    /// closed: the action program's <see cref="CapPolicy"/> (T24) answers it by NAMING which existing
    /// cap governs. Granted-by-paid-sources is deliberately uncapped, so §3.7(d)'s proposed 8 and its
    /// `TooManyGrantedActions` code have no raiser on either side of the seam.
    /// </summary>
    [Fact]
    public void The_granted_count_cap_is_answered_as_uncapped_and_this_module_mints_no_code_for_it()
    {
        Assert.False(ItemGrantLimits.GrantedCountCapExists);

        // The answer is structural, not a number: CapPolicy has no "grantedCap" member at all.
        var members = typeof(CapPolicy).GetMembers().Select(m => m.Name).ToArray();
        Assert.DoesNotContain(members, m => m.Contains("GrantedCap", StringComparison.OrdinalIgnoreCase));

        // The two caps it DOES name are real and already built.
        Assert.Equal(LoadoutSet.MaxSize, CapPolicy.EquippedSkillCap);
        Assert.True(CapPolicy.EquippedSkillCap > 0);
    }

    [Fact]
    public void The_reject_never_truncate_requirement_is_carried_by_the_shipped_loadout_refusal()
    {
        if (ItemGrantLimits.GrantedCountCapExists) return; // there is no granted cap to exceed

        // §3.7(d)'s rule is "reject at bind, never truncate", and the shipped cap that DOES exist
        // behaves that way: LoadoutSet.Validate refuses rather than dropping the overflow.
        var tooMany = Enumerable.Range(0, LoadoutSet.MaxSize + 1).Select(i => $"skill.{i}").ToList();
        var result = LoadoutSet.Validate(tooMany, _ => true, _ => ActionKind.Skill, () => false);
        Assert.False(result.Ok);
        Assert.Equal(LoadoutRejectionReason.LoadoutFull, result.Reason);
    }

    // ---- ⛔ X3, recorded as an ordinary external dependency ------------------------------------

    [Fact]
    public void X3_is_unresolved_and_the_flag_says_so()
    {
        Assert.False(ItemGrantLandedFlags.ActionCorpusProducerLanded);

        // Verified, not assumed: no production path turns an action seed into an rpg_action row.
        var srcDirs = new[]
        {
            Path.Combine(RepoRoot(), "src"),
            Path.Combine(RepoRoot(), "tools"),
        };

        foreach (var root in srcDirs)
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                Assert.DoesNotContain("ActionSeeder.Generate(", CodeOnly(file), StringComparison.Ordinal);
    }

    /// <summary>D36: we consume the producer when it lands. We do not build one, and we file nothing
    /// against `action-corpus`.</summary>
    [Fact]
    public void This_module_builds_no_action_producer_of_its_own()
    {
        var dir = Path.Combine(RepoRoot(), "src", "FusionRpg.Core", "Items", "Grants");
        foreach (var file in Directory.EnumerateFiles(dir, "*.cs"))
        {
            var code = CodeNoText(file);
            Assert.DoesNotContain("ActionSeeder", code, StringComparison.Ordinal);
            Assert.DoesNotContain("new ActionRow", code, StringComparison.Ordinal);
        }
    }

    // ---- the shipped base-type corpus ----------------------------------------------------------

    sealed record BaseTypeEntry(string Id, string Role, string Frame, string Class);

    static List<BaseTypeEntry> LoadBaseTypes()
    {
        var dir = Path.Combine(RepoRoot(), "data", "seed", "items", "base-types");
        var result = new List<BaseTypeEntry>();
        foreach (var path in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("entries", out var entries)) continue;
            foreach (var e in entries.EnumerateArray())
                result.Add(new BaseTypeEntry(
                    e.GetProperty("id").GetString()!,
                    e.GetProperty("role").GetString()!,
                    e.GetProperty("frame").GetString()!,
                    e.GetProperty("class").GetString()!));
        }
        return result;
    }

    /// <summary>GA2 ships with ZERO content rows, and that is asserted against the real corpus rather
    /// than promised: no shipped base type authors a grant.</summary>
    [Fact]
    public void No_shipped_base_type_authors_a_granted_action()
    {
        var dir = Path.Combine(RepoRoot(), "data", "seed", "items", "base-types");
        foreach (var path in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("grantsAction", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("grants_action", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("grantedAction", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// ⭐ §8.6's content rule, MEASURED against the shipped corpus for the first time. `default-attack`
    /// is authored per weapon class per frame — and the real corpus has exactly 3 classes × 2 frames on
    /// `armament-primary`, so the lane's "roughly 3 × 2 = 6" is right to the number.
    ///
    /// <para>⚠ Its companion figure is NOT: the lane says per-base-type authoring would be "344
    /// hand-authored actions". `default-attack` is legal only on `armament-primary` (§4.3 option C), of
    /// which the shipped corpus has 48 — so the per-base-type cost is 48, not 344. The mitigation still
    /// pays (48 → 6, an 8× saving); the number quoted for it is stale.</para>
    /// </summary>
    [Fact]
    public void The_per_class_authoring_rule_costs_six_actions_against_the_real_corpus()
    {
        var corpus = LoadBaseTypes();
        Assert.Equal(740, corpus.Count);

        var primaries = corpus.Where(e => e.Role == ItemGrantLimits.DefaultAttackRoleId).ToList();
        Assert.Equal(48, primaries.Count);

        var perClassPerFrame = primaries.Select(e => (e.Frame, e.Class)).Distinct().ToList();
        Assert.Equal(6, perClassPerFrame.Count);
        Assert.Equal(2, perClassPerFrame.Select(p => p.Frame).Distinct().Count());
    }

    /// <summary>The one role §4.3 names is a real role in the shipped registry — a rule naming a role
    /// that does not exist would refuse nothing while reading as protection (module 17's own device).</summary>
    [Fact]
    public void The_default_attack_role_is_a_real_role_in_the_shipped_slate()
    {
        Assert.True(BaseTypeSlate.TryLadderOf(ItemGrantLimits.DefaultAttackRoleId, out var ladder));
        Assert.Equal(ClassLadder.Weapon, ladder);
    }

    // ---- the closed code list is not grown -----------------------------------------------------

    [Fact]
    public void This_module_mints_no_new_rejection_code_and_registers_its_namespace()
    {
        ItemGrantRules.EnsureRegistered();
        Assert.Equal(35, Enum.GetNames<AtomRejectionReason>().Length);
        Assert.Contains(ItemGrantRules.Namespace, ContentRuleNamespaces.All);

        // Every rule id this module declares is under the registered namespace, by reflection — so a
        // rule added later without registration is a red test rather than a runtime throw in the field.
        var ruleIds = typeof(ItemGrantRules).GetFields()
            .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name != nameof(ItemGrantRules.Namespace))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(ruleIds);
        foreach (var id in ruleIds)
        {
            Assert.StartsWith(ItemGrantRules.Namespace + ".", id, StringComparison.Ordinal);
            Assert.True(ContentRuleNamespaces.IsRegistered(id), id);
        }
    }

    /// <summary>Two rule ids exist with no raiser on purpose, so each gap has a name a report can
    /// carry. Asserted so neither is deleted as dead code nor quietly wired to a refusal.</summary>
    [Fact]
    public void The_two_recorded_rules_have_no_raiser_anywhere_in_the_module()
    {
        var dir = Path.Combine(RepoRoot(), "src", "FusionRpg.Core", "Items", "Grants");
        var validator = CodeOnly(Path.Combine(dir, "ItemGrantValidator.cs"));

        Assert.DoesNotContain(nameof(ItemGrantRules.ActionCorpusAbsent), validator, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ItemGrantRules.TooManyGranted), validator, StringComparison.Ordinal);
    }
}
