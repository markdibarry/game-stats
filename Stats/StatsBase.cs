using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using GameCore.Utility;

namespace GameCore.Statistics;

public abstract class StatsBase : IPoolable
{
    static StatsBase()
    {
        s_calculateDefault = (stats, stat, mods, ignoreHidden) =>
        {
            return Modifier.Calculate(mods, stat.BaseValue, ignoreHidden);
        };
        s_isStatusEffect = (stats, statTypeId) => false;
        s_isImmuneToStatusEffect = (stats, statTypeId) => false;
    }

    private static readonly Dictionary<string, float> s_statDefault = [];
    private static readonly Dictionary<string, string> s_statToCalculateType = [];
    private static readonly Dictionary<string, CalculateDel> s_calculateTypeToDelegate = [];
    private static readonly CalculateDel s_calculateDefault;
    private static StatusEffectDel s_isStatusEffect;
    private static StatusEffectDel s_isImmuneToStatusEffect;

    [JsonIgnore]
    public IStatsOwner? StatsOwner { get; private set; }
    protected Dictionary<string, Stat> StatLookup { get; } = [];
    protected Dictionary<string, StatusEffect> StatusEffects { get; } = [];
    protected ModifierLookup ModifierLookup { get; } = [];

    public event Action<double>? ProcessTime;
    public event Action<StatsBase, string, ModChangeType>? ModChanged;
    public event Action<StatsBase, string, ModChangeType>? StatusEffectChanged;

    public delegate float CalculateDel(StatsBase stats, Stat stat, List<Modifier> mods, bool ignoreHidden);
    public delegate bool StatusEffectDel(StatsBase stats, string statTypeId);

    public static void RegisterStatType(string statTypeId, float defaultValue = 0, string calculateType = "Default")
    {
        s_statDefault.Add(statTypeId, defaultValue);
        s_statToCalculateType.Add(statTypeId, calculateType);
    }

    public static void RegisterCalculateType(string calculateType, CalculateDel? calculate = null)
    {
        calculate ??= s_calculateDefault;
        s_calculateTypeToDelegate[calculateType] = calculate;
    }

    public static void SetIsStatusEffect(StatusEffectDel isStatusEffect)
    {
        s_isStatusEffect = isStatusEffect;
    }

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

    public T Clone<T>() where T : StatsBase, new()
    {
        T clone = Create<T>(StatLookup, ModifierLookup, StatusEffects);
        clone.Initialize(null);
        return clone;
    }

    public static T Create<T>(
        Dictionary<string, Stat> statLookup,
        ModifierLookup modLookup,
        Dictionary<string, StatusEffect> statusEffects)
        where T : StatsBase, new()
    {
        T stats = Pool.Get<T>();

        foreach (KeyValuePair<string, StatusEffect> pair in statusEffects)
        {
            StatusEffect statusEffect = pair.Value.Clone();
            stats.StatusEffects.Add(statusEffect.StatTypeId, statusEffect);
        }

        foreach (KeyValuePair<string, float> pair in s_statDefault)
        {
            if (statLookup.TryGetValue(pair.Key, out Stat stat))
                stats.StatLookup[pair.Key] = stat;
            else
                stats.StatLookup[pair.Key] = new Stat(pair.Key, pair.Value);
        }

        modLookup.Clone(stats.ModifierLookup, false);
        return stats;
    }

    public void Initialize(IStatsOwner? statsOwner)
    {
        StatsOwner = statsOwner;

        foreach (KeyValuePair<string, StatusEffect> pair in StatusEffects)
            pair.Value.Register(this);

        ModifierLookup.RegisterAll(this);
    }

    public void AddMod(Modifier sourceMod, bool clone = true)
    {
        AddMod(sourceMod, clone, null);
    }

    public void AddMod(Modifier sourceMod, object? source)
    {
        AddMod(sourceMod, true, source);
    }

    private void AddMod(Modifier sourceMod, bool clone, object? source)
    {
        Modifier newMod = clone ? sourceMod.Clone() : sourceMod;

        if (IsStatusEffect(newMod.StatTypeId))
        {
            AddStatusEffectMod(newMod, source);
            return;
        }

        newMod.Register(this, source);

        if (source is null && !newMod.IsActive)
        {
            newMod.Unregister();
            newMod.ReturnToPool();
            return;
        }

        AddModToStatLookup(newMod);
    }

    public void CloneStatLookup(Dictionary<string, Stat> clone)
    {
        clone.Clear();

        foreach (var kvp in StatLookup)
            clone.Add(kvp.Key, kvp.Value);
    }

    public void CloneModifierLookup(ModifierLookup clone, bool ignoreModsWithSource = false)
    {
        ModifierLookup.Clone(clone, ignoreModsWithSource);
    }

    public void CloneStatusEffects(Dictionary<string, StatusEffect> clone)
    {
        clone.Clear();

        foreach (var pair in StatusEffects)
            clone.Add(pair.Key, pair.Value.Clone());
    }

