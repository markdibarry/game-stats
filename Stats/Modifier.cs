using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using GameCore.Utility;

namespace GameCore.Statistics;

public sealed class Modifier : IPoolable, IConditional
{
    private bool _registered;

    [JsonPropertyOrder(-5)]
    public string StatTypeId { get; set; } = string.Empty;
    [JsonPropertyOrder(-4)]
    public string Op { get; set; } = string.Empty;
    [JsonPropertyOrder(-3)]
    public float Value { get; set; }
    [JsonPropertyOrder(-1)]
    public bool IsHidden { get; set; }
    [JsonPropertyOrder(5)]
    public Condition? Duration { get; set; }
    [JsonIgnore]
    public bool IsActive { get; private set; }
    [JsonIgnore]
    public object? Source { get; private set; }
    [JsonIgnore]
    public StatsBase? Stats { get; private set; }

    public static Modifier Create() => Pool.Get<Modifier>();

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

    public float Apply(float baseValue) => OpDB.Compute(this, baseValue);

    public void ClearObject()
    {
        Unregister();
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
        Modifier mod = Pool.Get<Modifier>();

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
                Stats?.TryRemoveMod(this);
            else
                Stats?.UpdateCustomStatType(StatTypeId);
        }
    }

    public void Register(StatsBase stats, object? source)
    {
        if (_registered || Stats is not null)
            return;

        Stats = stats;
        Source = source;
        IsActive = true;

        if (Duration is not null)
        {
            Duration.Register(this, null);
            IsActive = !Duration.CheckAllConditions(Source is not null);
        }

        _registered = true;
    }

    public void Unregister()
    {
        if (!_registered || Stats is null)
            return;

        Duration?.Unregister();
        Stats = null;
        Source = null;
        IsActive = false;

        _registered = false;
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
            if (statTypeId == string.Empty)
                statTypeId = mod.StatTypeId;
            else if (mod.StatTypeId != statTypeId)
                return result;

            if ((ignoreInactive && !mod.IsActive) || (ignoreHidden && mod.IsHidden))
                continue;

            if (mod.Op != OpDB.PercentAdd)
            {
                result = mod.Apply(result);
                continue;
            }

            percentToAdd = mod.Apply(percentToAdd);

            // Is last percent mod
            if (i + 1 == mods.Count || mods[i + 1].Op != OpDB.PercentAdd)
                result *= 1 + percentToAdd;
        }

        return result;
    }

    public static float Calculate(
        IReadOnlyList<Modifier> mods,
        float baseValue = 0,
        bool ignoreHidden = false)
    {
        return Calculate(mods, 0, baseValue, ignoreInactive: true, ignoreHidden);
    }
}