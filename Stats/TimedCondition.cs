using System;
using GameCore.Pooling;

namespace GameCore.Statistics;

public sealed class TimedCondition : Condition
{
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

    protected override bool Evaluate(Stats stats) => TimeLeft <= 0;

    public void OnProcess(Stats stats, double delta)
    {
        if (Evaluate(stats))
            return;

        TimeLeft = Math.Max(0, TimeLeft - (float)delta);

        if (Evaluate(stats))
            RaiseConditionChanged();
    }
}
