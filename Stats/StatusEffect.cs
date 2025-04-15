using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace GameCore.Statistics;

public sealed class StatusEffect : IStatsPoolable, IConditional
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
    public object? Source => null;
    public string EffectTypeId { get; set; }
    public List<Condition>? CustomConditions { get; set; }
    public List<EffectStack> Stacks { get; }
    [JsonIgnore]
    public int TotalStackCount => Stacks.Count;
    [JsonIgnore]
    public int ActiveStackCount
    {
        get => field;
        private set
        {
            int prevVal = field;

            field = value;

            if (field < 0)
                throw new Exception("ActiveStackCount should never be less than 0.");

            if (Stats == null)
                return;

            if (prevVal > 0 && field > prevVal)
                EffectDef.OnAddStack?.Invoke(Stats, this);

            Stats.RaiseEffectStackChanged(EffectTypeId);

            if (prevVal > 0 && field == 0 || prevVal == 0 && field > 0)
                UpdateActive(Stats);
        }
    }
    [JsonIgnore]
    public Stats? Stats { get; private set; }
    [JsonIgnore]
    public bool IsActive { get; private set; }
    [JsonIgnore]
    public EffectDef EffectDef { get; private set; }

    public static StatusEffect Create(string effectTypeId)
    {
        if (!EffectDefDB.TryGetValue(effectTypeId, out EffectDef? effectDef))
            throw new Exception($"No definition registered for \"{effectTypeId}\".");

        return Create(effectDef);
    }

    public static StatusEffect Create(EffectDef effectDef)
    {
        StatusEffect statusEffect = StatsPool.Get<StatusEffect>();
        statusEffect.EffectTypeId = effectDef.EffectTypeId;
        statusEffect.EffectDef = effectDef;

        if (effectDef.CustomEffects.Count > 0)
        {
            statusEffect.CustomConditions = StatsPool.GetList<Condition>();

            foreach (EffectOnCondition effect in effectDef.CustomEffects)
            {
                Condition condition = effect.Condition.Clone();
                statusEffect.CustomConditions.Add(condition);
            }
        }

        return statusEffect;
    }

    public StatusEffect Clone(bool ignoreStacksWithSource)
    {
        StatusEffect clone = StatsPool.Get<StatusEffect>();
        clone.EffectTypeId = EffectTypeId;
        clone.EffectDef = EffectDef;

        if (CustomConditions is not null)
        {
            clone.CustomConditions = StatsPool.GetList<Condition>();

            foreach (Condition condition in CustomConditions)
            {
                Condition cloneCondition = condition.Clone();
                clone.CustomConditions.Add(cloneCondition);
            }
        }

        foreach (EffectStack stack in Stacks)
        {
            if (stack.Source != null && ignoreStacksWithSource)
                continue;

            clone.Stacks.Add(stack.Clone());
        }

        return clone;
    }

    public void ClearObject()
    {
        Uninitialize();

        if (CustomConditions is not null)
            StatsPool.Return(CustomConditions);

        CustomConditions = null;

        foreach (EffectStack stack in Stacks)
            stack.ReturnToPool();

        Stacks.Clear();
        Stats = null;
        EffectTypeId = string.Empty;
        EffectDef = null!;
    }

    public bool HasActiveStacks()
    {
        return ActiveStackCount > 0;
    }

    internal void Initialize(Stats stats, object? source, bool isImmune)
    {
        if (Stats is not null)
            return;

        Stats = stats;

        foreach (var stack in Stacks)
        {
            stack.Initialize(stats, this, source);

            if (stack.IsActive)
                ActiveStackCount++;
        }

        IsActive = !isImmune && HasActiveStacks();

        if (IsActive && CustomConditions is not null)
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

    public void OnConditionChanged(Condition condition)
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

        if (condition.ReupOnMet)
            condition.Reup();
    }

    internal void OnStackChanged(EffectStack stack)
    {
        int activeStacks = ActiveStackCount;

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

        ActiveStackCount = activeStacks;
    }

    internal void AddStackUnsafe(EffectStack stack)
    {
        Stacks.Add(stack);
    }

    internal void AddStack(Stats stats, EffectStack newStack, object? source)
    {
        int stacksToAdd = HandleNewStack(stats, newStack, source, ActiveStackCount);
        ActiveStackCount += stacksToAdd;
    }

    private int HandleNewStack(Stats stats, EffectStack newStack, object? source, int activeStacks)
    {
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

        if (EffectDef.StackMode == StackModes.Multi)
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

        if (EffectDef.StackMode == StackModes.Reup)
        {
            foreach (var stack in Stacks)
                stack.Duration?.ReupAllData();
        }

        EffectStack existingStack = Stacks.Last();

        if (existingStack.Source != null && !IsActive)
        {
            newStack.Value = Math.Min(newStack.Value, EffectDef.MaxStack - activeStacks);
            return AddStackInternal(stats, newStack, source, false);
        }

        if (EffectDef.StackMode == StackModes.Extend)
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

        int AddStackInternal(Stats stats, EffectStack stack, object? source, bool addToFront)
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
                effectTime.TimeLeft += stackTime.TimeLeft;
                return;
            }
        }
    }

    internal void RemoveStacksBySource(object? source)
    {
        int activeStacksRemoved = RemoveStacksBySourceInternal(source);
        ActiveStackCount -= activeStacksRemoved;
    }

    internal void RemoveStackByRef(EffectStack stack)
    {
        int activeStacksRemoved = RemoveStackByRefInternal(stack);
        ActiveStackCount -= activeStacksRemoved;
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
        Stats stats,
        object oldSource,
        EffectStack newStack,
        object? newSource)
    {
        int activeStacks = ActiveStackCount;
        int activeStacksRemoved = RemoveStacksBySourceInternal(oldSource);
        int activeStacksAdded = HandleNewStack(stats, newStack, newSource, activeStacks);

        activeStacks += activeStacksAdded - activeStacksRemoved;
        ActiveStackCount = activeStacks;
    }

    internal void UpdateActive(Stats stats)
    {
        string effectTypeId = EffectTypeId;
        bool wasActive = IsActive;
        bool isImmune = stats.IsImmuneToStatusEffect(effectTypeId);
        IsActive = !isImmune && HasActiveStacks();

        if (!wasActive && IsActive)
            Activate(stats, effectTypeId);
        else if (wasActive && !IsActive)
            Deactivate(stats, effectTypeId);
        else if (TotalStackCount == 0)
            stats.RemoveStatusEffect(effectTypeId);

        void Activate(Stats stats, string effectTypeId)
        {
            EffectDef.OnActivate?.Invoke(stats, this);

            if (CustomConditions is not null)
            {
                foreach (Condition condition in CustomConditions)
                    condition.Initialize(this, null);
            }

            stats.RaiseStatusEffectChanged(effectTypeId);
        }

        void Deactivate(Stats stats, string effectTypeId)
        {
            EffectDef.OnDeactivate?.Invoke(stats, this);

            if (CustomConditions is not null)
            {
                foreach (Condition condition in CustomConditions)
                    condition.Uninitialize();
            }

            int stacksRemoved = RemoveStacksBySourceInternal(null);

            if (stacksRemoved > 0)
                ActiveStackCount -= stacksRemoved;

            if (TotalStackCount == 0)
                stats.RemoveStatusEffect(effectTypeId);

            stats.RaiseStatusEffectChanged(effectTypeId);
        }
    }
}
