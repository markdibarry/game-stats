using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using GameCore.Pooling;

namespace GameCore.Statistics;

public sealed class Modifier : IPoolable, IConditional
{
    [JsonPropertyOrder(0)]
    public string StatTypeId { get; set; } = string.Empty;
    [JsonPropertyOrder(1)]
    public string Op { get; set; } = string.Empty;
    [JsonPropertyOrder(2)]
    public float Value { get; set; }
    [JsonPropertyOrder(3)]
    public bool IsHidden { get; set; }
    [JsonPropertyOrder(5)]
    public Condition? Duration { get; set; }
    [JsonIgnore]
    public bool IsActive { get; private set; }
    [JsonIgnore]
    public object? Source { get; internal set; }
    [JsonIgnore]
    public Stats? Stats { get; private set; }

    public static Modifier Create() => Pool.Get<Modifier>();

    public static Modifier Create(Action<Modifier> action)
    {
        Modifier mod = Create();
        action(mod);
        return mod;
    }

    public static Modifier Create(
        string statTypeId,
        string op,
        float value,
        Condition? duration = null,
        bool isHidden = false)
    {
        Modifier mod = Create(statTypeId, op, value, duration, isHidden, null);
        return mod;
    }

    public static Modifier Create(
        string statTypeId,
        string op,
        float value,
        Condition? duration,
        bool isHidden,
        object? source)
    {
        Modifier mod = Pool.Get<Modifier>();
        mod.StatTypeId = statTypeId;
        mod.Op = op;
        mod.Value = value;
        mod.Duration = duration;
        mod.IsHidden = isHidden;
        mod.Source = source;
        return mod;
    }

    public float Apply(float baseValue) => StatOps.Compute(this, baseValue);

    public void ClearObject()
    {
        Uninitialize();
        Duration?.ReturnToPool();
        Duration = default;
        IsActive = false;
        IsHidden = false;
        Op = string.Empty;
        Source = default;
        StatTypeId = string.Empty;
        Value = default;
        Stats = default;
    }

    public Modifier Clone()
    {
        Modifier mod = Create();

        mod.StatTypeId = StatTypeId;
        mod.Op = Op;
        mod.Value = Value;
        mod.Duration = Duration?.Clone();
        mod.IsHidden = IsHidden;
        mod.Source = Source;

        return mod;
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
                Stats?.RemoveModByRef(this);
            else
                Stats?.RaiseStatChanged(StatTypeId);
        }
    }

    internal void Initialize(Stats stats, object? source)
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

    internal void Uninitialize()
    {
        if (Stats is null)
            return;

        Duration?.Uninitialize();
        Stats = null;
        Source = null;
        IsActive = false;
    }

    public static float Calculate(
        IReadOnlyList<Modifier> mods,
        float baseValue = 0,
        bool ignoreHidden = false)
    {
        return Calculate(mods, 0, baseValue, ignoreInactive: true, ignoreHidden);
    }

    public static float Calculate(
        IReadOnlyList<Modifier> mods,
        int start,
        float baseValue = 0,
        bool ignoreInactive = true,
        bool ignoreHidden = false)
    {
        float result = baseValue;
        float percentToAdd = default;
        string statTypeId = string.Empty;

        for (int i = start; i < mods.Count; i++)
        {
            Modifier mod = mods[i];

            // Add only of same type
            if (statTypeId.Length == 0)
                statTypeId = mod.StatTypeId;
            else if (mod.StatTypeId != statTypeId)
                return result;

            if ((ignoreInactive && !mod.IsActive) || (ignoreHidden && mod.IsHidden))
                continue;

            if (mod.Op != StatOps.AddPercent)
            {
                result = mod.Apply(result);
                continue;
            }

            percentToAdd = mod.Apply(percentToAdd);

            // Is last percent mod
            if (i + 1 == mods.Count || mods[i + 1].Op != StatOps.AddPercent)
                result *= 1 + percentToAdd;
        }

        return result;
    }
}