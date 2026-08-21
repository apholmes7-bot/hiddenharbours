using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Loads a hull's interior pixels when somebody opens her door, and lets go when the last boat
    /// carrying them is gone.</b>
    ///
    /// <para><b>Ref-counted because the pixels are shared and the boats are not.</b> Eighteen lobster
    /// variants can be afloat in one creek and several may be the same class; they all draw the same
    /// <see cref="BoatInteriorCellsDef"/>. Unloading on the first hull's <c>OnDestroy</c> would pull the
    /// sheets out from under her sister still being stood in. So each cabin
    /// <see cref="Acquire"/>s and <see cref="Release"/>s, and only the last one out drops the
    /// reference.</para>
    ///
    /// <para><b>What "drops the reference" means, honestly.</b> This clears the strong reference so the
    /// engine MAY reclaim the memory; the reclaim itself happens at Unity's next
    /// <c>UnloadUnusedAssets</c>, which a scene load already runs — and region travel is a scene load.
    /// It deliberately does not call <c>Resources.UnloadAsset</c> on the sprites: that is immediate and
    /// unconditional, and a sprite still referenced by a renderer somewhere would turn into a hole in
    /// the picture rather than a saving.</para>
    ///
    /// <para><b>⚠️ Release belongs in <c>OnDestroy</c>, never <c>OnDisable</c>.</b> Root-toggling IS how
    /// a region hop works, so "disabled" happens constantly and does not mean "gone" — a release there
    /// would unload the cabin of a boat the player is standing inside, halfway across a region
    /// boundary. Same law, same reason, as never unregistering a Core service in <c>OnDisable</c>.</para>
    ///
    /// <para><b>Every count is guarded by "did I actually hold this?"</b> (#601's lesson). A cabin that
    /// never loaded releases nothing, and a double release cannot drive a count negative and evict a
    /// sheet somebody else is drawing.</para>
    /// </summary>
    public static class BoatInteriorCells
    {
        private sealed class Entry
        {
            public BoatInteriorCellsDef Def;
            public int Holders;
        }

        private static readonly Dictionary<string, Entry> Loaded = new();

        /// <summary>How many distinct hulls' sheets are resident right now. Read by tests: the number
        /// that should be 0 while the entry gate is shut fleet-wide.</summary>
        public static int ResidentSets
        {
            get
            {
                int n = 0;
                foreach (var kv in Loaded) if (kv.Value.Def != null) n++;
                return n;
            }
        }

        /// <summary>Megabytes of RGBA32 currently held, as the builder measured each set. The budget
        /// figure a test can assert against without opening a profiler.</summary>
        public static float ResidentMegabytes
        {
            get
            {
                float mb = 0f;
                foreach (var kv in Loaded) if (kv.Value.Def != null) mb += kv.Value.Def.ResidentMegabytes;
                return mb;
            }
        }

        /// <summary>
        /// Take a reference to one hull's cells, loading them on the first taker. Returns null when the
        /// id names nothing — a hull whose sheets were never baked, which is data rather than a fault.
        /// </summary>
        public static BoatInteriorCellsDef Acquire(string interiorDefId)
        {
            string path = BoatInteriorCellsDef.PathFor(interiorDefId);
            if (string.IsNullOrEmpty(path)) return null;

            if (!Loaded.TryGetValue(interiorDefId, out Entry entry))
            {
                entry = new Entry();
                Loaded[interiorDefId] = entry;
            }

            if (entry.Def == null)
            {
                entry.Def = Resources.Load<BoatInteriorCellsDef>(path);
                if (entry.Def == null)
                {
                    Debug.LogError($"[BoatInteriorCells] no cells asset at Resources/{path}. The hull " +
                                   "carries an interior def but her sheets were never wired — re-run " +
                                   "Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Wire Boat Interiors To Visuals.");
                    return null;
                }
            }

            entry.Holders++;
            return entry.Def;
        }

        /// <summary>
        /// Give a reference back. Guarded both ways: an id nobody holds is ignored rather than driven
        /// negative, and the sheets are only let go when the last holder is gone.
        /// </summary>
        public static void Release(string interiorDefId)
        {
            if (string.IsNullOrEmpty(interiorDefId)) return;
            if (!Loaded.TryGetValue(interiorDefId, out Entry entry)) return;

            entry.Holders--;
            if (entry.Holders > 0) return;

            entry.Holders = 0;
            entry.Def = null;      // the engine may reclaim at the next UnloadUnusedAssets
            Loaded.Remove(interiorDefId);
        }

        /// <summary>Drop everything (test isolation, and a hard teardown). Does not unload the engine's
        /// copy; it forgets that anything is held, which is what a fresh fixture wants.</summary>
        public static void Reset() => Loaded.Clear();
    }
}
