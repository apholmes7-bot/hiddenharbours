using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// <b>THE READ SIDE OF THE GAS-STATION KIT.</b> <see cref="GasStationKit"/> is what a BAKER asserts
    /// against before it writes a pixel; this is what a REGION BUILDER asks once the pixels exist — "give
    /// me the dock pedestal", "which cell faces east", "where in the world is that nozzle".
    ///
    /// <para>The sibling of <see cref="ShopCatalog"/>, and deliberately not a copy of
    /// <c>HiddenHarbours.Tools.RigBaking.IsoPackSprites</c>: this kit's art is already resolved into
    /// <see cref="StationPieceDef"/> assets (#613), so a caller wants a Def and its baked
    /// <see cref="StationPieceDef.Frame"/>, not a slice index off a sheet. What it shares with that class
    /// is the one thing that must not be re-decided anywhere — the facing arithmetic.</para>
    ///
    /// <para><b>⭐⭐ THE FACING RULE, MEASURED RATHER THAN READ.</b> The kit is registered
    /// <c>Clockwise</c>, which <c>AzimuthConvention</c> defines as "cell <c>i</c> depicts heading
    /// +45°·i". <b>The heading axis is the piece's local +Y.</b> Measured 2026-08-20 in the standalone V8
    /// harness through the rig's own <c>project()</c>, un-squashing the 40° elevation
    /// (<c>sin 40° = 0.642788</c>) so the reading is a GROUND bearing and not a screen one:</para>
    /// <list type="bullet">
    /// <item><description>local <b>+Y</b> bears <b>45°·d</b> — d0 north, d2 east, d4 south, d6 west.</description></item>
    /// <item><description>local <b>+X</b> bears <b>45°·d + 90°</b> — a quarter turn to starboard of it.</description></item>
    /// </list>
    /// <para>⚠️ The SCREEN bearings are ∓46.7525° and have the same magnitude for a clockwise and a
    /// counter-clockwise pack, so they cannot tell the two apart — the ground plane is the only
    /// admissible reading, which is the warning <c>RigCatalog.GasStationKit</c> already records for the
    /// bake side. ⚠️ And the convention is read from the committed CONTRACT rather than pinned here, so a
    /// re-bake that ever flipped it moves this file's answer with it instead of leaving a constant
    /// quietly disagreeing with the art.</para>
    ///
    /// <para><b>Null-tolerant on purpose</b> — the wharf dressing's standing reason: "declared in the
    /// contract" and "has pixels on disk" are different questions, and a partial art state should place
    /// what it can rather than throw halfway through a region build. Every miss returns null; the CALLER
    /// decides whether that is a warning or a failure.</para>
    /// </summary>
    public static class StationCatalog
    {
        /// <summary>Where #613 wrote the piece Defs.</summary>
        public const string DefFolder = "Assets/_Project/Data/StationPieces";

        /// <summary>…and the prefabs it built from them. ⚠️ <c>_Project/Prefabs</c>, not
        /// <c>_Project/Art/Prefabs</c>.</summary>
        public const string PrefabFolder = "Assets/_Project/Prefabs/GasStation";

        /// <summary>Facings on the compass — the kit's own, and the modulus a facing is wrapped into so a
        /// caller may hand in a compass turn without pre-wrapping it.</summary>
        public const int Facings = GasStationKit.Facings;

        /// <summary>Degrees of compass turn per facing cell.</summary>
        public const float DegreesPerFacing = 360f / Facings;

        // =====================================================================================
        //  THE CONTRACT
        // =====================================================================================

        static GasStationKit.Contract _contract;

        /// <summary>The committed bake contract, or null if the kit has not baked here. Cached: a build
        /// asks for the convention once per piece and this is a file read.</summary>
        public static GasStationKit.Contract Contract()
        {
            if (_contract != null) return _contract;
            if (!File.Exists(GasStationKit.ContractPath)) return null;
            try
            {
                _contract = JsonUtility.FromJson<GasStationKit.Contract>(
                    File.ReadAllText(GasStationKit.ContractPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[StationCatalog] {GasStationKit.ContractPath} did not parse: {e.Message}");
                _contract = null;
            }
            return _contract;
        }

        /// <summary>Drop every memo — for a test that re-bakes, and for the same reason
        /// <c>NineMileCreekYards.InvalidateCache</c> exists.</summary>
        public static void InvalidateCache()
        {
            _contract = null;
            _defsByKey = null;
        }

        /// <summary>
        /// True when the kit's cells run CLOCKWISE (cell <c>i</c> = heading +45°·i), which is what this
        /// kit's bake measured and declared.
        ///
        /// <para>Read from the contract, falling back to <c>true</c> when the kit has not baked — that is
        /// what <c>RigCatalog</c> declares, and a missing file must not silently mirror the world.</para>
        /// </summary>
        public static bool IsClockwise
        {
            get
            {
                string c = Contract()?.convention;
                return string.IsNullOrEmpty(c) ||
                       c.Equals("Clockwise", StringComparison.OrdinalIgnoreCase);
            }
        }

        // =====================================================================================
        //  THE FACING AXIS
        // =====================================================================================

        /// <summary>The cell that turns a piece's <b>local +Y</b> along
        /// <paramref name="headingDegrees"/> (compass, N = 0, clockwise) — the same arithmetic
        /// <c>IsoPackSprites.FacingForHeading</c> runs for the iso packs, over this kit's own declared
        /// convention.</summary>
        public static int FacingForHeading(float headingDegrees)
        {
            int step = Mathf.RoundToInt(headingDegrees * Facings / 360f);
            return Wrap(IsClockwise ? step : -step);
        }

        /// <summary>…and the heading that cell depicts — the inverse of <see cref="FacingForHeading"/> on
        /// the 45° lattice.</summary>
        public static float HeadingOfFacing(int facing) =>
            Wrap(IsClockwise ? facing : -facing) * DegreesPerFacing;

        /// <summary>Compass azimuth (N = 0, clockwise) of a plan direction — <c>atan2(east, north)</c>,
        /// the convention <c>IsoPackSprites.HeadingOf</c> and <c>CliffWallGeometry.AzimuthOf</c> both
        /// measure in. Restated rather than referenced because this assembly can see neither, and held
        /// equal to them by <c>NineMileCreekStationTests</c>.</summary>
        public static float HeadingOf(Vector2 planDirection)
        {
            if (planDirection.sqrMagnitude < 1e-12f) return 0f;
            float a = Mathf.Atan2(planDirection.x, planDirection.y) * Mathf.Rad2Deg;
            return a < 0f ? a + 360f : a;
        }

        /// <summary>The facing that turns a piece standing at <paramref name="from"/> to look at
        /// <paramref name="target"/>.</summary>
        public static int FacingToward(Vector2 from, Vector2 target) =>
            FacingForHeading(HeadingOf(target - from));

        /// <summary>
        /// The cell at which an arbitrary piece-LOCAL plan direction bears
        /// <paramref name="headingDegrees"/>.
        ///
        /// <para><b>⚠️⚠️ THIS IS THE ONE TO USE FOR A DISPENSER, AND <see cref="FacingForHeading"/> IS
        /// NOT.</b> A building has a front and it is its local +Y — a storefront's threshold and the price
        /// sign's own <c>prices</c> fitting are both on +Y, so turning +Y at the road is exactly right for
        /// them. A dispenser has no front: it has a HOSE SIDE, and in this kit that is local <b>+X</b>
        /// (the <c>sDock</c>'s only nozzle sits at <c>x = +0.44</c>, the <c>sMpd</c>'s two sides at
        /// <c>x = ±0.59</c>). Turning a dock pedestal's +Y at the water points its nozzle along the wall
        /// instead of over it — a quarter turn wrong, and it draws perfectly.</para>
        ///
        /// <para>Pass the direction the PIECE ITSELF publishes (the fitting's own local position), not a
        /// remembered axis, and a re-bake that moves the hose moves the facing with it.</para>
        /// </summary>
        public static int FacingForLocalDirection(Vector2 localDirection, float headingDegrees) =>
            FacingForHeading(headingDegrees - HeadingOf(localDirection));

        /// <summary>Wrap a facing into 0…7.</summary>
        public static int Wrap(int facing) => ((facing % Facings) + Facings) % Facings;

        // =====================================================================================
        //  PIECE-LOCAL → WORLD
        // =====================================================================================

        /// <summary>The world direction a piece's local <b>+Y</b> points at
        /// <paramref name="facing"/> — the measurement everything else here is built from.</summary>
        public static Vector2 ForwardOf(int facing)
        {
            float t = HeadingOfFacing(facing) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(t), Mathf.Cos(t));
        }

        /// <summary>…and the world direction its local <b>+X</b> points: a quarter turn to starboard of
        /// <see cref="ForwardOf"/>, which is what the harness measured.</summary>
        public static Vector2 RightOf(int facing)
        {
            float t = HeadingOfFacing(facing) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(t), -Mathf.Sin(t));
        }

        /// <summary>A piece-local plan DIRECTION in world terms. Rotation only — no origin.</summary>
        public static Vector2 LocalDirToWorld(Vector2 local, int facing) =>
            RightOf(facing) * local.x + ForwardOf(facing) * local.y;

        /// <summary>
        /// A piece-local plan POINT in world terms: the sidecar's own frame (metres, origin at the ground
        /// centre of the piece's own footprint, +z up) placed at <paramref name="origin"/> and turned to
        /// <paramref name="facing"/>.
        ///
        /// <para>⚠️ <b>Z is dropped, and that is right.</b> The world plane here IS the ground plane; the
        /// iso squash is a RENDER transform the sprite already carries. A reach point's z is how high the
        /// hand arrives, which the rig has already ruled on — it is not a world coordinate.</para>
        /// </summary>
        public static Vector2 LocalToWorld(Vector3 local, Vector2 origin, int facing) =>
            origin + LocalDirToWorld(new Vector2(local.x, local.y), facing);

        /// <inheritdoc cref="LocalToWorld(Vector3,Vector2,int)"/>
        public static Vector2 LocalToWorld(Vector2 local, Vector2 origin, int facing) =>
            origin + LocalDirToWorld(local, facing);

        /// <summary>
        /// The Z rotation a piece's COLLIDER child carries so its footprint covers the ground the piece
        /// actually stands on.
        ///
        /// <para><b>⚠️ The collider turns and the SPRITE DOES NOT.</b> For baked iso art the facing IS the
        /// rotation — turning the renderer would shear a picture that already contains the camera. But
        /// the sidecar's footprints are stated in the piece's own frame, so at any cell but 0 the ground
        /// they describe is turned. The blocker children are therefore rotated by this and the ROOT is
        /// left at identity — which is also what <c>PropertyBoundary</c> requires of any host it is
        /// given, so a wall may be hung off a station piece later without this coming apart.</para>
        ///
        /// <para>Negative because a compass heading turns CLOCKWISE and Unity's Z rotation turns
        /// counter-clockwise: the same sign flip <see cref="RightOf"/> carries.</para>
        /// </summary>
        public static float ColliderZRotation(int facing) => -HeadingOfFacing(facing);

        // =====================================================================================
        //  THE PIECES
        // =====================================================================================

        static Dictionary<string, StationPieceDef> _defsByKey;

        /// <summary>Every piece Def under <see cref="DefFolder"/>, keyed by the KIT's own key
        /// (<c>dispenser_sDock</c>) — which is the asset name, the string the contract carries, and the
        /// name the prefabs were built under. Ordinal: these are asset keys, not prose.</summary>
        public static IReadOnlyDictionary<string, StationPieceDef> Defs()
        {
            if (_defsByKey != null) return _defsByKey;

            _defsByKey = new Dictionary<string, StationPieceDef>(StringComparer.Ordinal);
            if (!AssetDatabase.IsValidFolder(DefFolder)) return _defsByKey;

            foreach (string guid in AssetDatabase.FindAssets("t:StationPieceDef", new[] { DefFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<StationPieceDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) _defsByKey[def.name] = def;
            }
            return _defsByKey;
        }

        /// <summary>The piece with this kit key, or null.</summary>
        public static StationPieceDef Find(string key) =>
            !string.IsNullOrEmpty(key) && Defs().TryGetValue(key, out StationPieceDef d) ? d : null;

        /// <summary>The kit key for a (type, size) pair — the naming the bake, the Defs and the prefabs
        /// all share.</summary>
        public static string KeyOf(string type, string size) => $"{type}_{size}";

        /// <summary>The piece for a (type, size) pair, or null.</summary>
        public static StationPieceDef Find(string type, string size) => Find(KeyOf(type, size));

        /// <summary>The prefab #613 built for this piece, or null.</summary>
        public static GameObject Prefab(string key) =>
            string.IsNullOrEmpty(key)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{key}.prefab");

        /// <summary>True when the kit has baked, been sliced and had its Defs built — the one question a
        /// region builder asks before deciding whether to warn or to place.</summary>
        public static bool IsInstalled => Defs().Count > 0;
    }
}
