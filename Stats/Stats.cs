using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using GameCore.Statistics.Pooling;

namespace GameCore.Statistics;

public class Stats : IPoolable
{
    static Stats()
    {
        s_calculateDefault = (stats, statTypeId, ignoreHidden) =>
        {
            Stat stat = stats.GetStat(statTypeId);
            IReadOnlyList<Modifier> mods = stats.GetModifiersOrEmpty(statTypeId);
            return Modifier.Calculate(mods, stat.BaseValue, ignoreHidden);
        };
        s_isImmuneToStatusEffect = (stats, statTypeId) => false;
    }

    public Stats()
    {
        StatLookup = [];
        Modifiers = [];
        StatusEffects = [];
    }

    public Stats(Dictionary<string, Stat> statLookup)
    {
        StatLookup = statLookup;
        Modifiers = [];
        StatusEffects = [];
    }

    [JsonConstructor]
    public Stats(
        IReadOnlyDictionary<string, Stat> attributes,
        ModifierLookup modifiersWithoutSources,
        EffectLookup effectsWithoutSources
    )
    {
        StatLookup = (Dictionary<string, Stat>)attributes;
        Modifiers = modifiersWithoutSources;
        StatusEffects = effectsWithoutSources;
    }

    private static readonly Dictionary<string, float> s_statDefault = [];
    private static readonly Dictionary<string, CalculateDel> s_statToCalculateDel = [];
    private static readonly Dictionary<string, ModifyDel> s_statToModifyDelegate = [];
    private static readonly CalculateDel s_calculateDefault;
    private static StatusEffectDel s_isImmuneToStatusEffect;

    private HashSet<TimedCondition>? _timedConditions;
    private HashSet<ResourceCondition>? _resourceConditions;

    [JsonIgnore]
    public object? StatsOwner { get; private set; }
    protected Dictionary<string, Stat> StatLookup { get; init; }
    protected ModifierLookup Modifiers { get; init; }
    protected EffectLookup StatusEffects { get; init; }

    /// <summary>
    /// Stat lookup for serialization.
    /// </summary>
    [JsonInclude]
    internal IReadOnlyDictionary<string, Stat> Attributes => StatLookup;
    /// <summary>
    /// Modifiers for serialization.
    /// </summary>
    [JsonInclude, JsonPropertyName(nameof(Modifiers))]
    internal ModifierLookup ModifiersWithoutSources => (ModifierLookup)GetModifiersUnsafe(true);
    /// <summary>
    /// Status effects for serialization.
    /// </summary>
    [JsonInclude, JsonPropertyName(nameof(StatusEffects))]
    internal EffectLookup EffectsWithoutSources => (EffectLookup)GetStatusEffectsUnsafe(true);

    public event Action<Stats, string>? StatChanged;
    public event Action<Stats, string>? StatusEffectChanged;
    public event Action<Stats, string>? EffectStackChanged;

    public delegate void ModifyDel(Stats stats, string statTypeId);
    public delegate float CalculateDel(Stats stats, string statTypeId, bool ignoreHidden);
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

    /// <summary>
    /// Creates a new Stats object using the collections provided as a base.
    /// </summary>
    /// <param name="statLookup">The stat lookup.</param>
    /// <param name="modLookup">The modifier lookup</param>
    /// <param name="statusEffects">The effect lookup</param>
    /// <returns>The new Stats object</returns>
    public static Stats Create(
        Dictionary<string, Stat>? statLookup,
        ModifierLookup? modLookup,
        EffectLookup? statusEffects)
    {
        Stats stats = Pool.Get<Stats>();

        foreach (KeyValuePair<string, float> pair in s_statDefault)
        {
            if (statLookup != null && statLookup.TryGetValue(pair.Key, out Stat stat))
                stats.StatLookup[pair.Key] = stat;
            else
                stats.StatLookup[pair.Key] = new Stat(pair.Value);
        }

        modLookup?.CopyTo(stats.Modifiers, false);
        statusEffects?.CopyTo(stats.StatusEffects, false);
        return stats;
    }

    public void ClearObject()
    {
        StatsOwner = null;
        Modifiers.ClearObject();
        StatusEffects.ClearObject();
        StatLookup.Clear();
        _timedConditions?.Clear();
        _resourceConditions?.Clear();
    }

    /// <summary>
    /// Clones the Stats object.
    /// </summary>
    /// <remarks>
    /// Can be useful for comparing stat changes.
    /// </remarks>
    /// <returns>The new Stats object</returns>
    public Stats Clone()
    {
        Stats clone = Create(StatLookup, Modifiers, StatusEffects);
        return clone;
    }

    /// <summary>
    /// Copies all Stats to a new Dictionary<string, Stat>.
    /// </summary>
    /// <param name="clone">The collection to clone to</param>
    public void CopyStatLookupTo(Dictionary<string, Stat> clone)
    {
        clone.Clear();

        foreach (var kvp in StatLookup)
            clone.Add(kvp.Key, kvp.Value);
    }

