using System.Collections.Generic;
using GameCore.Utility;

namespace GameCore.Statistics;

public class ModifierLookup : Dictionary<string, List<Modifier>>
{
    public void CopyTo(ModifierLookup lookupClone, bool ignoreModsWithSource)
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

    public void ClearObject()
    {
        foreach (List<Modifier> mods in Values)
            Pool.Return(mods);

        Clear();
    }

    internal void AddMod(Stats stats, Modifier sourceMod, object? source, bool clone)
    {
        Modifier mod = clone ? sourceMod.Clone() : sourceMod;
        mod.Initialize(stats, source);

        if (source is null && !mod.IsActive)
        {
            mod.Uninitialize();
            mod.ReturnToPool();
            return;
        }

        if (!TryGetValue(mod.StatTypeId, out List<Modifier>? mods))
        {
            mods = Pool.GetList<Modifier>();
            Add(mod.StatTypeId, mods);
        }

        mods.Add(mod);
        mods.SortByOp();
        stats.RaiseStatChanged(mod.StatTypeId);
    }

    internal void InitializeAll(Stats stats)
    {
        foreach (List<Modifier> mods in Values)
        {
            mods.SortByOp();

            foreach (Modifier mod in mods)
                mod.Initialize(stats, mod.Source);
        }
    }

    internal void RemoveModBySource(Stats stats, string statTypeId, object? source)
    {
        if (source is null)
            return;

        if (!TryGetValue(statTypeId, out List<Modifier>? mods))
            return;

        foreach (Modifier mod in mods)
        {
            if (mod.Source == source)
                RemoveMod(stats, mods, mod);
        }
    }

    internal void RemoveMod(Stats stats, Modifier mod)
    {
        if (!TryGetValue(mod.StatTypeId, out List<Modifier>? mods))
            return;

        RemoveMod(stats, mods, mod);
    }

    private void RemoveMod(Stats stats, List<Modifier> mods, Modifier mod)
    {
        if (!mods.Remove(mod))
            return;

        string statTypeId = mod.StatTypeId;
        mod.Uninitialize();
        mod.ReturnToPool();

        if (mods.Count == 0)
        {
            Pool.Return(mods);
            Remove(statTypeId);
        }

        stats.RaiseStatChanged(statTypeId);
    }
}