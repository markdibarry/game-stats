using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using GameCore.Pooling;

namespace GameCore.Stats;

public sealed class StatSet : IPoolable
{
    static StatSet()
    {
        s_calculateDefault = (stats, statTypeId, ignoreHidden) =>
        {
            StatAttribute stat = stats.GetStat(statTypeId);
            IReadOnlyList<Modifier> mods = stats.GetModifiersOrEmpty(statTypeId);
            return Modifier.Calculate(mods, stat.BaseValue, ignoreHidden);
        };
        s_isImmuneToStatusEffect = (stats, statTypeId) => false;
    }

    public StatSet()
    {
        Attributes = [];
        Modifiers = [];
        StatusEffects = [];
    }

    public StatSet(Dictionary<string, StatAttribute> attributes)
    {
        Attributes = attributes;
        Modifiers = [];
        StatusEffects = [];
    }

    [JsonConstructor]
    public StatSet(
        Dictionary<string, StatAttribute> attributes,
        ModifierLookup modifiersWithoutSources,
        EffectLookup effectsWithoutSources
    )
    {
        Attributes = attributes;
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

    /// <summary>
    /// The stats owner.
    /// </summary>
    [JsonIgnore]
    public object? Context { get; private set; }
    /// <summary>
    /// Stat lookup.
    /// </summary>
    public Dictionary<string, StatAttribute> Attributes { get; init; }
    [JsonIgnore]
    public ModifierLookup Modifiers { get; init; }
    [JsonIgnore]
    public EffectLookup StatusEffects { get; init; }

    /// <summary>
    /// Modifiers for serialization.
    /// </summary>
    [JsonInclude, JsonPropertyName(nameof(Modifiers))]
    public ModifierLookup ModifiersWithoutSources => (ModifierLookup)GetModifiersUnsafe(true);
    /// <summary>
    /// Status effects for serialization.
    /// </summary>
    [JsonInclude, JsonPropertyName(nameof(StatusEffects))]
    public EffectLookup EffectsWithoutSources => (EffectLookup)GetStatusEffectsUnsafe(true);

    public event Action<StatSet, string>? StatChanged;
    public event Action<StatSet, string>? StatusEffectChanged;
    public event Action<StatSet, string>? EffectStackChanged;

    public delegate void ModifyDel(StatSet stats, string statTypeId);
    public delegate float CalculateDel(StatSet stats, string statTypeId, bool ignoreHidden);
    public delegate bool StatusEffectDel(StatSet stats, string effectTypeId);

    public static EffectDef RegisterEffect(string effectTypeId)
    {
        return EffectDefDB.Register(effectTypeId);
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
    /// Creates a new StatSet object using the collections provided as a base.
    /// </summary>
    /// <param name="attributes">The stat lookup.</param>
    /// <param name="modLookup">The modifier lookup</param>
    /// <param name="statusEffects">The effect lookup</param>
    /// <returns>The new StatSet object</returns>
    public static StatSet Create(
        Dictionary<string, StatAttribute>? attributes,
        ModifierLookup? modLookup,
        EffectLookup? statusEffects)
    {
        StatSet stats = Pool.Get<StatSet>();

        foreach (KeyValuePair<string, float> pair in s_statDefault)
        {
            if (attributes != null && attributes.TryGetValue(pair.Key, out StatAttribute stat))
                stats.Attributes[pair.Key] = stat;
            else
                stats.Attributes[pair.Key] = new StatAttribute(pair.Value);
        }

        modLookup?.CopyTo(stats.Modifiers, false);
        statusEffects?.CopyTo(stats.StatusEffects, false);
        return stats;
    }

    /// <summary>
    /// Creates a new StatSet object using the existing StatSet as a base.
    /// </summary>
    /// <remarks>
    /// Can be useful for comparing stat changes.
    /// </remarks>
    /// <returns>The new StatSet object</returns>
    public static StatSet Create(StatSet statSet)
    {
        return Create(statSet.Attributes, statSet.Modifiers, statSet.StatusEffects);
    }

    public void ClearObject()
    {
        Context = null;
        Modifiers.ClearObject();
        StatusEffects.ClearObject();
        Attributes.Clear();

        if (_timedConditions != null)
        {
            foreach (var cond in _timedConditions)
                cond.ReturnToPool();

            _timedConditions.Clear();
        }
        _timedConditions?.Clear();
        _resourceConditions?.Clear();
    }

    public void Initialize(object? context)
    {
        Context = context;
        Modifiers.InitializeAll(this);
        StatusEffects.InitializeAll(this);
    }

    public StatAttribute GetStat(string statTypeId)
    {
        if (Attributes.TryGetValue(statTypeId, out StatAttribute stat))
            return stat;

        if (s_statDefault.TryGetValue(statTypeId, out float defaultValue))
            return new StatAttribute(defaultValue);

        return stat;
    }

    public void SetStatBase(string statTypeId, float baseValue)
    {
        StatAttribute stat = GetStat(statTypeId);
        Attributes[statTypeId] = stat with { BaseValue = baseValue };
        RaiseStatChanged(statTypeId);
        TryCallModifyDel(statTypeId);
    }

    public void SetStatCurrent(string statTypeId, float currentValue)
    {
        StatAttribute stat = GetStat(statTypeId);
        Attributes[statTypeId] = stat with { CurrentValue = currentValue };
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
        StatAttribute stat = GetStat(statTypeId);
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
        if (Context == null)
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

    public IReadOnlyDictionary<string, StatusEffect> GetStatusEffectsUnsafe(bool ignoreStacksWithSource = false)
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
    /// <param name="stackMode">The mode for how the stack should be added.</param>
    /// <param name="source">The source the stack is tied to.</param>
    public void AddStack(EffectStack stack, StackMode stackMode = StackMode.None, object? source = null)
    {
        string effectTypeId = stack.EffectTypeId;

        // If Stats is not initialized, add it without checks.
        if (Context == null)
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
            StatusEffects.AddStack(this, stack, stackMode, source);
        }
    }

    public void AddStack(string effectTypeId, StackMode stackMode = StackMode.None, object? source = null)
    {
        EffectStack stack = EffectStack.Create(effectTypeId);
        AddStack(stack, stackMode, source);
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
    /// Finds any stacks with the provided source, removes them, and adds a new stack with the new source.
    /// </summary>
    /// <param name="oldSource">The source to match when removing</param>
    /// <param name="newStack">The new stack to add</param>
    /// <param name="newSource">The source the new stack should be tied to</param>
    public void ReplaceStack(EffectStack newStack, StackMode stackMode, object oldSource, object? newSource)
    {
        StatusEffects.ReplaceStack(this, newStack, stackMode, oldSource, newSource);
    }

    public void ReplaceStack(string effectTypeId, StackMode stackMode, object oldSource, object? newSource)
    {
        EffectStack stack = EffectStack.Create(effectTypeId);
        ReplaceStack(stack, stackMode, oldSource, newSource);
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
    /// Manually checks and updates whether a StatusEffect is active.
    /// </summary>
    /// <param name="effectTypeId">The effect type identifier</param>
    public void UpdateStatusEffect(string effectTypeId)
    {
        if (StatusEffects.TryGetValue(effectTypeId, out StatusEffect? statusEffect))
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

    internal void AddTimedCondition(TimedCondition timedCondition)
    {
        _timedConditions ??= [];
        _timedConditions.Add(timedCondition);
    }

    internal void AddResourceCondition(ResourceCondition resourceCondition)
    {
        _resourceConditions ??= [];
        _resourceConditions.Add(resourceCondition);
    }

    internal void RemoveTimedCondition(TimedCondition timedCondition)
    {
        if (_timedConditions == null)
            return;

        _timedConditions.Remove(timedCondition);
    }

    internal void RemoveResourceCondition(ResourceCondition resourceCondition)
    {
        if (_resourceConditions == null)
            return;

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