    public Stat GetStat(string statTypeId)
    {
        StatLookup.TryGetValue(statTypeId, out Stat stat);
        return stat;
    }

    public void SetStatBase(string statTypeId, float baseValue)
    {
        if (StatLookup.TryGetValue(statTypeId, out Stat stat))
            StatLookup[statTypeId] = stat with { BaseValue = baseValue };
    }

    public void SetStatCurrent(string statTypeId, float currentValue)
    {
        if (StatLookup.TryGetValue(statTypeId, out Stat stat))
            StatLookup[statTypeId] = stat with { CurrentValue = currentValue };
    }

    public IReadOnlyList<Modifier> GetModifiersByType(string statTypeId)
    {
        if (!ModifierLookup.TryGetValue(statTypeId, out List<Modifier>? mods))
            mods = [];

        return mods;
    }

    public bool HasStatusEffect(string statusEffectTypeId)
    {
        return StatusEffects.ContainsKey(statusEffectTypeId);
    }

    public void Process(double delta, bool processTime)
    {
        if (processTime)
        {
            ProcessTime?.Invoke(delta);
        }
    }

    public bool TryRemoveModBySource(Modifier sourceMod, object? source)
    {
        string statTypeId = sourceMod.StatTypeId;

        if (!ModifierLookup.TryRemoveModBySource(sourceMod, source))
            return false;

        RaiseModChanged(statTypeId, ModChangeType.Remove);
        UpdateCustomStatType(statTypeId);

        return true;
    }

    public bool TryRemoveMod(Modifier mod)
    {
        // Use RemoveModBySource() to remove mod with source
        if (mod.Source is not null)
            return false;

        string statTypeId = mod.StatTypeId;

        if (!ModifierLookup.TryRemoveMod(mod))
            return false;

        RaiseModChanged(statTypeId, ModChangeType.Remove);
        UpdateCustomStatType(statTypeId);

        return true;
    }

    public bool TryRemoveStatusEffect(string statTypeId)
    {
        if (!StatusEffects.TryGetValue(statTypeId, out StatusEffect? statusEffect))
            return false;

        RemoveSourcelessMods(statTypeId);
        bool hasActiveMods = Calculate(statTypeId) > 0;

        if (hasActiveMods)
            return false;

        statusEffect.EffectDef?.OnDeactivate?.Invoke(this, statusEffect);
        statusEffect.Unregister();
        statusEffect.ReturnToPool();
        StatusEffects.Remove(statTypeId);
        StatusEffectChanged?.Invoke(this, statTypeId, ModChangeType.Remove);

        return true;
    }

    public virtual void UpdateCustomStatType(string statTypeId)
    {
        if (IsStatusEffect(statTypeId))
            UpdateStatusEffect(statTypeId);
    }

    public float Calculate(string statTypeId, bool ignoreHidden = false)
    {
        if (!StatLookup.TryGetValue(statTypeId, out Stat stat))
            return 0;

        if (!ModifierLookup.TryGetValue(statTypeId, out List<Modifier>? mods))
            mods = [];

        return Calculate(stat, mods, ignoreHidden);
    }

    public float Calculate(Stat stat, List<Modifier> mods, bool ignoreHidden)
    {
        if (!s_statToCalculateType.TryGetValue(stat.StatTypeId, out string? calculateType)
            || !s_calculateTypeToDelegate.TryGetValue(calculateType, out CalculateDel? func))
        {
            return s_calculateDefault(this, stat, mods, ignoreHidden);
        }

        return func(this, stat, mods, ignoreHidden);
    }

    protected bool IsStatusEffect(string statType)
    {
        return s_isStatusEffect(this, statType);
    }

    public bool IsImmuneToStatusEffect(string statType)
    {
        return s_isImmuneToStatusEffect(this, statType);
    }

    protected void UpdateStatusEffect(string statTypeId)
    {
        bool hasActiveMods = Calculate(statTypeId) > 0;

        if (StatusEffects.ContainsKey(statTypeId))
        {
            if (!hasActiveMods || IsImmuneToStatusEffect(statTypeId))
                TryRemoveStatusEffect(statTypeId);
        }
        else
        {
            if (hasActiveMods && !IsImmuneToStatusEffect(statTypeId))
                TryAddStatusEffect(statTypeId);
        }
    }

    private void AddModToStatLookup(Modifier newMod)
    {
        ModifierLookup.AddMod(newMod);
        UpdateCustomStatType(newMod.StatTypeId);
        ModChanged?.Invoke(this, newMod.StatTypeId, ModChangeType.Add);
    }

