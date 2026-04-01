namespace GameCore.Stats;

/// <summary>
/// Used to define how additional stacks are handled when added.
/// </summary>
public enum StackMode
{
    /// <summary>
    /// Additional stacks will have their value added to the first existing stack without a source.
    /// </summary>
    None,
    /// <summary>
    /// Additional stacks will refresh the conditions of the first existing stack without a source.
    /// </summary>
    Refresh,
    /// <summary>
    /// Additional stacks will extend the TimedCondition of the first existing stack without a source.
    /// </summary>
    Extend,
    /// <summary>
    /// All stacks will be independently tracked.
    /// </summary>
    MultiDuration,
    /// <summary>
    /// All stacks and custom conditions will be independently tracked.
    /// </summary>
    MultiFull
}