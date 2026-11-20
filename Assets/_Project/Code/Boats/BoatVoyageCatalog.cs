using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Joins each voyage record to the hull it is for</b> (crew slice R2): the record's minimums and
    /// work, the hull's <see cref="BoatHullDef.CrewSlots"/> as the day limit, and the hull's interior
    /// <c>sleep</c> anchors as the bunks (<see cref="HullBunks.Of"/>). The Core
    /// <see cref="IVoyageCatalog"/> the roster reads through <see cref="GameServices.Voyages"/>.
    ///
    /// <para>Built on demand from whatever hulls and records the caller holds; nothing registers it at
    /// runtime yet (the hiring slice does, once the hulls a save owns are known). A record whose hull is not
    /// in the set is left out: no spec is better than a spec with made-up limits.</para>
    /// </summary>
    public sealed class BoatVoyageCatalog : IVoyageCatalog
    {
        private readonly VoyageSpecCatalog _specs;

        public BoatVoyageCatalog(IEnumerable<BoatHullDef> hulls, IEnumerable<BoatVoyageDef> voyages)
        {
            var byId = new Dictionary<string, BoatHullDef>(StringComparer.Ordinal);
            if (hulls != null)
                foreach (var h in hulls)
                    if (h != null && !string.IsNullOrEmpty(h.Id) && !byId.ContainsKey(h.Id)) byId.Add(h.Id, h);
            var specs = new List<VoyageSpec>();
            if (voyages != null)
                foreach (var v in voyages)
                    if (v != null && byId.TryGetValue(v.HullId ?? "", out var hull))
                        specs.Add(SpecFor(hull, v));
            _specs = new VoyageSpecCatalog(specs);
        }

        public int Count => _specs.Count;

        public bool TryGet(string hullId, out VoyageSpec spec) => _specs.TryGet(hullId, out spec);

        /// <summary>One hull's spec: the record joined with the two numbers the hull owns.</summary>
        public static VoyageSpec SpecFor(BoatHullDef hull, BoatVoyageDef voyage)
        {
            if (hull == null || voyage == null) return null;
            return voyage.ToSpec(hull.CrewSlots, HullBunks.Of(hull));
        }
    }

    /// <summary>A hull's bunks, read the one way (<see cref="BunkCount.Of"/>) from the interior her
    /// visual names. No visual or no interior is no bunk.</summary>
    public static class HullBunks
    {
        public static int Of(BoatHullDef hull)
            => hull != null && hull.Visual != null ? BunkCount.Of(hull.Visual.Interior) : 0;
    }
}
