using System;
using System.Text.Json.Serialization;

namespace GameCore.Statistics;

public class EffectStack : IStatsPoolable, IConditional
{
    [JsonPropertyOrder(0)]
    public string EffectTypeId { get; set; } = string.Empty;
    [JsonPropertyOrder(1)]
    public int Value
    {
        get => field;
        set => field = Math.Max(value, 1);
    }
    [JsonPropertyOrder(2)]
    public Condition? Duration { get; set; }
    [JsonIgnore]
    public object? Source { get; set; }
    [JsonIgnore]
    public bool IsActive { get; private set; }
    [JsonIgnore]
    public Stats? Stats { get; private set; }
    [JsonIgnore]
    public StatusEffect? StatusEffect { get; set; }

    public static EffectStack Create() => StatsPool.Get<EffectStack>();

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
        Uninitialize();
        EffectTypeId = string.Empty;
        Value = 1;
        Duration?.ReturnToPool();
        Duration = null;
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
            StatusEffect?.OnStackChanged(this);
        }
    }

    public void Initialize(Stats stats, StatusEffect statusEffect, object? source)
    {
        if (Stats is not null)
            return;

        Stats = stats;
        StatusEffect = statusEffect;
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
        StatusEffect = null;
        Source = null;
        IsActive = false;
    }
}