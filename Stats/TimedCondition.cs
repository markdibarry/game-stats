using System;
using System.Text.Json.Serialization;
using GameCore.Utility;

namespace GameCore.Statistics;

public sealed class TimedCondition : Condition
{
    [JsonIgnore]
    public static string TypeId { get; set; } = "Timed";
    public float Duration { get; set; }
    public float TimeLeft { get; set; }

    public static TimedCondition Create(float duration, bool reupOnMet = false)
    {
        TimedCondition timedCondition = Pool.Get<TimedCondition>();
        timedCondition.TimeLeft = duration;
        timedCondition.Duration = duration;
        timedCondition.ReupOnMet = reupOnMet;
        return timedCondition;
    }

    protected override void SubscribeEvents() => Stats?.RegisterTimedCondition(this);

    protected override void UnsubscribeEvents() => Stats?.UnregisterTimedCondition(this);

    protected override bool GetResult() => TimeLeft <= 0;

    public void Process(double delta)
    {
        if (GetResult())
            return;

        TimeLeft = Math.Max(0, TimeLeft - (float)delta);

        if (GetResult())
            RaiseConditionChanged();
    }

    protected override void ResetData()
    {
        TimeLeft = Duration;
    }

    protected override void ClearData()
    {
        Duration = default;
        TimeLeft = default;
    }

    protected override void CopyData(Condition condition)
    {
        if (condition is not TimedCondition timedCondition)
            return;

        TimeLeft = timedCondition.TimeLeft;
        Duration = timedCondition.Duration;
    }
}
