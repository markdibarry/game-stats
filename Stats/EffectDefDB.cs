using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace GameCore.Statistics;

public static class EffectDefDB
{
    static EffectDefDB()
    {
        Data = s_data.AsReadOnly();
    }

    public static ReadOnlyDictionary<string, EffectDef> Data { get; }
    private static readonly Dictionary<string, EffectDef> s_data = [];

    public static bool TryGetValue(string effectTypeId, [MaybeNullWhen(false)] out EffectDef data)
    {
        return s_data.TryGetValue(effectTypeId, out data);
    }

    public static EffectDef Register(string effectTypeId)
    {
        if (effectTypeId.Length == 0 || s_data.ContainsKey(effectTypeId))
            throw new System.Exception("Effect definition must have a unique type id.");

        EffectDef effectDef = new(effectTypeId);
        s_data.Add(effectTypeId, effectDef);
        return effectDef;
    }
}
