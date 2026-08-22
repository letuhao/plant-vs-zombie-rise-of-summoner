namespace FusionRpg.Core.Effects.Atoms;

/// <summary>
/// Candidate (b) for E13: a <b>flattened, non-recursive</b> predicate encoding with precomputed
/// short-circuit targets.
///
/// <para>Every instruction carries the index to jump to on true and on false, so evaluation is one
/// loop over a flat array with no call stack and no bounds-checked slicing. Short-circuiting is not
/// a branch the evaluator decides — it is where the arrows already point.</para>
///
/// <para>The earlier scratch benchmark's flat candidate lost at 47 ns to a typed graph's 7 ns, but it
/// was a naive <c>ref int pc</c> span walker over six identical trees. This is the properly flattened
/// form the spec asked for, measured against real content in <c>tests/FusionRpg.Bench</c>.</para>
/// </summary>
public sealed class FlatPredicate : ICompiledPredicate
{
    /// <summary>Jump targets below zero are answers, not indices.</summary>
    internal const int True = -1;
    internal const int False = -2;

    /// <summary>One test plus where to go next. Strings are interned to ints at compile time.</summary>
    internal readonly struct Op
    {
        public readonly LeafId Id;
        public readonly Subject Subject;
        public readonly int Value;
        public readonly int[]? Set;
        public readonly int OnTrue;
        public readonly int OnFalse;

        public Op(LeafId id, Subject subject, int value, int[]? set, int onTrue, int onFalse)
        {
            Id = id; Subject = subject; Value = value; Set = set; OnTrue = onTrue; OnFalse = onFalse;
        }

        public Op Retarget(int onTrue, int onFalse) => new(Id, Subject, Value, Set, onTrue, onFalse);
    }

    readonly Op[] _ops;
    readonly int _entry;

    FlatPredicate(Op[] ops, int entry)
    {
        _ops = ops;
        _entry = entry;
    }

    public int OpCount => _ops.Length;

    public bool Evaluate(ref FactReader facts)
    {
        var pc = _entry;
        while (pc >= 0)
        {
            ref readonly var op = ref _ops[pc];
            pc = Test(in op, ref facts) ? op.OnTrue : op.OnFalse;
        }
        return pc == True;
    }

    static bool Test(in Op op, ref FactReader f) => op.Id switch
    {
        LeafId.SideIs => f.Side(op.Subject) == op.Value,
        LeafId.TypeIdIs => f.TypeId(op.Subject) == op.Value,
        LeafId.TypeIdIn => Contains(op.Set, f.TypeId(op.Subject)),
        LeafId.ActorIsKiller => f.IsKiller(op.Subject) == (op.Value != 0),
        LeafId.HasStatus => f.HasStatusBit(op.Subject, op.Value),
        LeafId.HpBelowMilli => f.HpMilli(op.Subject) < op.Value,
        LeafId.HpAboveMilli => f.HpMilli(op.Subject) > op.Value,
        LeafId.ElementIs => f.ElementId(op.Subject) == op.Value,
        LeafId.RowIs => f.Row(op.Subject) == op.Value,
        LeafId.ColIs => f.Col(op.Subject) == op.Value,
        LeafId.IsMindControlled => f.IsMindControlled(op.Subject) == (op.Value != 0),
        _ => true,
    };

    static bool Contains(int[]? set, int v)
    {
        if (set is null) return false;
        foreach (var t in set) if (t == v) return true;
        return false;
    }

    /// <summary>
    /// Flatten a validated tree. Emission runs <b>backwards</b> — a node needs its children's indices
    /// before it can point at them, and children are emitted first so those indices already exist.
    /// </summary>
    public static ICompiledPredicate Build(
        PredicateNode? tree, Func<string, int>? statusBit, Func<string, int>? elementId)
    {
        if (tree is null) return PredicateCompiler.Always;

        var ops = new List<Op>();
        var entry = Emit(tree, True, False, ops, statusBit, elementId);
        return new FlatPredicate(ops.ToArray(), entry);
    }

    /// <summary>Emit <paramref name="node"/> and return the index to enter it at.</summary>
    static int Emit(
        PredicateNode node, int onTrue, int onFalse, List<Op> ops,
        Func<string, int>? statusBit, Func<string, int>? elementId)
    {
        switch (node)
        {
            case PredicateNode.And a:
            {
                // Chain right to left: every child fails to the AND's false target, and succeeds
                // into the next child. The last child succeeds to the AND's true target.
                var next = onTrue;
                for (var i = a.Children.Count - 1; i >= 0; i--)
                    next = Emit(a.Children[i], next, onFalse, ops, statusBit, elementId);
                return next;
            }

            case PredicateNode.Or o:
            {
                // Mirror image: every child succeeds straight out, and failure walks to the next.
                var next = onFalse;
                for (var i = o.Children.Count - 1; i >= 0; i--)
                    next = Emit(o.Children[i], onTrue, next, ops, statusBit, elementId);
                return next;
            }

            case PredicateNode.Not n:
                // Negation is free here: swap the arrows rather than emit an instruction.
                return Emit(n.Child, onFalse, onTrue, ops, statusBit, elementId);

            case PredicateNode.Leaf leaf:
            {
                var (value, set) = Intern(leaf, statusBit, elementId);
                ops.Add(new Op(leaf.Id, leaf.Subject, value, set, onTrue, onFalse));
                return ops.Count - 1;
            }

            default:
                return onTrue;
        }
    }

    static (int Value, int[]? Set) Intern(
        PredicateNode.Leaf l, Func<string, int>? statusBit, Func<string, int>? elementId) => l.Id switch
    {
        LeafId.SideIs => (SideOrdinal(l.Text), null),
        LeafId.HasStatus => (statusBit?.Invoke(l.Text!) ?? -1, null),
        LeafId.ElementIs => (elementId?.Invoke(l.Text!) ?? -1, null),
        LeafId.TypeIdIn => (0, l.Values?.ToArray()),
        _ => (l.Value, null),
    };

    static int SideOrdinal(string? side) => side switch
    {
        "plant" => 0,
        "zombie" => 1,
        "bullet" => 2,
        _ => -1,
    };
}
