using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using GameCore.Pooling;

namespace GameCore.Stats;

public sealed class StatusEffect : IPoolable, IConditional
{
    public StatusEffect()
    {
        EffectTypeId = string.Empty;
        Stacks = [];
        EffectDef = null!;
    }

    [JsonConstructor]
    public StatusEffect(
        string effectTypeId,
        List<Condition>? customConditions,
        List<EffectStack> stacks)
    {
        EffectTypeId = effectTypeId;
        CustomConditions = customConditions;
        Stacks = stacks;

        if (!EffectDefDB.TryGetValue(EffectTypeId, out EffectDef? effectDef))
            throw new Exception($"No definition registered for \"{EffectTypeId}\".");

        EffectDef = effectDef;
    }

    [JsonIgnore]
    object? IConditional.Source => null;
    public string EffectTypeId { get; set; }
    public List<Condition>? CustomConditions { get; set; }
    public List<EffectStack> Stacks { get; }
    [JsonIgnore]
    public int TotalStackCount => Stacks.Count;
    [JsonIgnore]
    public int TotalActiveStackCount { get; private set; }
    [JsonIgnore]
    public StatSet? Stats { get; private set; }
    [JsonIgnore]
    public bool IsActive { get; private set; }
    [JsonIgnore]
    public EffectDef EffectDef { get; private set; }

    internal static StatusEffect Create(string effectTypeId)
    {
        if (!EffectDefDB.TryGetValue(effectTypeId, out EffectDef? effectDef))
            throw new Exception($"No definition registered for \"{effectTypeId}\".");

        return Create(effectDef);
    }

    internal static StatusEffect Create(EffectDef effectDef)
    {
        StatusEffect statusEffect = Pool.Get<StatusEffect>();
        statusEffect.EffectTypeId = effectDef.EffectTypeId;
        statusEffect.EffectDef = effectDef;

        if (effectDef.CustomEffects.Count > 0)
        {
            statusEffect.CustomConditions = ListPool.Get<Condition>();

            foreach (EffectOnCondition effect in effectDef.CustomEffects)
            {
                Condition condition = effect.Condition.Clone();
                statusEffect.CustomConditions.Add(condition);
            }
        }

        return statusEffect;
    }

    internal static StatusEffect Create(StatusEffect statusEffect, bool ignoreStacksWithSource)
    {
        StatusEffect clone = Pool.Get<StatusEffect>();
        clone.EffectTypeId = statusEffect.EffectTypeId;
        clone.EffectDef = statusEffect.EffectDef;

        if (statusEffect.CustomConditions is not null)
        {
            clone.CustomConditions = ListPool.Get<Condition>();

            foreach (Condition condition in statusEffect.CustomConditions)
            {
                Condition cloneCondition = condition.Clone();
                clone.CustomConditions.Add(cloneCondition);
            }
        }

        foreach (EffectStack stack in statusEffect.Stacks)
        {
            if (stack.Source != null && ignoreStacksWithSource)
                continue;

            clone.Stacks.Add(EffectStack.Create(stack));
        }

        return clone;
    }

    public void ClearObject()
    {
        Uninitialize();

        if (CustomConditions is not null)
            ListPool.Return(CustomConditions);

        CustomConditions = null;

        foreach (EffectStack stack in Stacks)
            stack.ReturnToPool();

        Stacks.Clear();
        Stats = null;
        EffectTypeId = string.Empty;
        EffectDef = null!;
    }

    internal void Initialize(StatSet stats, object? source, bool isImmune)
    {
        if (Stats is not null)
            return;

        Stats = stats;
        bool hasNonMultiStack = false;

        foreach (EffectStack stack in Stacks)
        {
            stack.Initialize(stats, this, source);

            if (stack.IsActive)
                UpdateActiveStackCount(TotalActiveStackCount + 1);

            if (stack.CustomConditions == null)
                hasNonMultiStack = true;
        }

        IsActive = !isImmune && TotalActiveStackCount > 0;

        if (IsActive && CustomConditions is not null && hasNonMultiStack)
        {
            foreach (Condition condition in CustomConditions)
                condition.Initialize(this, null);
        }
    }

    internal void Uninitialize()
    {
        if (Stats is null)
            return;

        if (IsActive && CustomConditions is not null)
        {
            foreach (Condition condition in CustomConditions)
                condition.Uninitialize();
        }

        foreach (EffectStack stack in Stacks)
            stack.Uninitialize();

        Stats = null;
    }

