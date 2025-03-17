using System.Collections.Generic;
using GameCore.Utility;

namespace GameCore.Statistics;

public static class StatsExtensions
{
    public static void AddEffects(
        this Dictionary<string, StatusEffect> effects,
        List<StatusEffect> effectsToAdd)
    {
        foreach (StatusEffect effect in effectsToAdd)
            effects.Add(effect.StatTypeId, effect.Clone());
    }

    public static void ClearObject(this Dictionary<string, StatusEffect> statusEffects)
    {
        foreach (KeyValuePair<string, StatusEffect> pair in statusEffects)
            pair.Value.ReturnToPool();

        statusEffects.Clear();
    }
}