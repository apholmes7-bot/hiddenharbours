#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;                 // SpriteLightMath — the shared bake camera's squash

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The ONE build path for a baked shop FIXTURE — the fixture family's answer to
    /// <see cref="ShopCatalog"/>, and deliberately the same shape, so a counter a builder stands up and
    /// a counter the owner drags into a scene are the same object.
    ///
    /// <para>Everything comes from <c>shopFixtures.contract.json</c>, which the bake wrote: the cell,
    /// the ground-centre pivot, the footprint in metres, and the per-facing service-face anchors. There
    /// is no table here to drift from the rig.</para>
    ///
    /// <para><b>The three rules a baked prop lives by</b>, and they are the building kits' hard-won
    /// ones: the pivot is the ground CENTRE and nothing may offset it; turning one means swapping the
    /// sub-sprite, never rotating the transform, because the art is baked at a fixed camera; and its
    /// drawn size is not its metric size — the honest number is the FOOTPRINT.</para>
    /// </summary>
    public static class ShopFixtureCatalog
    {
        /// <summary>Seed sorting order for a walk-up fixture. <c>YSortSprite</c> OWNS the final value —
        /// this is only what it shows before that runs. Matches the placeholder counter's 3 so the
        /// pre-Play scene view does not change meaning with this swap.</summary>
        public const int SortingOrder = 3;

        /// <summary>One placeable fixture: its contract entry, and the questions a placer asks of it.</summary>
        public readonly struct Placement
        {
            public readonly ShopFixtureKit.Entry Entry;

            public Placement(ShopFixtureKit.Entry entry) { Entry = entry; }

            public bool IsValid => Entry != null;
            public string Key => Entry?.key;
            public string Stem => Entry?.Stem;
            public string SheetPath => Entry == null ? null : ShopFixtureKit.SheetPath(Entry.key);

            /// <summary>Footprint in GROUND metres, as the rig reports it: <c>x</c> across the service
            /// face, <c>y</c> front-to-back. Unsquashed.</summary>
            public Vector2 FootprintMetres => Entry == null
                ? Vector2.zero
                : new Vector2(Entry.footprintWidthMetres, Entry.footprintDepthMetres);

            public string Label => Entry == null
                ? "(none)"
                : $"{Entry.label} — {FootprintMetres.x:0.##} × {FootprintMetres.y:0.##} m";
        }

        public static List<Placement> Scan()
        {
            var list = new List<Placement>();
            var c = ShopFixtureKit.Load();
            if (c?.fixtures != null) foreach (var e in c.fixtures) list.Add(new Placement(e));
            return list;
        }

        /// <summary>The fixture baked under <paramref name="key"/> (<c>counter_generalStore</c>).</summary>
        public static Placement Find(string key) => new Placement(ShopFixtureKit.Load()?.Find(key));

        /// <summary>The fixture baked for a trade, by rig fixture id — the call a placer that knows
        /// "the general store's counter" makes without having to build the key by hand.</summary>
        public static Placement Find(string fixture, string trade) =>
            Find($"{fixture}_{trade}");

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

            string want = placement.Entry.SpriteName(facing);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(placement.SheetPath))
                if (asset is Sprite s && string.Equals(s.name, want, StringComparison.Ordinal))
                    return s;
            return null;
        }

        /// <summary>
        /// Stand a fixture on a GameObject: renderer, unit scale, pinned rotation, Y-sort. The single
        /// build path — the same rule <see cref="ShopCatalog.Configure"/> sets for a shell.
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

            // ⚠️ And the colour, because this replaces a TINTED placeholder. A leftover tint would
            // multiply through the baked art and nothing would report it.
            sr.color = Color.white;

            // Honest metric size: the rig baked the camera's foreshortening in at 32 px = 1 m, so
            // scaling to the footprint metres would break the pixel grid and the scale ladder together.
            go.transform.localScale = Vector3.one;

            // Baked at a FIXED camera — rotating the transform would tip the projection. Turning is
            // SetFacing's job.
            go.transform.localRotation = Quaternion.identity;

            if (ySort && go.GetComponent<YSortSprite>() == null) go.AddComponent<YSortSprite>();

            return sr;
        }

        /// <summary>Turn an already-configured fixture to another facing — the ONLY way to rotate one.
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
        /// Which facing turns this fixture's SERVICE FACE toward <paramref name="target"/> — the side a
        /// customer stands at.
        ///
        /// <para>Measured from the bake's own anchors by <see cref="BuildingFacing"/>, never derived
        /// from a cell index. It is the same call that aims a door, on purpose: the contract's
        /// <c>frontX</c>/<c>frontY</c> carry the service face in exactly the shape and frame
        /// <c>ShopKit.Entry.doorX</c>/<c>doorY</c> carry a doorway, so there is one piece of sign
        /// reasoning in the repo rather than two — and the second copy is where #495 found the village
        /// kit pointing a schoolhouse 92° away from the green it faces.</para>
        /// </summary>
        public static int FacingToward(Placement p, Vector2 from, Vector2 target) =>
            !p.IsValid ? 0
                       : BuildingFacing.FacingToward(p.Entry.frontY, p.Entry.pivotY, p.Entry.facings,
                                                     SpriteLightMath.GroundDepthScale, from, target);

        /// <summary>How far off, in degrees, a facing points the service face from the target — the
        /// honest error, for a test to bound rather than re-deriving the choice and finding it agrees
        /// with itself.</summary>
        public static float FacingErrorDegrees(Placement p, Vector2 from, Vector2 target, int facing) =>
            !p.IsValid ? 0f
                       : BuildingFacing.DoorErrorDegrees(p.Entry.frontY, p.Entry.pivotY, p.Entry.facings,
                                                         SpriteLightMath.GroundDepthScale, from, target,
                                                         facing);

        /// <summary>The half-diagonal of a footprint in metres: the ground a fixture reserves at ANY
        /// facing. A circle rather than a rectangle, for the building kits' reason — the facing is
        /// derived from where the fixture is looking, and a quarter-turned one presents its diagonal.</summary>
        public static float FootprintRadiusMetres(Placement p) =>
            !p.IsValid ? 0f
                       : 0.5f * Mathf.Sqrt(p.FootprintMetres.x * p.FootprintMetres.x +
                                           p.FootprintMetres.y * p.FootprintMetres.y);
    }
}
#endif
