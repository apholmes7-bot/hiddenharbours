using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;   // ITidalTerrain — the ground height, read without referencing World (rule 4)

namespace HiddenHarbours.Art
{
    /// <summary>
    /// One piece of vegetation a grass tuft may shelter beside: where it stands (its pivot — the trunk base
    /// of a tree, the root of a shrub) and how far from it a tuft still reads as growing IN it.
    /// </summary>
    public readonly struct VegetationAnchor
    {
        public readonly Vector2 Position;
        public readonly float Radius;

        public VegetationAnchor(Vector2 position, float radius)
        {
            Position = position;
            Radius = Mathf.Max(0f, radius);
        }
    }

    /// <summary>
    /// "Is this point inside any tree's or shrub's shelter?" answered with a coarse bucket grid, so a field
    /// of 34,000 sites asks a few anchors each instead of all 800. Built once per meadow rebuild; nothing
    /// here runs per frame.
    /// </summary>
    public sealed class VegetationAnchorIndex
    {
        readonly Dictionary<long, List<VegetationAnchor>> _buckets = new Dictionary<long, List<VegetationAnchor>>();
        readonly float _bucket;

        /// <summary>How many anchors the index holds.</summary>
        public int Count { get; }

        public VegetationAnchorIndex(IReadOnlyList<VegetationAnchor> anchors)
        {
            float widest = 0f;
            if (anchors != null)
                for (int i = 0; i < anchors.Count; i++) widest = Mathf.Max(widest, anchors[i].Radius);
            // A bucket as wide as the widest shelter means a point only ever has to look one bucket out.
            _bucket = Mathf.Max(widest, 1e-3f);

            if (anchors == null) return;
            int count = 0;
            for (int i = 0; i < anchors.Count; i++)
            {
                var a = anchors[i];
                if (a.Radius <= 0f) continue;
                long key = Key(Mathf.FloorToInt(a.Position.x / _bucket), Mathf.FloorToInt(a.Position.y / _bucket));
                if (!_buckets.TryGetValue(key, out var list)) _buckets[key] = list = new List<VegetationAnchor>();
                list.Add(a);
                count++;
            }
            Count = count;
        }

        /// <summary>True when <paramref name="p"/> lies within some anchor's own radius (edge inclusive).</summary>
        public bool IsNear(Vector2 p)
        {
            if (Count == 0) return false;
            int bx = Mathf.FloorToInt(p.x / _bucket), by = Mathf.FloorToInt(p.y / _bucket);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (!_buckets.TryGetValue(Key(bx + dx, by + dy), out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                    if ((list[i].Position - p).sqrMagnitude <= list[i].Radius * list[i].Radius) return true;
            }
            return false;
        }

        static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
    }

    /// <summary>
    /// <b>THE KEEP RULE for the old grass tufts</b> (owner, 2026-11-15: "remove most of the old grass
    /// sprites … some areas can probably keep it … in some trees or mixed amongst other vegetation").
    ///
    /// <para>A baked tuft survives only if ALL of these hold, cheapest test first:</para>
    /// <list type="number">
    /// <item><b>The density cap.</b> Its own stable keep roll (<see cref="GrassFieldScatter.KeepRoll"/>,
    /// hashed from the site, never <c>Random</c>) is under the rule's keep fraction — so even where grass may
    /// stay, only that share of it does.</item>
    /// <item><b>Shelter.</b> It stands within a tree's or a shrub's radius (<see cref="VegetationAnchorIndex"/>)
    /// — open sand, mud, rock, roads and wharves carry no trees, so they keep nothing.</item>
    /// <item><b>Dry ground.</b> When the rule names a ground height source, the ground under it is at or
    /// above the rule's floor — the belt-and-braces that keeps a tuft near a shoreline shrub off the
    /// strand and out of the water.</item>
    /// </list>
    /// <para>Pure: no scene, no <c>Random</c>, no <c>Time</c>. Every number it compares against comes in
    /// from the caller, who reads it off a <see cref="GrassThinningDef"/> (rule 6).</para>
    /// </summary>
    public static class GrassThinning
    {
        /// <param name="position">The tuft's world position (its derived site).</param>
        /// <param name="keepRoll">The site's stable 0..1 keep roll.</param>
        /// <param name="keepFraction">Share of sheltered tufts that survive, 0..1.</param>
        /// <param name="anchors">Trees and shrubs. Null or empty keeps nothing.</param>
        /// <param name="ground">Ground height source, or null to skip the dry-ground test.</param>
        /// <param name="minElevationMetres">The lowest ground (m above datum) a tuft may stand on.</param>
        public static bool Keeps(Vector2 position, float keepRoll, float keepFraction,
                                 VegetationAnchorIndex anchors, ITidalTerrain ground, float minElevationMetres)
        {
            if (keepRoll >= Mathf.Clamp01(keepFraction)) return false;
            if (anchors == null || !anchors.IsNear(position)) return false;
            if (ground != null && !(ground.ElevationAt(position) >= minElevationMetres)) return false;
            return true;
        }
    }
}
