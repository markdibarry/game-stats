using GameCore.Pooling;

namespace GameCore.Stats;

public interface ICondition { }

public abstract class Condition<TState> : Condition, ICondition where TState : struct
{
    public abstract TState State { get; set; }

    protected static TCond Create<TCond>() where TCond : Condition, new()
    {
        return Pool.Get<TCond>();
    }

    public override sealed void ClearObject()
    {
        base.ClearObject();
        State = default;
    }

    internal override sealed Condition Clone()
    {
        if (Pool.GetSameTypeOrNull(this) is not Condition<TState> clone)
            clone = (Condition<TState>)ConditionDB.GetNew(this);

        clone.AutoRefresh = AutoRefresh;
        clone.SourceIgnored = SourceIgnored;
        clone.State = State;

        if (And is not null)
            clone.And = And.Clone();

        if (Or is not null)
            clone.Or = Or.Clone();

        return clone;
    }
}