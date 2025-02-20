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

    public static bool TryGetValue(string statusEffectId, [MaybeNullWhen(false)] out EffectDef data)
    {
        return s_data.TryGetValue(statusEffectId, out data);
    }

    // public static EffectDef Add(EffectDef effectDef)
    // {
    //     if (effectDef.StatTypeId.Length == 0 || s_data.ContainsKey(effectDef.StatTypeId))
    //         throw new System.Exception("Effect definition must have a unique type id.");

    //     s_data.Add(effectDef.StatTypeId, effectDef);
    //     return effectDef;
    // }

    public static EffectDef Add(string effectName)
    {
        if (effectName.Length == 0 || s_data.ContainsKey(effectName))
            throw new System.Exception("Effect definition must have a unique type id.");

        EffectDef effectDef = new(effectName);
        s_data.Add(effectName, effectDef);
        return effectDef;
    }
}
