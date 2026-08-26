using System.Globalization;
using System.Text;
using FusionRpg.Core.Combat;
using FusionRpg.Tools.CombatSim;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Usage();
    return 0;
}

try
{
    var command = args[0];
    var opts = ParseArgs(args.Skip(1).ToArray());
    return command switch
    {
        "run" => Run(opts),
        "sweep" => Sweep(opts),
        "compare" => Compare(opts),
        "fight" => Fight(opts),
        "matrix" => Matrix(opts),
        "ladder" => Ladder(opts),
        "search" => SearchCmd(opts),
        "explain" => Explain(opts),
        "predict" => Predict(opts),
        "status" => StatusSweep(opts),
        "marginal" => MarginalCmd(opts),
        "list" => List(),
        _ => Fail($"unknown command '{command}'")
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine("error: " + ex.Message);
    return 1;
}

int Run(Options o)
{
    TuningBootstrap.Load(o.Sets);
    var scenario = LoadScenario(o);
    if (o.Trials is { } t) scenario.Trials = t;
    if (o.Seed is { } s) scenario.Seed = s;

    Console.WriteLine(Header(scenario, o));
    var results = Simulator.Run(scenario, done => Console.Error.Write($"\r  {done:N0}/{scenario.Trials:N0}"));
    Console.Error.Write("\r                         \r");

    var summary = Summary.From(scenario.Name, results);
    Console.WriteLine(summary.ToConsole());

    if (o.Out is { } path)
    {
        WriteCsv(path, new[] { ("scenario", (object)scenario.Name) }, new[] { summary });
        Console.WriteLine($"  wrote {path}");
    }
    return 0;
}

int Sweep(Options o)
{
    TuningBootstrap.Load(o.Sets);
    var scenario = LoadScenario(o);
    if (o.Trials is { } t) scenario.Trials = t;
    if (o.Seed is { } s) scenario.Seed = s;

    var channel = o.Channel ?? throw new InvalidOperationException("sweep needs --channel <id>");
    var side = (o.Side ?? "defender").ToLowerInvariant();
    if (side is not ("attacker" or "defender"))
        throw new InvalidOperationException("--side must be attacker or defender");
    var from = o.From ?? 0;
    var to = o.To ?? throw new InvalidOperationException("sweep needs --to <value>");
    var steps = o.Steps ?? 11;
    if (steps < 2) throw new InvalidOperationException("--steps must be >= 2");

    Console.WriteLine(Header(scenario, o));
    Console.WriteLine($"  SWEEP  {side}.{channel}  {from:0.##} → {to:0.##}  in {steps} steps" +
                      (o.FightMode ? "   [fights to the death]" : ""));
    Console.WriteLine();

    if (o.FightMode)
    {
        Console.WriteLine($"  {"value",12} {"ATTACKER dies",14} {"defender dies",14} {"both die",9} {"stalemate",10} {"swings med",11}");
        Console.WriteLine("  " + new string('-', 76));
        for (var i = 0; i < steps; i++)
        {
            var value = from + (to - from) * i / (steps - 1);
            var step = scenario.Clone();
            (side == "attacker" ? step.Attacker : step.Defender)[channel] = StatRange.Fixed(value);
            step.Validate("--channel");
            if (o.Rounds is { } mr) step.MaxRounds = mr;
            var fr = FightSummary.From(value.ToString("0.##", CultureInfo.InvariantCulture),
                Simulator.RunFights(step), (step.AttackerHp.Min + step.AttackerHp.Max) / 2.0);
            Console.WriteLine($"  {fr.Label,12} {fr.DefenderWinRate,14:P1} {fr.AttackerWinRate,14:P1} " +
                              $"{fr.MutualKillRate,9:P1} {fr.StalemateRate,10:P1} {fr.Rounds.Median,11:N0}");
        }
        return 0;
    }

    var summaries = new List<Summary>();
    var values = new List<double>();
    for (var i = 0; i < steps; i++)
    {
        var value = from + (to - from) * i / (steps - 1);
        var step = scenario.Clone();
        var target = side == "attacker" ? step.Attacker : step.Defender;
        target[channel] = StatRange.Fixed(value);
        step.Validate("--channel");

        var results = Simulator.Run(step);
        summaries.Add(Summary.From(value.ToString("0.##", CultureInfo.InvariantCulture), results));
        values.Add(value);
        Console.Error.Write($"\r  step {i + 1}/{steps}");
    }
    Console.Error.Write("\r                         \r");

    Console.WriteLine($"  {"value",12} {"miss",8} {"parry",8} {"block",8} {"crit|cl",8} {"dmg mean",12} {"dmg/base",9} {"reflect",8} {"self%",8}");
    Console.WriteLine("  " + new string('-', 94));
    foreach (var row in summaries)
        Console.WriteLine(
            $"  {row.Label,12} {row.MissRate,8:P1} {row.ParryRate,8:P1} {row.BlockRate,8:P1} " +
            $"{row.CritRateOfCleanHits,8:P1} {row.DefenderDamage.Mean,12:N1} {row.MeanDamageRatio,9:F3} " +
            $"{row.ReflectRate,8:P1} {row.SelfDamageShareOfDealt,8:P1}");

    if (o.Out is { } path)
    {
        WriteCsv(path, values.Select(v => ("value", (object)v)).ToArray(), summaries);
        Console.WriteLine();
        Console.WriteLine($"  wrote {path}");
    }
    return 0;
}

int Explain(Options o)
{
    TuningBootstrap.Load(o.Sets);
    var ladder = new FusionRpg.Core.Power.PowerLadder(
        FusionRpg.Core.Power.PowerTuningLoader.Parse(
            File.ReadAllText(Path.Combine(TuningBootstrap.RepoRoot, "data", "tuning", "power-scale.v2.json"))));
    var model = AptitudeModel.Load(o.Models ?? "h-split");
    var names = (o.Archetypes ?? "finesse,bastion").Split(',', StringSplitOptions.TrimEntries);
    var theta = o.Thetas is { } th ? int.Parse(th.Split(',')[0]) : 100;
    var a = Build.Load(names[0]).At(theta, model, ladder);
    var b = Build.Load(names[1]).At(theta, model, ladder);

    foreach (var (atk, def) in new[] { (a, b), (b, a) })
    {
        var sc = new Scenario
        {
            Name = $"{atk.Name} attacking {def.Name}",
            Trials = o.Trials ?? 20000,
            Seed = o.Seed ?? 42,
            BaseDamage = atk.BaseDamage,
            Elements = ElementMode.Fixed,
            FixedElements = new List<string> { atk.Element ?? "fire" },
            DefenderElement = def.Element,
            Attacker = new Dictionary<string, StatRange>(atk.Stats, StringComparer.Ordinal),
            Defender = new Dictionary<string, StatRange>(def.Stats, StringComparer.Ordinal),
            ShieldHp = StatRange.Fixed(0),  // per-trial refresh would absorb every hit
            Reflection = true
        };
        Console.WriteLine();
        Console.WriteLine($"  ===== {sc.Name} =====  (defender hp {def.Hp.Min:N0})");
        Console.WriteLine(Summary.From(sc.Name, Simulator.Run(sc)).ToConsole());
    }
    return 0;
}

int SearchCmd(Options o)
{
    TuningBootstrap.Load(o.Sets);
    var ladder = new FusionRpg.Core.Power.PowerLadder(
        FusionRpg.Core.Power.PowerTuningLoader.Parse(
            File.ReadAllText(Path.Combine(TuningBootstrap.RepoRoot, "data", "tuning", "power-scale.v2.json"))));
    // A tuning config and a hypothesis model resolve the same way; the search does not care which.
    var modelName = o.Models ?? "h-split";
    var model = modelName.StartsWith("aptitudes", StringComparison.OrdinalIgnoreCase)
        ? AptitudeTuning.Load(modelName).ToModel(modelName)
        : AptitudeModel.Load(modelName);
    var names = (o.Archetypes ?? "force,finesse,bastion")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var theta = o.Thetas is { } th ? int.Parse(th.Split(',')[0]) : 100;
    // The search must see the same fight predict does, or it solves a model nobody plays.
    var searchActions = o.Actions is { } sa ? ActionSet.Load(sa) : null;
    Analytic.Status = o.Status ? StatusProfile.Default : null;
    var trials = o.Trials ?? 700;
    var iters = o.Steps ?? 40;
    var restarts = o.Restarts ?? 6;
    var rng = new Random(o.Seed ?? 42);

    Console.WriteLine();
    Console.WriteLine(o.Analytic
        ? $"  SEARCH  model {model.Name} · Θ {theta} · CLOSED FORM (no duels) · {restarts} restarts × {iters} iters"
        : $"  SEARCH  model {model.Name} · Θ {theta} · {trials:N0} duels/arrow · {restarts} restarts × {iters} iters");
    Console.WriteLine($"  target {Search.Target:P0} per arrow · cycle FORCE>BASTION>FINESSE>FORCE");
    Console.WriteLine();
    Console.WriteLine($"  {"restart",8} {"F>B",9} {"B>N",9} {"N>F",9} {"spread",9} {"score",9}");
    Console.WriteLine("  " + new string('-', 60));

    Search.Candidate? globalBest = null;
    for (var r = 1; r <= restarts; r++)
    {
        // Each restart begins from a fresh RANDOM allocation, not the seed file -- a hill-climb from
        // one start finds one local optimum, and the degenerate 100/0 corners are strong attractors.
        var cur = names.Select(Build.Load).ToList();
        Search.Perturb(cur, rng, r == 1 ? 0.0 : 1.4);
        var best = o.Analytic
            ? Search.EvaluateAnalytic(cur, model, ladder, theta, searchActions)
            : Search.Evaluate(cur, model, ladder, theta, trials, o.Seed ?? 42, o.Rounds ?? 3000);
        var temp = 0.6;
        for (var i = 1; i <= iters; i++)
        {
            var trial = names.Select(Build.Load).ToList();
            for (var k = 0; k < trial.Count; k++)
                trial[k].Points = new Dictionary<string, double>(best.Points[trial[k].Name], StringComparer.Ordinal);
            Search.Perturb(trial, rng, temp);
            var cand = o.Analytic
                ? Search.EvaluateAnalytic(trial, model, ladder, theta, searchActions)
                : Search.Evaluate(trial, model, ladder, theta, trials, o.Seed ?? 42, o.Rounds ?? 3000);
            if (cand.Score < best.Score) best = cand;
            temp = Math.Max(0.10, temp * 0.94);
        }
        var mark = globalBest is null || best.Score < globalBest.Score ? "  *" : "";
        Console.WriteLine($"  {r,8} {best.Arrows[0],9:P1} {best.Arrows[1],9:P1} {best.Arrows[2],9:P1} " +
                          $"{best.Spread,9:P1} {best.Score,9:F3}{mark}");
        if (globalBest is null || best.Score < globalBest.Score) globalBest = best;
    }

    var g = globalBest!;
    Console.WriteLine();
    Console.WriteLine($"  BEST  F>B {g.Arrows[0]:P1} · B>N {g.Arrows[1]:P1} · N>F {g.Arrows[2]:P1} · spread {g.Spread:P1} · score {g.Score:F3}");
    Console.WriteLine("  ALLOCATION (points per build, normalised to 100)");
    foreach (var (name, pts) in g.Points)
    {
        Console.WriteLine($"    {name}");
        foreach (var (k, v) in pts.OrderByDescending(x => x.Value))
            Console.WriteLine($"      {k,-14} {v,6:F1}");
    }
    if (o.Out is not null)
    {
        foreach (var (name, pts) in g.Points)
        {
            var b = Build.Load(name.ToLowerInvariant());
            b.Points = pts;
            File.WriteAllText(
                Path.Combine(TuningBootstrap.RepoRoot, "tools", "CombatSim", "builds", name.ToLowerInvariant() + ".json"),
                System.Text.Json.JsonSerializer.Serialize(b, new System.Text.Json.JsonSerializerOptions
                    { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
        }
        Console.WriteLine("  wrote best allocation back to builds/");
    }
    return 0;
}

// The deterministic core, and the residual it leaves. Computes every arrow of the posture matrix in
// closed form (no trials, no RNG), then — unless --no-verify — runs the simulator on the same builds
// and prints the gap. The gap is the point: it is what the math does not capture, measured rather
// than argued, and it is the quantity a statistical fit has to close.
int Predict(Options o)
{
    TuningBootstrap.Load(o.Sets);
    Analytic.AssertChannelsRegistered();

    var ladder = new FusionRpg.Core.Power.PowerLadder(
        FusionRpg.Core.Power.PowerTuningLoader.Parse(
            File.ReadAllText(Path.Combine(TuningBootstrap.RepoRoot, "data", "tuning", "power-scale.v2.json"))));

    var tuning = AptitudeTuning.Load(o.Models ?? "aptitudes.v1");
    var model = tuning.ToModel(o.Models ?? "aptitudes.v1");
    var builds = (o.Archetypes ?? "force,finesse,bastion")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(Build.Load).ToList();
    var thetas = (o.Thetas ?? "100")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(int.Parse).ToList();
    var verify = !o.NoVerify;
    var trials = o.Trials ?? 3000;
    var actions = o.Actions is { } an ? ActionSet.Load(an) : null;
    var status = o.Status ? StatusProfile.Default : null;
    Analytic.Status = status;

    Console.WriteLine();
    Console.WriteLine($"  CONFIG   {model.Name}   ({model.Description})");
    Console.WriteLine($"  SHAPE    defense={CombatPolicy.Default.DefenseShape} k={CombatPolicy.Default.DefenseDivisorK}  "
                      + $"amp={CombatPolicy.Default.AmpShape}  parryNeutral={CombatPolicy.Default.ParryNeutralShareKPm}permille");
    Console.WriteLine(verify
        ? $"  METHOD   closed form, then {trials:N0} simulated duels per arrow as a cross-check"
        : "  METHOD   closed form only");
    Console.WriteLine(status is null
        ? "  STATUS   none — the fourth axis is not in play at all"
        : $"  STATUS   {status.StatusId} (dot) — {status.MagnitudeShareOfBase:P0} of base per round for {status.BaseDurationRounds:0.#} rounds, before potency");
    Console.WriteLine(actions is null
        ? "  ACTIONS  none — every swing is free. Resource pools do not constrain anything."
        : $"  ACTIONS  {actions.Name} — {string.Join(", ", actions.Actions.Select(x => x.Cost is null ? x.Id + " (free)" : $"{x.Id} ({x.Cost.ResourceId})"))}");
    Console.WriteLine();

    var head = $"  {"Θ",-6}{"matchup",-22}{"predicted",12}{"rounds A",11}{"rounds B",11}";
    if (verify) head += $"{"simulated",12}{"residual",11}{"simRnds",9}";
    Console.WriteLine(head);
    Console.WriteLine("  " + new string('-', head.Length));

    var residuals = new List<double>();
    foreach (var theta in thetas)
    {
        for (var i = 0; i < builds.Count; i++)
        for (var j = 0; j < builds.Count; j++)
        {
            if (i == j) continue;
            var a = builds[i].At(theta, model, ladder);
            var b = builds[j].At(theta, model, ladder);
            var p = Analytic.Predict(a, b, actions);
            var row = $"  {theta,-6}{a.Name + " v " + b.Name,-22}{p.WinShareA,12:P1}"
                      + $"{p.RoundsA,11:0.0}{p.RoundsB,11:0.0}";
            if (verify)
            {
                var duel = Simulator.Duel(a, b, trials, o.Seed ?? 8888, o.Rounds ?? 3000, actions, status);
                var sim = duel.AWinShare;
                var residual = p.WinShareA - sim;
                residuals.Add(Math.Abs(residual));
                // Median simulated rounds against the predicted shorter kill-time: if these agree the
                // RATE is right and the gap is variance; if they disagree the rate itself is wrong.
                // Diagnostic first, guessing second - the lesson from class-rps-balance 5.2.
                row += $"{sim,12:P1}{residual,11:+0.0%;-0.0%;0.0%}{duel.MedianRounds,9:0.0}";
            }
            Console.WriteLine(row);
        }
    }

    if (verify && residuals.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"  RESIDUAL  mean {residuals.Average(),6:P1}   max {residuals.Max(),6:P1}   over {residuals.Count} arrows");
        Console.WriteLine(residuals.Max() <= 0.05
            ? "  → the closed form predicts the simulator. Balance can be stated by math, and the"
            : "  → the closed form and the simulator disagree by more than sampling noise. Everything");
        Console.WriteLine(residuals.Max() <= 0.05
            ? "    simulator's job becomes falsifying it, not producing the number."
            : "    the math omits — depleting pools, ordering, combination — lives in this gap.");
    }

    // One matchup in full, so the mixture is visible rather than asserted.
    if (builds.Count >= 2)
    {
        var a0 = builds[0].At(thetas[0], model, ladder);
        var b0 = builds[1].At(thetas[0], model, ladder);
        var d = Analytic.Predict(a0, b0, actions);
        Console.WriteLine();
        Console.WriteLine($"  PER-ROUND MIXTURE — {a0.Name} striking {b0.Name} at Θ={thetas[0]}");
        var s = d.StrikeA;
        Console.WriteLine($"    p(hit) {s.PHit,7:P1}   p(parry) {s.PParry,7:P1}   p(block) {s.PBlock,7:P1}   "
                          + $"p(clean) {s.PClean,7:P1}   p(crit|clean) {s.PCrit,7:P1}");
        Console.WriteLine($"    damage: clean {s.DClean,12:N0}   crit {s.DCrit,12:N0}   "
                          + $"parried {s.DParry,10:N0}   blocked {s.DBlock,10:N0}");
        Console.WriteLine($"    E[damage/swing] {s.Mean,12:N0}   SD {Math.Sqrt(s.Variance),12:N0}");
        Console.WriteLine($"    reflected back per round: to {a0.Name} {d.ReflectMeanToA,10:N0}   "
                          + $"to {b0.Name} {d.ReflectMeanToB,10:N0}");
        Console.WriteLine($"    HP depletion per round: {b0.Name} {d.RateAgainstB,12:N0}   {a0.Name} {d.RateAgainstA,12:N0}");
    }
    return 0;
}

// The free-build test: where does one more point pay, and does the answer depend on the opponent?
// An aptitude that is best against everyone is a tax; one that is best against nobody is dead. Both
// read off the same table, and both are invisible to a simulated run — the per-point effect is a
// fraction of a percent and sampling noise at 3,000 duels is ~0.9pp.
int MarginalCmd(Options o)
{
    TuningBootstrap.Load(o.Sets);
    Analytic.AssertChannelsRegistered();
    var ladder = new FusionRpg.Core.Power.PowerLadder(
        FusionRpg.Core.Power.PowerTuningLoader.Parse(
            File.ReadAllText(Path.Combine(TuningBootstrap.RepoRoot, "data", "tuning", "power-scale.v2.json"))));

    var modelName = o.Models ?? "aptitudes.v1";
    var model = modelName.StartsWith("aptitudes", StringComparison.OrdinalIgnoreCase)
        ? AptitudeTuning.Load(modelName).ToModel(modelName)
        : AptitudeModel.Load(modelName);
    var builds = (o.Archetypes ?? "force-ns,finesse-ns,bastion-ns")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(Build.Load).ToList();
    var theta = o.Thetas is { } th ? int.Parse(th.Split(',')[0]) : 100;

    Console.WriteLine();
    Console.WriteLine($"  MARGINAL VALUE OF ONE APTITUDE POINT  (+{Marginal.Delta:0.#} of 100, renormalised) · Θ {theta}");
    Console.WriteLine($"  {model.Name}");
    Console.WriteLine("  Change in win rate. Renormalisation means every other share falls, so this is");
    Console.WriteLine("  the point's value NET of what it costs elsewhere — the only question free build asks.");

    foreach (var subject in builds)
    {
        var opponents = builds.Where(b => b.Name != subject.Name).ToList();
        if (opponents.Count == 0) continue;
        var rows = Marginal.For(subject, opponents, model, ladder, theta, Marginal.Roster);

        Console.WriteLine();
        Console.WriteLine($"  ── as {subject.Name} ──");
        var hdr = $"  {"aptitude",-14}{"has",7}";
        foreach (var op in opponents) hdr += $"{"vs " + op.Name,18}";
        hdr += $"{"spread",10}";
        Console.WriteLine(hdr);
        Console.WriteLine("  " + new string('-', hdr.Length - 2));

        var best = rows.OrderByDescending(r => r.Best).First();
        foreach (var r in rows.OrderByDescending(r => r.Best))
        {
            var line = $"  {r.Aptitude,-14}{r.CurrentPoints,7:0.#}";
            foreach (var d in r.DeltaWinPerOpponent) line += $"{d,17:+0.000%;-0.000%;0.000%} ";
            line += $"{r.Spread,9:0.000%}";
            // Best point against EVERY opponent, or against NONE — the two failure modes.
            var mandatory = r.DeltaWinPerOpponent.Select((d, i) =>
                rows.All(x => x.DeltaWinPerOpponent[i] <= d)).All(x => x);
            var dead = r.Best <= 0;
            if (mandatory) line += "  MANDATORY";
            else if (dead) line += "  DEAD";
            Console.WriteLine(line);
        }
        Console.WriteLine($"    best single point: {best.Aptitude} ({best.Best:+0.000%;-0.000%})");
    }

    Console.WriteLine();
    Console.WriteLine("  A healthy free-build distribution has NO row marked MANDATORY and NO row marked DEAD:");
    Console.WriteLine("  every aptitude is the best point somewhere, and none is the best point everywhere.");
    return 0;
}

// Sweep every status in the locked catalog and report what each one actually does to a fight.
// The apply contest is the shipped ResistanceEvaluator throughout; only the per-round bookkeeping
// (dot ticks, cc lost rounds) belongs to this tool.
int StatusSweep(Options o)
{
    TuningBootstrap.Load(o.Sets);
    Analytic.AssertChannelsRegistered();
    var ladder = new FusionRpg.Core.Power.PowerLadder(
        FusionRpg.Core.Power.PowerTuningLoader.Parse(
            File.ReadAllText(Path.Combine(TuningBootstrap.RepoRoot, "data", "tuning", "power-scale.v2.json"))));
    var modelName = o.Models ?? "aptitudes.v1";
    var model = modelName.StartsWith("aptitudes", StringComparison.OrdinalIgnoreCase)
        ? AptitudeTuning.Load(modelName).ToModel(modelName)
        : AptitudeModel.Load(modelName);
    var builds = (o.Archetypes ?? "force,finesse,bastion")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(Build.Load).ToList();
    var theta = o.Thetas is { } th ? int.Parse(th.Split(',')[0]) : 100;
    var actions = o.Actions is { } an ? ActionSet.Load(an) : null;
    var baseline = StatusProfile.Default;

    Console.WriteLine();
    Console.WriteLine($"  ALL {StatusProfile.AllIds.Count} STATUSES IN THE LOCKED CATALOG · Θ {theta} · {model.Name}");
    Console.WriteLine($"  profile: {baseline.MagnitudeShareOfBase:P0} of base per round, {baseline.BaseDurationRounds:0.#} rounds, grant {baseline.GrantChance:P0} — BEFORE potency");
    Console.WriteLine("  Every number below is what the shipped ResistanceEvaluator returns for that pairing.");
    Console.WriteLine();

    var hdr = $"  {"status",-13}{"cat",-11}{"attacker",-10}{"defender",-10}{"p(apply)",10}{"netF",8}{"mag x",9}{"dur",8}{"kill rnds",11}{"vs none",10}";
    Console.WriteLine(hdr);
    Console.WriteLine("  " + new string('-', hdr.Length - 2));

    var worst = new List<(string Id, string Cat, double Ratio, string Pair)>();
    foreach (var id in StatusProfile.AllIds)
    {
        var profile = baseline.With(id);
        for (var i = 0; i < builds.Count; i++)
        for (var j = 0; j < builds.Count; j++)
        {
            if (i == j) continue;
            var atk = builds[i].At(theta, model, ladder);
            var def = builds[j].At(theta, model, ladder);

            Analytic.Status = null;
            var noneRounds = Analytic.Predict(atk, def, actions).RoundsB;
            Analytic.Status = profile;
            var pred = Analytic.Predict(atk, def, actions);
            Analytic.Status = null;

            var (p, mag, dur) = StatusMath.Expected(atk, def, profile, (atk.BaseDamage.Min + atk.BaseDamage.Max) / 2.0);
            var netF = dur <= 0 ? 0 : dur / profile.BaseDurationRounds;
            var magX = profile.MagnitudeShareOfBase <= 0 ? 0
                : mag / ((atk.BaseDamage.Min + atk.BaseDamage.Max) / 2.0 * profile.MagnitudeShareOfBase);
            var ratio = noneRounds <= 0 ? 1 : pred.RoundsB / noneRounds;
            worst.Add((id, profile.Category, ratio, $"{atk.Name}->{def.Name}"));

            // One row per status, on its strongest pairing only — 21 x 6 rows is a wall, not a report.
            if (i == 0 && j == 1)
                Console.WriteLine($"  {id,-13}{profile.Category,-11}{atk.Name,-10}{def.Name,-10}{p,10:P1}{netF,8:0.0}x{magX,8:0.0}x{dur,8:0.0}{pred.RoundsB,11:0.0}{ratio,9:P0}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("  MOST LETHAL PAIRINGS — kill time as a share of the same fight with no status at all");
    foreach (var w in worst.OrderBy(x => x.Ratio).Take(8))
        Console.WriteLine($"    {w.Id,-13}{w.Cat,-11}{w.Pair,-24}{w.Ratio,8:P0}  of baseline");
    Console.WriteLine();
    var byCat = worst.GroupBy(x => x.Cat).OrderBy(g => g.Average(x => x.Ratio));
    Console.WriteLine("  BY CATEGORY (mean across all 6 orderings)");
    foreach (var g in byCat)
        Console.WriteLine($"    {g.Key,-11}{g.Average(x => x.Ratio),8:P0} of baseline   ·  best {g.Min(x => x.Ratio):P0}  worst {g.Max(x => x.Ratio):P0}");
    Console.WriteLine();
    Console.WriteLine("  contagion is a DOT here with its spread removed — a 1v1 has no second host, so its");
    Console.WriteLine("  real mechanic is structurally unmeasurable in this harness.");
    return 0;
}

int Ladder(Options o)
{
    TuningBootstrap.Load(o.Sets);
    var ladder = new FusionRpg.Core.Power.PowerLadder(
        FusionRpg.Core.Power.PowerTuningLoader.Parse(
            File.ReadAllText(Path.Combine(TuningBootstrap.RepoRoot, "data", "tuning", "power-scale.v2.json"))));
    var builds = (o.Archetypes ?? "force,finesse,bastion")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(Build.Load).ToList();
    var thetas = (o.Thetas ?? "10,20,50,100,300,1000")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(int.Parse).ToList();
    foreach (var name in (o.Models ?? "h-contest,h-magnitude")
             .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        LadderTest.Run(AptitudeModel.Load(name), builds, thetas,
            o.Trials ?? 1500, o.Seed ?? 42, o.Rounds ?? 3000, ladder, Console.Out);
    return 0;
}

int Matrix(Options o)
{
    TuningBootstrap.Load(o.Sets);
    var names = (o.Archetypes ?? "force,finesse,bastion")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var arch = names.Select(Archetype.Load).ToList();
    var n = o.Trials ?? 2000;
    var seed = o.Seed ?? 42;
    var rounds = o.Rounds ?? 2000;

    var cells = new Dictionary<(int, int), DuelSummary>();
    for (var i = 0; i < arch.Count; i++)
    for (var j = 0; j < arch.Count; j++)
    {
        if (i == j) continue;
        Console.Error.WriteLine($"  ... {arch[i].Name} vs {arch[j].Name}");
        cells[(i, j)] = Simulator.Duel(arch[i], arch[j], n, seed, rounds);
    }

    Console.WriteLine(Header(new Scenario { Name = "posture matrix", Seed = seed }, o));
    Console.WriteLine($"  {n:N0} duels per cell, max {rounds} rounds, initiative alternates each duel");
    Console.WriteLine();
    Console.WriteLine("  ROW's win share vs COLUMN  (0.50 = coin flip, >0.50 = row beats column)");
    Console.WriteLine();
    Console.Write($"  {"",-10}");
    foreach (var c in arch) Console.Write($"{c.Name,12}");
    Console.WriteLine();
    for (var i = 0; i < arch.Count; i++)
    {
        Console.Write($"  {arch[i].Name,-10}");
        for (var j = 0; j < arch.Count; j++)
            Console.Write(i == j ? $"{"—",12}" : $"{cells[(i, j)].AWinShare,12:P1}");
        Console.WriteLine();
    }
    Console.WriteLine();
    Console.WriteLine($"  {"matchup",-22} {"A wins",9} {"B wins",9} {"both die",9} {"stale",8} {"rounds",8}");
    Console.WriteLine("  " + new string('-', 70));
    for (var i = 0; i < arch.Count; i++)
    for (var j = i + 1; j < arch.Count; j++)
    {
        var d = cells[(i, j)];
        Console.WriteLine($"  {d.A + " vs " + d.B,-22} {d.AWins,9:P1} {d.BWins,9:P1} " +
                          $"{d.MutualKills,9:P1} {d.Stalemates,8:P1} {d.MedianRounds,8:N0}");
    }
    return 0;
}

int Fight(Options o)
{
    TuningBootstrap.Load(o.Sets);
    var names = (o.Scenario ?? throw new InvalidOperationException("fight needs --scenario"))
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var rows = new List<FightSummary>();
    Scenario? first = null;
    foreach (var name in names)
    {
        var scenario = LoadScenario(new Options { Scenario = name });
        first ??= scenario;
        if (o.Trials is { } t) scenario.Trials = t;
        if (o.Seed is { } sd) scenario.Seed = sd;
        if (o.Rounds is { } mr) scenario.MaxRounds = mr;
        var startHp = (scenario.AttackerHp.Min + scenario.AttackerHp.Max) / 2.0;
        rows.Add(FightSummary.From(scenario.Name, Simulator.RunFights(scenario), startHp));
        Console.Error.Write($"\r  {scenario.Name}                      ");
    }
    Console.Error.Write("\r                                        \r");

    Console.WriteLine(Header(first!, o));
    Console.WriteLine($"  {rows[0].Fights:N0} fights each, max {first!.MaxRounds} swings\n");
    Console.WriteLine($"  {"build",-24} {"ATTACKER dies",14} {"defender dies",14} {"both die",9} {"stalemate",10} {"swings med",11}");
    Console.WriteLine("  " + new string('-', 88));
    foreach (var r in rows)
    {
        Console.WriteLine($"  {r.Label,-24} {r.DefenderWinRate,14:P1} {r.AttackerWinRate,14:P1} " +
                          $"{r.MutualKillRate,9:P1} {r.StalemateRate,10:P1} {r.Rounds.Median,11:N0}");
        // The four outcomes partition every fight; if they ever do not, the table is lying and the
        // run should be thrown away rather than read.
        var total = r.DefenderWinRate + r.AttackerWinRate + r.MutualKillRate + r.StalemateRate;
        if (Math.Abs(total - 1.0) > 1e-9)
            throw new InvalidOperationException($"{r.Label}: outcomes sum to {total:P4}, not 100%");
    }
    Console.WriteLine();
    Console.WriteLine($"  {"build",-24} {"dealt",13} {"reflected",13} {"refl/dealt",11} {"atk hp left",12}");
    Console.WriteLine("  " + new string('-', 78));
    foreach (var r in rows)
        Console.WriteLine($"  {r.Label,-24} {r.MeanDamageDealt,13:N0} {r.MeanDamageReflected,13:N0} " +
                          $"{r.ReflectedShareOfDealt,11:P1} {r.MeanAttackerHpLeftPct,12:P1}");

    if (o.Out is { } path)
    {
        using var w = new StreamWriter(path);
        w.WriteLine("build," + string.Join(",", rows[0].Metrics().Select(m => m.Key)));
        foreach (var r in rows)
            w.WriteLine(r.Label + "," + string.Join(",", r.Metrics().Select(m =>
                m.Value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture))));
        Console.WriteLine($"\n  wrote {path}");
    }
    return 0;
}

int Compare(Options o)
{
    // Load once with no overrides purely to resolve RepoRoot for scenario lookup; each variant
    // re-loads with its own patch set below.
    TuningBootstrap.Load(Array.Empty<string>());
    var variants = VariantSet.Load(o.Variants
        ?? throw new InvalidOperationException("compare needs --variants <name|path>"));
    var scenarioName = o.Scenario ?? variants.Scenario
        ?? throw new InvalidOperationException("no scenario: pass --scenario or set it in the variants file");

    var summaries = new List<Summary>();
    foreach (var v in variants.Variants)
    {
        TuningBootstrap.Load(v.Set);
        var scenario = LoadScenario(new Options { Scenario = scenarioName });
        if (o.Trials is { } t) scenario.Trials = t;
        if (o.Seed is { } sd) scenario.Seed = sd;
        summaries.Add(Summary.From(v.Name, Simulator.Run(scenario)));
        Console.Error.Write($"\r  {v.Name}                    ");
    }
    Console.Error.Write("\r                                        \r");

    Console.WriteLine();
    Console.WriteLine($"  COMPARE   {variants.Name}   scenario '{scenarioName}'   " +
                      $"{summaries[0].Trials:N0} trials each");
    Console.WriteLine();
    Console.WriteLine($"  {"variant",-22} {"miss",7} {"parry",7} {"block",7} {"dmg mean",11} {"dmg/base",9} {"zeroDmg",8} {"reflect",8} {"self%",9}");
    Console.WriteLine("  " + new string('-', 100));
    foreach (var r in summaries)
        Console.WriteLine(
            $"  {r.Label,-22} {r.MissRate,7:P1} {r.ParryRate,7:P1} {r.BlockRate,7:P1} " +
            $"{r.DefenderDamage.Mean,11:N1} {r.MeanDamageRatio,9:F3} {r.ZeroDamageTrials,8:N0} " +
            $"{r.ReflectRate,8:P1} {r.SelfDamageShareOfDealt,9:P1}");
    Console.WriteLine();
    Console.WriteLine($"  {"variant",-22} {"p5",9} {"p25",9} {"median",9} {"p75",9} {"p95",9} {"max",9}");
    Console.WriteLine("  " + new string('-', 82));
    foreach (var r in summaries)
        Console.WriteLine(
            $"  {r.Label,-22} {r.DefenderDamage.P5,9:N0} {r.DefenderDamage.P25,9:N0} " +
            $"{r.DefenderDamage.Median,9:N0} {r.DefenderDamage.P75,9:N0} {r.DefenderDamage.P95,9:N0} " +
            $"{r.DefenderDamage.Max,9:N0}");

    if (o.Out is { } path)
    {
        WriteCsv(path, summaries.Select(x => ("variant", (object)x.Label)).ToArray(), summaries);
        Console.WriteLine();
        Console.WriteLine($"  wrote {path}");
    }
    return 0;
}

int List()
{
    foreach (var dir in ScenarioDirs())
    {
        if (!Directory.Exists(dir)) continue;
        Console.WriteLine(dir);
        foreach (var f in Directory.EnumerateFiles(dir, "*.json").OrderBy(x => x))
        {
            var name = Path.GetFileNameWithoutExtension(f);
            string? desc = null;
            try { desc = Scenario.Load(f).Description; } catch { /* listing must not fail on one bad file */ }
            Console.WriteLine($"  {name,-28} {desc}");
        }
    }
    return 0;
}

string Header(Scenario s, Options o)
{
    var p = CombatPolicy.Default;
    var b = new StringBuilder();
    b.AppendLine();
    b.AppendLine($"  SCENARIO  {s.Name}");
    if (!string.IsNullOrWhiteSpace(s.Description)) b.AppendLine($"            {s.Description}");
    b.AppendLine($"  seed {s.Seed}   elements {s.Elements}   reflection {(s.Reflection ? "on" : "off")}");
    b.AppendLine($"  TUNING    pierce {p.PierceScale:0.##}  amp {p.AmpScale:0.##}  " +
                 $"reflectRate {p.ReflectRateScale:0.##}  reflectShare {p.ReflectShareScale:0.##}");
    b.AppendLine($"            parryCap {p.ParryCapPermille}‰  blockCap {p.BlockCapPermille}‰  " +
                 $"avoidBandCap {p.AvoidanceBandCapPermille}‰  procDepth {p.ProcDepthLimit}");
    if (o.Sets.Count > 0) b.AppendLine($"  OVERRIDES {string.Join("  ", o.Sets)}");
    return b.ToString();
}

void WriteCsv(string path, IReadOnlyList<(string Key, object Value)> firstCol, IReadOnlyList<Summary> rows)
{
    using var w = new StreamWriter(path);
    var metricNames = rows[0].Metrics().Select(m => m.Key).ToArray();
    w.WriteLine(firstCol[0].Key + "," + string.Join(",", metricNames));
    for (var i = 0; i < rows.Count; i++)
    {
        var lead = Convert.ToString(firstCol[Math.Min(i, firstCol.Count - 1)].Value, CultureInfo.InvariantCulture);
        w.WriteLine(lead + "," + string.Join(",",
            rows[i].Metrics().Select(m => m.Value.ToString("G17", CultureInfo.InvariantCulture))));
    }
}

Scenario LoadScenario(Options o)
{
    var name = o.Scenario ?? throw new InvalidOperationException("need --scenario <name|path>");
    if (File.Exists(name)) return Scenario.Load(name);
    foreach (var dir in ScenarioDirs())
    {
        var candidate = Path.Combine(dir, name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name : name + ".json");
        if (File.Exists(candidate)) return Scenario.Load(candidate);
    }
    throw new FileNotFoundException($"scenario '{name}' not found (try: CombatSim list)");
}

IEnumerable<string> ScenarioDirs()
{
    yield return Path.Combine(AppContext.BaseDirectory, "scenarios");
    if (!string.IsNullOrEmpty(TuningBootstrap.RepoRoot))
        yield return Path.Combine(TuningBootstrap.RepoRoot, "tools", "CombatSim", "scenarios");
}

Options ParseArgs(string[] a)
{
    var o = new Options();
    for (var i = 0; i < a.Length; i++)
    {
        string Next(string flag) =>
            i + 1 < a.Length ? a[++i] : throw new InvalidOperationException($"{flag} needs a value");
        switch (a[i])
        {
            case "--scenario" or "-s": o.Scenario = Next("--scenario"); break;
            case "--trials" or "-n": o.Trials = int.Parse(Next("--trials")); break;
            case "--seed": o.Seed = int.Parse(Next("--seed")); break;
            case "--set": o.Sets.Add(Next("--set")); break;
            case "--channel" or "-c": o.Channel = Next("--channel"); break;
            case "--side": o.Side = Next("--side"); break;
            case "--from": o.From = double.Parse(Next("--from"), CultureInfo.InvariantCulture); break;
            case "--to": o.To = double.Parse(Next("--to"), CultureInfo.InvariantCulture); break;
            case "--steps": o.Steps = int.Parse(Next("--steps")); break;
            case "--csv" or "--out": o.Out = Next("--csv"); break;
            case "--variants" or "-v": o.Variants = Next("--variants"); break;
            case "--fight": o.FightMode = true; break;
            case "--archetypes" or "--builds" or "-a": o.Archetypes = Next("--archetypes"); break;
            case "--models" or "-m": o.Models = Next("--models"); break;
            case "--theta": o.Thetas = Next("--theta"); break;
            case "--restarts": o.Restarts = int.Parse(Next("--restarts")); break;
            case "--rounds": o.Rounds = int.Parse(Next("--rounds")); break;
            case "--no-verify": o.NoVerify = true; break;
            case "--analytic": o.Analytic = true; break;
            case "--actions": o.Actions = Next("--actions"); break;
            case "--status": o.Status = true; break;
            default: throw new InvalidOperationException($"unknown option '{a[i]}'");
        }
    }
    return o;
}

int Fail(string message)
{
    Console.Error.WriteLine("error: " + message);
    Usage();
    return 1;
}

void Usage() => Console.WriteLine("""
    CombatSim — drives the real combat pipeline over many randomized fights.

      run     --scenario <name|path> [--trials N] [--seed N] [--set d.k=v]... [--csv out.csv]
      sweep   --scenario <name|path> --channel <id> [--side attacker|defender]
              --from A --to B [--steps N] [--trials N] [--set d.k=v]... [--csv out.csv]
      compare --variants <name|path> [--scenario <name>] [--trials N] [--csv out.csv]
      list

    --set patches a tuning file in memory before it loads, e.g.
      --set combat.pierceScale=200      --set shield.chipFloorKPm=50

    Examples
      CombatSim run   -s baseline -n 10000
      CombatSim sweep -s duel --channel combat.penetration.omni --side attacker --from 0 --to 200
      CombatSim run   -s duel --set combat.pierceScale=500
    """);

sealed class Options
{
    public string? Scenario;
    public int? Trials;
    public int? Seed;
    public string? Channel;
    public string? Side;
    public double? From;
    public double? To;
    public int? Steps;
    public string? Out;
    public string? Variants;
    public bool FightMode;
    public string? Archetypes;
    public string? Models;
    public string? Thetas;
    public int? Restarts;
    public int? Rounds;
    public bool NoVerify;
    public bool Analytic;
    public string? Actions;
    public bool Status;
    public List<string> Sets = new();
}
