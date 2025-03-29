using System;
using System.Text.Json.Serialization;
using GameCore.Utility;

namespace GameCore.Statistics;

public class EffectStack : IPoolable, IConditional
{
    [JsonPropertyOrder(0)]
    public string EffectTypeId { get; set; } = string.Empty;
    [JsonPropertyOrder(1)]
    public float Value { get; set; } = 1;
    [JsonPropertyOrder(2)]
    public Condition? Duration { get; set; }
    [JsonIgnore]
    public object? Source { get; set; }
    [JsonIgnore]
    public bool IsActive { get; private set; }
    [JsonIgnore]
    public Stats? Stats { get; private set; }

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

    public void ClearObject()
    {
        EffectTypeId = string.Empty;
        Value = 1;
        Duration?.ReturnToPool();
        Duration = null;
        Source = null;
    }

    public EffectStack Clone()
    {
        EffectStack clone = Create();
        clone.EffectTypeId = EffectTypeId;
        clone.Value = Value;
        clone.Duration = Duration?.Clone();
        clone.Source = Source;
        return clone;
    }

    public void OnConditionChanged(Condition condition)
    {
        bool isActive = true;

        if (Duration is not null)
            isActive = !Duration.CheckAllConditions(Source is not null);

        if (IsActive != isActive)
        {
            IsActive = isActive;

            if (!IsActive && Source is null)
                Stats?.RemoveStackByRef(this);
        }
    }

    public void Initialize(Stats stats, object? source)
    {
        if (Stats is not null)
            return;

        Stats = stats;
        Source = source;
        IsActive = true;

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

        Duration?.Uninitialize();
        Stats = null;
        Source = null;
        IsActive = false;
    }
}