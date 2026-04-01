using System;
using System.Collections.Generic;

namespace GameCore.Stats;

public delegate void EffectDel(StatSet stats, StatusEffect statusEffect);

public class EffectDef
{
    public EffectDef(string statTypeId)
    {
        EffectTypeId = statTypeId;
        CustomEffects = [];
        Modifiers = [];
    }

    public string EffectTypeId { get; }
    public int MaxStack { get; private set; } = -1;
    public Condition? DefaultDuration { get; private set; }
    public List<EffectOnCondition> CustomEffects { get; }
    public EffectDel? OnActivate { get; private set; }
    public EffectDel? OnAddStack { get; private set; }
    public EffectDel? OnRemoveStack { get; private set; }
    public EffectDel? OnDeactivate { get; private set; }
    public List<Modifier> Modifiers { get; }

    /// <summary>
    /// Adds a modifier that will be applied when this effect is active.
    /// </summary>
    /// <param name="mod">A delegate to configure the modifier to be applied.</param>
    /// <returns>The effect definition</returns>
    public EffectDef WithModifier(Action<Modifier> modDelegate)
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
    public EffectDef WithModifier(Modifier mod)
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
    public EffectDef WithCustomEffect(Condition condition, EffectDel effect)
    {
        CustomEffects.Add(new EffectOnCondition(effect, condition));
        return this;
    }

    /// <summary>
    /// Sets a default condition for stacks of this effect type if no condition is already present.
    /// </summary>
    /// <param name="condition">The Condition</param>
    /// <returns>The effect definition</returns>
    public EffectDef WithDefaultDuration(Condition condition)
    {
        DefaultDuration = condition;
        return this;
    }

    /// <summary>
    /// Sets a delegate to be called when the status effect activates.
    /// </summary>
    /// <param name="effect">The delegate</param>
    /// <returns>The effect definition</returns>
    public EffectDef WithOnActivate(EffectDel effect)
    {
        OnActivate = effect;
        return this;
    }

    /// <summary>
    /// Sets a delegate to be called when a stack is added to the status effect when it's active.
    /// </summary>
    /// <param name="effect">The delegate</param>
    /// <returns>The effect definition</returns>
    public EffectDef WithOnAddStack(EffectDel effect)
    {
        OnAddStack = effect;
        return this;
    }

    /// <summary>
    /// Sets a delegate to be called when a stack is removed from the status effect when it's active.
    /// </summary>
    /// <param name="effect">The delegate</param>
    /// <returns>The effect definition</returns>
    public EffectDef WithOnRemoveStack(EffectDel effect)
    {
        OnRemoveStack = effect;
        return this;
    }

    /// <summary>
    /// Sets a delegate to be called when the status effect deactivates.
    /// </summary>
    /// <param name="effect">The delegate</param>
    /// <returns>The effect definition</returns>
    public EffectDef WithOnDeactivate(EffectDel effect)
    {
        OnDeactivate = effect;
        return this;
    }

    /// <summary>
    /// Sets the max allowable stacks for the effect.
    /// </summary>
    /// <param name="max">The max number of stacks.</param>
    /// <returns>The effect definition</returns>
    public EffectDef WithMaxStacks(int max)
    {
        MaxStack = max;
        return this;
    }
}

public class EffectOnCondition
{
    public EffectOnCondition(EffectDel effect, Condition condition)
    {
        Effect = effect;
        Condition = condition;
    }

    public EffectDel Effect { get; set; }
    public Condition Condition { get; set; }
}
