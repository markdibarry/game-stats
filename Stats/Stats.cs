using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GameCore.Statistics;

public class Stats : IStatsPoolable
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
    private static readonly List<TimedCondition> s_conditionsToRemove = [];

    private readonly List<TimedCondition> _conditionsToProcess = [];
    private bool _isProcessing;

    [JsonIgnore]
    public object? StatsOwner { get; private set; }
    protected Dictionary<string, Stat> StatLookup { get; } = [];
    protected ModifierLookup Modifiers { get; } = [];
    protected EffectLookup StatusEffects { get; } = [];

    public event Action<Stats, string>? StatChanged;
    public event Action<Stats, string>? StatusEffectChanged;

    public delegate void ModifyDel(Stats stats, string statTypeId);
    public delegate float CalculateDel(Stats stats, Stat stat, List<Modifier> mods, bool ignoreHidden);
    public delegate bool StatusEffectDel(Stats stats, string effectTypeId);

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
        Modifiers.ClearObject();
        StatLookup.Clear();
        StatusEffects.ClearObject();
    }

    public Stats Clone()
    {
        Stats clone = Create(StatLookup, Modifiers, StatusEffects);
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
        Modifiers.CopyTo(clone, ignoreModsWithSource);
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
        Stats stats = StatsPool.Get<Stats>();

        foreach (KeyValuePair<string, float> pair in s_statDefault)
        {
            if (statLookup.TryGetValue(pair.Key, out Stat stat))
                stats.StatLookup[pair.Key] = stat;
            else
                stats.StatLookup[pair.Key] = new Stat(pair.Value);
        }

        modLookup.CopyTo(stats.Modifiers, false);
        statusEffects.CopyTo(stats.StatusEffects);
        return stats;
    }

    public void Initialize(object? statsOwner)
    {
        StatsOwner = statsOwner;
        Modifiers.InitializeAll(this);
        StatusEffects.InitializeAll(this);
    }

    public Stat GetStat(string statTypeId)
    {
        if (StatLookup.TryGetValue(statTypeId, out Stat stat))
            return stat;

        if (s_statDefault.TryGetValue(statTypeId, out float defaultValue))
            return new Stat(defaultValue);

        return stat;
    }

    public void SetStatBase(string statTypeId, float baseValue)
    {
        Stat stat = GetStat(statTypeId);
        StatLookup[statTypeId] = stat with { BaseValue = baseValue };
        RaiseStatChanged(statTypeId);
        TryCallModifyDel(statTypeId);
    }

    public void SetStatCurrent(string statTypeId, float currentValue)
    {
        Stat stat = GetStat(statTypeId);
        StatLookup[statTypeId] = stat with { CurrentValue = currentValue };
        RaiseStatChanged(statTypeId);
        TryCallModifyDel(statTypeId);
    }

    public float Calculate(string statTypeId, bool ignoreHidden = false)
    {
        Stat stat = GetStat(statTypeId);

        if (!Modifiers.TryGetValue(statTypeId, out List<Modifier>? mods))
            mods = [];

        if (!s_statToCalculateDel.TryGetValue(statTypeId, out CalculateDel? func))
            return s_calculateDefault(this, stat, mods, ignoreHidden);

        return func(this, stat, mods, ignoreHidden);
    }

    public void AddModNoCopy(Modifier sourceMod, object? source)
    {
        Modifiers.AddMod(this, sourceMod, source, false);
    }

    public void AddMod(Modifier sourceMod, object? source)
    {
        Modifiers.AddMod(this, sourceMod, source, true);
    }

    public ModifierLookup GetModifiersUnsafe()
    {
        return Modifiers;
    }

    public IReadOnlyList<Modifier> GetModifiersByType(string statTypeId)
    {
        if (!Modifiers.TryGetValue(statTypeId, out List<Modifier>? mods))
            mods = [];

        return mods;
    }

    public void RemoveMod(string statTypeId, object? source)
    {
        Modifiers.RemoveModBySource(this, statTypeId, source);
    }

    public void RemoveModByRef(Modifier mod)
    {
        Modifiers.RemoveMod(this, mod);
    }

    public EffectLookup GetStatusEffectsUnsafe()
    {
        return StatusEffects;
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
        return s_isImmuneToStatusEffect(this, effectTypeId);
    }

    public void UpdateStatusEffect(string effectTypeId)
    {
        if (!StatusEffects.TryGetValue(effectTypeId, out StatusEffect? statusEffect)
           || !EffectDefDB.TryGetValue(effectTypeId, out EffectDef? effectDef))
            return;

        StatusEffects.UpdateActive(this, statusEffect, effectDef);
    }

    /// <summary>
    /// Processes timer conditions.
    /// </summary>
    /// <param name="delta"></param>
    public void Process(double delta)
    {
        // Found it 2-8 times faster (depending on the amount of timers) and with no allocation to
        // manage the conditions manually vs via events.
        _isProcessing = true;

        foreach (TimedCondition condition in _conditionsToProcess)
            condition.OnProcess(delta);

        foreach (TimedCondition condition in s_conditionsToRemove)
            _conditionsToProcess.Remove(condition);

        s_conditionsToRemove.Clear();
        _isProcessing = false;
    }

    protected internal void AddTimedCondition(TimedCondition timedCondition)
    {
        _conditionsToProcess.Add(timedCondition);
    }

    protected internal void RemoveTimedCondition(TimedCondition timedCondition)
    {
        if (_isProcessing)
            s_conditionsToRemove.Add(timedCondition);
        else
            _conditionsToProcess.Remove(timedCondition);
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
        if (!s_statToModifyDelegate.TryGetValue(statTypeId, out ModifyDel? modify))
            return;

        modify(this, statTypeId);
    }
}
