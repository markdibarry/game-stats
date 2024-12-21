using System.Collections.Generic;
using System.Text.Json.Serialization;
using GameCore.Utility;

namespace GameCore.Statistics;

public class Stat : IPoolable
{
    public string StatTypeId { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Growth? Growth { get; set; }
    public float BaseValue { get; set; }
    public float CurrentValue { get; set; }
    public List<Modifier> Modifiers { get; set; } = Pool.GetList<Modifier>();

    public static Stat Create(string statTypeId, float baseValue)
    {
        return Create(statTypeId, baseValue, new(), []);
    }

    public static Stat Create(string statTypeId, float baseValue, List<Modifier> mods)
    {
        return Create(statTypeId, baseValue, new(), mods);
    }

    public static Stat Create(string statTypeId, float baseValue, Growth growth, List<Modifier> mods)
    {
        Stat stat = Pool.Get<Stat>();
        stat.StatTypeId = statTypeId;
        stat.BaseValue = baseValue;
        stat.CurrentValue = baseValue;
        stat.Growth = growth;

        if (mods.Count != 0)
            stat.Modifiers.AddRange(mods);

        return stat;
    }

    public void AddMod(Modifier mod)
    {
        Modifiers.Add(mod);
        Modifiers.SortByOp();
    }

    public float CalculateDefault(bool ignoreHidden)
    {
        return Modifier.Calculate(Modifiers, BaseValue, ignoreHidden);
    }

    public void ClearObject()
    {
        StatTypeId = string.Empty;
        Growth = default;
        BaseValue = default;
        CurrentValue = default;

        foreach (Modifier mod in Modifiers)
            mod.ReturnToPool();

        Modifiers.Clear();
    }

    public Modifier? GetFirstModifier(bool hasSource)
    {
        foreach (Modifier mod in Modifiers)
        {
            if (hasSource == mod.Source is not null)
                return mod;
        }

        return null;
    }

    public bool IsEmpty()
    {
        return Modifiers.Count == 0 && string.IsNullOrEmpty(Growth?.TypeId) && BaseValue == 0;
    }

    public Stat Clone(bool ignoreModsWithSource = false)
    {
        Stat clone = Pool.Get<Stat>();
        clone.StatTypeId = StatTypeId;
        clone.Growth = Growth;
        clone.BaseValue = BaseValue;
        clone.CurrentValue = CurrentValue;

        foreach (Modifier mod in Modifiers)
        {
            if (!ignoreModsWithSource || mod.Source is null)
                clone.Modifiers.Add(mod.Clone());
        }

        return clone;
    }

    public void SortModifiers()
    {
        Modifiers.SortByOp();
    }

    public bool TryRemoveMod(Modifier mod)
    {
        if (!Modifiers.Remove(mod))
            return false;

        mod.Unregister();
        mod.ReturnToPool();
        return true;
    }

    public bool TryRemoveModBySource(Modifier sourceMod, object? source)
    {
        if (source is null)
            return false;

        Modifier? mod = FindModBySource(sourceMod, source);

        if (mod is null)
            return false;

        return TryRemoveMod(mod);
    }

    /// <summary>
    /// Removes Modifiers without sources.
    /// </summary>
    /// <param name="statType"></param>
    public void RemoveSourcelessMods(StatsBase stats)
    {
        for (int i = Modifiers.Count - 1; i >= 0; i--)
        {
            Modifier mod = Modifiers[i];

            if (mod.Source is null)
            {
                Modifiers.RemoveAt(i);
                stats.RaiseModChanged(StatTypeId, ModChangeType.Remove);
                mod.Unregister();
                mod.ReturnToPool();
            }
        }
    }

    private Modifier? FindModBySource(Modifier sourceMod, object source)
    {
        foreach (Modifier mod in Modifiers)
        {
            if (mod.Source == source && mod.Op == sourceMod.Op)
                return mod;
        }

        return null;
    }
}

public readonly struct Growth()
{
    public Growth(string typeId, float start, float end)
        : this()
    {
        TypeId = typeId;
        Start = start;
        End = end;
    }

    public string TypeId { get; } = string.Empty;
    public float Start { get; }
    public float End { get; }
}