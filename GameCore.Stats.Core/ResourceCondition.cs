using System;
using static GameCore.Stats.ResourceCondition;

namespace GameCore.Stats;

public sealed class ResourceCondition : Condition<ResourceState>
{
    public struct ResourceState
    {
        public ResourceState()
        {
        }

        public ResourceState(string statTypeId, CompareOp compareOp, int targetValue, bool isPercent)
        {
            StatTypeId = statTypeId;
            CompareOp = compareOp;
            TargetValue = targetValue;
            IsPercent = isPercent;
        }

        public string StatTypeId { get; set; } = string.Empty;
        public CompareOp CompareOp { get; set; }
        public int TargetValue { get; set; }
        public bool IsPercent { get; set; }
    }

    public static string TypeId { get; set; } = "Resource";

    public override ResourceState State { get; set; }

    public static ResourceCondition Create(ResourceState state)
    {
        ResourceCondition condition = Create<ResourceCondition>();
        condition.State = state;
        return condition;
    }

    protected override void RefreshData() { }

    protected override bool Evaluate(StatSet stats)
    {
        int target = State.TargetValue;

        if (State.IsPercent)
        {
            float maxValue = stats.Calculate(State.StatTypeId);
            target = Math.Max((int)(maxValue * (State.TargetValue * 0.01)), 1);
        }

        float value = stats.GetStat(State.StatTypeId)!.CurrentValue;
        return State.CompareOp.Compare((int)value, target);
    }

    protected override void SubscribeEvents() => Stats?.AddResourceCondition(this);

    protected override void UnsubscribeEvents() => Stats?.RemoveResourceCondition(this);

    public void OnStatChanged(string statTypeId)
    {
        if (State.StatTypeId == statTypeId)
            UpdateCondition();
    }
}
