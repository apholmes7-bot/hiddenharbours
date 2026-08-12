#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;                 // SpriteLightMath — the shared bake camera's squash

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The ONE build path for a baked shop — the shop kit's answer to
    /// <see cref="VillageBuildingCatalog"/>, and deliberately the same shape, so a shop the owner drags
    /// into a scene and a shop a region builder instantiates are the same object.
    ///
    /// <para>Everything comes from <c>shops.contract.json</c>, which the bake wrote: the cell, the
    /// ground-centre pivot, the footprint in metres, and the per-facing door anchors. There is no table
    /// here to drift from the rig.</para>
    ///
    /// <para><b>A shop is TWO sheets, not one</b>, and that is the difference from the village kit. The
    /// <see cref="Shell"/> is the closed exterior and the <see cref="Level"/> is its floor plan, baked at
    /// the same size on the same ground point. <see cref="ShopKit.Contract.shellFacingOffset"/> is
    /// <b>0</b> — MEASURED every bake — so a level is shown at the SAME facing as its shell.
    /// ⚠️ The house family's answer is 4 and carrying it here would put every doorway against a back
    /// wall; <see cref="InteriorFacingFor"/> exists so no caller has to know which kit it is holding.</para>
    ///
    /// <para><b>Three things about a baked building this file exists to keep right</b>, all of them the
    /// village kit's hard-won lessons: the pivot is the ground CENTRE and nothing may offset it; turning
    /// one means swapping the sub-sprite, never rotating the transform, because the art is baked at a
    /// fixed camera; and its drawn size is not its metric size — the honest number is the FOOTPRINT.</para>
    /// </summary>
    public static class ShopCatalog
    {
        /// <summary>Seed sorting order for a shop shell. <c>YSortSprite</c> OWNS the final value — this
        /// is only what it shows before that runs. Matches the village kit's seed so the two read alike
        /// in a scene that has both.</summary>
        public const int SortingOrder = 4;

        /// <summary>The room sheet's order: one below <c>YSortSprite</c>'s safe band, so no occupant can
        /// be sorted behind the floor they are standing on. Same value and same reason as
        /// <see cref="InteriorCatalog.RoomSortingOrder"/>.</summary>
        public const int RoomSortingOrder = 1;

        // =================================================================================
        //  what is placeable
        // =================================================================================

        /// <summary>One placeable sheet: its contract entry, and the questions a placer asks of it.</summary>
        public readonly struct Placement
        {
            public readonly ShopKit.Entry Entry;

            public Placement(ShopKit.Entry entry) { Entry = entry; }

            public bool IsValid => Entry != null;
            public string Key => Entry?.key;

            /// <summary>Empty for a shell, <c>ground</c>/<c>upper</c> for a level.</summary>
            public string Level => Entry?.level ?? "";

            public bool IsShell => string.IsNullOrEmpty(Level);

            public string Stem => ShopKit.StemFor(Entry);
            public string SheetPath => ShopKit.SheetPathFor(Entry);

            /// <summary>Footprint in GROUND metres, as the rig reports it. Unsquashed — the world squash
            /// is <c>InteriorFootprint</c>'s job, not this one's.</summary>
            public Vector2 FootprintMetres => Entry == null
                ? Vector2.zero
                : new Vector2(Entry.footprintWidthMetres, Entry.footprintLengthMetres);

            public string Label => Entry == null
                ? "(none)"
                : $"{Entry.label} — {FootprintMetres.x:0.##} × {FootprintMetres.y:0.##} m";
        }

        public static List<Placement> ScanShells()
        {
            var list = new List<Placement>();
            var c = ShopKit.Load();
            if (c?.shells != null) foreach (var e in c.shells) list.Add(new Placement(e));
            return list;
        }

        public static List<Placement> ScanLevels()
        {
            var list = new List<Placement>();
            var c = ShopKit.Load();
            if (c?.levels != null) foreach (var e in c.levels) list.Add(new Placement(e));
            return list;
        }

        public static Placement FindShell(string key) => new Placement(ShopKit.FindShell(ShopKit.Load(), key));

        public static Placement FindLevel(string key, string level) =>
            new Placement(ShopKit.FindLevel(ShopKit.Load(), key, level));

        // =================================================================================
        //  loading and configuring
        // =================================================================================

        /// <summary>
        /// One facing's sprite, BY NAME off the sliced sheet. Returns null (never throws) when the sheet
        /// is missing or unsliced.
        ///
        /// <para>⚠️ By name, and the name is the contract's, because a sheet can be Multiple-mode and
        /// still not be sliced the way this kit needs — Unity's automatic alpha slicing produces the
        /// right COUNT of sprites with the wrong cells and a thrown-away pivot. A lookup by index would
        /// happily find one of those.</para>
        /// </summary>
        public static Sprite LoadFacing(Placement placement, int facing)
        {
            if (!placement.IsValid) return null;

            string want = ShopKit.SpriteNameFor(placement.Entry, facing);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(placement.SheetPath))
                if (asset is Sprite s && string.Equals(s.name, want, StringComparison.Ordinal))
                    return s;
            return null;
        }

        /// <summary>
        /// Stand a shell (or a level) on a GameObject: renderer, unit scale, pinned rotation, Y-sort.
        /// The single build path — the same rule <see cref="VillageBuildingCatalog.Configure"/> sets.
        /// </summary>
        public static SpriteRenderer Configure(GameObject go, Placement placement, Sprite sprite,
                                               int sortingOrder = SortingOrder, bool ySort = true)
        {
            if (go == null) throw new ArgumentNullException(nameof(go));

            if (!placement.IsValid)
                throw new ArgumentException(
                    "Configure was handed a placement with no contract entry, so there is no pivot, no " +
                    "footprint and no facing count to give it. Placing on a plausible default is exactly " +
                    "the reading this kit's contract replaces — fix the caller.",
                    nameof(placement));

            // ⚠️ `GetComponent<T>() ?? AddComponent<T>()` compiles and is wrong: `??` tests the real C#
            // reference and never reaches UnityEngine.Object's overloaded `==`, so a "fake null"
            // satisfies it and the first field write throws. Compare with `== null`.
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();

            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;

            // Honest metric size: the rig baked the camera's foreshortening in at 32 px = 1 m, so scaling
            // to the footprint metres would break the pixel grid and the scale ladder together.
            go.transform.localScale = Vector3.one;

            // Baked at a FIXED camera — rotating the transform would tip the projection. Turning is
            // SetFacing's job.
            go.transform.localRotation = Quaternion.identity;

            if (ySort && go.GetComponent<YSortSprite>() == null) go.AddComponent<YSortSprite>();

            return sr;
        }

        /// <summary>Turn an already-configured shop to another facing — the ONLY way to rotate one.
        /// Returns false and changes nothing if that facing has no sprite.</summary>
        public static bool SetFacing(GameObject go, Placement placement, int facing)
        {
            if (go == null || !placement.IsValid) return false;

            Sprite sprite = LoadFacing(placement, facing);
            if (sprite == null) return false;

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) return false;

            sr.sprite = sprite;
            return true;
        }

        // =================================================================================
        //  the questions a placer asks
        // =================================================================================

        /// <summary>
        /// The INTERIOR facing that stands under a given exterior facing. Reads the contract's MEASURED
        /// <see cref="ShopKit.Contract.shellFacingOffset"/>, which is 0 for this kit.
        ///
        /// <para>⚠️ It exists precisely so no caller writes the 0 in by hand. The house family's is 4,
        /// the two kits genuinely differ, and both numbers are measurements — a caller that "knows" the
        /// answer is a caller that will carry the wrong one across.</para>
        /// </summary>
        public static int InteriorFacingFor(int exteriorFacing, int facings = ShopKit.Facings)
        {
            int offset = ShopKit.Load()?.shellFacingOffset ?? 0;
            int n = Mathf.Max(1, facings);
            return ((exteriorFacing + offset) % n + n) % n;
        }

        /// <summary>The half-diagonal of a footprint in metres: the ground a shop reserves at ANY facing.
        /// A circle rather than a rectangle, for the village kit's reason — the facing is derived from
        /// where the building is looking, and a quarter-turned building presents its diagonal.</summary>
        public static float FootprintRadiusMetres(Placement p) =>
            !p.IsValid ? 0f
                       : 0.5f * Mathf.Sqrt(p.FootprintMetres.x * p.FootprintMetres.x +
                                           p.FootprintMetres.y * p.FootprintMetres.y);

        /// <summary>Which facing turns this shop's door toward <paramref name="target"/> — measured from
        /// the bake's own anchors by <see cref="BuildingFacing"/>, never derived from a cell index.</summary>
        public static int FacingToward(Placement p, Vector2 from, Vector2 target) =>
            !p.IsValid ? 0
                       : BuildingFacing.FacingToward(p.Entry.doorY, p.Entry.pivotY, p.Entry.facings,
                                                     SpriteLightMath.GroundDepthScale, from, target);

        /// <summary>
        /// Where the street door sits in the building's own model frame, in metres: <c>x</c> across the
        /// front wall from its centre, <c>y</c> front-to-back.
        ///
        /// <para>The interior's walls need both halves of this. ⚠️ On two of the three shops the door is
        /// NOT centred — the post office's is 1.68 m off and the restaurant's 2.52 m — so a doorway gap
        /// cut at the middle of the wall would block the door the player can see and open a hole in the
        /// wall beside it.</para>
        /// </summary>
        public static Vector2 DoorModelMetres(Placement p)
        {
            if (!p.IsValid) return Vector2.zero;
            var c = ShopKit.Load();
            float ppu = Mathf.Max(1, c?.ppu ?? 32);
            return BuildingFacing.DoorModelMetres(p.Entry.doorX, p.Entry.doorY,
                                                  p.Entry.pivotX, p.Entry.pivotY,
                                                  ppu, SpriteLightMath.GroundDepthScale);
        }
    }
}
#endif
