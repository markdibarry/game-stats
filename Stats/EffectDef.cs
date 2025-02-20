using System.Collections.Generic;

namespace GameCore.Statistics;

public class EffectDef
{
    public EffectDef(string statTypeId)
    {
        StatTypeId = statTypeId;
    }

    public delegate void Effect(StatsBase stats, StatusEffect statusEffect);
    public string StatTypeId { get; }
    public int MaxStack { get; init; } = 1;
    /// <summary>
    /// Controls how the status effect handles adding a new stack.
    /// </summary>
    public string StackMode { get; init; } = StackModes.None;
    /// <summary>
    /// If true, when the status effect duration times out, the stack will decrease by 1 and the duration will be
    /// refreshed. Ignored if StackMode is set to 'Active'.
    /// </summary>
    public bool ReupOnTimeout { get; init; }
    public Condition? DefaultDuration { get; private set; }
    public List<EffectOnCondition> CustomEffects { get; } = [];
    public Effect? OnActivate { get; private set; }
    public Effect? OnAddStack { get; private set; }
    public Effect? OnRemoveStack { get; private set; }
    public Effect? OnDeactivate { get; private set; }
    public List<Modifier> Modifiers { get; } = [];

    public EffectDef AddModifier(Modifier mod)
    {
        Modifiers.Add(mod);
        return this;
    }

    public EffectDef AddCustomEffect(Condition condition, Effect effect)
    {
        CustomEffects.Add(new EffectOnCondition(effect, condition));
        return this;
    }

    public EffectDef SetDefaultDuration(Condition condition)
    {
        DefaultDuration = condition;
        return this;
    }
}

public class EffectOnCondition
{
    public EffectOnCondition(EffectDef.Effect effect, Condition condition)
    {
        Effect = effect;
        Condition = condition;
    }

    public EffectDef.Effect Effect { get; set; }
    public Condition Condition { get; set; }
}

public static class StackModes
{
    /// <summary>
    /// No change will occur on stack increase.
    /// </summary>
    public const string None = "None";
    /// <summary>
    /// Status Effect conditions will be refreshed on stack added.
    /// </summary>
    public const string Reup = "Reup";
    /// <summary>
    /// TimedCondition will be added to the status effect's duration.
    /// </summary>
    public const string Extend = "Extend";
    /// <summary>
    /// All modifier stacks will be independently tracked.
    /// </summary>
    public const string Multi = "Multi";
}