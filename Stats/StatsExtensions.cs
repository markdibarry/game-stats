using System.Collections.Generic;
using GameCore.Utility;

namespace GameCore.Statistics;

public static class StatsExtensions
{
    public static void AddMods(this Dictionary<string, Stat> statLookup, List<Modifier> modsToAdd)
    {
        foreach (Modifier mod in modsToAdd)
        {
            if (statLookup.TryGetValue(mod.StatTypeId, out Stat? stat))
            {
                stat.AddMod(mod.Clone());
            }
            else
            {
                stat = Stat.Create(mod.StatTypeId, 0);
                stat.AddMod(mod.Clone());
                statLookup.Add(mod.StatTypeId, stat);
            }
        }
    }

    public static void AddEffects(
        this Dictionary<string, StatusEffect> effects,
        List<StatusEffect> effectsToAdd)
    {
        foreach (StatusEffect effect in effectsToAdd)
            effects.Add(effect.StatTypeId, effect.Clone());
    }

    public static void ClearObject(this Dictionary<string, Stat> statLookup)
    {
        foreach (KeyValuePair<string, Stat> pair in statLookup)
            pair.Value.ReturnToPool();

        statLookup.Clear();
    }

    public static void ClearObject(this Dictionary<string, List<Modifier>> modLookup)
    {
        foreach (KeyValuePair<string, List<Modifier>> pair in modLookup)
            Pool.Return(pair.Value);

        modLookup.Clear();
    }

    public static void ClearObject(this Dictionary<string, StatusEffect> statusEffects)
    {
        foreach (KeyValuePair<string, StatusEffect> pair in statusEffects)
            pair.Value.ReturnToPool();

        statusEffects.Clear();
    }

    public static void CloneTo(
        this Dictionary<string, Stat> statLookup,
        Dictionary<string, Stat> cloneTo,
        bool ignoreModsWithSource = false)
    {
        foreach (KeyValuePair<string, Stat> pair in statLookup)
        {
            Stat clone = pair.Value.Clone(ignoreModsWithSource);

            if (clone.IsEmpty())
                clone.ReturnToPool();
            else
                cloneTo.Add(pair.Key, clone);
        }
    }

    public static Dictionary<string, Stat> Clone(
        this Dictionary<string, Stat> statLookup,
        bool ignoreModsWithSource = false)
    {
        Dictionary<string, Stat> clone = [];
        statLookup.CloneTo(clone, ignoreModsWithSource);

        return clone;
    }
}