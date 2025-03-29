using System.Collections.Generic;
using GameCore.Utility;

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
            statusEffect.Initialize(stats);
    }

    internal void AddStack(Stats stats, EffectStack sourceStack, object? source, bool clone)
    {
        if (!EffectDefDB.TryGetValue(sourceStack.EffectTypeId, out EffectDef? effectDef))
            return;

        EffectStack stack = clone ? sourceStack.Clone() : sourceStack;

        // Copy global duration if available
        if (stack.Duration is null && effectDef.DefaultDuration is not null)
            stack.Duration = effectDef.DefaultDuration.Clone();

        // Must register to check if active
        stack.Initialize(stats, source);

        if (stack.Source is null && (!stack.IsActive || stats.IsImmuneToStatusEffect(stack.EffectTypeId)))
        {
            stack.Uninitialize();
            stack.ReturnToPool();
        }

        if (TryGetValue(stack.EffectTypeId, out StatusEffect? statusEffect))
            UpdateStatusEffect(stats, statusEffect, stack, effectDef);
        else
            AddStatusEffect(stats, stack, effectDef);
    }

    private void AddStatusEffect(Stats stats, EffectStack stack, EffectDef effectDef)
    {
        StatusEffect statusEffect = StatusEffect.Create(effectDef);
        statusEffect.Stacks.Add(stack);
        Add(stack.EffectTypeId, statusEffect);
        statusEffect.Initialize(stats);
        UpdateActive(stats, statusEffect, effectDef);
    }

    private void UpdateStatusEffect(
        Stats stats,
        StatusEffect statusEffect,
        EffectStack newStack,
        EffectDef effectDef)
    {
        // There may be an inactive stack with a source
        if (!statusEffect.IsActive)
        {
            statusEffect.Stacks.Add(newStack);
            UpdateActive(stats, statusEffect, effectDef);
            return;
        }

        if (effectDef.StackMode == StackModes.Multi
            || newStack.Source is not null
            || GetFirstStack(statusEffect) is not EffectStack existingStack)
        {
            statusEffect.Stacks.Add(newStack);
            effectDef.OnAddStack?.Invoke(stats, statusEffect);
            return;
        }

        // TODO: Replace?
        // if (newStack.Op == OpDB.Replace && newStack.Duration is Condition condition)
        // {
        //     condition.Unregister();
        //     newStack.Duration = null;
        //     existingStack.Duration?.Unregister();
        //     existingStack.Duration = condition;
        //     condition.Register(existingStack, null);
        // }

        if (effectDef.StackMode == StackModes.Reup)
            existingStack.Duration?.ReupAllData();
        else if (effectDef.StackMode == StackModes.Extend)
            Extend(existingStack, newStack);

        // TODO: Max stack
        existingStack.Value += newStack.Value;

        static void Extend(EffectStack existingStack, EffectStack newStack)
        {
            // See if the new stack has a time condition
            if (newStack.Duration?.GetFirstCondition<TimedCondition>() is not TimedCondition stackTime)
                return;

            // If existing stack has a time condition, extend it.
            if (existingStack.Duration?.GetFirstCondition<TimedCondition>() is TimedCondition effectTime)
            {
                effectTime.TimeLeft += stackTime.TimeLeft;
                return;
            }

            // If not, clone the stack's timed condition and apply it to the effect.
            Condition clonedTime = stackTime.CloneSingle();
            clonedTime.Initialize(existingStack, null);

            if (existingStack.Duration is not null)
                clonedTime.SetOr(existingStack.Duration);

            existingStack.Duration = clonedTime;
        }

        static EffectStack? GetFirstStack(StatusEffect statusEffect)
        {
            foreach (EffectStack stack in statusEffect.Stacks)
            {
                if (stack.Source is not null)
                    return stack;
            }

            return null;
        }
    }

    internal void RemoveStacksBySource(Stats stats, string effectTypeId, object? source)
    {
        if (!TryGetValue(effectTypeId, out StatusEffect? statusEffect)
            || !EffectDefDB.TryGetValue(statusEffect.EffectTypeId, out var effectDef))
            return;

        statusEffect.RemoveStacksBySource(source);
        UpdateActive(stats, statusEffect, effectDef);
    }

    internal void RemoveStack(Stats stats, EffectStack stack)
    {
        if (!TryGetValue(stack.EffectTypeId, out StatusEffect? statusEffect)
            || !EffectDefDB.TryGetValue(statusEffect.EffectTypeId, out var effectDef))
            return;

        statusEffect.RemoveStack(stack);
        UpdateActive(stats, statusEffect, effectDef);
    }

    private void RemoveStatusEffect(StatusEffect statusEffect)
    {
        string effectTypeId = statusEffect.EffectTypeId;
        statusEffect.Uninitialize();
        statusEffect.ReturnToPool();
        Remove(effectTypeId);
    }

    internal void UpdateActive(Stats stats, StatusEffect statusEffect, EffectDef effectDef)
    {
        bool wasActive = statusEffect.IsActive;
        bool isImmune = stats.IsImmuneToStatusEffect(statusEffect.EffectTypeId);
        statusEffect.IsActive = !isImmune && statusEffect.HasActiveStacks();

        if (!wasActive && statusEffect.IsActive)
        {
            effectDef.OnActivate?.Invoke(stats, statusEffect);
            stats.RaiseStatusEffectChanged(statusEffect.EffectTypeId);
        }
        else if (wasActive && !statusEffect.IsActive)
        {
            effectDef.OnDeactivate?.Invoke(stats, statusEffect);
            stats.RaiseStatusEffectChanged(statusEffect.EffectTypeId);
        }

        if (statusEffect.Stacks.Count == 0)
            RemoveStatusEffect(statusEffect);
    }
}