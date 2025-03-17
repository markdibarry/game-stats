using System;
using System.Collections.Generic;

namespace GameCore.Statistics;

public static class OpDB
{
    static OpDB()
    {
        OrderedLookup = new()
        {
            { PercentAdd, (mod, a, b) => a + b },
            { Add, (mod, a, b) => a + b },
            { PercentMult, (mod, a, b) => a * (1 + b) },
            { Negate, (mod, a, b) => -a },
            { Max, (mod, a, b) => Math.Max(a, b) },
            { Min, (mod, a, b) => Math.Min(a, b) },
            { Replace, (mod, a, b) => b },
            { Zero, (mod, a, b) => 0 },
            { One, (mod, a, b) => 1 }
        };

        OpSortCompare = static (x, y) =>
        {
            int res = string.Compare(x.StatTypeId, y.StatTypeId);

            if (res != 0)
                return res;

            int left = OrderedLookup.IndexOf(x.Op);
            int right = OrderedLookup.IndexOf(y.Op);

            if (left < right)
                return -1;
            if (left > right)
                return 1;
            return 0;
        };
    }

    public const string PercentAdd = "PercentAdd";
    public const string Add = "Add";
    public const string PercentMult = "PercentMult";
    public const string Negate = "Negate";
    public const string Min = "Min";
    public const string Max = "Max";
    public const string Replace = "Replace";
    public const string Zero = "Zero";
    public const string One = "One";

    public delegate float OpComputeDel(Modifier mod, float a, float b);
    public static readonly OrderedDictionary<string, OpComputeDel> OrderedLookup;
    public static readonly Comparison<Modifier> OpSortCompare;

    public static void AddCustomOp(string op, OpComputeDel func, int index = -1)
    {
        if (index == -1)
            OrderedLookup.Add(op, func);
        else if (index >= 0 && index <= OrderedLookup.Count)
            OrderedLookup.Insert(index, op, func);
    }

    public static bool Compare(this CompareOp op, int a, int b)
    {
        return op switch
        {
            CompareOp.Equals => a == b,
            CompareOp.NotEquals => a != b,
            CompareOp.LessEquals => a <= b,
            CompareOp.GreaterEquals => a >= b,
            CompareOp.Less => a < b,
            CompareOp.Greater => a > b,
            CompareOp.None or
            _ => false
        };
    }

    public static float Compute(string op, Modifier mod, float a, float b)
    {
        if (OrderedLookup.TryGetValue(op, out var func))
            return func(mod, a, b);

        return a;
    }

    public static float Compute(Modifier mod, float a)
    {
        return Compute(mod.Op, mod, a, mod.Value);
    }

    public static void SortByOp(this Modifier[] mods)
    {
        Array.Sort(mods, OpSortCompare);
    }

    public static void SortByOp(this List<Modifier> mods)
    {
        mods.Sort(OpSortCompare);
    }
}

public enum CompareOp
{
    None,
    LessEquals,
    GreaterEquals,
    Less,
    Greater,
    Equals,
    NotEquals
}
