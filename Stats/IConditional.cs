namespace GameCore.Statistics;

public interface IConditional
{
    Stats? Stats { get; }
    void OnConditionChanged(Condition condition);
}