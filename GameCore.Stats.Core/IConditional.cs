namespace GameCore.Stats;

internal interface IConditional
{
    object? Source { get; }
    StatSet? Stats { get; }
    void OnConditionChanged(Condition condition);
}