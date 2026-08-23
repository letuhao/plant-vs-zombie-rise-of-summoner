using FusionRpg.Core.Battle;

namespace FusionRpg.Core.World.Turn;

/// <summary>
/// What a calendar boundary produced. Wave 1 records the rolls; the economic effects land with
/// sector-development, which is the module that owns growth.
/// </summary>
public readonly record struct CalendarRoll(
    bool WeekBoundary, bool MonthBoundary, bool SpecialWeek, bool SpecialMonth, bool Plague);

/// <summary>
/// A turn is a day, seven days a week, four weeks a month (spec-turn-engine.md §Calendar). The
/// boundaries are where the world gets its heartbeat: recruits arrive in pulses, and a rare plague
/// month is what tells a player whether their defence depended on fresh bodies.
///
/// Pure in (turn, seed): the same day of the same world always rolls the same way, so a replay
/// cannot drift and a client can honestly show next week before it arrives.
/// </summary>
public static class TurnCalendar
{
    public static int DaysPerWeek => World.WorldTuningHub.Tuning.Calendar.DaysPerWeek;
    public static int WeeksPerMonth => World.WorldTuningHub.Tuning.Calendar.WeeksPerMonth;
    public static int DaysPerMonth => DaysPerWeek * WeeksPerMonth;

    // Genre-proven rates: a quarter of weeks are special, plague is rare enough to be a story.
    public static int SpecialWeekChanceMilli => World.WorldTuningHub.Tuning.Calendar.SpecialWeekChanceMilli;
    public static int SpecialMonthChanceMilli => World.WorldTuningHub.Tuning.Calendar.SpecialMonthChanceMilli;
    public static int PlagueChanceMilli => World.WorldTuningHub.Tuning.Calendar.PlagueChanceMilli;

    public static CalendarRoll Roll(int turn, ulong seed)
    {
        if (turn <= 0) return default;

        var weekBoundary = turn % DaysPerWeek == 0;
        var monthBoundary = turn % DaysPerMonth == 0;
        if (!weekBoundary) return default;

        // One stream per boundary kind, derived from the turn: an extra roll in one never shifts
        // the other, which is the same discipline the battle engine uses.
        var weekRng = SeededRng.DeriveStream(seed, "calendar:week:" + turn);
        var specialWeek = weekRng.NextInt(1000) < SpecialWeekChanceMilli;

        var specialMonth = false;
        var plague = false;
        if (monthBoundary)
        {
            var monthRng = SeededRng.DeriveStream(seed, "calendar:month:" + turn);
            specialMonth = monthRng.NextInt(1000) < SpecialMonthChanceMilli;
            plague = monthRng.NextInt(1000) < PlagueChanceMilli;

            // A month cannot both double growth and halt it; the plague wins, because the month
            // people remember is the bad one.
            if (plague) specialMonth = false;
        }

        return new CalendarRoll(weekBoundary, monthBoundary, specialWeek, specialMonth, plague);
    }
}
