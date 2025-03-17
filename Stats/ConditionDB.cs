using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace GameCore.Statistics;

public static class ConditionDB
{
    static ConditionDB()
    {
        Register<TimedCondition>(TimedCondition.TypeId);
    }

    private static readonly Dictionary<string, Type> s_types = [];
    private static readonly Dictionary<Type, Func<Condition>> s_createFuncs = [];
    private static readonly Dictionary<Type, string> s_ids = [];
    private static readonly List<JsonDerivedType> s_jsonDerivedTypes = [];

    public static void Register<T>(string typeId) where T : Condition, new()
    {
        Type type = typeof(T);
        s_types.Add(typeId, type);
        s_ids.Add(type, typeId);
        s_createFuncs.Add(type, () => new T());
        s_jsonDerivedTypes.Add(new JsonDerivedType(type, typeId));
    }

    public static Condition GetNew(this Condition condition)
    {
        Type type = condition.GetType();

        if (!s_createFuncs.TryGetValue(type, out Func<Condition>? func))
            throw new Exception($"Condition {type.Name} not registered.");

        return func();
    }

    public static void ResolveCondition(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(Condition))
            return;

        typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "ConditionType",
            IgnoreUnrecognizedTypeDiscriminators = true,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization
        };

        foreach (JsonDerivedType derivedType in s_jsonDerivedTypes)
            typeInfo.PolymorphismOptions.DerivedTypes.Add(derivedType);
    }
}
