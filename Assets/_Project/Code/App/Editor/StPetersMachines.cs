#if UNITY_EDITOR
using System.Collections.Generic;
using HiddenHarbours.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE THREE MACHINES, STANDING OUTSIDE THE SHOP</b> — the ATV pack's quad, trike and enduro,
    /// parked at St Peters where a player can walk up to one and get on (owner 2026-09-07:
    /// <i>"a handoff atvs imported and driveable"</i>, extended the same evening to <i>"also park an
    /// enduro bike and trike"</i>).
    ///
    /// <para><b>Why they exist, and why here.</b> The owner's words at the drop: <i>"These will be
    /// used in recreation by residents. Some use them as transportation. It's the only used
    /// transportation on St. Peter's island other than boats."</i> The general store is where the
    /// island's five doors already point, and the shop's forecourt is the one yard on the green
    /// deliberately left OPEN to the lane (<see cref="StPetersYards"/>: <i>"a shop forecourt, open to
    /// the lane on purpose"</i>). Machines left outside the shop is what a village looks like, and it
    /// puts all three within one short walk of where the player starts.</para>
    ///
    /// <para><b>⚠ WHOSE THEY ARE IS STILL THE OWNER'S CALL.</b> Ruling (a) of the ATV pack — which
    /// resident owns which machine and where each is parked — is owed, and this does not pre-empt it.
    /// They are the owner's TEST machines: nobody rides them but the player and no routine claims
    /// one. The day (a) lands, each moves to its owner's yard in a one-line change here, because
    /// every position below is DERIVED from the store and the green rather than typed.</para>
    ///
    /// <para><b>Places, does NOT draw</b> — the law <see cref="NineMileCreekTruckPark"/> is written
    /// against, for its reason: the mesh path is runtime-owned, the Art service registers at
    /// <c>RuntimeInitializeOnLoadMethod</c> and deliberately does not register inside an editor
    /// builder, so a builder that skinned one here would serialise the unskinned fallback into the
    /// committed scene, permanently and silently (memory <c>mesh-hulls-must-skin-at-runtime</c>). A
    /// <see cref="ParkedVehicle"/> carrying a Def skins itself on enable, per run.</para>
    ///
    /// <para><b>⚠⚠ THE ENDURO'S PARKED PICTURE DOES NOT EXIST YET, AND SHE IS PARKED ANYWAY.</b> Her
    /// rig publishes a side stand (<c>STAND</c>, <c>default: 1</c>) and says in capitals what it is
    /// for: <i>"she BAKES PARKED. A dirtbike does not stand upright without a rider … a game that
    /// shows her upright with nobody aboard is showing a bug the rig will not catch."</i> The mesh
    /// this repo holds is <c>stand:0</c> — the RIDDEN state, 497 faces — because the parked one is
    /// 502 faces and a 12° lean: a topology change, not a rotation, so it cannot be faked from what
    /// is here (ATV PR 0's measurement). So she stands upright on her wheels until the second bake
    /// lands. <c>StPetersMachinesTests</c> pins that as a NAMED gap rather than letting it pass
    /// quietly. The quad and the trike publish no stand at all — three wheels and four stand up by
    /// themselves — so for them parked and ridden are the same picture and nothing is owed.</para>
    /// </summary>
    public static class StPetersMachines
    {
        // ---- what stands here ------------------------------------------------------------------

        /// <summary>Repo path of each machine's def.</summary>
        public const string QuadDefPath = "Assets/_Project/Data/Vehicles/UtilityQuad.asset";

        /// <inheritdoc cref="QuadDefPath"/>
        public const string TrikeDefPath = "Assets/_Project/Data/Vehicles/Trike200.asset";

        /// <inheritdoc cref="QuadDefPath"/>
        public const string EnduroDefPath = "Assets/_Project/Data/Vehicles/Enduro250.asset";

        /// <summary>The placed objects' names, so a re-run finds and replaces rather than doubling the
        /// row — #560's gate was never "no machine", it was "never place her twice".</summary>
        public const string QuadName = "UtilityQuadAtTheStore";

        /// <inheritdoc cref="QuadName"/>
        public const string TrikeName = "Trike200AtTheStore";

        /// <inheritdoc cref="QuadName"/>
        public const string EnduroName = "Enduro250AtTheStore";

        /// <summary>The three, in row order: the quad nearest the gate — she is the work machine and
        /// the one that gets used — then the trike, then the bike at the far end.</summary>
        public static IReadOnlyList<(string Name, string DefPath)> Row { get; } = new[]
        {
            (QuadName, QuadDefPath),
            (TrikeName, TrikeDefPath),
            (EnduroName, EnduroDefPath),
        };

        // ---- where the row stands, DERIVED ------------------------------------------------------

        /// <summary>
        /// How far outside the shop's front boundary the machines stand (m).
        ///
        /// <para>Outside it, not on it, and that is the mow line talking: the store's yard is
        /// <c>MownStyle.Striped</c> and somebody cuts it. A stride and a half puts three machines on
        /// the open ground at the lane rather than on the shop's own grass, and it is short enough
        /// that they still read as belonging to the shop.</para>
        /// </summary>
        public const float StandOffMetres = 2f;

        /// <summary>Clear ground between two machines, measured between their published bodies (m).
        /// Room to walk between them and swing a leg over: the mount reach points stand 1.15–1.19 m
        /// off each centreline, so this is what stops one machine's way on falling inside her
        /// neighbour.</summary>
        public const float GapMetres = 0.9f;

        /// <summary>
        /// The yard's front boundary point the row is measured from — the store's own gate, the one
        /// derived point that already says "the lane edge of the shop's forecourt".
        ///
        /// <para>Read off <see cref="StPetersYards"/> rather than recomputed here, so the row moves
        /// with the yard and there is exactly one statement of where the frontage is.</para>
        /// </summary>
        public static Vector2 Gate
        {
            get
            {
                foreach (Yard y in StPetersYards.Yards)
                    if (y.Name == StPetersYards.GeneralStoreYard && y.Gates.Length > 0)
                        return y.Gates[0];

                // The store has a yard and the yard has a gate. This is the honest answer if either is
                // ever taken away — her own door — rather than the world origin.
                return new Vector2(StPetersBuilder.GeneralStorePos.x, StPetersBuilder.GeneralStorePos.y);
            }
        }

        /// <summary>The way the shop FACES — toward the green, exactly as every door on this island
        /// does (<see cref="StPetersYards"/>: no row carries an angle). Outward from the building.
        /// </summary>
        public static Vector2 Forward
        {
            get
            {
                var store = new Vector2(StPetersBuilder.GeneralStorePos.x,
                                        StPetersBuilder.GeneralStorePos.y);
                Vector2 d = StPetersBuilder.VillageGreen - store;
                return d.sqrMagnitude < 1e-6f ? Vector2.down : d.normalized;
            }
        }

        /// <summary>Across the frontage — the row's own axis. The right-hand perpendicular of
        /// <see cref="Forward"/>, so the row runs along the shop's front rather than out into the
        /// green.</summary>
        public static Vector2 Across => new Vector2(Forward.y, -Forward.x);

        /// <summary>
        /// Where machine <paramref name="index"/> stands, in world XY — the row laid out from the
        /// gate, along the frontage, at each machine's OWN published width.
        ///
        /// <para><b>⭐ The spacing is the art's, not a number here.</b> Each beam comes off that
        /// machine's <c>VehicleMeshDef</c> collider box (the enduro measures 0.86 m over her grips,
        /// the trike 1.20 over her rear fender, the quad 1.28 over her fender aprons), so a re-bake
        /// that widens one machine widens the gaps either side of her instead of quietly overlapping
        /// her neighbour. <see cref="GapMetres"/> is the clear space BETWEEN bodies, which is the
        /// number a person walking down the row actually experiences.</para>
        ///
        /// <para>The row starts clear of the gate itself — <see cref="YardPlan.GateWidthMetres"/>, so
        /// the opening the fence data already publishes stays walkable — and runs along
        /// <see cref="Across"/> away from it.</para>
        /// </summary>
        public static Vector2 StandFor(int index, IReadOnlyList<float> beamsMetres)
        {
            if (beamsMetres == null || index < 0 || index >= beamsMetres.Count) return Gate;

            // Distance along the row to THIS machine's centreline: past the gate's own opening, then
            // every earlier machine's half-beam, the gap, and this one's half-beam in turn.
            float along = YardPlan.GateWidthMetres * 0.5f + beamsMetres[0] * 0.5f;
            for (int i = 1; i <= index; i++)
                along += beamsMetres[i - 1] * 0.5f + GapMetres + beamsMetres[i] * 0.5f;

            return Gate + Forward * StandOffMetres + Across * along;
        }

        /// <summary>Each machine's published beam (m), in row order — her collider box across, which
        /// is the widest part the art measured. Empty when a def or a mesh is missing, which is what
        /// <see cref="Place"/> refuses on.</summary>
        public static List<float> Beams()
        {
            var beams = new List<float>();
            foreach ((string _, string path) in Row)
            {
                var def = AssetDatabase.LoadAssetAtPath<VehicleDef>(path);
                if (def == null || def.Mesh == null) return new List<float>();
                beams.Add(def.Mesh.ColliderMaxMeters.x - def.Mesh.ColliderMinMeters.x);
            }

            return beams;
        }

        /// <summary>
        /// Which way she points: NOSE OUT, toward the green.
        ///
        /// <para><b>Not a taste — the enduro has no reverse worth the name</b> (1.2 m/s, a rider
        /// walking her backwards; her class has no reverse gear at all). A machine parked nose-in
        /// would have to be pushed out of the row before she could be ridden, which is the opposite of
        /// what getting on one should feel like (P5). Nose out is also what somebody who rode up to a
        /// shop and turned round actually leaves behind.</para>
        ///
        /// <para><c>transform.up</c> is the nose — the fleet's one heading convention, the same one
        /// <c>VehicleDoor</c> reads her door points through, so a machine turned here has her two ways
        /// on turned with her.</para>
        ///
        /// <para><b>⭐ Built as the pure z-turn it IS, not with <c>Quaternion.FromToRotation</c>.</b>
        /// Two reasons, and the first is the one that matters: this shop faces 176.4° away from
        /// <c>Vector3.up</c>, and <c>FromToRotation</c> near half a turn is exactly where the axis it
        /// has to pick becomes ill-conditioned — a shop that moved a metre could flip the row's
        /// heading by 180°. A ground vehicle turns about z and only about z, so
        /// <c>θ = atan2(−fwd.x, fwd.y)</c> states that directly and is stable everywhere. The second
        /// is that it is then pure managed arithmetic, which means the row's heading can be checked
        /// outside an editor.</para>
        /// </summary>
        public static Quaternion Heading
        {
            get
            {
                Vector2 f = Forward;
                // R(θ)·up = (−sinθ, cosθ), so this is the turn that puts her nose on `f`.
                float half = Mathf.Atan2(-f.x, f.y) * 0.5f;
                return new Quaternion(0f, 0f, Mathf.Sin(half), Mathf.Cos(half));
            }
        }

        // ---- placing ----------------------------------------------------------------------------

        /// <summary>
        /// Stand all three at the shop. Returns how many were placed — 0 with a warning when the defs
        /// are not on disk, which is the honest answer for a region built before the ATV bake rather
        /// than three empty GameObjects.
        ///
        /// <para>Idempotent BY NAME: an existing object of the same name is removed first, so running
        /// this on a scene that already carries the row replaces it rather than doubling it.</para>
        /// </summary>
        public static int Place(Transform parent = null)
        {
            var defs = new List<VehicleDef>();
            foreach ((string name, string path) in Row)
            {
                var def = AssetDatabase.LoadAssetAtPath<VehicleDef>(path);
                if (def == null || def.Mesh == null)
                {
                    Debug.LogWarning(
                        $"[StPetersMachines] no usable def at {path} ({name}) — the shop's row builds " +
                        "EMPTY. Bake the ATV pack (#742) before the region.");
                    return 0;
                }

                defs.Add(def);
            }

            List<float> beams = Beams();
            Quaternion heading = Heading;
            int placed = 0;

            for (int i = 0; i < defs.Count; i++)
            {
                string name = Row[i].Name;

                // Replace, never double. A scene that already carries the row is being re-run.
                GameObject existing = Find(name, parent);
                if (existing != null) Object.DestroyImmediate(existing);

                var go = new GameObject(name, typeof(Rigidbody2D));
                if (parent != null) go.transform.SetParent(parent, worldPositionStays: true);

                Vector2 stand = StandFor(i, beams);
                go.transform.position = new Vector3(stand.x, stand.y, 0f);
                go.transform.rotation = heading;

                // Serialized zero too, though VehicleController.Awake re-zeroes it at play — a machine
                // must not fall south through a top-down world. NineMileCreekTruckPark's own note.
                go.GetComponent<Rigidbody2D>().gravityScale = 0f;

                go.AddComponent<ParkedVehicle>().Configure(defs[i], drivable: true);
                placed++;
            }

            return placed;
        }

        /// <summary>Find one of the row by name — under <paramref name="parent"/> when there is one,
        /// else among the open scene's roots. Used by <see cref="Place"/> so a re-run replaces.
        /// </summary>
        static GameObject Find(string name, Transform parent)
        {
            if (parent != null)
            {
                Transform t = parent.Find(name);
                return t != null ? t.gameObject : null;
            }

            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == name) return root;

            return null;
        }

        /// <summary>
        /// ⭐ <b>The one-click way to get them into the scene the OWNER actually plays.</b>
        ///
        /// <para><b>Why a menu and not only a builder call.</b> <c>StPetersBuilder.Build()</c> is a
        /// FULL rebuild that wipes the hand-authored layer, and the guard's own escape hatch —
        /// "Refresh St Peters Island Logic" — does not exist for this region (memory
        /// <c>st-peters-scene-cannot-be-rebuilt</c>). And the owner's own <c>StPeters.unity</c> is a
        /// long way from the committed one, so a committed-scene edit alone would not put a machine in
        /// front of him. Same shape #765 shipped its quay-face fix in, for the same measured reason.
        /// The row goes into the committed scene as well, so CI and the repo agree with this.</para>
        /// </summary>
        [MenuItem("Hidden Harbours/St Peters/Park the Three Machines at the Store", priority = 60)]
        public static void ParkMenu()
        {
            int placed = Place();
            if (placed == 0)
            {
                Debug.LogWarning("[StPetersMachines] nothing placed — see the warning above.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[StPetersMachines] {placed} machines parked at the store — gate " +
                      $"{Gate.ToString("0.00")}, row along {Across.ToString("0.00")}, noses out on " +
                      $"{Forward.ToString("0.00")}. Save the scene to keep them.");
        }
    }
}
#endif
