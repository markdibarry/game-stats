using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using GameCore.Utility;

namespace GameCore.Statistics;

public abstract class StatsBase : IPoolable
{
    [JsonIgnore]
    public IStatsOwner? StatsOwner { get; private set; }
    protected Dictionary<string, Stat> StatLookup { get; } = [];
    protected Dictionary<string, StatusEffect> StatusEffects { get; } = [];

    public event Action<double>? ProcessTime;
    public event Action<StatsBase, string, ModChangeType>? ModChanged;
    public event Action<StatsBase, string, ModChangeType>? StatusEffectChanged;

    public void ClearObject()
    {
        StatsOwner = null;
        StatLookup.ClearObject();
        StatusEffects.ClearObject();
        ClearData();
    }

    protected abstract void ClearData();

    public T Clone<T>() where T : StatsBase, new()
    {
        T clone = Pool.Get<T>();
        SetCloneData(clone);
        clone.Initialize(null, StatLookup, StatusEffects);
        return clone;
    }

    public void Initialize(
        IStatsOwner? statsOwner,
        Dictionary<string, Stat> statLookup,
        Dictionary<string, StatusEffect> statusEffects)
    {
        StatsOwner = statsOwner;

        foreach (KeyValuePair<string, StatusEffect> pair in statusEffects)
        {
            StatusEffect statusEffect = pair.Value.Clone();
            statusEffect.Register(this);
            StatusEffects.Add(statusEffect.StatTypeId, statusEffect);
        }

        statLookup.CloneTo(StatLookup);
        AddDefaultStats();

        foreach (var pair in StatLookup)
        {
            pair.Value.SortModifiers();

            foreach (Modifier mod in pair.Value.Modifiers)
                mod.Register(this, mod.Source);
        }
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

    public virtual float CalculateStat(string statTypeId, bool ignoreHidden = false)
    {
        return CalculateDefault(statTypeId, ignoreHidden);
    }

    public Dictionary<string, Stat> CloneStatLookup(bool ignoreModsWithSource = false)
    {
        return StatLookup.Clone(ignoreModsWithSource);
    }

    public Dictionary<string, StatusEffect> CloneEffects()
    {
        Dictionary<string, StatusEffect> result = [];

        foreach (var pair in StatusEffects)
        {
            result.Add(pair.Key, pair.Value.Clone());
        }

        return result;
    }

    public Stat? GetStat(string statTypeId)
    {
        StatLookup.TryGetValue(statTypeId, out Stat? stat);
        return stat;
    }

    public IReadOnlyList<Modifier> GetModifiersByType(string statTypeId)
    {
        return GetStat(statTypeId)?.Modifiers ?? [];
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

        if (!StatLookup.TryGetValue(statTypeId, out Stat? stat))
            return false;

        if (!stat.TryRemoveModBySource(sourceMod, source))
            return false;

        if (stat.IsEmpty())
            RemoveStat(stat);

        RaiseModChanged(statTypeId, ModChangeType.Remove);
        UpdateCustomStatType(statTypeId);

        return true;
    }

    public bool TryRemoveMod(Modifier mod)
    {
        if (!StatLookup.TryGetValue(mod.StatTypeId, out Stat? stat))
            return false;

        // Use RemoveModBySource() to remove mod with source
        if (mod.Source is not null)
            return false;

        string statTypeId = mod.StatTypeId;

        if (!stat.TryRemoveMod(mod))
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
        bool hasActiveMods = CalculateStat(statTypeId) > 0;

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

    protected virtual void AddDefaultStats() { }

    protected void AddDefaultStat(string statTypeId, float defaultValue)
    {
        AddDefaultStat(statTypeId, defaultValue, new());
    }

    protected void AddDefaultStat(string statTypeId, float defaultValue, Growth growth)
    {
        if (StatLookup.ContainsKey(statTypeId))
            return;

        Stat stat = Pool.Get<Stat>();
        stat.StatTypeId = statTypeId;
        stat.BaseValue = defaultValue;
        stat.CurrentValue = defaultValue;
        stat.Growth = growth;
        StatLookup[statTypeId] = stat;
    }

    protected float CalculateDefault(string statTypeId, bool ignoreHidden)
    {
        if (!StatLookup.TryGetValue(statTypeId, out Stat? stat))
            return 0;

        return stat.CalculateDefault(ignoreHidden);
    }

    protected virtual bool IsStatusEffect(string statType) => false;

    protected virtual bool IsImmuneToStatusEffect(string statType) => false;

    protected abstract void SetCloneData(StatsBase clone);

    protected void UpdateStatusEffect(string statTypeId)
    {
        bool hasActiveMods = CalculateStat(statTypeId) > 0;

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
        if (!StatLookup.TryGetValue(newMod.StatTypeId, out Stat? stat))
        {
            stat = Stat.Create(newMod.StatTypeId, 0);
            StatLookup[newMod.StatTypeId] = stat;
        }

        stat.AddMod(newMod);
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
        if (!StatLookup.TryGetValue(statTypeId, out Stat? stat))
            return null;

        return stat.GetFirstModifier(hasSource);
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

    public void RemoveStat(string statTypeId)
    {
        if (!StatLookup.TryGetValue(statTypeId, out Stat? stat))
            return;

        RemoveStat(stat);
    }

    public void RemoveStat(Stat stat)
    {
        StatLookup.Remove(stat.StatTypeId);
        stat.ReturnToPool();
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
        if (!StatLookup.TryGetValue(statTypeId, out Stat? stat))
            return;

        stat.RemoveSourcelessMods(this);

        if (stat.IsEmpty())
            RemoveStat(stat);
    }
}
