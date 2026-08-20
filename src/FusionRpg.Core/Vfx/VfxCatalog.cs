namespace FusionRpg.Core.Vfx;

/// <summary>Cue id → recipe SSOT. Replace-whole-entry semantics only (vfx-ssot.md §6.2).</summary>
public sealed class VfxCatalog
{
    readonly Dictionary<string, VfxRecipe> _byId = new(StringComparer.OrdinalIgnoreCase);

    public void ReplaceAll(IEnumerable<VfxRecipe> recipes)
    {
        if (recipes == null) throw new ArgumentNullException(nameof(recipes));
        var next = new Dictionary<string, VfxRecipe>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in recipes)
        {
            Validate(r);
            next[r.CueId] = r;
        }

        _byId.Clear();
        foreach (var kv in next) _byId[kv.Key] = kv.Value;
    }

    public bool TryGet(string? cueId, out VfxRecipe recipe)
    {
        if (!string.IsNullOrWhiteSpace(cueId) && _byId.TryGetValue(cueId, out var r))
        {
            recipe = r;
            return true;
        }

        recipe = null!;
        return false;
    }

    public IReadOnlyList<string> Ids => _byId.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

    public static void Validate(VfxRecipe recipe)
    {
        if (recipe == null) throw new ArgumentNullException(nameof(recipe));
        if (string.IsNullOrWhiteSpace(recipe.CueId))
            throw new ArgumentException("recipe.CueId is required");
        if (recipe.Primitives == null || recipe.Primitives.Count == 0)
            throw new ArgumentException(recipe.CueId + ": recipe needs at least one primitive");
        foreach (var p in recipe.Primitives)
        {
            if (p == null) throw new ArgumentException(recipe.CueId + ": null primitive spec");
            if (p.LifeSeconds <= 0f)
                throw new ArgumentException(recipe.CueId + ": primitive LifeSeconds must be > 0");
            if (p.Count < 1 || p.Count > 64)
                throw new ArgumentException(recipe.CueId + ": primitive Count must be 1..64");
            if (p.DelaySeconds < 0f)
                throw new ArgumentException(recipe.CueId + ": primitive DelaySeconds must be >= 0");
        }
    }
}

/// <summary>Cue id vocabulary — vfx-ssot.md §4.</summary>
public static class VfxCueIds
{
    public const string CombatHit = "combat.hit";
    public const string CombatHeal = "combat.heal";
    public const string DebugProbe = "debug.probe";
}

/// <summary>C#-seeded catalog, mirroring EffectSeedCatalog (vfx-ssot.md §6.2).</summary>
public static class VfxSeedCatalog
{
    /// <summary>Legacy world-flash orange — the pre-VFX-SSOT burst color, kept for continuity.</summary>
    public static readonly (byte R, byte G, byte B) ProbeOrange = (255, 128, 20);

    public static List<VfxRecipe> CreateAll() => new()
    {
        new VfxRecipe
        {
            CueId = VfxCueIds.CombatHit,
            Primitives = new[]
            {
                new VfxPrimitiveSpec
                {
                    Kind = VfxPrimitiveKind.Floater,
                    Label = VfxLabelSourceKind.TagAmount,
                    LifeSeconds = VfxRules.FloaterLifeSeconds
                },
                new VfxPrimitiveSpec
                {
                    Kind = VfxPrimitiveKind.Burst,
                    LifeSeconds = VfxRules.BurstLifeSeconds,
                    Count = 28
                }
            }
        },
        new VfxRecipe
        {
            CueId = VfxCueIds.CombatHeal,
            Primitives = new[]
            {
                new VfxPrimitiveSpec
                {
                    Kind = VfxPrimitiveKind.Floater,
                    Label = VfxLabelSourceKind.TagAmount,
                    LifeSeconds = VfxRules.FloaterLifeSeconds
                }
            }
        },
        new VfxRecipe
        {
            CueId = VfxCueIds.DebugProbe,
            Primitives = new[]
            {
                new VfxPrimitiveSpec
                {
                    Kind = VfxPrimitiveKind.Burst,
                    Color = VfxColorSourceKind.Fixed,
                    FixedRgb = ProbeOrange,
                    LifeSeconds = VfxRules.BurstLifeSeconds,
                    Count = 28
                }
            }
        }
    };
}
