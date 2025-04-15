using System.Collections.Generic;

namespace GameCore.Statistics;

public class EffectLookup : Dictionary<string, StatusEffect>
{
    public void AddEffects(List<StatusEffect> effectsToAdd)
    {
        foreach (StatusEffect effect in effectsToAdd)
            Add(effect.EffectTypeId, effect.Clone());
    }

    public void ClearObject()
    {
        foreach (var value in Values)
            value.ReturnToPool();

        Clear();
    }

    public void CopyTo(EffectLookup statusEffects)
    {
        statusEffects.Clear();

        foreach (StatusEffect statusEffect in Values)
            statusEffects.Add(statusEffect.EffectTypeId, statusEffect.Clone());
    }

    public bool IsActive(string effectTypeId)
    {
        if (!TryGetValue(effectTypeId, out StatusEffect? statusEffect))
            return false;

        return statusEffect.IsActive;
    }

    internal void InitializeAll(Stats stats)
    {
        foreach (StatusEffect statusEffect in Values)
        {
            bool isImmune = stats.IsImmuneToStatusEffect(statusEffect.EffectTypeId);
            statusEffect.Initialize(stats, null, isImmune);
        }
    }

    internal void AddStack(Stats stats, EffectStack stack, object? source)
    {
        if (!EffectDefDB.TryGetValue(stack.EffectTypeId, out EffectDef? effectDef))
            return;

        // Copy global duration if available
        if (stack.Duration is null && effectDef.DefaultDuration is not null)
            stack.Duration = effectDef.DefaultDuration.Clone();

        bool isActive = !stack.Duration?.EvaluateAllConditions(stats, false) ?? true;
        bool isImmune = stats.IsImmuneToStatusEffect(stack.EffectTypeId);
        bool shouldAddNew = isActive && !isImmune;

        if (source is null && !shouldAddNew)
        {
            stack.ReturnToPool();
            return;
        }

        if (!TryGetValue(stack.EffectTypeId, out StatusEffect? statusEffect))
        {
            statusEffect = StatusEffect.Create(effectDef);
            statusEffect.Initialize(stats, source, isImmune);
            Add(stack.EffectTypeId, statusEffect);
        }

        statusEffect.AddStack(stats, stack, source);
    }

    internal void ReplaceStack(Stats stats, object oldSource, EffectStack newStack, object? newSource)
    {
        if (!EffectDefDB.TryGetValue(newStack.EffectTypeId, out EffectDef? effectDef))
            return;

        // Copy global duration if available
        if (newStack.Duration is null && effectDef.DefaultDuration is not null)
            newStack.Duration = effectDef.DefaultDuration.Clone();

        bool isActive = !newStack.Duration?.EvaluateAllConditions(stats, false) ?? true;
        bool isImmune = stats.IsImmuneToStatusEffect(newStack.EffectTypeId);
        bool shouldAddNew = isActive && !isImmune;

        if (TryGetValue(newStack.EffectTypeId, out StatusEffect? statusEffect))
        {
            if (shouldAddNew)
                statusEffect.ReplaceStackBySource(stats, oldSource, newStack, newSource);
            else
                statusEffect.RemoveStacksBySource(oldSource);
        }
        else
        {
            if (shouldAddNew)
            {
                statusEffect = StatusEffect.Create(effectDef);
                statusEffect.Initialize(stats, newSource, isImmune);
                Add(newStack.EffectTypeId, statusEffect);
                statusEffect.AddStack(stats, newStack, newSource);
            }
        }

        if (!shouldAddNew)
            newStack.ReturnToPool();
    }

    internal void RemoveStacksBySource(string effectTypeId, object? source)
    {
        if (!TryGetValue(effectTypeId, out StatusEffect? statusEffect))
            return;

        if (statusEffect.TotalStackCount == 0)
            RemoveStatusEffect(statusEffect);
        else
            statusEffect.RemoveStacksBySource(source);
    }

    internal void RemoveStackByRef(EffectStack stack)
    {
        if (!TryGetValue(stack.EffectTypeId, out StatusEffect? statusEffect))
            return;

        statusEffect.RemoveStackByRef(stack);
    }

    private void RemoveStatusEffect(StatusEffect statusEffect)
    {
        string effectTypeId = statusEffect.EffectTypeId;
        statusEffect.ReturnToPool();
        Remove(effectTypeId);
    }
}