    void IConditional.OnConditionChanged(Condition condition)
    {
        int index = CustomConditions?.IndexOf(condition) ?? -1;

        if (index == -1)
            return;

        if (condition.CheckAllConditions())
        {
            if (Stats is null || CustomConditions is null)
                return;

            EffectDef.CustomEffects[index].Effect.Invoke(Stats, this);
        }

        if (condition.AutoRefresh)
            condition.Refresh();
    }

    internal void OnStackChanged(EffectStack stack)
    {
        int activeStacks = TotalActiveStackCount;

        if (stack.IsActive)
        {
            activeStacks += stack.Value;
        }
        else
        {
            activeStacks -= stack.Value;

            if (stack.Source is null)
            {
                Stacks.Remove(stack);
                stack.ReturnToPool();
            }
        }

        UpdateActiveStackCount(activeStacks);
    }

    internal void AddStackUnsafe(EffectStack stack)
    {
        Stacks.Add(stack);
    }

    internal void AddStack(StatSet stats, EffectStack newStack, StackMode stackMode, object? source)
    {
        int stacksToAdd = HandleNewStack(stats, newStack, stackMode, source, TotalActiveStackCount);
        UpdateActiveStackCount(TotalActiveStackCount + stacksToAdd);
    }

    private int HandleNewStack(StatSet stats, EffectStack newStack, StackMode stackMode, object? source, int activeStacks)
    {
        if (stackMode == StackMode.MultiFull && EffectDef.CustomEffects.Count > 0)
        {
            newStack.CustomConditions = ListPool.Get<Condition>();

            foreach (EffectOnCondition effect in EffectDef.CustomEffects)
            {
                Condition condition = effect.Condition.Clone();
                newStack.CustomConditions.Add(condition);
            }
        }

        // If new stack has a source, brute force it
        if (source != null)
            return AddStackInternal(stats, newStack, source, true);

        // If first non-sourced stack
        if (TotalStackCount == 0)
        {
            if (EffectDef.MaxStack > 0)
                newStack.Value = Math.Min(newStack.Value, EffectDef.MaxStack);

            return AddStackInternal(stats, newStack, source, false);
        }

        if (stackMode is StackMode.MultiDuration or StackMode.MultiFull)
        {
            if (EffectDef.MaxStack > 0)
            {
                if (activeStacks >= EffectDef.MaxStack)
                {
                    newStack.ReturnToPool();
                    return 0;
                }

                newStack.Value = Math.Min(newStack.Value, EffectDef.MaxStack - activeStacks);
            }

            return AddStackInternal(stats, newStack, source, false);
        }

        if (stackMode == StackMode.Refresh)
        {
            foreach (var stack in Stacks)
                stack.Duration?.RefreshAllData();
        }

        EffectStack existingStack = Stacks.Last();

        if (existingStack.Source != null && !IsActive)
        {
            newStack.Value = Math.Min(newStack.Value, EffectDef.MaxStack - activeStacks);
            return AddStackInternal(stats, newStack, source, false);
        }

        if (stackMode == StackMode.Extend)
            Extend(existingStack, newStack);

        int activeStacksAdded = 0;

        if (existingStack.Source == null)
        {
            if (EffectDef.MaxStack > 0)
            {
                if (activeStacks >= EffectDef.MaxStack)
                {
                    newStack.ReturnToPool();
                    return 0;
                }

                newStack.Value = Math.Min(newStack.Value, EffectDef.MaxStack - activeStacks);
            }

            existingStack.Value += newStack.Value;
            activeStacksAdded = newStack.Value;
        }

        newStack.ReturnToPool();
        return activeStacksAdded;

        int AddStackInternal(StatSet stats, EffectStack stack, object? source, bool addToFront)
        {
            stack.Initialize(stats, this, source);

            if (addToFront)
                Stacks.Insert(0, stack);
            else
                Stacks.Add(stack);

            return stack.IsActive ? stack.Value : 0;
        }

        static void Extend(EffectStack existingStack, EffectStack newStack)
        {
            // See if the new stack has a time condition
            if (newStack.Duration?.GetFirstCondition<TimedCondition>() is not TimedCondition stackTime)
                return;

            // If existing stack has a time condition, extend it.
            if (existingStack.Duration?.GetFirstCondition<TimedCondition>() is TimedCondition effectTime)
            {
                var timeLeft = effectTime.State.TimeLeft + stackTime.State.TimeLeft;
                effectTime.State = effectTime.State with { TimeLeft = timeLeft };
                return;
            }
        }
    }

