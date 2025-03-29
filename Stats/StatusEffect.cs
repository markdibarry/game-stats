using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using GameCore.Utility;

namespace GameCore.Statistics;

public sealed class StatusEffect : IPoolable, IConditional
{
    public string EffectTypeId { get; set; } = string.Empty;
    public List<Condition>? CustomConditions { get; set; }
    public List<EffectStack> Stacks { get; set; } = [];
    [JsonIgnore]
    public Stats? Stats { get; private set; }
    [JsonIgnore]
    public bool IsActive { get; internal set; }

    public static StatusEffect Create(string effectTypeId)
    {
        if (!EffectDefDB.TryGetValue(effectTypeId, out EffectDef? effectDef))
            throw new System.Exception($"EffectTypeId \"{effectTypeId}\" not found.");

        return Create(effectDef);
    }

    public static StatusEffect Create(EffectDef effectDef)
    {
        StatusEffect statusEffect = Pool.Get<StatusEffect>();
        statusEffect.EffectTypeId = effectDef.EffectTypeId;

        if (effectDef.CustomEffects.Count > 0)
        {
            statusEffect.CustomConditions = Pool.GetList<Condition>();

            foreach (EffectOnCondition effect in effectDef.CustomEffects)
            {
                Condition condition = effect.Condition.Clone();
                statusEffect.CustomConditions.Add(condition);
            }
        }

        return statusEffect;
    }

    public StatusEffect Clone()
    {
        StatusEffect clone = Pool.Get<StatusEffect>();
        clone.EffectTypeId = EffectTypeId;

        if (CustomConditions is not null)
        {
            clone.CustomConditions = Pool.GetList<Condition>();

            foreach (Condition condition in CustomConditions)
            {
                Condition cloneCondition = condition.Clone();
                clone.CustomConditions.Add(cloneCondition);
            }
        }

        foreach (EffectStack stack in Stacks)
            clone.Stacks.Add(stack.Clone());

        return clone;
    }

    public void ClearObject()
    {
        Uninitialize();

        if (CustomConditions is not null)
            Pool.Return(CustomConditions);

        CustomConditions = null;

        foreach (EffectStack stack in Stacks)
            stack.ReturnToPool();

        Stacks.Clear();
        Stats = null;
        EffectTypeId = string.Empty;
    }

    public int GetStackCount()
    {
        int count = 0;

        for (int i = 0; i < Stacks.Count; i++)
        {
            EffectStack stack = Stacks[i];

            if (stack.IsActive)
                count += (int)stack.Value;
        }

        return count;
    }

    public bool HasActiveStacks()
    {
        return Stacks.Any(x => x.IsActive && x.Value > 0);
    }

    public void OnConditionChanged(Condition condition)
    {
        if (TryInvokeCustom(condition))
            return;
    }

    internal void Initialize(Stats stats)
    {
        if (Stats is not null)
            return;

        Stats = stats;

        if (CustomConditions is not null)
        {
            foreach (Condition condition in CustomConditions)
                condition.Initialize(this, null);
        }
    }

    internal void Uninitialize()
    {
        if (Stats is null)
            return;

        if (CustomConditions is not null)
        {
            foreach (Condition condition in CustomConditions)
                condition.Uninitialize();
        }

        foreach (EffectStack stack in Stacks)
            stack.Uninitialize();

        Stats = null;
    }

    private bool TryInvokeCustom(Condition condition)
    {
        int index = CustomConditions?.IndexOf(condition) ?? -1;

        if (index == -1)
            return false;

        if (condition.CheckAllConditions())
        {
            if (!EffectDefDB.TryGetValue(EffectTypeId, out var effectDef))
                return false;

            if (Stats is null || CustomConditions is null)
                return false;

            effectDef.CustomEffects[index].Effect.Invoke(Stats, this);
        }

        if (condition.ReupOnMet)
            condition.Reup();

        return true;
    }

    internal void RemoveStack(EffectStack stack)
    {
        if (Stacks.Remove(stack))
        {
            stack.Uninitialize();
            stack.ReturnToPool();
        }
    }

    internal void RemoveStacksBySource(object? source)
    {
        for (int i = Stacks.Count - 1; i >= 0; i--)
        {
            EffectStack stack = Stacks[i];

            if (stack.Source == source)
            {
                Stacks.RemoveAt(i);
                stack.Uninitialize();
                stack.ReturnToPool();
            }
        }
    }
}
