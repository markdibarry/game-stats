namespace GameCore.Stats;

public interface IConditional
{
    object? Source { get; }
    StatSet? Stats { get; }
    void OnConditionChanged(Condition condition);
}