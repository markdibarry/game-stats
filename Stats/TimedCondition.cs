using System;
using System.Text.Json.Serialization;

namespace GameCore.Statistics;

public sealed class TimedCondition : Condition
{
    [JsonIgnore]
    public static string TypeId { get; set; } = "Timed";
    public float Duration { get; set; }
    public float TimeLeft { get; set; }

    public static TimedCondition Create(float duration, bool reupOnMet = false)
    {
        TimedCondition timedCondition = StatsPool.Get<TimedCondition>();
        timedCondition.TimeLeft = duration;
        timedCondition.Duration = duration;
        timedCondition.ReupOnMet = reupOnMet;
        return timedCondition;
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

    protected override void SubscribeEvents()
    {
        Stats?.AddTimedCondition(this);
    }

    protected override void UnsubscribeEvents()
    {
        Stats?.RemoveTimedCondition(this);
    }

    protected override bool Evaluate() => TimeLeft <= 0;

    public void OnProcess(double delta)
    {
        if (Evaluate())
            return;

        TimeLeft = Math.Max(0, TimeLeft - (float)delta);

        if (Evaluate())
            RaiseConditionChanged();
    }
}
