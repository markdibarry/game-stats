using System;
using GameCore.Statistics.Pooling;

namespace GameCore.Statistics;

public sealed class ResourceCondition : Condition
{
    public static string TypeId { get; set; } = "Resource";

    public string StatTypeId { get; set; } = string.Empty;
    public CompareOp CompareOp { get; set; }
    public int TargetValue { get; set; }
    public bool IsPercent { get; set; }

    public static ResourceCondition Create(
        string statTypeId,
        CompareOp compareOp,
        int targetValue,
        bool isPercent,
        bool reupOnMet = false)
    {
        ResourceCondition condition = Pool.Get<ResourceCondition>();
        condition.StatTypeId = statTypeId;
        condition.CompareOp = compareOp;
        condition.TargetValue = targetValue;
        condition.IsPercent = isPercent;
        condition.ReupOnMet = reupOnMet;
        return condition;
    }

    protected override void ResetData() { }

    protected override void ClearData()
    {
        CompareOp = default;
        TargetValue = default;
        StatTypeId = string.Empty;
        IsPercent = default;
    }

    protected override void CopyData(Condition condition)
    {
        if (condition is not ResourceCondition resourceCondition)
            return;

        CompareOp = resourceCondition.CompareOp;
        TargetValue = resourceCondition.TargetValue;
        StatTypeId = resourceCondition.StatTypeId;
        IsPercent = resourceCondition.IsPercent;
    }

    protected override bool Evaluate(Stats stats)
    {
        int target = TargetValue;

        if (IsPercent)
        {
            float maxValue = stats.Calculate(StatTypeId);
            target = Math.Max((int)(maxValue * (TargetValue * 0.01)), 1);
        }

        float value = stats.GetStat(StatTypeId)!.CurrentValue;

        return CompareOp.Compare((int)value, target);
    }

    protected override void SubscribeEvents()
    {
        Stats?.AddResourceCondition(this);
    }

    protected override void UnsubscribeEvents()
    {
        Stats?.RemoveResourceCondition(this);
    }

    public void OnStatChanged(string statTypeId)
    {
        if (StatTypeId == statTypeId)
            RaiseConditionChanged();
    }
}
