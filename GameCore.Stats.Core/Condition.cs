using System.Text.Json.Serialization;
using GameCore.Pooling;

namespace GameCore.Stats;

[JsonConverter(typeof(ConditionJsonConverter))]
public abstract class Condition : IPoolable
{
    private Condition? _parent;

    internal IConditional? Conditional { get; set; }
    public StatSet? Stats => Conditional?.Stats;
    public bool Result { get; protected set; }
    public bool AutoRefresh { get; set; }
    /// <summary>
    /// Ignore if Conditional has source
    /// </summary>
    public bool SourceIgnored { get; set; }
    public Condition? And { get; set; }
    public Condition? Or { get; set; }

    public virtual void ClearObject()
    {
        Uninitialize();
        Result = false;
        _parent = null;
        And?.ReturnToPool();
        And = null;
        Or?.ReturnToPool();
        Or = null;

        Conditional = null;
        AutoRefresh = false;
        SourceIgnored = false;
    }

    public bool CheckAllConditions(bool hasSource = false)
    {
        if (Result && !(SourceIgnored && hasSource))
            return And?.CheckAllConditions(hasSource) ?? true;
        else
            return Or?.CheckAllConditions(hasSource) ?? false;
    }

    internal bool EvaluateAllConditions(StatSet stats, bool hasSource = false)
    {
        if (Evaluate(stats) && !(SourceIgnored && hasSource))
            return And?.EvaluateAllConditions(stats, hasSource) ?? true;
        else
            return Or?.EvaluateAllConditions(stats, hasSource) ?? false;
    }

    public TCond? GetFirstCondition<TCond>() where TCond : Condition, new()
    {
        if (this is TCond t)
            return t;

        if (And?.GetFirstCondition<TCond>() is TCond andResult)
            return andResult;

        if (Or?.GetFirstCondition<TCond>() is TCond orResult)
            return orResult;

        return null;
    }

    internal virtual Condition Clone()
    {
        if (Pool.GetSameTypeOrNull(this) is not Condition clone)
            clone = ConditionDB.GetNew(this);

        clone.AutoRefresh = AutoRefresh;
        clone.SourceIgnored = SourceIgnored;

        if (And is not null)
            clone.And = And.Clone();

        if (Or is not null)
            clone.Or = Or.Clone();

        return clone;
    }

    /// <summary>
    /// Recursively calls up until it returns the top-most condition.
    /// </summary>
    /// <returns></returns>
    internal Condition GetHeadCondition()
    {
        return _parent?.GetHeadCondition() ?? this;
    }

    internal void Refresh()
    {
        RefreshData();
        UpdateResult();
    }

    internal void RefreshAllData()
    {
        And?.RefreshAllData();
        Or?.RefreshAllData();
        RefreshData();
    }

    internal void Initialize(IConditional owner, Condition? parent)
    {
        if (Conditional != null)
            return;

        _parent = parent;
        Conditional = owner;

        if (!SourceIgnored || owner.Source == null)
            SubscribeEvents();

        And?.Initialize(owner, this);
        Or?.Initialize(owner, this);
        UpdateResult();
    }

    internal void Uninitialize()
    {
        if (Conditional == null)
            return;

        if (!SourceIgnored || Conditional.Source == null)
            UnsubscribeEvents();

        And?.Uninitialize();
        Or?.Uninitialize();
        _parent = null;
        Conditional = null;
        Result = false;
    }

    /// <summary>
    /// Updates the Result flag and returns true if the result is different
    /// than the previous value.
    /// </summary>
    /// <returns>The result of whether the condition was updated or not.</returns>
    private bool UpdateResult()
    {
        if (Stats == null)
            return false;

        bool result = Evaluate(Stats);

        if (result != Result)
        {
            Result = result;
            return true;
        }

        return false;
    }

    protected void UpdateCondition()
    {
        if (!UpdateResult())
            return;

        Condition condition = GetHeadCondition();
        Conditional?.OnConditionChanged(condition);
    }

    /// <summary>
    /// Reverts condition data to initial user set values.
    /// </summary>
    protected abstract void RefreshData();

    protected abstract bool Evaluate(StatSet stats);

    protected abstract void SubscribeEvents();

    protected abstract void UnsubscribeEvents();
}

public static class ConditionExtensions
{
    public static T WithOr<T>(this T condition, Condition or) where T : Condition
    {
        condition.Or = or;
        return condition;
    }

    public static T WithAnd<T>(this T condition, Condition and) where T : Condition
    {
        condition.And = and;
        return condition;
    }

    /// <summary>
    /// If true this condition will be ignored when the Conditional has a source.
    /// </summary>
    /// <remarks>
    /// Note: Should not be called on a Condition currently in use.
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    /// <param name="condition"></param>
    /// <param name="enable"></param>
    /// <returns></returns>
    public static T WithIgnoreSource<T>(this T condition) where T : Condition
    {
        condition.SourceIgnored = true;
        return condition;
    }

    public static T WithRefreshOnMet<T>(this T condition) where T : Condition
    {
        condition.AutoRefresh = true;
        return condition;
    }
}