    private void AddStatusEffectMod(Modifier newMod, object? source)
    {
        if (!EffectDefDB.TryGetValue(newMod.StatTypeId, out EffectDef? effectDef))
            return;

        // Copy global duration if available
        if (newMod.Duration is null && effectDef.DefaultDuration is not null)
            newMod.Duration = effectDef.DefaultDuration.Clone();

        newMod.Register(this, source);

        if (!TryAddStatusEffectMod(newMod, effectDef))
        {
            newMod.Unregister();
            newMod.ReturnToPool();
        }

        bool TryAddStatusEffectMod(Modifier newMod, EffectDef effectDef)
        {
            if (newMod.Source is null && (!newMod.IsActive || IsImmuneToStatusEffect(newMod.StatTypeId)))
                return false;

            if (StatusEffects.TryGetValue(newMod.StatTypeId, out StatusEffect? statusEffect))
                return TryUpdateStatusEffect(statusEffect, newMod, effectDef);

            return TryAddStatusEffect(newMod);
        }
    }

    /// <summary>
    /// Gets the first modifier that has or doesn't have a source depending on the hasSource parameter.
    /// </summary>
    /// <param name="statTypeId"></param>
    /// <param name="hasSource"></param>
    /// <returns></returns>
    private Modifier? GetFirstModifier(string statTypeId, bool hasSource)
    {
        return ModifierLookup.GetFirstModifier(statTypeId, hasSource);
    }

    /// <summary>
    /// Creates a status effect from a modifier.
    /// </summary>
    /// <param name="newMod"></param>
    /// <returns></returns>
    private bool TryAddStatusEffect(Modifier newMod)
    {
        if (!EffectDefDB.TryGetValue(newMod.StatTypeId, out EffectDef? effectDef))
            return false;

        StatusEffect statusEffect = StatusEffect.Create(effectDef);
        statusEffect.Register(this);
        StatusEffects.Add(newMod.StatTypeId, statusEffect);
        AddModToStatLookup(newMod);
        effectDef.OnActivate?.Invoke(this, statusEffect);
        StatusEffectChanged?.Invoke(this, newMod.StatTypeId, ModChangeType.Add);

        return true;
    }

    /// <summary>
    /// Creates a status effect.
    /// </summary>
    /// <param name="statTypeId"></param>
    /// <returns></returns>
    private bool TryAddStatusEffect(string statTypeId)
    {
        // Redundant if only used by UpdateStatusEffect()
        if (StatusEffects.ContainsKey(statTypeId))
            return false;

        if (!EffectDefDB.TryGetValue(statTypeId, out EffectDef? effectDef))
            return false;

        StatusEffect statusEffect = StatusEffect.Create(effectDef);
        statusEffect.Register(this);
        StatusEffects.Add(statTypeId, statusEffect);
        effectDef.OnActivate?.Invoke(this, statusEffect);
        StatusEffectChanged?.Invoke(this, statTypeId, ModChangeType.Add);

        return true;
    }

    private bool TryUpdateStatusEffect(StatusEffect statusEffect, Modifier newMod, EffectDef effectDef)
    {
        if (effectDef.StackMode == StackModes.Multi ||
            newMod.Source is not null ||
            GetFirstModifier(newMod.StatTypeId, false) is not Modifier existingMod)
        {
            AddModToStatLookup(newMod);
            effectDef.OnAddStack?.Invoke(this, statusEffect);
            return true;
        }

        if (newMod.Op == OpDB.Replace && newMod.Duration is Condition condition)
        {
            condition.Unregister();
            newMod.Duration = null;
            existingMod.Duration?.Unregister();
            existingMod.Duration = condition;
            condition.Register(existingMod, null);
        }

        if (effectDef.StackMode == StackModes.Reup)
            existingMod.Duration?.ReupAllData();
        else if (effectDef.StackMode == StackModes.Extend)
            TryExtend(existingMod, newMod);

        // TODO: Max stack
        existingMod.Value += newMod.Value;
        return false;

        static bool TryExtend(Modifier existingMod, Modifier newMod)
        {
            // See if the new mod has a time condition
            if (newMod.Duration?.GetFirstCondition<TimedCondition>() is not TimedCondition modTime)
                return false;

            // If existing mod has a time condition, extend it.
            if (existingMod.Duration?.GetFirstCondition<TimedCondition>() is TimedCondition effectTime)
            {
                effectTime.TimeLeft += modTime.TimeLeft;
                return true;
            }

            // If not, clone the modifier's timed condition and apply it to the effect.
            Condition clonedTime = modTime.CloneSingle();
            clonedTime.Register(existingMod, null);

            if (existingMod.Duration is not null)
                clonedTime.SetOr(existingMod.Duration);

            existingMod.Duration = clonedTime;

            return true;
        }
    }

    public void RaiseModChanged(string statTypeId, ModChangeType modChangeType)
    {
        ModChanged?.Invoke(this, statTypeId, modChangeType);
    }

    /// <summary>
    /// Removes Modifiers without sources.
    /// </summary>
    /// <param name="statTypeId"></param>
    private void RemoveSourcelessMods(string statTypeId)
    {
        ModifierLookup.RemoveSourcelessMods(this, statTypeId);
    }
}
