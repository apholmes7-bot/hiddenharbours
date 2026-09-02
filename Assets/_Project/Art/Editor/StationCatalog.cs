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
        //  PIECE-LOCAL → WORLD — rotate on the GROUND, then the art's own squash (ADR 0042)
        // =====================================================================================

        /// <summary>
        /// How far up the screen one metre of NORTHWARD ground travel draws — <c>sin 40°</c> at the
        /// shared bake camera, <see cref="SpriteLightMath.GroundDepthScale"/>. Every placement in this
        /// kit carries it (ADR 0042): the squash is baked into the PIXELS of every cell and nothing at
        /// render time transforms anything, so geometry that has to coincide with the art — a wall
        /// collider, a standing spot, the second machine on an island's second plate — has to be
        /// squashed the same way or it lands beside the picture rather than on it.
        /// </summary>
        public static float DepthScale => SpriteLightMath.GroundDepthScale;

        /// <summary>The GROUND direction a piece's local <b>+Y</b> points at
        /// <paramref name="facing"/> — a unit compass vector, BEFORE the squash. The measurement
        /// everything else here is built from: it is what a heading is (ADR 0034), not where a metre of
        /// it lands on screen — that is <see cref="LocalDirToWorld"/>.</summary>
        public static Vector2 ForwardOf(int facing)
        {
            float t = HeadingOfFacing(facing) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(t), Mathf.Cos(t));
        }

        /// <summary>…and the GROUND direction its local <b>+X</b> points: a quarter turn to starboard of
        /// <see cref="ForwardOf"/>, which is what the harness measured.</summary>
        public static Vector2 RightOf(int facing)
        {
            float t = HeadingOfFacing(facing) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(t), -Mathf.Sin(t));
        }

        /// <summary>
        /// A piece-local plan DIRECTION in world units. No origin — a rotation, and then the squash.
        ///
        /// <para><b>Rotate on the ground FIRST, squash SECOND</b> — the same shape as
        /// <c>InteriorFootprint.ModelToWorld</c>, which the house and shop family have always placed
        /// with. The other order shears the wrong way, and the two are indistinguishable by eye at the
        /// four orthogonal facings — only the diagonals show it.</para>
        ///
        /// <para>⚠️ The result is NOT unit length for a unit input: a metre of local <c>+Y</c> at cell 0
        /// is 0.643 world units. Anything that wants a ground bearing or a ground distance works in the
        /// piece's frame and projects at the end; <see cref="WorldDirToLocal"/> is the way back.</para>
        /// </summary>
        public static Vector2 LocalDirToWorld(Vector2 local, int facing)
        {
            Vector2 ground = RightOf(facing) * local.x + ForwardOf(facing) * local.y;
            return new Vector2(ground.x, ground.y * DepthScale);
        }

        /// <summary>
        /// A piece-local plan POINT in world units: the sidecar's own frame (metres, origin at the ground
        /// centre of the piece's own footprint, +z up) placed at <paramref name="origin"/>, turned to
        /// <paramref name="facing"/> and squashed as the art is.
        ///
        /// <para>⚠️ <b>Z is dropped, and that is right.</b> A reach point's z is how high the hand
        /// arrives, which the rig has already ruled on — it is not a world coordinate.</para>
        ///
        /// <para>⚠️ <b>And the squash is NOT a render transform the sprite carries — it is in the
        /// pixels.</b> An earlier remark here said the opposite and placed in unsquashed ground metres,
        /// which left every collider on the Route 91 forecourt over-hanging its own picture: the C-store's
        /// wall ring 2.07 m past each drawn side wall, the island kerb 2.77 m too long, the bollards
        /// 1.28 m out. It drew perfectly. ADR 0042 carries the measurements.</para>
        /// </summary>
        public static Vector2 LocalToWorld(Vector3 local, Vector2 origin, int facing) =>
            origin + LocalDirToWorld(new Vector2(local.x, local.y), facing);

        /// <inheritdoc cref="LocalToWorld(Vector3,Vector2,int)"/>
        public static Vector2 LocalToWorld(Vector2 local, Vector2 origin, int facing) =>
            origin + LocalDirToWorld(local, facing);

        /// <summary>
        /// A world direction back in a piece's frame: UN-squash first, then un-rotate. The exact inverse
        /// of <see cref="LocalDirToWorld"/>. The rotation half is orthonormal, so its transpose undoes
        /// it — but only once the squash has been taken off, which is what a transpose-only inverse
        /// forgets.
        /// </summary>
        public static Vector2 WorldDirToLocal(Vector2 world, int facing)
        {
            var ground = new Vector2(world.x, world.y / DepthScale);
            return new Vector2(Vector2.Dot(ground, RightOf(facing)), Vector2.Dot(ground, ForwardOf(facing)));
        }

        /// <summary>A world point back in the frame of a piece standing at <paramref name="origin"/> —
        /// the inverse of <see cref="LocalToWorld(Vector2,Vector2,int)"/>.</summary>
        public static Vector2 WorldToLocal(Vector2 world, Vector2 origin, int facing) =>
            WorldDirToLocal(world - origin, facing);

        // =====================================================================================
        //  THE COLLIDERS — one enumeration, shared by the prefab builder and the placement
        // =====================================================================================

        /// <summary>
        /// One collider a piece carries, in the piece's OWN frame (metres, unsquashed) — a polygon,
        /// always: a circular blocker is polygonised here and a shut door leaf is its quad.
        ///
        /// <para><b>Why polygons only.</b> The world shape is the ground shape ROTATED and then SQUASHED
        /// (<see cref="LocalDirToWorld"/>), and that is a shear at every facing but the four orthogonal
        /// ones. A <c>BoxCollider2D</c> cannot express a shear, a <c>CircleCollider2D</c> cannot be an
        /// ellipse, and a child's <c>localRotation</c> — the kit's earlier answer — turns a shape without
        /// squashing it, which is why that answer was retired (ADR 0042). A polygon path can be any of
        /// them, so every collider in this kit is one.</para>
        /// </summary>
        public readonly struct ColliderShape
        {
            /// <summary>The child's name: <c>blocker_&lt;kind&gt;</c>, or
            /// <c>door_Entry_shut</c> / <c>door_ServiceDoor_shut</c>.</summary>
            public readonly string Name;

            /// <summary>The ground polygon, piece-local metres, unsquashed.</summary>
            public readonly Vector2[] Local;

            public ColliderShape(string name, Vector2[] local)
            {
                Name = name;
                Local = local;
            }
        }

        /// <summary>Segments a circular blocker is drawn with. Sixteen keeps every edge within 2 mm of a
        /// 0.11 m bollard's true rim and within 8 mm of the 0.42 m sign base's — under the 32 px/m grid
        /// either way.</summary>
        public const int CirclePathSegments = 16;

        /// <summary>The thinnest a shut door leaf's collider may be (m): a sill that is only step-high
        /// is still a shut door, and a zero-thickness leaf stops nobody.</summary>
        public const float DoorLeafMinThicknessMetres = 0.08f;

        /// <summary>
        /// Every collider this piece earns, in order: one per blocker that
        /// <see cref="StationBlocker.Blocks"/>, in the Def's own order (<c>wall</c> and
        /// <c>waist_block</c>; <c>step_over</c> and <c>flat</c> stop nobody), then the shut ENTRY leaf,
        /// then the shut SERVICE-DOOR leaf, where the Def publishes them.
        ///
        /// <para>This is the ONE definition of "what colliders does a station piece have":
        /// <c>StationPieceDefBuilder</c> writes the prefab from it and <c>StationForecourt</c> re-projects
        /// each placed instance from it, matched child by child, so the two cannot drift.</para>
        /// </summary>
        public static List<ColliderShape> ColliderShapes(StationPieceDef def)
        {
            var shapes = new List<ColliderShape>();
            if (def == null) return shapes;

            foreach (StationBlocker b in def.Blockers ?? Array.Empty<StationBlocker>())
            {
                if (b == null || !b.Blocks) continue;
                if (b.IsCircle)
                    shapes.Add(new ColliderShape("blocker_" + b.Kind, CirclePolygon(b.Center, b.Radius)));
                else if (b.Footprint != null && b.Footprint.Length >= 3)
                    shapes.Add(new ColliderShape("blocker_" + b.Kind, (Vector2[])b.Footprint.Clone()));
            }

            AddDoorLeaf(shapes, def.Entry, "Entry");
            AddDoorLeaf(shapes, def.ServiceDoor, "ServiceDoor");
            return shapes;
        }

        /// <summary>
        /// The shut door, as a shape. For the bipart slider that is the two leaves meeting at the
        /// threshold — the collider IS the leaf, which is why the sidecar publishes <c>keep_clear: null</c>
        /// for it and why reading that null as "no data" would leave the shop's front wall with a hole in
        /// it. The service door is a single leaf swinging out and owns a keep-clear, but shut it is still
        /// just its own leaf.
        /// </summary>
        static void AddDoorLeaf(List<ColliderShape> shapes, StationDoorway d, string name)
        {
            if (d == null || !d.Exists || d.ClearWidthMeters <= 0f) return;

            float halfWide = d.ClearWidthMeters * 0.5f;
            float halfDeep = Mathf.Max(DoorLeafMinThicknessMetres, d.SillStepMeters) * 0.5f;
            float x = d.Threshold.x, y = d.Threshold.y;

            shapes.Add(new ColliderShape($"door_{name}_shut", new[]
            {
                new Vector2(x - halfWide, y - halfDeep), new Vector2(x + halfWide, y - halfDeep),
                new Vector2(x + halfWide, y + halfDeep), new Vector2(x - halfWide, y + halfDeep),
            }));
        }

        /// <summary>A circle on the ground as a polygon, counter-clockwise from <c>+x</c>.</summary>
        public static Vector2[] CirclePolygon(Vector2 centre, float radius, int segments = CirclePathSegments)
        {
            int n = Mathf.Max(3, segments);
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * (2f * Mathf.PI / n);
                pts[i] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            return pts;
        }

        /// <summary>
        /// A piece-local ground polygon as a collider PATH for a piece drawn at <paramref name="facing"/>:
        /// every vertex through <see cref="LocalDirToWorld"/>, relative to the piece's own origin — which
        /// is what a <c>PolygonCollider2D</c> on a child at local zero with no rotation of its own wants.
        /// A rectangle comes out as a parallelogram at the diagonal facings, and that is correct: it is
        /// the shape the art draws.
        /// </summary>
        public static Vector2[] FootprintPath(Vector2[] local, int facing)
        {
            if (local == null) return Array.Empty<Vector2>();
            var path = new Vector2[local.Length];
            for (int i = 0; i < local.Length; i++) path[i] = LocalDirToWorld(local[i], facing);
            return path;
        }

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
