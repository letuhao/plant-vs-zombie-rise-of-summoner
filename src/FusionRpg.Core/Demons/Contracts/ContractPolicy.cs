using FusionRpg.Core.Battle;

namespace FusionRpg.Core.Demons.Contracts;

/// <summary>Where a demon's loyalty sits. Derived from the number, never stored — a policy change
/// must never leave a stale rank in the database.</summary>
public enum LoyaltyRank
{
    /// <summary>Under the deploy floor: owned, contracted, and refusing to be fielded.</summary>
    Insubordinate,
    Bound,
    Sworn,
    Trusted,
    Devoted
}

/// <summary>Rolled per specimen; scales how fast loyalty is earned, lost, and what tribute it demands.</summary>
public enum DemonPersonality
{
    Loyal,
    Stoic,
    Proud,
    Calculating,
    Feral
}

/// <summary>Integer percentages, applied as <c>x * pct / 100</c>.</summary>
public sealed record PersonalityRates(int GainPct, int DecayPct, int UpkeepPct);

public static class DemonPersonalityIds
{
    public static string ToId(this DemonPersonality personality) => personality switch
    {
        DemonPersonality.Loyal => "loyal",
        DemonPersonality.Stoic => "stoic",
        DemonPersonality.Proud => "proud",
        DemonPersonality.Calculating => "calculating",
        DemonPersonality.Feral => "feral",
        _ => throw new ArgumentOutOfRangeException(nameof(personality), personality, null)
    };

    public static bool TryParse(string? value, out DemonPersonality personality)
    {
        switch (value)
        {
            case "loyal": personality = DemonPersonality.Loyal; return true;
            case "stoic": personality = DemonPersonality.Stoic; return true;
            case "proud": personality = DemonPersonality.Proud; return true;
            case "calculating": personality = DemonPersonality.Calculating; return true;
            case "feral": personality = DemonPersonality.Feral; return true;
            default: personality = DemonPersonality.Loyal; return false;
        }
    }
}

/// <summary>
/// Contract rules (spec-demon-contracts.md, owner locks 2026-08-21). Pure integers, no clock of its
/// own — the store passes the time in. Numbers are spec-locked; tuning is ask-first.
///
/// The one property worth stating out loud: a fresh contract lands in the <see cref="LoyaltyRank.Bound"/>
/// band, which pays +0‰. Adopting contracts therefore cannot move a single battle or expedition
/// golden — loyalty only starts paying once a demon has earned Sworn.
/// </summary>
public static class ContractPolicy
{
    public const int PolicyVersion = 1;

    public const int LoyaltyMax = 1000;
    /// <summary>Below this a demon cannot be fielded. Decay stops here; only defeats cross it.</summary>
    public const int DeployFloor = 200;
    public const int BindLoyalty = 300;

    public const int WinGain = 15;
    public const int LossPenalty = 10;
    public const int DailyGainCap = 60;
    public const int DecayPerDay = 25;
    public const int RitualGain = 100;

    public const int BaseSlots = 12;
    public const int MaxSlots = 48;
    public const int SlotPriceStep = 300;

    /// <summary>A six-month absence settles thirty days: bounded work, bounded bill.</summary>
    public const int MaxSettleDays = 30;

    public static LoyaltyRank RankFor(int loyalty) => loyalty switch
    {
        < DeployFloor => LoyaltyRank.Insubordinate,
        < 400 => LoyaltyRank.Bound,
        < 600 => LoyaltyRank.Sworn,
        < 800 => LoyaltyRank.Trusted,
        _ => LoyaltyRank.Devoted
    };

    /// <summary>Per-mille bonus on the demon's OWN combat channels, applied at squad build.</summary>
    public static int RankBonusMilli(LoyaltyRank rank) => rank switch
    {
        LoyaltyRank.Sworn => 15,
        LoyaltyRank.Trusted => 35,
        LoyaltyRank.Devoted => 60,
        _ => 0
    };

    public static bool IsDeployable(int loyalty) => loyalty >= DeployFloor;