    /// <summary>
    /// Copies all Stats modifiers to a new ModifierLookup.
    /// </summary>
    /// <param name="clone">The ModifierLookup to clone to</param>
    /// <param name="ignoreModsWithSource">If true, does not copy modifiers with a source.</param>
    public void CopyModifierLookupTo(ModifierLookup clone, bool ignoreModsWithSource)
    {
        Modifiers.CopyTo(clone, ignoreModsWithSource);
    }

    /// <summary>
    /// Copies all Stats effects to a new EffectLookup.
    /// </summary>
    /// <param name="clone">The EffectLookup to clone to</param>
    /// <param name="ignoreModsWithSource">If true, does not copy modifiers with a source.</param>
    public void CopyStatusEffectsTo(EffectLookup clone, bool ignoreStacksWithSource)
    {
        StatusEffects.CopyTo(clone, ignoreStacksWithSource);
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

    /// <summary>
    /// Calculates all stats and modifiers of the provided type.
    /// </summary>
    /// <param name="statTypeId">The stat type identifier</param>
    /// <param name="ignoreHidden">If true, ignores modifiers with the IsHidden flag</param>
    /// <returns>The result of the calculation</returns>
    public float Calculate(string statTypeId, bool ignoreHidden = false)
    {
        Stat stat = GetStat(statTypeId);
        IReadOnlyList<Modifier> mods = GetModifiersOrEmpty(statTypeId);

        if (!s_statToCalculateDel.TryGetValue(statTypeId, out CalculateDel? func))
            return s_calculateDefault(this, statTypeId, ignoreHidden);

        return func(this, statTypeId, ignoreHidden);
    }

    public IReadOnlyDictionary<string, List<Modifier>> GetModifiersUnsafe(bool ignoreModsWithSource)
    {
        if (!ignoreModsWithSource)
            return Modifiers;

        ModifierLookup result = [];
        Modifiers.CopyTo(result, true);
        return result;
    }

    public IReadOnlyList<Modifier> GetModifiersOrEmpty(string statTypeId)
    {
        if (!Modifiers.TryGetValue(statTypeId, out List<Modifier>? list))
            return Array.Empty<Modifier>();

        return list;
    }

    /// <summary>
    /// Adds a Modifier.
    /// </summary>
    /// <remarks>
    /// If the modifier is self-managed, supply null as the source.
    /// <br/>
    /// If the modifier provided is meant to be reused, clone the modifier using Modifier.Clone().
    /// </remarks>
    /// <param name="mod">The modifier to be added.</param>
    /// <param name="source">The source the modifier is tied to.</param>
    public void AddMod(Modifier mod, object? source)
    {
        string statTypeId = mod.StatTypeId;

        // If Stats is not initialized, add it without checks.
        if (StatsOwner == null)
        {
            if (!Modifiers.TryGetValue(statTypeId, out var mods))
            {
                mods = ListPool.Get<Modifier>();
                Modifiers.Add(statTypeId, mods);
            }

            mod.Source = source;
            mods.Add(mod);
        }
        else
        {
            Modifiers.AddMod(this, mod, source);
            TryCallModifyDel(statTypeId);
        }
    }

    /// <summary>
    /// Removes all modifiers matching the type and source provided.
    /// </summary>
    /// <param name="statTypeId">The stat type identifier</param>
    /// <param name="source">The source to match to</param>
    public void RemoveMod(string statTypeId, object? source)
    {
        Modifiers.RemoveModBySource(this, statTypeId, source);
        TryCallModifyDel(statTypeId);
    }

    /// <summary>
    /// Removes a modifier by reference.
    /// </summary>
    /// <param name="mod">The modifier to remove</param>
    public void RemoveModByRef(Modifier mod)
    {
        string statTypeId = mod.StatTypeId;
        Modifiers.RemoveMod(this, mod);
        TryCallModifyDel(statTypeId);
    }

    public IReadOnlyDictionary<string, StatusEffect> GetStatusEffectsUnsafe(bool ignoreStacksWithSource)
    {
        if (!ignoreStacksWithSource)
            return StatusEffects;

        EffectLookup result = [];
        StatusEffects.CopyTo(result, true);
        return result;
    }

    /// <summary>
    /// Returns current status effects, both active and unactive.
    /// </summary>
    /// <returns></returns>
    public IReadOnlyCollection<string> GetStatusEffects()
    {
        return StatusEffects.Keys;
    }

    /// <summary>
    /// If true, a StatusEffect matching the effect type Id provided is currently active.
    /// </summary>
    /// <param name="effectTypeId">The effect type identifier</param>
    /// <returns></returns>
    public bool HasStatusEffect(string effectTypeId)
    {
        return StatusEffects.IsActive(effectTypeId);
    }

    /// <summary>
    /// Adds an EffectStack to a StatusEffect of the same type.
    /// </summary>
    /// <remarks>
    /// If the stack is self-managed, supply null as the source.
    /// <br/>
    /// If the stack provided is meant to be reused, clone the stack using EffectStack.Clone().
    /// </remarks>
    /// <param name="stack">The stack to be added.</param>
    /// <param name="source">The source the stack is tied to.</param>
    public void AddStack(EffectStack stack, object? source)
    {
        string effectTypeId = stack.EffectTypeId;

        // If Stats is not initialized, add it without checks.
        if (StatsOwner == null)
        {
            if (!StatusEffects.TryGetValue(effectTypeId, out var effect))
            {
                effect = StatusEffect.Create(effectTypeId);
                StatusEffects.Add(effectTypeId, effect);
            }

            effect.AddStackUnsafe(stack);
        }
        else
        {
            StatusEffects.AddStack(this, stack, source);
        }
    }

    /// <summary>
    /// Removes all stacks matching the type and source provided.
    /// </summary>
    /// <param name="effectTypeId">The effect type identifier</param>
    /// <param name="source">The source to match to</param>
    public void RemoveStack(string effectTypeId, object source)
    {
        StatusEffects.RemoveStacksBySource(effectTypeId, source);
    }

    /// <summary>
    /// Removes a stack by reference.
    /// </summary>
    /// <param name="stack">The stack to remove</param>
    public void RemoveStackByRef(EffectStack stack)
    {
        StatusEffects.RemoveStackByRef(stack);
    }

    /// <summary>
    /// Removes all self-managed stacks associated with the StatusEffect.
    /// </summary>
    /// <remarks>
    /// Note: Does not remove stacks with sources.
    /// </remarks>
    /// <param name="effectTypeId"></param>
    public void RemoveStatusEffect(string effectTypeId)
    {
        StatusEffects.RemoveStacksBySource(effectTypeId, null);
    }

    /// <summary>
    /// Finds any stacks with the provided source, removes them, and adds a new stack.
    /// </summary>
    /// <param name="oldSource">The source to match when removing</param>
    /// <param name="newStack">The new stack to add</param>
    /// <param name="newSource">The source the new stack should be tied to</param>
    public void ReplaceStack(object oldSource, EffectStack newStack, object? newSource)
    {
        StatusEffects.ReplaceStack(this, oldSource, newStack, newSource);
    }

    /// <summary>
    /// If true, the user is immune to the provided status effect type.
    /// </summary>
    /// <remarks>
    /// Note: If SetIsImmuneToStatusEffect() is not called on configuration, this will always return false.
    /// </remarks>
    /// <param name="effectTypeId"></param>
    /// <returns></returns>
    public bool IsImmuneToStatusEffect(string effectTypeId)
    {
        return s_isImmuneToStatusEffect(this, effectTypeId);
    }

    /// <summary>
    /// Manually checks and updates whether a StatusEffect's active status.
    /// </summary>
    /// <param name="effectTypeId">The effect type identifier</param>
    public void UpdateStatusEffect(string effectTypeId)
    {
        if (!StatusEffects.TryGetValue(effectTypeId, out StatusEffect? statusEffect))
            return;

        statusEffect.UpdateActive(this);
    }

    /// <summary>
    /// Processes timer conditions.
    /// </summary>
    /// <param name="delta"></param>
    public void Process(double delta)
    {
        if (_timedConditions == null)
            return;

        foreach (TimedCondition condition in _timedConditions)
            condition.OnProcess(this, delta);
    }

    protected internal void AddTimedCondition(TimedCondition timedCondition)
    {
        _timedConditions ??= [];
        _timedConditions.Add(timedCondition);
    }

    protected internal void AddResourceCondition(ResourceCondition resourceCondition)
    {
        _resourceConditions ??= [];
        _resourceConditions.Add(resourceCondition);
    }

    protected internal void RemoveTimedCondition(TimedCondition timedCondition)
    {
        _timedConditions ??= [];
        _timedConditions.Remove(timedCondition);
    }

    protected internal void RemoveResourceCondition(ResourceCondition resourceCondition)
    {
        _resourceConditions ??= [];
        _resourceConditions.Remove(resourceCondition);
    }

    internal void RaiseStatChanged(string statTypeId)
    {
        if (_resourceConditions != null)
        {
            foreach (var condition in _resourceConditions)
                condition.OnStatChanged(statTypeId);
        }

        StatChanged?.Invoke(this, statTypeId);
    }

    internal void RaiseStatusEffectChanged(string effectTypeId)
    {
        StatusEffectChanged?.Invoke(this, effectTypeId);
    }

    internal void RaiseEffectStackChanged(string effectTypeId)
    {
        EffectStackChanged?.Invoke(this, effectTypeId);
    }

    private void TryCallModifyDel(string statTypeId)
    {
        if (!s_statToModifyDelegate.TryGetValue(statTypeId, out ModifyDel? modify))
            return;

        modify(this, statTypeId);
    }
}
