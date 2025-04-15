namespace GameCore.Statistics;

public interface IConditional
{
    object? Source { get; }
    Stats? Stats { get; }
    void OnConditionChanged(Condition condition);
}