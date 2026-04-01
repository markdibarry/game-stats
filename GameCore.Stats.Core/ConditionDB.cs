using System;
using System.Collections.Generic;
using System.Text.Json;
using GameCore.Pooling;

namespace GameCore.Stats;

public static class ConditionDB
{
    private static readonly Dictionary<Type, string> s_ids = new()
    {
        { typeof(TimedCondition), "Timed" },
        { typeof(ResourceCondition), "Resource" }
    };
    private static readonly Dictionary<string, Func<Condition>> s_createFromId = new()
    {
        { "Timed", Pool.Get<TimedCondition> },
        { "Resource", Pool.Get<ResourceCondition> }
    };
    private static readonly Dictionary<Type, Func<Condition>> s_createFromType = new()
    {
        { typeof(TimedCondition), Pool.Get<TimedCondition> },
        { typeof(ResourceCondition), Pool.Get<ResourceCondition> }
    };

    public static string GetTypeId(this Condition condition)
    {
        Type condType = condition.GetType();

        if (!s_ids.TryGetValue(condType, out string? typeId))
            throw new JsonException($"Unregistered type '{condType.FullName}'");

        return typeId;
    }

    public static Condition GetNew(Condition condition)
    {
        Type type = condition.GetType();

        if (!s_createFromType.TryGetValue(type, out Func<Condition>? func))
            throw new Exception($"Condition {type.Name} not registered.");

        return func();
    }

    public static Condition GetNew(string typeId)
    {
        if (!s_createFromId.TryGetValue(typeId, out Func<Condition>? func))
            throw new Exception($"Condition {typeId} not registered.");

        return func();
    }

    public static void Register<T>(string typeId, DeserializeDel deser, Action<Utf8JsonWriter, Condition> ser)
        where T : Condition, new()
    {
        Type type = typeof(T);
        s_ids.Add(type, typeId);
        s_createFromId.Add(typeId, Pool.Get<T>);
        s_createFromType.Add(type, Pool.Get<T>);
        ConditionJsonConverter.Register(typeId, deser, ser);
    }
}
