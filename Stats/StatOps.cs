using System;
using System.Collections.Generic;

namespace GameCore.Statistics;

public static class StatOps
{
    /// <summary>
    /// Id for operation which adds the given value to the previous value.
    /// </summary>
    public const string Add = "Add";
    /// <summary>
    /// Id for operation which adds all modifiers with the same operator id as a percentage,
    /// then multiplies it by the provided value.
    /// </summary>
    /// <remarks>
    /// Ex: baseValue * (1 + (0.2)) = +20% increase
    /// <br/>
    /// Ex: baseValue * (1 + (0.2 + 0.3 - 0.1)) = +40% increase
    /// </remarks>
    public const string AddPercent = "AddPercent";
    /// <summary>
    /// Id for operation which multiplies the given value by the previous value.
    /// </summary>
    public const string Mult = "Mult";
    /// <summary>
    /// Id for operation which adds the given value to the previous value.
    /// These will be added after percentage and multipliers.
    /// </summary>
    public const string AddLate = "AddLate";
    /// <summary>
    /// Id for operation which will change the value to its negative or positive counterpart.
    /// </summary>
    public const string Negate = "Negate";
    /// <summary>
    /// Id for operation which will result in the minimum of the two values.
    /// </summary>
    public const string Min = "Min";
    /// <summary>
    /// Id for operation which will result in the maximum of the two values.
    /// </summary>
    public const string Max = "Max";
    /// <summary>
    /// Id for operation which will replace the value with a new value.
    /// </summary>
    public const string Replace = "Replace";
    /// <summary>
    /// Id for operation which will replace the value with 0.
    /// </summary>
    public const string Zero = "Zero";
    /// <summary>
    /// Id for operation which will replace the value with 1.
    /// </summary>
    public const string One = "One";

    static StatOps()
    {
        s_orderedLookup = new()
        {
            { Add, (mod, a, b) => a + b },
            { AddPercent, (mod, a, b) => a + b },
            { Mult, (mod, a, b) => a * b },
            { AddLate, (mod, a, b) => a + b },
            { Negate, (mod, a, b) => -a },
            { Max, (mod, a, b) => Math.Max(a, b) },
            { Min, (mod, a, b) => Math.Min(a, b) },
            { Replace, (mod, a, b) => b },
            { Zero, (mod, a, b) => 0 },
            { One, (mod, a, b) => 1 }
        };

        s_opSortCompare = static (x, y) =>
        {
            int res = string.Compare(x.StatTypeId, y.StatTypeId);

            if (res != 0)
                return res;

            int left = GetOrder(x.Op);
            int right = GetOrder(y.Op);

            if (left < right)
                return -1;
            if (left > right)
                return 1;
            return 0;
        };
    }

    private static readonly OrderedDictionary<string, OpComputeDel> s_orderedLookup;
    private static readonly Comparison<Modifier> s_opSortCompare;

    /// <summary>
    /// A delegate for computing two values with custom logic.
    /// </summary>
    /// <param name="mod">The modifier</param>
    /// <param name="a">The first value</param>
    /// <param name="b">The second value</param>
    /// <returns>The result of the calculation</returns>
    public delegate float OpComputeDel(Modifier mod, float a, float b);

    /// <summary>
    /// Gets the priority index of the operator.
    /// </summary>
    /// <param name="op">The operator id</param>
    /// <returns>The priority index</returns>
    public static int GetOrder(string op)
    {
        return s_orderedLookup.IndexOf(op);
    }

    /// <summary>
    /// Registers a new operator id and compute delegate and inserts it in the priority
    /// order provided. Otherwise adds it as lowest priority.
    /// </summary>
    /// <param name="opId">The operator id</param>
    /// <param name="func">The compute delegate</param>
    /// <param name="orderIndex">The priority order of the new operator</param>
    public static void RegisterOp(string opId, OpComputeDel func, int orderIndex = -1)
    {
        if (orderIndex == -1)
            s_orderedLookup.Add(opId, func);
        else if (orderIndex >= 0 && orderIndex <= s_orderedLookup.Count)
            s_orderedLookup.Insert(orderIndex, opId, func);
    }

    /// <summary>
    /// Computes two values based on the operator and modifier provided.
    /// If no operator is found, returns the first value.
    /// </summary>
    /// <param name="op">The operator id</param>
    /// <param name="mod">The modifier</param>
    /// <param name="a">The first value</param>
    /// <param name="b">The second value</param>
    /// <returns>The result of the calculation</returns>
    public static float Compute(string op, Modifier mod, float a, float b)
    {
        if (s_orderedLookup.TryGetValue(op, out var func))
            return func(mod, a, b);

        return a;
    }

    /// <summary>
    /// Computes a value with a provided modifier based on the operator and value contained.
    /// </summary>
    /// <param name="mod">The modifier</param>
    /// <param name="a">The value</param>
    /// <returns>The result of the calculation</returns>
    public static float Compute(Modifier mod, float a)
    {
        return Compute(mod.Op, mod, a, mod.Value);
    }

    /// <summary>
    /// Sorts an array of modifiers by operator priority.
    /// </summary>
    /// <param name="mods">The modifier array</param>
    public static void SortByOp(this Modifier[] mods)
    {
        Array.Sort(mods, s_opSortCompare);
    }

    /// <summary>
    /// Sorts a list of modifiers by operator priority.
    /// </summary>
    /// <param name="mods">The modifier list</param>
    public static void SortByOp(this List<Modifier> mods)
    {
        mods.Sort(s_opSortCompare);
    }
}
