using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using GameCore.Utility;

namespace GameCore.Statistics;

public class Stats : IPoolable
{
    static Stats()
    {
        s_calculateDefault = (stats, stat, mods, ignoreHidden) =>
        {
            return Modifier.Calculate(mods, stat.BaseValue, ignoreHidden);
        };
        s_isImmuneToStatusEffect = (stats, statTypeId) => false;
    }

    private static readonly Dictionary<string, float> s_statDefault = [];
    private static readonly Dictionary<string, CalculateDel> s_statToCalculateDel = [];
    private static readonly Dictionary<string, ModifyDel> s_statToModifyDelegate = [];
    private static readonly CalculateDel s_calculateDefault;
    private static StatusEffectDel s_isImmuneToStatusEffect;

    [JsonIgnore]
    public object? StatsOwner { get; private set; }
    protected Dictionary<string, Stat> StatLookup { get; } = [];
    protected ModifierLookup ModifierLookup { get; } = [];
    protected EffectLookup StatusEffects { get; } = [];

    public event Action<double>? ProcessTime;
    public event Action<Stats, string>? StatChanged;
    public event Action<Stats, string>? StatusEffectChanged;

    public delegate void ModifyDel(Stats stats, string statTypeId);
    public delegate float CalculateDel(Stats stats, Stat stat, List<Modifier> mods, bool ignoreHidden);
    public delegate bool StatusEffectDel(Stats stats, StatusEffect statusEffect);

    public static EffectDef RegisterEffect(string effectTypeId)
    {
        return EffectDefDB.Register(effectTypeId);
    }

    public static void RegisterCondition<T>(string conditionTypeId) where T : Condition, new()
    {
        ConditionDB.Register<T>(conditionTypeId);
    }

    /// <summary>
    /// Registers a stat type.
    /// </summary>
    /// <param name="statTypeId">The stat type identifier</param>
    /// <param name="defaultValue">The default value for the stat</param>
    /// <param name="calculateDel">The delegate for custom calculation logic</param>
    /// <param name="modifyDel">The delegate that will be called when this stat is modified</param>
    public static void RegisterStatType(
        string statTypeId,
        float defaultValue = 0,
        CalculateDel? calculateDel = null,
        ModifyDel? modifyDel = null)
    {
        s_statDefault.Add(statTypeId, defaultValue);
        calculateDel ??= s_calculateDefault;
        s_statToCalculateDel.Add(statTypeId, calculateDel);

        if (modifyDel is not null)
            s_statToModifyDelegate.Add(statTypeId, modifyDel);
    }

    /// <summary>
    /// Sets the delegate that determines if a status effect should be active.
    /// </summary>
    /// <param name="isImmune">The delegate that determines if a status effect should be active</param>
    public static void SetIsImmuneToStatusEffect(StatusEffectDel isImmune)
    {
        s_isImmuneToStatusEffect = isImmune;
    }

    public void ClearObject()
    {
        StatsOwner = null;
        ModifierLookup.ClearObject();
        StatLookup.Clear();
        StatusEffects.ClearObject();
    }

    public Stats Clone()
    {
        Stats clone = Create(StatLookup, ModifierLookup, StatusEffects);
        clone.Initialize(null);
        return clone;
    }

    public void CopyStatLookupTo(Dictionary<string, Stat> clone)
    {
        clone.Clear();

        foreach (var kvp in StatLookup)
            clone.Add(kvp.Key, kvp.Value);
    }

    public void CopyModifierLookupTo(ModifierLookup clone, bool ignoreModsWithSource = false)
    {
        ModifierLookup.CopyTo(clone, ignoreModsWithSource);
    }

    public void CopyStatusEffectsTo(EffectLookup clone)
    {
        StatusEffects.CopyTo(clone);
    }

    public static Stats Create(
        Dictionary<string, Stat> statLookup,
        ModifierLookup modLookup,
        EffectLookup statusEffects)
    {
        Stats stats = Pool.Get<Stats>();

        foreach (KeyValuePair<string, float> pair in s_statDefault)
        {
            if (statLookup.TryGetValue(pair.Key, out Stat stat))
                stats.StatLookup[pair.Key] = stat;
            else
                stats.StatLookup[pair.Key] = new Stat(pair.Value);
        }

        modLookup.CopyTo(stats.ModifierLookup, false);
        statusEffects.CopyTo(stats.StatusEffects);
        return stats;
    }

