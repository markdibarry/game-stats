using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GameCore.Utility;

namespace GameCore.Statistics;

public static class OpDB
{
    static OpDB()
    {
        string[] ops =
        [
            BaseAdd,
            PercentAdd,
            Add,
            PercentMult,
            Negate,
            Min,
            Max,
            Replace,
            Zero,
            One
        ];
        OrderedOps = ops.AsReadOnly();
        s_orderedOpsIndexed = ops.ToIndexedDictionary();
    }

    public const string BaseAdd = "BaseAdd";
    public const string PercentAdd = "PercentAdd";
    public const string Add = "Add";
    public const string PercentMult = "PercentMult";
    public const string Negate = "Negate";
    public const string Min = "Min";
    public const string Max = "Max";
    public const string Replace = "Replace";
    public const string Zero = "Zero";
    public const string One = "One";

    public static ReadOnlyCollection<string> OrderedOps { get; private set; }
    public static readonly Comparison<Modifier> OpSortCompare =
        (x, y) =>
        {
            int res = string.Compare(x.StatTypeId, y.StatTypeId);

            if (res != 0)
                return res;

            int left = x is null ? -1 : GetIndex(x.Op);
            int right = y is null ? -1 : GetIndex(y.Op);

            if (left == right)
                return 0;
            if (left < right)
                return -1;
            if (left > right)
                return 1;
            return 0;
        };
    private static readonly Dictionary<string, Func<Modifier, float, float, float>> s_computeLookup =
        new()
        {
            { Add, (mod, a, b) => a + b },
            { BaseAdd, (mod, a, b) => a + b },
            { PercentAdd, (mod, a, b) => a + b },
            { PercentMult, (mod, a, b) => a * (1 + b) },
            { Negate, (mod, a, b) => -a },
            { Max, (mod, a, b) => Math.Max(a, b) },
            { Min, (mod, a, b) => Math.Min(a, b) },
            { Replace, (mod, a, b) => b },
            { Zero, (mod, a, b) => 0 },
            { One, (mod, a, b) => 1 }
        };
    private static Dictionary<string, int> s_orderedOpsIndexed;

    public static void AddCustomOp(string op, Func<Modifier, float, float, float> func)
    {
        s_computeLookup.Add(op, func);
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
        if (s_computeLookup.TryGetValue(op, out var func))
            return func(mod, a, b);

        return a;
    }

    public static float Compute(Modifier mod, float a)
    {
        return Compute(mod.Op, mod, a, mod.Value);
    }

    public static int GetIndex(string op)
    {
        if (s_orderedOpsIndexed.TryGetValue(op, out int value))
            return value;

        return -1;
    }

    public static void SetOrder(string[] order)
    {
        OrderedOps = order.AsReadOnly();
        s_orderedOpsIndexed = order.ToIndexedDictionary();
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
