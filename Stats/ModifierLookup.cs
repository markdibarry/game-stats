using System.Collections.Generic;

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

                listClone ??= StatsPool.GetList<Modifier>();
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
            StatsPool.Return(mods);

        Clear();
    }

    internal void AddMod(Stats stats, Modifier mod, object? source)
    {
        mod.Initialize(stats, source);

        if (source is null && !mod.IsActive)
        {
            mod.Uninitialize();
            mod.ReturnToPool();
            return;
        }

        if (!TryGetValue(mod.StatTypeId, out List<Modifier>? mods))
        {
            mods = StatsPool.GetList<Modifier>();
            Add(mod.StatTypeId, mods);
        }

        int insertIndex = BinarySearchOp(mods, mod);
        mods.Insert(insertIndex, mod);
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
        if (!TryGetValue(statTypeId, out List<Modifier>? mods))
            return;

        for (int i = mods.Count - 1; i >= 0; i--)
        {
            Modifier mod = mods[i];

            if (mod.Source == source)
                RemoveModAt(stats, mods, mod, i);
        }
    }

    internal void RemoveMod(Stats stats, Modifier mod)
    {
        if (!TryGetValue(mod.StatTypeId, out List<Modifier>? mods))
            return;

        int index = mods.IndexOf(mod);

        if (index != -1)
            RemoveModAt(stats, mods, mod, index);
    }

    private void RemoveModAt(Stats stats, List<Modifier> mods, Modifier mod, int index)
    {
        mods.RemoveAt(index);
        string statTypeId = mod.StatTypeId;
        mod.Uninitialize();
        mod.ReturnToPool();

        if (mods.Count == 0)
        {
            StatsPool.Return(mods);
            Remove(statTypeId);
        }

        stats.RaiseStatChanged(statTypeId);
    }

    private static int BinarySearchOp(List<Modifier> list, Modifier item)
    {
        int lo = 0;
        int hi = list.Count - 1;
        int newOrder = StatOps.GetOrder(item.Op);

        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            int currOrder = StatOps.GetOrder(list[mid].Op);

            if (currOrder <= newOrder)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return lo;
    }
}