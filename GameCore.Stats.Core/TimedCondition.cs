using System;
using static GameCore.Stats.TimedCondition;

namespace GameCore.Stats;

public sealed class TimedCondition : Condition<TimedState>
{
    public struct TimedState
    {
        public TimedState() { }

        public TimedState(float duration)
        {
            Duration = duration;
            TimeLeft = duration;
        }

        public TimedState(float duration, float timeLeft)
        {
            Duration = duration;
            TimeLeft = timeLeft;
        }

        public float Duration { get; set; }
        public float TimeLeft { get; set; }
    }

    public static string TypeId { get; set; } = "Timed";

    public override TimedState State { get; set; }

    public static TimedCondition Create(TimedState state)
    {
        TimedCondition condition = Create<TimedCondition>();
        condition.State = state;
        return condition;
    }

    protected override void RefreshData() => State = State with { TimeLeft = State.Duration };

    protected override bool Evaluate(StatSet stats) => State.TimeLeft <= 0;

    protected override void SubscribeEvents() => Stats?.AddTimedCondition(this);

    protected override void UnsubscribeEvents() => Stats?.RemoveTimedCondition(this);

    public void OnProcess(StatSet stats, double delta)
    {
        if (State.TimeLeft <= 0)
            return;

        State = State with { TimeLeft = Math.Max(0, State.TimeLeft - (float)delta) };

        if (State.TimeLeft <= 0)
            UpdateCondition();
    }
}
