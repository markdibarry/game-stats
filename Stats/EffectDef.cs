using System;
using System.Collections.Generic;

namespace GameCore.Statistics;

public class EffectDef
{
    public EffectDef(string statTypeId)
    {
        EffectTypeId = statTypeId;
    }

    public delegate void Effect(Stats stats, StatusEffect statusEffect);
    public string EffectTypeId { get; }
    public int MaxStack { get; init; } = -1;
    /// <summary>
    /// Controls how the status effect handles adding a new stack.
    /// </summary>
    public string StackMode { get; private set; } = StackModes.Reup;
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

    /// <summary>
    /// Adds a modifier that will be applied when this effect is active.
    /// </summary>
    /// <param name="mod">A delegate to configure the modifier to be applied.</param>
    /// <returns>The effect definition</returns>
    public EffectDef AddModifier(Action<Modifier> modDelegate)
    {
        Modifier mod = Modifier.Create();
        modDelegate(mod);
        Modifiers.Add(mod);
        return this;
    }

    /// <summary>
    /// Adds a modifier that will be applied when this effect is active.
    /// </summary>
    /// <param name="mod">The modifier to be applied</param>
    /// <returns>The effect definition</returns>
    public EffectDef AddModifier(Modifier mod)
    {
        Modifiers.Add(mod);
        return this;
    }

    /// <summary>
    /// Adds a custom effect delegate that will be called when the provided Condition is met.
    /// </summary>
    /// <param name="condition">The Condition</param>
    /// <param name="effect">The effect delegate</param>
    /// <returns>The effect definition</returns>
    public EffectDef AddCustomEffect(Condition condition, Effect effect)
    {
        CustomEffects.Add(new EffectOnCondition(effect, condition));
        return this;
    }

    /// <summary>
    /// Sets a default condition for stacks of this effect type if no condition is already present.
    /// </summary>
    /// <param name="condition">The Condition</param>
    /// <returns>The effect definition</returns>
    public EffectDef SetDefaultDuration(Condition condition)
    {
        DefaultDuration = condition;
        return this;
    }

    /// <summary>
    /// Sets a delegate to be called when the status effect activates.
    /// </summary>
    /// <param name="effect">The delegate</param>
    /// <returns>The effect definition</returns>
    public EffectDef SetOnActivate(Effect effect)
    {
        OnActivate = effect;
        return this;
    }

    /// <summary>
    /// Sets a delegate to be called when a stack is added to the status effect when it's active.
    /// </summary>
    /// <param name="effect">The delegate</param>
    /// <returns>The effect definition</returns>
    public EffectDef SetOnAddStack(Effect effect)
    {
        OnAddStack = effect;
        return this;
    }

    /// <summary>
    /// Sets a delegate to be called when a stack is removed from the status effect when it's active.
    /// </summary>
    /// <param name="effect">The delegate</param>
    /// <returns>The effect definition</returns>
    public EffectDef SetOnRemoveStack(Effect effect)
    {
        OnRemoveStack = effect;
        return this;
    }

    /// <summary>
    /// Sets a delegate to be called when the status effect deactivates.
    /// </summary>
    /// <param name="effect">The delegate</param>
    /// <returns>The effect definition</returns>
    public EffectDef SetOnDeactivate(Effect effect)
    {
        OnDeactivate = effect;
        return this;
    }

    /// <summary>
    /// Sets the stack mode of the status effect.
    /// </summary>
    /// <remarks>
    /// For a list of valid values and their definitions see the static class <see cref="StackModes"/>.
    /// </remarks>
    /// <param name="stackMode"></param>
    /// <returns></returns>
    public EffectDef SetStackMode(string stackMode)
    {
        StackMode = stackMode;
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
    /// Additional stacks will have their value added to the first existing stack without a source.
    /// </summary>
    public const string None = "None";
    /// <summary>
    /// Additional stacks will refresh the conditions of the first existing stack without a source.
    /// </summary>
    public const string Reup = "Reup";
    /// <summary>
    /// Additional stacks will extend the TimedCondition of the first existing stack without a source.
    /// </summary>
    public const string Extend = "Extend";
    /// <summary>
    /// All effect stacks will be independently tracked.
    /// </summary>
    public const string Multi = "Multi";
}