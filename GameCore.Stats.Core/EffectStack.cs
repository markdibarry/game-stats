using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using GameCore.Pooling;

namespace GameCore.Stats;

public sealed class EffectStack : IPoolable, IConditional
{
    public string EffectTypeId { get; set; } = string.Empty;
    public int Value
    {
        get;
        set => field = Math.Max(value, 1);
    }
    public List<Condition>? CustomConditions { get; set; }
    public Condition? Duration { get; set; }
    [JsonIgnore]
    public object? Source { get; set; }
    [JsonIgnore]
    public bool IsActive { get; private set; }
    [JsonIgnore]
    public StatSet? Stats { get; private set; }
    [JsonIgnore]
    public StatusEffect? StatusEffect { get; set; }

    public static EffectStack Create() => Pool.Get<EffectStack>();

    public static EffectStack Create(Action<EffectStack> action)
    {
        EffectStack stack = Create();
        action(stack);
        return stack;
    }

    public static EffectStack Create(string effectTypeId)
    {
        EffectStack stack = Create();
        stack.EffectTypeId = effectTypeId;
        stack.Value = 1;
        return stack;
    }

    public static EffectStack Create(EffectStack stack)
    {
        EffectStack clone = Create();
        clone.EffectTypeId = stack.EffectTypeId;
        clone.Value = stack.Value;
        clone.Duration = stack.Duration?.Clone();

        if (stack.CustomConditions != null)
        {
            clone.CustomConditions = ListPool.Get<Condition>();

            foreach (Condition cond in stack.CustomConditions)
                clone.CustomConditions.Add(cond.Clone());
        }

        clone.Source = stack.Source;
        return clone;
    }

    public void ClearObject()
    {
        Uninitialize();
        EffectTypeId = string.Empty;
        Value = 1;

        if (CustomConditions != null)
            ListPool.Return(CustomConditions);

        Duration?.ReturnToPool();
        Duration = null;
    }

    void IConditional.OnConditionChanged(Condition condition)
    {
        int index = CustomConditions?.IndexOf(condition) ?? -1;

        // Handle Multi
        if (index != -1)
        {
            if (condition.CheckAllConditions())
            {
                if (Stats is null || CustomConditions is null || StatusEffect is null)
                    return;

                StatusEffect.EffectDef.CustomEffects[index].Effect.Invoke(Stats, StatusEffect);
            }

            if (condition.AutoRefresh)
                condition.Refresh();

            return;
        }

        bool isActive = true;

        if (Duration is not null)
            isActive = !Duration.CheckAllConditions(Source is not null);

        if (IsActive != isActive)
        {
            IsActive = isActive;
            StatusEffect?.OnStackChanged(this);
        }
    }

    public void Initialize(StatSet stats, StatusEffect statusEffect, object? source)
    {
        if (Stats is not null)
            return;

        Stats = stats;
        StatusEffect = statusEffect;
        Source = source;
        IsActive = true;

        if (CustomConditions != null)
        {
            foreach (Condition condition in CustomConditions)
                condition.Initialize(this, null);
        }

        if (Duration is not null)
        {
            Duration.Initialize(this, null);
            IsActive = !Duration.CheckAllConditions(Source is not null);
        }
    }

    public void Uninitialize()
    {
        if (Stats is null)
            return;

        if (CustomConditions != null)
        {
            foreach (Condition condition in CustomConditions)
                condition.Uninitialize();
        }

        Duration?.Uninitialize();
        Stats = null;
        StatusEffect = null;
        Source = null;
        IsActive = false;
    }
}