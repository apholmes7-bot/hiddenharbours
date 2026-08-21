#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The ONE build path for a baked dooryard piece — the boundary family's answer to
    /// <see cref="ShopFixtureCatalog"/>, and deliberately the same shape.
    ///
    /// <para>Everything comes from <c>yardIso.contract.json</c>, which the bake wrote: the cell, the
    /// ground-centre pivot, the panel's own metres, and — the one this kit turns on — the MEASURED
    /// per-cell face bearings. There is no table here to drift from the rig.</para>
    ///
    /// <para><b>⭐⭐ THE FACING COMES FROM THE MEASURED BEARINGS, NEVER FROM A LABEL.</b> The rig's
    /// gameplay sidecar publishes <c>contract.facings = [N,NE,E,SE,S,SW,W,NW]</c> against its own
    /// <c>dir</c> index, and that list is MIRRORED against the art: ask it for "E", take dir 2, and
    /// draw a panel facing WEST — a plausible picture, with no error anywhere. <see cref="FacingFor"/>
    /// reads the bake's own measured <c>cellFaceBearings</c> instead, so the mirrored label cannot
    /// reach a placed fence even if someone re-emits it wrongly again. (The bake refuses outright if
    /// those bearings stop being a clockwise compass — see <c>YardRegistrationProbe</c>.)</para>
    ///
    /// <para><b>The three rules a baked prop lives by</b>, the building kits' hard-won ones: the pivot
    /// is the ground CENTRE and nothing may offset it; turning one means swapping the sub-sprite, never
    /// rotating the transform, because the art is baked at a fixed camera; and its drawn size is not
    /// its metric size — the honest number is the FOOTPRINT.</para>
    /// </summary>
    public static class YardCatalog
    {
        /// <summary>Seed sorting order for a boundary piece. <c>YSortSprite</c> OWNS the final value —
        /// this is only what it shows before that runs, and it matches the shop fixtures' so a
        /// pre-Play scene view reads the same.</summary>
        public const int SortingOrder = 3;

        /// <summary>One placeable piece: its contract entry, and the questions a placer asks of it.
        /// </summary>
        public readonly struct Placement
        {
            public readonly YardKit.Entry Entry;

            public Placement(YardKit.Entry entry) { Entry = entry; }

            public bool IsValid => Entry != null;
            public string Key => Entry?.key;
            public string Stem => Entry?.Stem;
            public string SheetPath => Entry == null ? null : YardKit.SheetPath(Entry.key);

            /// <summary>Footprint in GROUND metres, as the rig reports it AT THE BAKED LEN:
            /// <c>x</c> ALONG the run, <c>y</c> through the fence. Unsquashed.</summary>
            public Vector2 FootprintMetres => Entry == null
                ? Vector2.zero
                : new Vector2(Entry.footprintWidthMetres, Entry.footprintDepthMetres);

            /// <summary>How long one panel is along the boundary — the number a run is tiled on.
            /// </summary>
            public float RunMetres => Entry?.footprintWidthMetres ?? 0f;

            public string Label => Entry == null
                ? "(none)"
                : $"{Entry.label} — {FootprintMetres.x:0.##} × {FootprintMetres.y:0.##} m, " +
                  $"kept {Entry.kept:0.00}";
        }

        // -------------------------------------------------------------------------------------
        //  finding a piece
        // -------------------------------------------------------------------------------------

        public static List<Placement> Scan()
        {
            var list = new List<Placement>();
            var c = YardKit.Load();
            if (c?.pieces != null) foreach (var e in c.pieces) list.Add(new Placement(e));
            return list;
        }

        /// <summary>The piece called <paramref name="key"/>, or an invalid placement. Null-tolerant on
        /// purpose: "declared in the build table" and "has pixels on disk" are different questions, and
        /// a region should dress with what has landed rather than throw halfway through a build.
        /// </summary>
        public static Placement Find(string key) => new Placement(YardKit.Load()?.Find(key));

        /// <summary>The piece called <paramref name="key"/> out of an ALREADY-LOADED contract — the
        /// form a placer wants, because a fence walk asks for the same three pieces a few hundred times
        /// and <see cref="YardKit.Load"/> re-reads the file each call.</summary>
        public static Placement Find(YardKit.Contract contract, string key) =>
            new Placement(contract?.Find(key));

        // -------------------------------------------------------------------------------------
        //  the facing
        // -------------------------------------------------------------------------------------

        /// <summary>
        /// The cell whose FACE points nearest <paramref name="bearingDegrees"/> — degrees clockwise
        /// from north, the frame the whole world is measured in.
        ///
        /// <para><b>Read from the contract's MEASURED bearings</b>, so this is a search over eight
        /// numbers rather than an index computed from an assumption. It comes out as
        /// <c>round(bearing / 45)</c> for a healthy bake — and the point is that it does not have to:
        /// if a re-emitted rig ever reorders its cells, the sheets are re-measured at bake time and
        /// every placer follows without a line changing.</para>
        ///
        /// <para>Returns 0 when the contract carries no bearings — a bake predating this table, which
        /// <see cref="HasMeasuredBearings"/> lets a caller refuse on rather than silently place a
        /// north-facing fence all the way round a yard.</para>
        /// </summary>
        public static int FacingFor(YardKit.Contract contract, float bearingDegrees)
        {
            var bearings = contract?.cellFaceBearings;
            if (bearings == null || bearings.Length == 0) return 0;

            int best = 0;
            float bestGap = float.MaxValue;
            for (int cell = 0; cell < bearings.Length; cell++)
            {
                float gap = AngleGap(bearings[cell], bearingDegrees);
                if (gap >= bestGap) continue;
                bestGap = gap;
                best = cell;
            }
            return best;
        }

        /// <summary>How far off, in degrees, the chosen cell's face points from what was asked — the
        /// honest error, for a test to bound rather than re-deriving the choice and finding it agrees
        /// with itself.</summary>
        public static float FacingErrorDegrees(YardKit.Contract contract, float bearingDegrees, int facing)
        {
            var bearings = contract?.cellFaceBearings;
            if (bearings == null || facing < 0 || facing >= bearings.Length) return 180f;
            return AngleGap(bearings[facing], bearingDegrees);
        }

        /// <summary>Did the bake record a measured facing table? A contract without one predates the
        /// measurement and must not be placed from.</summary>
        public static bool HasMeasuredBearings(YardKit.Contract contract) =>
            contract?.cellFaceBearings != null &&
            contract.cellFaceBearings.Length == contract.facings &&
            contract.facings > 0;

        /// <summary>
        /// Compass bearing of a world direction: degrees CLOCKWISE FROM NORTH, in [0, 360).
        ///
        /// <para>⚠️ Not <c>Atan2(y, x)</c>. The world's compass runs the other way round from the maths
        /// convention, and the two agree at exactly one place (east/90°) — which is where a wrong one
        /// gets spot-checked and passes.</para>
        /// </summary>
        public static float BearingOf(Vector2 direction)
        {
            if (direction.sqrMagnitude < 1e-12f) return 0f;
            float deg = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            return deg < 0f ? deg + 360f : deg;
        }

        /// <summary>Smallest absolute angle between two bearings, in degrees.</summary>
        public static float AngleGap(float a, float b)
        {
            float d = Mathf.Repeat(a - b, 360f);
            return d > 180f ? 360f - d : d;
        }

        // -------------------------------------------------------------------------------------
        //  standing one up
        // -------------------------------------------------------------------------------------

        /// <summary>
        /// The sprite for one facing, BY NAME.
        ///
        /// <para>⚠️ Two traps in one call. <c>LoadAssetAtPath&lt;Sprite&gt;</c> returns null on a
        /// Multiple-sliced sheet (the sprites are sub-assets), and a lookup BY INDEX into
        /// <c>LoadAllAssetsAtPath</c> finds whatever order the importer happened to return — including
        /// on a silently downscaled sheet, which carries the right COUNT of sprites with the wrong
        /// cells and a thrown-away pivot.</para>
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
        /// Stand a piece on a GameObject: renderer, unit scale, pinned rotation, Y-sort. The single
        /// build path.
        /// </summary>
        public static SpriteRenderer Configure(GameObject go, Placement placement, Sprite sprite,
                                               int sortingOrder = SortingOrder, bool ySort = true)
        {
            if (go == null) throw new ArgumentNullException(nameof(go));

            if (!placement.IsValid)
                throw new ArgumentException(
                    "Configure was handed a placement with no contract entry, so there is no pivot, no " +
                    "footprint and no facing count to give it. Placing on a plausible default is " +
                    "exactly the reading this kit's contract replaces — fix the caller.",
                    nameof(placement));

            // ⚠️ `GetComponent<T>() ?? AddComponent<T>()` compiles and is wrong: `??` tests the real C#
            // reference and never reaches UnityEngine.Object's overloaded `==`, so a "fake null"
            // satisfies it and the first field write throws. Compare with `== null`.
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();

            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.color = Color.white;

            // Honest metric size: the rig baked the camera's foreshortening in at 32 px = 1 m, so
            // scaling to the footprint metres would break the pixel grid and the scale ladder together.
            go.transform.localScale = Vector3.one;

            // Baked at a FIXED camera — rotating the transform would tip the projection. Turning is
            // done by swapping the sub-sprite, which is what makes the measured facing table the only
            // way a piece can be aimed.
            go.transform.localRotation = Quaternion.identity;

            if (ySort && go.GetComponent<YSortSprite>() == null) go.AddComponent<YSortSprite>();

            return sr;
        }

        /// <summary>The half-diagonal of a footprint in metres — the ground a piece reserves at ANY
        /// facing. A circle rather than a rectangle, for the building kits' reason: the facing is
        /// derived from the boundary it stands on, and a quarter-turned piece presents its
        /// diagonal.</summary>
        public static float FootprintRadiusMetres(Placement p) =>
            !p.IsValid ? 0f
                       : 0.5f * Mathf.Sqrt(p.FootprintMetres.x * p.FootprintMetres.x +
                                           p.FootprintMetres.y * p.FootprintMetres.y);
    }
}
#endif
