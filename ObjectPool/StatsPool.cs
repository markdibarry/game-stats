using System;
using System.Collections.Generic;
using System.Text;

namespace GameCore.Statistics;

/// <summary>
/// Represents a pool of objects that can be borrowed and returned. 
/// </summary>
public static class StatsPool
{
    private static readonly Dictionary<Type, StatsLimitedQueue<IStatsPoolable>> s_pool = [];
    private static readonly Dictionary<Type, StatsLimitedQueue<object>> s_listPool = [];
    private static readonly StringBuilder s_sb = new();

    public static string PrintPool()
    {
        s_sb.AppendLine();
        s_sb.Append("|--Current pool types and counts--|");
        s_sb.AppendLine();

        foreach (var kvp in s_pool)
        {
            s_sb.Append($"{kvp.Key}: {kvp.Value.Count}");
        }

        foreach (var kvp in s_listPool)
        {
            s_sb.Append($"List<{kvp.Key}>: {kvp.Value.Count}");
        }

        s_sb.AppendLine();
        string result = s_sb.ToString();
        s_sb.Clear();
        return result;
    }

    public static void ClearPool()
    {
        s_pool.Clear();
        s_listPool.Clear();
    }

    /// <summary>
    /// Populates the provided queue with the specified number of objects.
    /// </summary>
    /// <typeparam name="T">The pool type to allocate to.</typeparam>
    /// <param name="amount">The amount of objects to allocate to the pool.</param>
    public static void Allocate<T>(int amount) where T : IStatsPoolable, new()
    {
        Type type = typeof(T);
        StatsLimitedQueue<IStatsPoolable> limitedQueue = GetLimitedQueue(type);
        int toAllocate = Math.Max(amount, 0);

        if (limitedQueue.Limit != -1)
            toAllocate = Math.Min(toAllocate, limitedQueue.Limit - limitedQueue.Count);

        for (int i = 0; i < toAllocate; i++)
        {
            IStatsPoolable obj = new T();
            limitedQueue.Enqueue(obj);
        }
    }

    public static T? GetSameTypeOrNull<T>(this T poolable) where T : IStatsPoolable
    {
        Type type = poolable.GetType();
        StatsLimitedQueue<IStatsPoolable> limitedQueue = GetLimitedQueue(type);
        return limitedQueue.Count > 0 ? (T)limitedQueue.Dequeue() : default;
    }

    /// <summary>
    /// Retrieves an object from the pool of a registered type.
    /// If the pool is empty, a new object is created.
    /// </summary>
    /// <typeparam name="T">The type of object to borrow.</typeparam>
    /// <returns>An object of the specified type</returns>
    public static T Get<T>() where T : IStatsPoolable, new()
    {
        Type type = typeof(T);
        StatsLimitedQueue<IStatsPoolable> limitedQueue = GetLimitedQueue(type);
        return limitedQueue.Count > 0 ? (T)limitedQueue.Dequeue() : new();
    }

    /// <summary>
    /// Retrieves a List from the pool of a registered type.
    /// If the pool is empty, a new List is created.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static List<T> GetList<T>() where T : IStatsPoolable
    {
        Type type = typeof(T);

        if (!s_listPool.TryGetValue(type, out StatsLimitedQueue<object>? limitedQueue))
            return [];

        return limitedQueue.Count > 0 ? (List<T>)limitedQueue.Dequeue() : [];
    }

    /// <summary>
    /// Returns the provided object to the pool of the underlying registered type.
    /// </summary>
    /// <param name="poolable">The object to return.</param>
    public static void Return(IStatsPoolable poolable)
    {
        poolable.ClearObject();
        Type type = poolable.GetType();
        StatsLimitedQueue<IStatsPoolable> limitedQueue = GetLimitedQueue(type);
        limitedQueue.Enqueue(poolable);
    }

    /// <summary>
    /// Returns the provided List to the pool of the underlying registered type.
    /// If the List contains IPoolable objects, they will be returned to their pool as well.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    public static void Return<T>(List<T> list) where T : IStatsPoolable
    {
        Type type = typeof(T);

        foreach (T item in list)
            Return(item);

        list.Clear();

        if (!s_listPool.TryGetValue(type, out StatsLimitedQueue<object>? limitedQueue))
        {
            limitedQueue = new();
            s_listPool[type] = limitedQueue;
        }

        limitedQueue.Enqueue(list);
    }

    /// <summary>
    /// Returns the provided object to the pool of the underlying registered type.
    /// </summary>
    /// <param name="poolable">The object to return.</param>
    public static void ReturnToPool(this IStatsPoolable poolable) => Return(poolable);

    private static StatsLimitedQueue<IStatsPoolable> GetLimitedQueue(Type type)
    {
        if (!s_pool.TryGetValue(type, out StatsLimitedQueue<IStatsPoolable>? limitedQueue))
        {
            limitedQueue = new();
            s_pool[type] = limitedQueue;
        }

        return limitedQueue;
    }

    /// <summary>
    /// An exception for accessing types that are not registered to the pool.
    /// </summary>
    [Serializable]
    private class UnregisteredTypeException : Exception
    {
        public UnregisteredTypeException(Type type)
            : base($"Type \"${type.Name}\" is not registered for Pool.")
        { }
    }
}