    internal void RemoveStacksBySource(object? source)
    {
        int activeStacksRemoved = RemoveStacksBySourceInternal(source);
        UpdateActiveStackCount(TotalActiveStackCount - activeStacksRemoved);
    }

    internal void RemoveStackByRef(EffectStack stack)
    {
        int activeStacksRemoved = RemoveStackByRefInternal(stack);
        UpdateActiveStackCount(TotalActiveStackCount - activeStacksRemoved);
    }

    private int RemoveStackByRefInternal(EffectStack stack)
    {
        int activeStacksRemoved = stack.IsActive ? stack.Value : 0;
        Stacks.Remove(stack);
        stack.ReturnToPool();

        return activeStacksRemoved;
    }

    private int RemoveStacksBySourceInternal(object? source)
    {
        int activeStacksRemoved = 0;

        for (int i = Stacks.Count - 1; i >= 0; i--)
        {
            EffectStack stack = Stacks[i];

            if (stack.Source == source)
            {
                activeStacksRemoved += stack.IsActive ? stack.Value : 0;
                Stacks.RemoveAt(i);
                stack.ReturnToPool();
            }
        }

        return activeStacksRemoved;
    }

    internal void ReplaceStackBySource(
        StatSet stats,
        EffectStack newStack,
        StackMode stackMode,
        object oldSource,
        object? newSource)
    {
        int activeStacks = TotalActiveStackCount;
        int activeStacksRemoved = RemoveStacksBySourceInternal(oldSource);
        int activeStacksAdded = HandleNewStack(stats, newStack, stackMode, newSource, activeStacks);

        activeStacks += activeStacksAdded - activeStacksRemoved;
        UpdateActiveStackCount(activeStacks);
    }

    internal void UpdateActive(StatSet stats)
    {
        string effectTypeId = EffectTypeId;
        bool wasActive = IsActive;
        bool isImmune = stats.IsImmuneToStatusEffect(effectTypeId);
        IsActive = !isImmune && TotalActiveStackCount > 0;

        if (!wasActive && IsActive)
        {
            Activate(stats, effectTypeId);
        }
        else if (wasActive && !IsActive)
        {
            Deactivate(stats, effectTypeId);
        }
        else if (TotalStackCount == 0)
        {
            stats.RemoveStatusEffect(effectTypeId);
        }
        else if (IsActive && CustomConditions is not null)
        {
            if (HasActiveNonMultiStacks())
            {
                foreach (Condition condition in CustomConditions)
                    condition.Initialize(this, null);
            }
            else
            {
                foreach (Condition condition in CustomConditions)
                    condition.Uninitialize();
            }
        }

        void Activate(StatSet stats, string effectTypeId)
        {
            EffectDef.OnActivate?.Invoke(stats, this);

            if (CustomConditions is not null && HasActiveNonMultiStacks())
            {
                foreach (Condition condition in CustomConditions)
                    condition.Initialize(this, null);
            }

            stats.RaiseStatusEffectChanged(effectTypeId);
        }

        void Deactivate(StatSet stats, string effectTypeId)
        {
            EffectDef.OnDeactivate?.Invoke(stats, this);

            if (CustomConditions is not null)
            {
                foreach (Condition condition in CustomConditions)
                    condition.Uninitialize();
            }

            int stacksRemoved = RemoveStacksBySourceInternal(null);

            if (stacksRemoved > 0)
                UpdateActiveStackCount(TotalActiveStackCount - stacksRemoved);

            if (TotalStackCount == 0)
                stats.RemoveStatusEffect(effectTypeId);

            stats.RaiseStatusEffectChanged(effectTypeId);
        }
    }

    private bool HasActiveNonMultiStacks()
    {
        foreach (EffectStack stack in Stacks)
        {
            if (stack.IsActive && stack.CustomConditions == null)
                return true;
        }

        return false;
    }

    private void UpdateActiveStackCount(int value)
    {
        int prevVal = TotalActiveStackCount;

        if (value < 0)
            throw new Exception("ActiveStackCount should never be less than 0.");

        if (Stats == null)
            return;

        TotalActiveStackCount = value;

        if (prevVal > 0 && value > prevVal)
            EffectDef.OnAddStack?.Invoke(Stats, this);

        Stats.RaiseEffectStackChanged(EffectTypeId);

        //if (prevVal > 0 && value == 0 || prevVal == 0 && value > 0)
        UpdateActive(Stats);
    }
}
