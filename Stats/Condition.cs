using System.Text.Json.Serialization;
using GameCore.Utility;

namespace GameCore.Statistics;

public abstract class Condition : IPoolable
{
    private bool _result;
    private Condition? _parent;

    [JsonPropertyOrder(-4)]
    public bool Not { get; set; }
    [JsonPropertyOrder(-3)]
    public bool ReupOnMet { get; set; }
    [JsonPropertyOrder(-2)]
    public bool IgnoreModsWithSource { get; set; }
    [JsonPropertyOrder(20)]
    public Condition? And { get; set; }
    [JsonPropertyOrder(21)]
    public Condition? Or { get; set; }
    [JsonIgnore]
    public StatsBase? Stats => Conditional?.Stats;
    [JsonIgnore]
    public bool Registered { get; private set; }
    protected IConditional? Conditional { get; private set; }

    public bool CheckAllConditions(bool hasSource = false)
    {
        if (_result && !(IgnoreModsWithSource && hasSource))
            return And?.CheckAllConditions(hasSource) ?? true;
        else
            return Or?.CheckAllConditions(hasSource) ?? false;
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
        _result = false;
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
        Not = false;
        IgnoreModsWithSource = false;
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

    public Condition CloneSingle()
    {
        if (Pool.GetSameTypeOrNull(this) is not Condition clone)
            clone = ConditionDB.GetNew(this);

        clone.ReupOnMet = ReupOnMet;
        clone.Not = Not;
        clone.IgnoreModsWithSource = IgnoreModsWithSource;
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
        UpdateCondition();
    }

    public void ReupAllData()
    {
        And?.ResetData();
        Or?.ResetData();
        ResetData();
    }

    public void Register(IConditional owner, Condition? parent)
    {
        if (Registered)
            return;

        _parent = parent;
        Conditional = owner;
        SubscribeEvents();
        And?.Register(owner, this);
        Or?.Register(owner, this);
        UpdateCondition();
        Registered = true;
    }

    public void Unregister()
    {
        if (!Registered)
            return;

        UnsubscribeEvents();
        And?.Unregister();
        Or?.Unregister();
        _parent = null;
        Conditional = null;
        Registered = false;
    }

    protected abstract void SubscribeEvents();

    protected abstract void UnsubscribeEvents();

    protected abstract bool GetResult();

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
    /// <param name="condition">A cloned Condition of the derived type.</param>
    protected abstract void CopyData(Condition condition);

    /// <summary>
    /// Updates the _conditionMet flag and returns true if the result is different
    /// than the previous value.
    /// </summary>
    /// <returns>The result of whether the condition was updated or not.</returns>
    protected bool UpdateCondition()
    {
        bool result = GetResult();

        if (Not)
            result = !result;

        if (result != _result)
        {
            _result = result;
            return true;
        }

        return false;
    }

    protected void RaiseConditionChanged()
    {
        if (!UpdateCondition())
            return;

        Conditional?.OnConditionChanged(this);
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

    public static T SetIgnoreModsWithSource<T>(this T condition, bool enable) where T : Condition
    {
        condition.IgnoreModsWithSource = enable;
        return condition;
    }

    public static T SetNot<T>(this T condition, bool enable) where T : Condition
    {
        condition.Not = enable;
        return condition;
    }

    public static T SetReupOnMet<T>(this T condition, bool enable) where T : Condition
    {
        condition.ReupOnMet = enable;
        return condition;
    }
}