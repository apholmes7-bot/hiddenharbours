#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The PLACEMENT half of the village building kit — "what buildings can I put in a scene, which
    /// sprite is which facing, and what does one of them look like once it is standing there?" The
    /// sibling of <see cref="AcadianTreeCatalog"/>, and the one place
    /// <see cref="VillageBuildingPrefabBuilder"/>, a future paint/placement tool and the tests go
    /// through, so they cannot drift from each other.
    ///
    /// <para><b>It owns no numbers.</b> Cell, pivot, facing convention and footprint metres all come from
    /// <see cref="VillageBuildingKit"/> reading the bake's own <c>Buildings.json</c>. This type only
    /// answers questions about SPRITES and SCENE OBJECTS. Splitting it that way keeps the bake lane's
    /// file separate from the placement lane's — the same split the tree kit uses.</para>
    ///
    /// <para><b>Three things about a building that are not true of other decor in this repo:</b></para>
    /// <list type="bullet">
    ///   <item><b>It does not pivot bottom-centre, and the gap is metres.</b> The rig's pivot is the
    ///   ground CENTRE — the middle of the footprint — and the ¾ camera projects the whole near half of
    ///   that footprint below it (3.3–6.5 m across the M1 set). The pivot is baked into the sliced
    ///   sub-sprite so nothing here re-derives it, but anything that "corrects" a building to
    ///   <c>{0.5, 0}</c> stands it that far into the dirt and it reads as an art bug.
    ///
    ///   <para>A consequence worth knowing before placing a village: <see cref="YSortSprite"/> sorts by
    ///   the transform's Y, which for these is the footprint CENTRE, not the near wall. That is correct
    ///   for a player walking in front of or behind a building, and it is a coin flip for one standing
    ///   level with its middle — inherent to Y-sorting, not to this pivot.
    ///   <see cref="NearEdgeOffsetMetres"/> is the number a placement tool would feed
    ///   <c>YSortSprite</c>'s pivot-Y offset if that ever matters.</para></item>
    ///   <item><b>It has EIGHT facings and no rotation.</b> Turning a building means swapping the
    ///   sub-sprite, never rotating the transform — the art is baked at a fixed camera, so a rotated
    ///   SpriteRenderer would tip the whole projection. <see cref="SetFacing"/> is the only way to
    ///   turn one.</item>
    ///   <item><b>Its drawn height is NOT its height in metres.</b> Same as the trees: the rig bakes the
    ///   camera's foreshortening in, so <c>localScale</c> stays 1 and the honest metric number to check
    ///   is the FOOTPRINT (<see cref="Placement.FootprintMetres"/>), which the azimuth probe already
    ///   cross-checked against the rendered silhouette at 32 px = 1 m.</item>
    /// </list>
    /// </summary>
    public static class VillageBuildingCatalog
    {
        /// <summary>Where the built prefabs land. Under the decor prefab tree beside
        /// <c>Prefabs/Decor/Buildings</c> (the loose greybox set <see cref="DecorPrefabBuilder"/>
        /// builds) so neither builder can overwrite the other's output.</summary>
        public const string PrefabRoot = "Assets/_Project/Prefabs/Decor/Buildings/Village";

        /// <summary>
        /// Pre-YSort sorting order. <see cref="YSortSprite"/> OWNS the final value (it recomputes from
        /// world Y on enable); this is only what a building shows before that runs. Matches
        /// <see cref="DecorPrefabBuilder"/>'s building default so the two sets read alike — and sits
        /// inside YSortSprite's decor band (SortingBands.DecorFloor upward), above the wharf deck's −4..1 so a building on a
        /// wharf never sinks into the planking.
        /// </summary>
        public const int SortingOrder = 4;

        // =================================================================================
        // what is placeable
        // =================================================================================

        /// <summary>One placeable building: its contract entry, and the questions a placement tool asks
        /// about it.</summary>
        public readonly struct Placement
        {
            public readonly VillageBuildingKit.Entry Entry;

            public Placement(VillageBuildingKit.Entry entry) { Entry = entry; }

            public bool IsValid => Entry != null;

            public string Key => Entry.key;

            /// <summary>The sheet's stem — also the prefab name and the sprite-name prefix.</summary>
            public string Stem => VillageBuildingKit.StemFor(Entry.key);

            public string SheetPath => VillageBuildingKit.SheetPath(Entry.key);

            /// <summary>Footprint in metres, <c>(width, length)</c> — the rig's own <c>Wd</c>/<c>Ln</c>.
            /// What a placement tool spaces a village by, and what makes the P2 scale ladder honest.</summary>
            public Vector2 FootprintMetres =>
                new Vector2(Entry.footprintWidthMetres, Entry.footprintLengthMetres);

            /// <summary>The facing whose door faces the camera — DERIVED from the baked door anchors,
            /// not declared. See <see cref="VillageBuildingKit.FrontFacing"/>.</summary>
            public int FrontFacing => VillageBuildingKit.FrontFacing(Entry);

            /// <summary>For a tool dropdown and the scene hierarchy: "General store — 7.1 × 8.9 m". The
            /// footprint is the number the owner is being asked to judge, so it is in the label.</summary>
            public string Label =>
                $"{Entry.label} — {Entry.footprintWidthMetres:0.#} × {Entry.footprintLengthMetres:0.#} m";
        }

        /// <summary>
        /// Every placeable building the committed contract claims, in the kit's own order (the school and
        /// the store first, then the three houses — the order <see cref="VillageBuildingKit.M1Set"/>
        /// declares, which is the order a village reads in). Returns empty (never throws) when the
        /// contract is missing, so a tool can say so rather than fall over.
        /// </summary>
        public static List<Placement> Scan()
        {
            var list = new List<Placement>();
            VillageBuildingKit.Contract contract = VillageBuildingKit.Load();
            if (contract?.buildings == null) return list;

            foreach (var entry in contract.buildings)
                if (entry != null) list.Add(new Placement(entry));
            return list;
        }

        /// <summary>One building by key, or an invalid placement if the bake did not cover it.</summary>
        public static Placement Find(string key)
        {
            foreach (var p in Scan())
                if (string.Equals(p.Key, key, StringComparison.Ordinal)) return p;
            return default;
        }

        // =================================================================================
        // sprites
        // =================================================================================

        /// <summary>
        /// One facing's sprite, matched BY NAME (never by <see cref="AssetDatabase.LoadAllAssetsAtPath"/>'s
        /// array order, which Unity does not promise). The sheets import spriteMode MULTIPLE, so a plain
        /// <c>LoadAssetAtPath&lt;Sprite&gt;</c> returns null for them.
        ///
        /// <para>Returns null (no throw) when the sheet is missing or unsliced, so a partial art state
        /// still places what it can.</para>
        /// </summary>
        public static Sprite LoadFacing(Placement placement, int facing)
        {
            if (!placement.IsValid || facing < 0 || facing >= placement.Entry.facings) return null;

            string wanted = VillageBuildingKit.SpriteNameFor(placement.Entry.key, facing);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(placement.SheetPath))
                if (o is Sprite s && string.Equals(s.name, wanted, StringComparison.Ordinal))
                    return s;
            return null;
        }

        /// <summary>Every facing of a building, in order, with any that failed to load dropped.</summary>
        public static List<Sprite> LoadAllFacings(Placement placement)
        {
            var sprites = new List<Sprite>();
            if (!placement.IsValid) return sprites;
            for (int f = 0; f < placement.Entry.facings; f++)
            {
                var s = LoadFacing(placement, f);
                if (s != null) sprites.Add(s);
            }
            return sprites;
        }

        // =================================================================================
        // the canonical scene object
        // =================================================================================

        /// <summary>
        /// Turn <paramref name="go"/> into the canonical village building: a
        /// <see cref="SpriteRenderer"/> on the default sprite material, and a <see cref="YSortSprite"/>
        /// so it layers around the player by world Y. Nothing else.
        ///
        /// <para><b>Both the prefab builder and any placement tool go through here</b>, so a building the
        /// owner drags in and one a builder instantiates are the same object.</para>
        ///
        /// <para><b>What is deliberately absent.</b> No custom material — the buildings have no shader of
        /// their own, and there is no lighting/mask work in this kit (the tree kit's mask/normal channels
        /// have no building equivalent yet). No <c>ReflectiveObject</c>: the default sprite material
        /// carries no <c>HHReflect</c> pass, so one would join the reflective set and draw nothing —
        /// the same reason <see cref="DecorPrefabBuilder"/> withholds it from its buildings. No
        /// collider, no door trigger, no interaction: that is the gameplay lane's call, after
        /// placement.</para>
        ///
        /// <para>⚠️ <paramref name="sortingOrder"/> is a SEED, not the final value.
        /// <see cref="YSortSprite"/> recomputes the order from world Y the moment it is enabled, so this
        /// is only what the building shows before that runs (measured: a seeded 4 comes back as 10, the
        /// band's value at Y = 0). To change how a building layers, move it — or change the
        /// <c>YSortSprite</c> fields, which own the mapping.</para>
        ///
        /// <para>Returns the renderer for callers that want to tint or re-face it.</para>
        /// </summary>
        public static SpriteRenderer Configure(GameObject go, Placement placement, Sprite sprite,
                                               int sortingOrder = SortingOrder)
        {
            if (go == null) throw new ArgumentNullException(nameof(go));

            if (!placement.IsValid)
                throw new ArgumentException(
                    "Configure was handed a placement with no contract entry, so there is no pivot, no " +
                    "footprint and no facing count to give it. Placing on a plausible default is " +
                    "exactly the reading this kit's contract replaces — fix the caller.",
                    nameof(placement));

            // ⚠️ `GetComponent<T>() ?? AddComponent<T>()` LOOKS right and is wrong: `??` tests the real
            // C# reference, so it never reaches UnityEngine.Object's overloaded `==` and a "fake null"
            // component satisfies it. AddComponent is then skipped and the first field write throws
            // MissingComponentException. Always compare with `== null`.
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();

            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;

            // Honest metric size (P2, and the art-pipeline charter's first guardrail). The rig baked the
            // camera's foreshortening in at 32 px = 1 m, so scaling to the footprint metres is the
            // tempting wrong move — it would break the pixel grid and the scale ladder in one go.
            go.transform.localScale = Vector3.one;

            // A building is baked at a FIXED camera. Rotating the transform would tip the projection, so
            // the rotation is pinned here and turning is SetFacing's job.
            go.transform.localRotation = Quaternion.identity;

            if (go.GetComponent<YSortSprite>() == null) go.AddComponent<YSortSprite>();

            return sr;
        }

        /// <summary>
        /// Turn an already-configured building to another facing — the ONLY way to rotate one, because
        /// the art is baked at a fixed camera and a rotated transform would tip the whole projection.
        ///
        /// <para>Returns false and leaves the object alone if that facing's sprite is missing, so a
        /// partial art state cannot blank a placed building.</para>
        /// </summary>
        public static bool SetFacing(GameObject go, Placement placement, int facing)
        {
            if (go == null || !placement.IsValid) return false;

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) return false;

            var sprite = LoadFacing(placement, facing);
            if (sprite == null) return false;

            sr.sprite = sprite;
            return true;
        }

        /// <summary>
        /// Which facing a placed building is currently showing, read back from its sprite name, or −1 if
        /// it is not one of this building's facings. Lets a placement tool round-trip a heading without
        /// a runtime component to serialise.
        /// </summary>
        public static int FacingOf(GameObject go, Placement placement)
        {
            if (go == null || !placement.IsValid) return -1;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return -1;

            for (int f = 0; f < placement.Entry.facings; f++)
                if (string.Equals(sr.sprite.name,
                                  VillageBuildingKit.SpriteNameFor(placement.Entry.key, f),
                                  StringComparison.Ordinal))
                    return f;
            return -1;
        }

        // =================================================================================
        // measurements a tool reports (all derived, none stored)
        // =================================================================================

        /// <summary>The full drawn width of a cell in metres — what a lineup spaces buildings by so
        /// their roofs do not overlap. Wider than the footprint, because the roof overhang and the
        /// quarter-turn diagonal both draw outside the wall box.</summary>
        public static float DrawnWidthMetres(VillageBuildingKit.Entry entry, int ppu) =>
            entry == null || ppu <= 0 ? 0f : (float)entry.cellW / ppu;

        /// <summary>How tall the building DRAWS above its ground line, in metres at PPU 32 — what the
        /// owner sees, as opposed to the footprint, which is how big it IS.</summary>
        public static float DrawnHeightMetres(VillageBuildingKit.Entry entry, int ppu) =>
            entry == null || ppu <= 0 ? 0f : entry.pivotY / ppu;

        /// <summary>How far below the footprint's centre the drawn art reaches, in metres — the exact
        /// distance a bottom-centre pivot would sink this building.</summary>
        public static float BelowGroundMetres(VillageBuildingKit.Entry entry, int ppu) =>
            entry == null || ppu <= 0 ? 0f : (float)VillageBuildingKit.BelowGroundPad(entry) / ppu;

        /// <summary>
        /// How far toward the camera the building's near wall sits from its pivot, in world metres —
        /// half the footprint length. The offset a placement tool would give
        /// <see cref="YSortSprite"/>'s pivot-Y term if it ever wanted a building to sort by its near
        /// wall instead of its middle.
        ///
        /// <para>Deliberately NOT applied here: the correct value varies by facing (a quarter-turn is
        /// deeper than face-on), and a wrong sort offset is worse than an honest centre one. Exposed so
        /// the number comes from the bake if world-content decides it wants it.</para>
        /// </summary>
        public static float NearEdgeOffsetMetres(VillageBuildingKit.Entry entry) =>
            entry == null ? 0f : entry.footprintLengthMetres * 0.5f;

        /// <summary>PPU from the bake's own contract — the number the sheets were baked at, not the
        /// import default, so the two cannot disagree silently.</summary>
        public static int PixelsPerUnit()
        {
            VillageBuildingKit.Contract contract = VillageBuildingKit.Load();
            return contract != null && contract.ppu > 0
                ? contract.ppu
                : (int)ArtImportPipeline.PixelsPerUnit;
        }

        /// <summary>
        /// A one-line report of what is placeable, for a tool's info box: how many buildings, the
        /// footprint spread, and how far bottom-centre would sink them — which is the whole reason the
        /// pivot comes from the contract.
        /// </summary>
        public static string Summary(IReadOnlyList<Placement> placements)
        {
            if (placements == null || placements.Count == 0) return "No village buildings baked.";

            int ppu = PixelsPerUnit();
            float smallest = placements.Min(p => p.Entry.footprintWidthMetres);
            float largest = placements.Max(p => p.Entry.footprintLengthMetres);
            float sinkLo = placements.Min(p => BelowGroundMetres(p.Entry, ppu));
            float sinkHi = placements.Max(p => BelowGroundMetres(p.Entry, ppu));

            return $"{placements.Count} baked building(s), {smallest:0.#}–{largest:0.#} m across " +
                   $"{placements[0].Entry.facings} facings each; each pivots on its GROUND CENTRE — " +
                   $"bottom-centre would sink them {sinkLo:0.00}–{sinkHi:0.00} m.";
        }
    }
}
#endif