    public static PersonalityRates Rates(DemonPersonality personality) => personality switch
    {
        DemonPersonality.Loyal => new PersonalityRates(120, 80, 100),
        DemonPersonality.Stoic => new PersonalityRates(90, 60, 100),
        DemonPersonality.Proud => new PersonalityRates(100, 100, 130),
        DemonPersonality.Calculating => new PersonalityRates(100, 90, 110),
        DemonPersonality.Feral => new PersonalityRates(80, 150, 70),
        _ => throw new ArgumentOutOfRangeException(nameof(personality), personality, null)
    };

    public static int BaseUpkeepPerDay(DemonRarity rarity) => rarity switch
    {
        DemonRarity.Common => 2,
        DemonRarity.Rare => 5,
        DemonRarity.Epic => 12,
        DemonRarity.Legendary => 25,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null)
    };

    /// <summary>Daily tribute for one bound demon. Truncation favours the player, but never to zero —
    /// a free demon would make the whole capacity economy optional.</summary>
    public static int UpkeepPerDay(DemonRarity rarity, DemonPersonality personality) =>
        Math.Max(1, BaseUpkeepPerDay(rarity) * Rates(personality).UpkeepPct / 100);

    public static int DecayPerDayFor(DemonPersonality personality) =>
        DecayPerDay * Rates(personality).DecayPct / 100;

    /// <summary>One unpaid day. Never drops a demon below the floor, and never pushes a demon that
    /// is already under it (defeats put it there) any lower.</summary>
    public static int ApplyDecay(int loyalty, DemonPersonality personality) =>
        loyalty <= DeployFloor ? loyalty : Math.Max(DeployFloor, loyalty - DecayPerDayFor(personality));

    /// <summary>Credits a win against the demon's rolling daily window.</summary>
    public static (int Loyalty, int GainToday) ApplyGain(
        int loyalty, int gainToday, int baseGain, DemonPersonality personality)
    {
        var scaled = baseGain * Rates(personality).GainPct / 100;
        var credited = Math.Max(0, Math.Min(scaled, DailyGainCap - gainToday));
        return (Math.Min(LoyaltyMax, loyalty + credited), gainToday + credited);
    }

    /// <summary>A defeat. Uncapped by the daily window, and the only thing that can cross the floor.</summary>
    public static int ApplyLoss(int loyalty) => Math.Max(0, loyalty - LossPenalty);

    public static int RitualGainFor(DemonPersonality personality) =>
        RitualGain * Rates(personality).GainPct / 100;

    public static long RitualPrice(DemonRarity rarity) => rarity switch
    {
        DemonRarity.Common => 50,
        DemonRarity.Rare => 100,
        DemonRarity.Epic => 200,
        DemonRarity.Legendary => 400,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null)
    };

    public static int Capacity(int purchasedSlots) =>
        Math.Min(MaxSlots, BaseSlots + Math.Max(0, purchasedSlots));

    public static long NextSlotPrice(int purchasedSlots) =>
        (long)SlotPriceStep * (Math.Max(0, purchasedSlots) + 1);

    public static bool CanBuySlot(int purchasedSlots) => Capacity(purchasedSlots) < MaxSlots;

    /// <summary>Whole UTC days between two stamps, clamped to <see cref="MaxSettleDays"/>. Day-quantised,
    /// not 24-hour windows: a minute past midnight UTC is a new tribute day. A stamp in the future
    /// (the SIM clock hook travels forward) bills nothing.</summary>
    public static int ElapsedDays(DateTimeOffset lastSettled, DateTimeOffset now)
    {
        var days = (now.UtcDateTime.Date - lastSettled.UtcDateTime.Date).Days;
        return Math.Clamp(days, 0, MaxSettleDays);
    }

    /// <summary>Personality is a property of the specimen, derived from its id through the owned PRNG —
    /// so demons minted before this module existed have one too, and it never drifts.</summary>
    public static DemonPersonality PersonalityFor(string instanceId) =>
        (DemonPersonality)(int)(SeededRng.DeriveStream(0UL, instanceId ?? "").NextULong() % 5UL);
}
