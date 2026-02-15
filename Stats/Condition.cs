using System;
using System.Text.Json.Serialization;
using GameCore.Pooling;

namespace GameCore.Statistics;

public abstract class Condition : IPoolable
{
    private Condition? _parent;

    protected IConditional? Conditional { get; private set; }

    [JsonIgnore]
    public bool Result { get; private set; }
    [JsonPropertyOrder(-3)]
    public bool ReupOnMet { get; set; }
    /// <summary>
    /// Ignore if Conditional has source
    /// </summary>
    [JsonPropertyOrder(-2)]
    public bool SourceIgnored { get; set; }
    [JsonPropertyOrder(20)]
    public Condition? And { get; set; }
    [JsonPropertyOrder(21)]
    public Condition? Or { get; set; }
    [JsonIgnore]
    public Stats? Stats => Conditional?.Stats;

    public static T Create<T>(Action<T> setup) where T : Condition, new()
    {
        T cond = Pool.Get<T>();
        setup(cond);
        return cond;
    }

    public bool CheckAllConditions(bool hasSource = false)
    {
        if (Result && !(SourceIgnored && hasSource))
            return And?.CheckAllConditions(hasSource) ?? true;
        else
            return Or?.CheckAllConditions(hasSource) ?? false;
    }

    internal bool EvaluateAllConditions(Stats stats, bool hasSource = false)
    {
        if (Evaluate(stats) && !(SourceIgnored && hasSource))
            return And?.EvaluateAllConditions(stats, hasSource) ?? true;
        else
            return Or?.EvaluateAllConditions(stats, hasSource) ?? false;
    }

    public T? GetFirstCondition<T>() where T : Condition, new()
    {
        if (this is T t)
            return t;

        if (And?.GetFirstCondition<T>() is T andResult)
            return andResult;

        if (Or?.GetFirstCondition<T>() is T orResult)
            return orResult;

        return null;
    }

    public void ClearObject()
    {
        Uninitialize();
        Result = false;
        _parent = null;

        if (And is not null)
        {
            And.ReturnToPool();
            And = null;
        }

        if (Or is not null)
        {
            Or.ReturnToPool();
            Or = null;
        }

        Conditional = null;
        ReupOnMet = false;
        SourceIgnored = false;
        ClearData();
    }

    public Condition Clone()
    {
        Condition clone = CloneSingle();

        if (And is not null)
            clone.And = And.Clone();

        if (Or is not null)
            clone.Or = Or.Clone();

        return clone;
    }

    private Condition CloneSingle()
    {
        if (Pool.GetSameTypeOrNull(this) is not Condition clone)
            clone = ConditionDB.GetNew(this);

        clone.ReupOnMet = ReupOnMet;
        clone.SourceIgnored = SourceIgnored;
        clone.CopyData(this);
        return clone;
    }

    /// <summary>
    /// Recursively calls up until it returns the top-most condition.
    /// </summary>
    /// <returns></returns>
    public Condition GetHeadCondition()
    {
        return _parent?.GetHeadCondition() ?? this;
    }

    public void Reup()
    {
        ResetData();
        UpdateResult();
    }

    public void ReupAllData()
    {
        And?.ResetData();
        Or?.ResetData();
        ResetData();
    }

    public void Initialize(IConditional owner, Condition? parent)
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

    public void Uninitialize()
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

    protected abstract void SubscribeEvents();

    protected abstract void UnsubscribeEvents();

    protected abstract bool Evaluate(Stats stats);

    /// <summary>
    /// Reverts condition data to initial user set values.
    /// </summary>
    protected abstract void ResetData();

    /// <summary>
    /// Clears all condition data.
    /// </summary>
    protected abstract void ClearData();

    /// <summary>
    /// Used to assign values for a derived Condition object.
    /// </summary>
    /// <param name="source">The condition to copy values from.</param>
    protected abstract void CopyData(Condition source);

    /// <summary>
    /// Updates the Result flag and returns true if the result is different
    /// than the previous value.
    /// </summary>
    /// <returns>The result of whether the condition was updated or not.</returns>
    protected bool UpdateResult()
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

    protected void RaiseConditionChanged()
    {
        if (!UpdateResult())
            return;

        Condition condition = GetHeadCondition();
        Conditional?.OnConditionChanged(condition);
    }
}

public static class ConditionExtensions
{
    public static T SetOr<T>(this T condition, Condition or) where T : Condition
    {
        condition.Or = or;
        return condition;
    }

    public static T SetAnd<T>(this T condition, Condition and) where T : Condition
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
    public static T SetSourceIgnored<T>(this T condition, bool enable) where T : Condition
    {
        condition.SourceIgnored = enable;
        return condition;
    }

    public static T SetReupOnMet<T>(this T condition, bool enable) where T : Condition
    {
        condition.ReupOnMet = enable;
        return condition;
    }
}