    public void Initialize(object? statsOwner)
    {
        StatsOwner = statsOwner;
        ModifierLookup.InitializeAll(this);
        StatusEffects.InitializeAll(this);
    }

    public Stat GetStat(string statTypeId)
    {
        StatLookup.TryGetValue(statTypeId, out Stat stat);
        return stat;
    }

    public void SetStatBase(string statTypeId, float baseValue)
    {
        if (StatLookup.TryGetValue(statTypeId, out Stat stat))
        {
            StatLookup[statTypeId] = stat with { BaseValue = baseValue };
            RaiseStatChanged(statTypeId);
            TryCallModifyDel(statTypeId);
        }
    }

    public void SetStatCurrent(string statTypeId, float currentValue)
    {
        if (StatLookup.TryGetValue(statTypeId, out Stat stat))
        {
            StatLookup[statTypeId] = stat with { CurrentValue = currentValue };
            RaiseStatChanged(statTypeId);
            TryCallModifyDel(statTypeId);
        }
    }

    public void AddModNoCopy(Modifier sourceMod, object? source)
    {
        ModifierLookup.AddMod(this, sourceMod, source, false);
    }

    public void AddMod(Modifier sourceMod, object? source)
    {
        ModifierLookup.AddMod(this, sourceMod, source, true);
    }

    public IReadOnlyList<Modifier> GetModifiersByType(string statTypeId)
    {
        if (!ModifierLookup.TryGetValue(statTypeId, out List<Modifier>? mods))
            mods = [];

        return mods;
    }

    public void RemoveMod(string statTypeId, object? source)
    {
        ModifierLookup.RemoveModBySource(this, statTypeId, source);
    }

    public void RemoveModByRef(Modifier mod)
    {
        ModifierLookup.RemoveMod(this, mod);
    }

    public bool HasStatusEffect(string effectTypeId)
    {
        return StatusEffects.IsActive(effectTypeId);
    }

    public void AddStackNoCopy(EffectStack stack, object? source)
    {
        StatusEffects.AddStack(this, stack, source, false);
    }

    public void AddStack(EffectStack stack, object? source)
    {
        StatusEffects.AddStack(this, stack, source, true);
    }

    public void RemoveStackByRef(EffectStack stack)
    {
        StatusEffects.RemoveStack(this, stack);
    }

    public void RemoveStack(string effectTypeId, object source)
    {
        StatusEffects.RemoveStacksBySource(this, effectTypeId, source);
    }

    public void RemoveStatusEffect(string effectTypeId)
    {
        StatusEffects.RemoveStacksBySource(this, effectTypeId, null);
    }

    public bool IsImmuneToStatusEffect(string effectTypeId)
    {
        if (!StatusEffects.TryGetValue(effectTypeId, out StatusEffect? statusEffect))
            return false;

        return IsImmuneToStatusEffect(statusEffect);
    }

    public bool IsImmuneToStatusEffect(StatusEffect statusEffect)
    {
        return s_isImmuneToStatusEffect(this, statusEffect);
    }

    public void UpdateStatusEffect(string effectTypeId)
    {
        if (!StatusEffects.TryGetValue(effectTypeId, out StatusEffect? statusEffect)
           || !EffectDefDB.TryGetValue(effectTypeId, out EffectDef? effectDef))
            return;

        StatusEffects.UpdateActive(this, statusEffect, effectDef);
    }

    public float Calculate(string statTypeId, bool ignoreHidden = false)
    {
        if (!StatLookup.TryGetValue(statTypeId, out Stat stat))
            return 0;

        if (!ModifierLookup.TryGetValue(statTypeId, out List<Modifier>? mods))
            mods = [];

        if (!s_statToCalculateDel.TryGetValue(statTypeId, out CalculateDel? func))
            return s_calculateDefault(this, stat, mods, ignoreHidden);

        return func(this, stat, mods, ignoreHidden);
    }

    public void Process(double delta, bool processTime)
    {
        if (processTime)
            ProcessTime?.Invoke(delta);
    }

    public void RaiseStatChanged(string statTypeId)
    {
        StatChanged?.Invoke(this, statTypeId);
    }

    public void RaiseStatusEffectChanged(string effectTypeId)
    {
        StatusEffectChanged?.Invoke(this, effectTypeId);
    }

    private void TryCallModifyDel(string statTypeId)
    {
        if (!s_statToModifyDelegate.TryGetValue(statTypeId, out var func))
            return;

        func(this, statTypeId);
    }
}
