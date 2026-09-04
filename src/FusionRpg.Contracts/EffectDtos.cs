using System.Text.Json.Serialization;

namespace FusionRpg.Contracts;

/// <summary>Bump when EffectEvent / IntentPlan / Grant DTO shapes break. v2 adds FA10 ApplyResourceDelta.</summary>
public static class FoundationContractVersion
{
    public const int Current = 2;
}

public static class EffectTriggers
{
    public const string OnSpawn = "OnSpawn";
    public const string OnDamageDealt = "OnDamageDealt";
    public const string OnDamageTaken = "OnDamageTaken";
    public const string OnDeath = "OnDeath";
    public const string OnGranted = "OnGranted";
    public const string OnRemoved = "OnRemoved";
    public const string OnTimer = "OnTimer";

    /// <summary>E33 (spec-activation-edge.md §2.1): an actor's own decision to act. Mirrors
    /// AtomTriggers.OnActivate (A18b) — the two constants must be byte-identical, because EffectBag
    /// matches by string.</summary>
    public const string OnActivate = "OnActivate";

    // E34 (spec-trigger-vocabulary.md §2.1): five match/board-economy triggers — input only, no kind
    // or executor of their own (that is E35/E36's job). Mirrors AtomTriggers.OnWave etc. exactly, for
    // the same byte-identical reason as OnActivate above.
    public const string OnWave = "OnWave";
    public const string OnMatchStart = "OnMatchStart";
    public const string OnMatchEnd = "OnMatchEnd";
    public const string OnSunCollect = "OnSunCollect";
    public const string OnGridPlace = "OnGridPlace";
}

public static class EffectActions
{
    public const string ModifyStat = "ModifyStat";
    public const string ApplyStatus = "ApplyStatus";
    public const string ClearStatus = "ClearStatus";
    public const string SpawnEntity = "SpawnEntity";
    public const string BoardAction = "BoardAction";
    public const string SpawnGridItem = "SpawnGridItem";
    public const string ClearGridItem = "ClearGridItem";
    public const string SetBoxType = "SetBoxType";
    public const string Economy = "Economy";
    public const string ApplyResourceDelta = "ApplyResourceDelta";
    public const string GrantShield = "GrantShield";

    /// <summary>
    /// aura-skill-todo.md Phase 5 / TC2 — the derived-channel write. Unlike every opcode above it,
    /// this one is DECLARATIVE: nothing executes it. A `stat.derived` atom is a permanent modifier
    /// that declares no trigger (AtomKindRegistry: "Permanent modifier: declares no trigger"), so the
    /// bag never fires it; the grant's mere presence is the effect, folded at resolve time by
    /// `GrantedDerivedAtomReader` -> `AtomDerivedSubsystem` -> `ActorHub`. It therefore needs no sink
    /// executor, which is why adding it does not touch `InjectorEffectActionSink` or `BattleEffectSink`.
    /// </summary>
    public const string ModifyDerivedStat = "ModifyDerivedStat";

    /// <summary>
    /// E35 (spec-match-modify.md §2.5): match.modify's opcode — sets one `Board.config` field for the
    /// match through `CheatState` + `CheatActions.ApplyBoardConfig`. `/effects/contract`'s `actions`
    /// array already reflects every public const on this class (`DebugEndpoints.cs`,
    /// `PublicConstStrings(typeof(EffectActions))`), so this constant publishing itself is the whole
    /// of this module's own "grow the published list" obligation — no endpoint edit needed.
    /// </summary>
    public const string ModifyMatch = "ModifyMatch";

    /// <summary>
    /// E36 (spec-wave-control.md §2.1): wave.control's opcode — summon|huge|setTimer|hold, all four
    /// against existing `CheatActions`/`DebugActions` entry points, no new host write. Refused at
    /// `EffectEventDto.ChainDepth > 0` (§2.3) — `summon`/`huge` cause spawns, which re-emit the events
    /// that could re-trigger this same atom, and that loop cannot be diagnosed after the fact. Same
    /// reflection-published obligation as `ModifyMatch` above — declaring this constant is the whole
    /// of the "grow the published list" requirement.
    /// </summary>
    public const string WaveControl = "WaveControl";

    /// <summary>
    /// E37 (spec-projectile-control.md §2b): <c>bullet.modify</c>'s opcode — changes the damage/type/
    /// moveWay of a bullet the GAME fires (not one <c>spawn.entity{kind:bullet}</c> creates). Like
    /// <see cref="ModifyDerivedStat"/> this is DECLARATIVE: a permanent modifier with no trigger, read
    /// as a resolved grant inside <c>Bullet.InitData</c>'s existing postfix
    /// (<c>CheatPrefixes.BulletInitCheat</c>) rather than executed by either sink. Same reflection-
    /// published obligation as <see cref="ModifyMatch"/>/<see cref="WaveControl"/> — declaring this
    /// constant is the whole of the "grow the published /effects/contract list" requirement.
    /// </summary>
    public const string BulletModify = "BulletModify";
}

public static class EffectTypes
{
    public const string Passive = "Passive";
    public const string Triggered = "Triggered";
}

/// <summary>
/// Owner key grammar: <c>match</c>, <c>plant:{typeId}</c>, <c>zombie:{typeId}</c>, <c>entity:{ptr}</c>, <c>player:{id}</c>.
/// </summary>
public static class EffectOwnerKeys
{
    public const string Match = "match";

