using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using GameCore.Utility;

namespace GameCore.Statistics;

public sealed class StatusEffect : IPoolable, IConditional
{
    public string StatTypeId { get; set; } = string.Empty;
    public List<Condition>? CustomConditions { get; set; }
    [JsonIgnore]
    public EffectDef? EffectDef { get; private set; }
    [JsonIgnore]
    public StatsBase? Stats { get; private set; }

    public static StatusEffect Create(EffectDef effectDef)
    {
        StatusEffect statusEffect = Pool.Get<StatusEffect>();
        statusEffect.Initialize(effectDef);
        return statusEffect;
    }

    public static StatusEffect Create(string statTypeId)
    {
        StatusEffect statusEffect = Pool.Get<StatusEffect>();
        statusEffect.Initialize(statTypeId);
        return statusEffect;
    }

    public StatusEffect Clone()
    {
        StatusEffect clone = Pool.Get<StatusEffect>();
        clone.Initialize(this);
        return clone;
    }

    public void ClearObject()
    {
        Unregister();

        if (CustomConditions is not null)
            Pool.Return(CustomConditions);

        CustomConditions = null;
        EffectDef = null;
        Stats = null;
        StatTypeId = string.Empty;
    }

    public int GetStackCount()
    {
        if (Stats is null || EffectDef is null)
            return 0;

        IReadOnlyCollection<Modifier> mods = Stats.GetModifiersByType(StatTypeId);
        float total = 0;

        for (int i = 0; i < mods.Count; i++)
        {
            total += mods.ElementAt(i).Value;
        }

        return Math.Min((int)total, EffectDef.MaxStack);
    }

    public void Initialize(string statTypeId)
    {
        if (!EffectDefDB.TryGetValue(statTypeId, out EffectDef? effectDef))
            return;

        Initialize(effectDef);
    }

    public void Initialize(EffectDef effectDef)
    {
        EffectDef = effectDef;
        StatTypeId = effectDef.StatTypeId;

        if (effectDef.CustomEffects.Count == 0)
            return;

        CustomConditions = Pool.GetList<Condition>();

        foreach (var effect in effectDef.CustomEffects)
        {
            Condition condition = effect.Condition.Clone();
            CustomConditions.Add(condition);
        }
    }

    public void Initialize(StatusEffect statusEffect)
    {
        if (!EffectDefDB.TryGetValue(statusEffect.StatTypeId, out EffectDef? effectDef))
            return;

        EffectDef = effectDef;
        StatTypeId = statusEffect.StatTypeId;

        if (statusEffect.CustomConditions is null)
            return;

        CustomConditions = Pool.GetList<Condition>();

        foreach (Condition condition in statusEffect.CustomConditions)
        {
            Condition clone = condition.Clone();
            CustomConditions.Add(clone);
        }
    }

    public void OnConditionChanged(Condition condition)
    {
        if (TryInvokeCustom(condition))
            return;
    }

    public void Register(StatsBase stats)
    {
        if (Stats is not null)
            return;

        Stats = stats;

        if (CustomConditions is null)
            return;

        foreach (Condition condition in CustomConditions)
            condition.Register(this, null);
    }

    public void Unregister()
    {
        if (Stats is null)
            return;

        if (CustomConditions is not null)
        {
            foreach (Condition condition in CustomConditions)
                condition.Unregister();
        }

        Stats = null;
    }

    private bool TryInvokeCustom(Condition condition)
    {
        int index = CustomConditions?.IndexOf(condition) ?? -1;

        if (index == -1)
            return false;

        if (condition.CheckAllConditions())
        {
            if (Stats is null || EffectDef is null || CustomConditions is null)
                return false;

            EffectDef.CustomEffects[index].Effect.Invoke(Stats, this);
        }

        if (condition.ReupOnMet)
            condition.Reup();

        return true;
    }
}
