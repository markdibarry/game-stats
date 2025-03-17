using System.Collections.Generic;
using GameCore.Utility;

namespace GameCore.Statistics;

public class ModifierLookup : Dictionary<string, List<Modifier>>
{
    public void Clone(ModifierLookup lookupClone, bool ignoreModsWithSource)
    {
        lookupClone.Clear();

        foreach (var kvp in this)
        {
            List<Modifier>? listClone = null;

            foreach (Modifier mod in kvp.Value)
            {
                if (mod.Source != null && ignoreModsWithSource)
                    continue;

                listClone ??= Pool.GetList<Modifier>();
                listClone.Add(mod.Clone());
            }

            if (listClone == null)
                continue;

            lookupClone.Add(kvp.Key, listClone);
        }
    }

    public void AddMod(Modifier mod)
    {
        if (!TryGetValue(mod.StatTypeId, out List<Modifier>? mods))
        {
            mods = Pool.GetList<Modifier>();
            Add(mod.StatTypeId, mods);
        }

        mods.Add(mod);
        mods.SortByOp();
    }

    public void ClearObject()
    {
        foreach (List<Modifier> mods in Values)
            Pool.Return(mods);

        Clear();
    }

    public Modifier? GetFirstModifier(string statTypeId, bool hasSource)
    {
        if (!TryGetValue(statTypeId, out var mods))
            return null;

        foreach (Modifier mod in mods)
        {
            if (hasSource == mod.Source is not null)
                return mod;
        }

        return null;
    }

    public void RegisterAll(StatsBase stats)
    {
        foreach (List<Modifier> mods in Values)
        {
            mods.SortByOp();

            foreach (Modifier mod in mods)
                mod.Register(stats, mod.Source);
        }
    }

    /// <summary>
    /// Removes Modifiers without sources.
    /// </summary>
    /// <param name="statType"></param>
    public void RemoveSourcelessMods(StatsBase stats, string statTypeId)
    {
        if (!TryGetValue(statTypeId, out var mods))
            return;

        for (int i = mods.Count - 1; i >= 0; i--)
        {
            Modifier mod = mods[i];

            if (mod.Source is null)
            {
                mods.RemoveAt(i);
                stats.RaiseModChanged(statTypeId, ModChangeType.Remove);
                mod.Unregister();
                mod.ReturnToPool();
            }
        }
    }

    public bool TryRemoveModBySource(Modifier sourceMod, object? source)
    {
        if (source is null)
            return false;

        if (!TryGetValue(sourceMod.StatTypeId, out List<Modifier>? mods))
            return false;

        Modifier? mod = FindModBySource(mods, sourceMod, source);

        if (mod is null)
            return false;

        return TryRemoveMod(mods, mod);
    }

    public bool TryRemoveMod(Modifier mod)
    {
        if (!TryGetValue(mod.StatTypeId, out List<Modifier>? mods))
            return false;

        return TryRemoveMod(mods, mod);
    }

    private bool TryRemoveMod(List<Modifier> mods, Modifier mod)
    {
        if (!mods.Remove(mod))
            return false;

        string statTypeId = mod.StatTypeId;
        mod.Unregister();
        mod.ReturnToPool();

        if (mods.Count == 0)
        {
            Pool.Return(mods);
            Remove(statTypeId);
        }

        return true;
    }

    private Modifier? FindModBySource(List<Modifier> mods, Modifier sourceMod, object source)
    {
        foreach (Modifier mod in mods)
        {
            if (mod.Source == source && mod.Op == sourceMod.Op)
                return mod;
        }

        return null;
    }
}