    public static string PlantType(int typeId) => "plant:" + typeId;
    public static string ZombieType(int typeId) => "zombie:" + typeId;
    public static string Entity(string ptr) => "entity:" + ptr;
    public static string Player(long id) => "player:" + id;
}

public sealed class EffectEventDto
{
    [JsonPropertyName("trigger")] public string Trigger { get; set; } = "";
    [JsonPropertyName("matchKey")] public string? MatchKey { get; set; }
    [JsonPropertyName("side")] public string? Side { get; set; }
    [JsonPropertyName("actorPtr")] public string? ActorPtr { get; set; }
    [JsonPropertyName("targetPtr")] public string? TargetPtr { get; set; }
    [JsonPropertyName("typeId")] public int? TypeId { get; set; }
    [JsonPropertyName("targetTypeId")] public int? TargetTypeId { get; set; }
    [JsonPropertyName("damage")] public long? Damage { get; set; }
    [JsonPropertyName("killerPtr")] public string? KillerPtr { get; set; }
    [JsonPropertyName("tick")] public long Tick { get; set; }
    [JsonPropertyName("scenarioId")] public string? ScenarioId { get; set; }
    [JsonPropertyName("chainDepth")] public int ChainDepth { get; set; }
    [JsonPropertyName("sourceGrantId")] public string? SourceGrantId { get; set; }
    /// <summary>Merged physical hits this event represents (v2 coalescing); 1 = a single hit.</summary>
    [JsonPropertyName("hitCount")] public int HitCount { get; set; } = 1;
    /// <summary>E34 (spec-trigger-vocabulary.md §2.2): the wave number for OnWave. Additive nullable —
    /// breaks no existing shape, so FoundationContractVersion.Current stays at its current value.</summary>
    [JsonPropertyName("wave")] public int? Wave { get; set; }
}

public sealed class EffectGrantDto
{
    [JsonPropertyName("grantId")] public string GrantId { get; set; } = "";
    [JsonPropertyName("effectId")] public string EffectId { get; set; } = "";
    [JsonPropertyName("ownerKind")] public string OwnerKind { get; set; } = "match";
    [JsonPropertyName("ownerKey")] public string OwnerKey { get; set; } = EffectOwnerKeys.Match;
    [JsonPropertyName("pluginId")] public string PluginId { get; set; } = "debug";
    [JsonPropertyName("priority")] public int Priority { get; set; }
    [JsonPropertyName("overlay")] public Dictionary<string, object?>? Overlay { get; set; }
}

public sealed class EffectActionPlanItem
{
    [JsonPropertyName("seq")] public int Seq { get; set; }
    [JsonPropertyName("action")] public string Action { get; set; } = "";
    [JsonPropertyName("effectId")] public string EffectId { get; set; } = "";
    [JsonPropertyName("grantId")] public string GrantId { get; set; } = "";
    [JsonPropertyName("sourceTag")] public string SourceTag { get; set; } = "";
    [JsonPropertyName("params")] public Dictionary<string, object?> Params { get; set; } = new();
    [JsonPropertyName("tags")] public Dictionary<string, string> Tags { get; set; } = new();
}

public sealed class IntentPlanDto
{
    [JsonPropertyName("contractVersion")] public int ContractVersion { get; set; } = FoundationContractVersion.Current;
    [JsonPropertyName("trigger")] public string Trigger { get; set; } = "";
    [JsonPropertyName("actions")] public List<EffectActionPlanItem> Actions { get; set; } = new();
    [JsonPropertyName("skipped")] public List<string> Skipped { get; set; } = new();
}

public sealed class EffectFiredDto
{
    [JsonPropertyName("grantId")] public string GrantId { get; set; } = "";
    [JsonPropertyName("effectId")] public string EffectId { get; set; } = "";
    [JsonPropertyName("trigger")] public string Trigger { get; set; } = "";
    [JsonPropertyName("action")] public string Action { get; set; } = "";
    [JsonPropertyName("ok")] public bool Ok { get; set; } = true;
    [JsonPropertyName("error")] public string? Error { get; set; }
}

public sealed class EffectCatalogSnapshotDto
{
    [JsonPropertyName("contractVersion")] public int ContractVersion { get; set; } = FoundationContractVersion.Current;
    [JsonPropertyName("defs")] public List<EffectDefDto> Defs { get; set; } = new();
    [JsonPropertyName("grants")] public List<EffectGrantDto> Grants { get; set; } = new();
    [JsonPropertyName("revision")] public int Revision { get; set; }
}

public sealed class EffectDefDto
{
    [JsonPropertyName("effectId")] public string EffectId { get; set; } = "";
    [JsonPropertyName("effectType")] public string EffectType { get; set; } = EffectTypes.Triggered;
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("sourceTag")] public string SourceTag { get; set; } = "";
    [JsonPropertyName("triggers")] public List<string> Triggers { get; set; } = new();
    [JsonPropertyName("actions")] public List<EffectDefActionDto> Actions { get; set; } = new();
}

public sealed class EffectDefActionDto
{
    [JsonPropertyName("seq")] public int Seq { get; set; }
    [JsonPropertyName("action")] public string Action { get; set; } = "";
    [JsonPropertyName("params")] public Dictionary<string, object?> Params { get; set; } = new();
}
