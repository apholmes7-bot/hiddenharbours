using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Gives a boat her cabin</b> — the placement half of ADR 0038, built at runtime from the hull's
    /// own data and nothing else.
    ///
    /// <para><b>Runtime-spawned, exactly like the tank and the tackle.</b>
    /// <see cref="BoatController"/> mounts this in <c>Awake</c>, play-mode only, so every scene already
    /// built grows a cabin on load: <b>no builder re-run, no prefab churn, no scene edit.</b> That is the
    /// same reasoning <see cref="BoatAnchor"/> and <c>BoatFuelTank</c> ship under, and it is what makes a
    /// fleet-wide feature land without touching twenty-odd committed scenes.</para>
    ///
    /// <para><b>Mounted unconditionally; INERT unless the data says otherwise.</b> A hull with no
    /// <see cref="BoatVisualDef.Interior"/> has never been measured, and that is data rather than a fault
    /// — she builds nothing and costs one null check on load. The owner enrolling a hull later is a Def
    /// edit with nothing to re-wire.</para>
    ///
    /// <para><b>⭐ REBUILT ON EVERY HULL CHANGE</b> (owner, 2026-09-17). The hulls a player actually
    /// sails are swaps onto a root that never stopped existing — the dev picker, a purchase and a save
    /// restore all go through <see cref="BoatController.SetHull"/> — and a cabin built once at
    /// <c>Start</c> stayed the FIRST hull's cabin for the rest of the session: a cape islander or a
    /// lobster boat swapped onto a dory root had no door, no room and no cutaway at all.
    /// <see cref="Rebuild"/> takes the old cabin down and builds the one the new hull's data
    /// describes.</para>
    ///
    /// <para><b>⭐ WHAT IT BUILDS, AND WHY THE SHAPE.</b> Children of the boat ROOT, each a
    /// <see cref="HullLocalAnchor"/> so it stays square to the screen while the body under it yaws:</para>
    /// <list type="bullet">
    ///   <item><b>the room</b> — at the hull's own pivot (rig-local zero), carrying the
    ///   <see cref="SpriteRenderer"/> the interior cells are drawn into. Under the root and not under the
    ///   hull's visual child, because that child is where her RIDE is applied and a room hanging off it
    ///   would take her heave twice. <see cref="BoatInterior"/>'s own class doc states this requirement;
    ///   this is the thing that satisfies it.</item>
    ///   <item><b>the door</b> — at the def's <see cref="BoatInteriorDoor.ThresholdPoint"/>, projected
    ///   onto the drawn hull, carrying an ordinary <see cref="BoatCabinDoor"/>. It follows her round as
    ///   she turns, so the threshold is where the art puts it at every heading.</item>
    ///   <item><b>every additional door</b> — one per <see cref="BoatInteriorDef.AdditionalDoors"/>
    ///   entry (the 90's skylounge slider), each on its own child named
    ///   <c><see cref="DoorChildName"/>.&lt;id&gt;</c>, built exactly as the main door is and pointed at
    ///   the same cabin by its index. Only the main threshold draws the baked leaf pair.</item>
    /// </list>
    ///
    /// <para><b>⚠ THE EXTERIOR HALF IS DELIBERATELY NULL, AND THE DOOR IS DELIBERATELY SILENT.</b> The S0
    /// spike measured that a mesh hull has nothing to hand it: no submesh, no material subset that
    /// isolates a house, no free per-face channel, and an occluder that is per LEVEL rather than per
    /// hull. Until the per-level face tags land, the swap cannot complete — so
    /// <see cref="BoatInterior.SwapIsCompletable"/> is false, <see cref="BoatCabinDoor.IsAvailable"/> is
    /// false, and no cabin in the fleet is enterable. <b>That is the ruled outcome, not a gap.</b> The
    /// alternative — opening onto a half-wired swap — draws the interior and the exterior at once, posed
    /// differently, which is the co-visibility ADR 0038 forbids and the reason its motion clamp is safe.
    /// When a hull's exterior half arrives it is ONE argument here, and that cabin lights on its own.</para>
    ///
    /// <para><b>Refusals are loud; absences are not.</b> A hull with no interior is silent. A hull whose
    /// def is wired but MALFORMED — no px/m to composite at, or a cell array that does not match her
    /// levels — is named on the console and built no further, because that one is somebody's mistake and
    /// a cabin drawn at the wrong scale still looks like a cabin.</para>
    ///
    /// <para>Visual + interaction only: no sim, <b>no save</b>, and nothing per-hull in C# (rule 2) —
    /// every number comes off the def.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoatInteriorInstaller : MonoBehaviour
    {
        /// <summary>The room's node, so a test or a later pass can find it without a name lookup.</summary>
        public const string RoomChildName = "BoatInteriorRoom";

        /// <summary>The door's node.</summary>
        public const string DoorChildName = "BoatCabinDoor";

        [Tooltip("Sorting order for the interior cell. ABOVE the hull's own slot: the room is drawn " +
                 "over her, and what is behind it is hidden by covering it rather than by sorting.")]
        [SerializeField] private int _sortingOrderAboveHull = 2;

        /// <summary>The cabin this installer built, or null when the hull has none. Read by tests.</summary>
        public BoatInterior Interior { get; private set; }

        /// <summary>The door it built, or null.</summary>
        public BoatCabinDoor Door { get; private set; }

        /// <summary>The ADDITIONAL doors it built, in the def's order — empty on every hull with one way
        /// in. Read by tests; the walker finds doors by searching the root, as it always has.</summary>
        public IReadOnlyList<BoatCabinDoor> AdditionalDoors => _additionalDoors;

        private readonly List<BoatCabinDoor> _additionalDoors = new List<BoatCabinDoor>();

        /// <summary>The cutaway it built, or null on a hull with no mesh to cut. Read by tests.</summary>
        public BoatCutaway Cutaway { get; private set; }

        /// <summary>True when this hull was measured and built. False is the ordinary answer for most of
        /// the fleet and is not a fault.</summary>
        public bool Built => Interior != null;

        /// <summary>The visual def the last <see cref="Build"/> read — the cabin standing now was built
        /// from it, and "she has none" is an answer too. <see cref="Rebuild"/> compares against it.</summary>
        private BoatVisualDef _builtFrom;

        /// <summary>True once <see cref="Build"/> has read a hull. Before that, <c>Start</c>'s own build
        /// is still to come and will read whatever hull she wears by then.</summary>
        private bool _hasRead;

        /// <summary>The sprite room's node, or null on a mesh-room hull.</summary>
        private GameObject _room;

        private void Start() => Build();

        /// <summary>
        /// Build the cabin for the hull she wears now. Public so a test drives it without waiting on a
        /// frame, and idempotent so a second call is free — a DIFFERENT hull goes through
        /// <see cref="Rebuild"/>.
        /// </summary>
        public void Build()
        {
            if (Interior != null) return;

            var controller = GetComponent<BoatController>();
            BoatHullDef hull = controller != null ? controller.Hull : null;
            BoatVisualDef visual = hull != null ? hull.Visual : null;
            BoatInteriorDef def = visual != null ? visual.Interior : null;
            _builtFrom = visual;
            _hasRead = true;

            // ABSENCE IS DATA — most of the fleet has never been measured. Silent, and cheap.
            if (def == null || !def.HasInterior()) return;

            // REFUSAL IS LOUD — this one is somebody's mistake, and a cabin composited at the fleet's
            // 32 px/m when her sheets baked at 16 is still a perfectly plausible-looking cabin.
            if (def.PixelsPerMetre <= 0)
            {
                Debug.LogError($"[BoatInteriorInstaller] '{name}' carries interior '{def.Id}' with " +
                               $"PixelsPerMetre {def.PixelsPerMetre}. Two pixel grids live in this kit " +
                               "(the tanker bakes at 16 where the fleet is 32) and there is no fleet " +
                               "default to fall back on. Re-run the interior def builder.", this);
                return;
            }

            float elevation = BakeElevationDegrees(visual);
            bool exteriorCcw = ExteriorAzimuthCounterClockwise(visual);

            // --- which picture (ADR 0041) -------------------------------------------------------
            // A hull whose bake appended her room to the hull mesh already carries her cabin in the
            // hull renderer; the cutaway reveals it. She gets NO sprite room — no child, no
            // SpriteRenderer, no cells, ever — or she would draw her cabin twice (measured on main
            // 2026-09-01: TWO sources below decks on both converted hulls). The predicate is the
            // bake's own output (HullMeshDef.InteriorRamps), read through one method and never a list
            // kept here. Everything else about the def stays exactly as it was.
            bool roomIsGeometry = visual.HasHullMesh() && visual.HullMesh.HasMeshInterior();

            // --- the room (sprite hulls only) -----------------------------------------------------
            SpriteRenderer roomRenderer = null;
            Transform roomPivot = null;
            if (!roomIsGeometry)
            {
                var roomGo = new GameObject(RoomChildName);
                roomGo.transform.SetParent(transform, false);
                var roomAnchor = roomGo.AddComponent<HullLocalAnchor>();
                roomAnchor.Configure(transform, Vector3.zero, exteriorCcw, elevation);

                roomRenderer = roomGo.AddComponent<SpriteRenderer>();
                roomRenderer.sortingOrder = _sortingOrderAboveHull;
                roomRenderer.enabled = false;   // nobody is inside a boat that has just been built
                roomPivot = roomGo.transform;
                _room = roomGo;
            }

            Interior = gameObject.AddComponent<BoatInterior>();
            Interior.Configure(
                def,
                // ⚠ THE EXTERIOR HALF, LEFT NULL ON PURPOSE — see the class remarks. This argument is
                // the whole of what the per-level face tags will fill in, and it is kept in the
                // signature precisely so that landing them is one edit here and not a redesign.
                exterior: null,
                interior: roomRenderer,
                fittings: null,
                interiorPivot: roomPivot,
                boatRoot: transform,
                // ⚠ NO CELLS HERE, deliberately. The pixels are megabytes and this runs on every
                // hull that spawns; handing them in would put a cabin's whole sheet set in memory for
                // a boat nobody boards. The cabin loads them itself at the door's cue start, from
                // Resources, keyed off the def — so a hull that is never entered costs nothing.
                cells: null,
                facings: 8,
                cellsAreCounterClockwise: true,
                zeroHeadingDegrees: visual.ZeroHeadingDegrees,
                deckRollDegrees: RockRollDegrees(visual),
                deckHeavePixels: RockHeavePixels(visual),
                deckPitchLiftMeters: PitchLiftMetres(visual),
                // ⚠ The def's levels are NOT the sheet's rows; the map arrives with the cells.
                cellRowForLevel: null,
                // The mesh itself, not a flag off it: the cabin re-asks it which levels are rooms
                // (LevelIndexAtHeight), which a bool could not answer.
                meshRoom: roomIsGeometry ? visual.HullMesh : null,
                // …and a SPRITE cabin that still wears a hull mesh reads which levels are rooms off
                // that mesh's level table until her cells arrive (the Convertible, the Skybridge, the
                // tanker): the walker taking a companionway must know before the sheets are loaded.
                levelTable: visual.HasHullMesh() ? visual.HullMesh : null);

            // --- the cutaway --------------------------------------------------------------------
            // The owner's 2026-08-26 ruling: below decks, her house is CUT AWAY rather than covered
            // over. Only a MESH hull can be cut — a sprite hull's house is pixels in a cell — so a
            // hull with no mesh gets no component at all rather than an inert one that subscribes to
            // three signals forever to answer 0 every time. The controller is handed in as the helm
            // identity token: it is the same object ControlSwitcher declares as the piloted hull, and
            // re-deriving it later is how a token drifts off the declaration it is compared against.
            if (visual.HasHullMesh())
            {
                Cutaway = gameObject.AddComponent<BoatCutaway>();
                Cutaway.Configure(Interior, visual.HullMesh, transform, controller);
            }

            // --- the doors ----------------------------------------------------------------------
            // The main threshold, then every ADDITIONAL door the def names (Phase A, C5) — the 90's
            // skylounge slider onto her upper aft deck, which the def carried and nothing built, so the
            // skylounge had no way out but the stairs. Each is the same shape as the main door: its
            // own child, its own anchor at its own sill, an ordinary BoatCabinDoor pointed at the ONE
            // cabin by its index. A def with no main threshold still builds its extras.
            if (def.Door != null)
                Door = BuildDoor(DoorChildName, def, def.Door, -1, exteriorCcw, elevation);

            BoatInteriorDoor[] extras = def.AdditionalDoors;
            if (extras == null) return;
            for (int i = 0; i < extras.Length; i++)
            {
                BoatInteriorDoor extra = extras[i];
                if (extra == null) continue;
                _additionalDoors.Add(BuildDoor($"{DoorChildName}.{DoorId(extra, i)}", def, extra, i,
                                               exteriorCcw, elevation));
            }
        }

        /// <summary>
        /// One door: a child of the root anchored at its own sill on the drawn hull, carrying a
        /// <see cref="BoatCabinDoor"/> that reads its threshold off the def by
        /// <paramref name="additionalIndex"/> (-1 = the main threshold).
        /// </summary>
        private BoatCabinDoor BuildDoor(string childName, BoatInteriorDef def, BoatInteriorDoor door,
                                        int additionalIndex, bool exteriorCcw, float elevation)
        {
            var doorGo = new GameObject(childName);
            doorGo.transform.SetParent(transform, false);
            var doorAnchor = doorGo.AddComponent<HullLocalAnchor>();
            doorAnchor.Configure(transform, door.ThresholdPoint, exteriorCcw, elevation);

            var built = doorGo.AddComponent<BoatCabinDoor>();
            built.Configure(Interior, $"fixture.boat.{def.Id}.{DoorId(door, additionalIndex)}", additionalIndex,
                            // ⚠ The labels name what the PRESS does, and since 2026-08-28 the press moves
                            // the LEAF — going below is a walk and has no prompt of its own. "Go below" on
                            // a press that opens a door would be the words drifting from the action, which
                            // is the exact failure VerbLabel derives itself to avoid (rule 6).
                            ReachMetres(door), "Open the door", "Close the door");
            return built;
        }

        /// <summary>
        /// <b>Her hull has changed: take the old cabin down and build the new hull's.</b> Called by
        /// <see cref="BoatController.SetHull"/>. Free when nothing changed — the same visual def is the
        /// same cabin, so a save restore of the hull she already wears keeps a player who is below
        /// exactly where they are — and a no-op before the first <see cref="Build"/>, whose read of the
        /// hull is still to come.
        /// </summary>
        public void Rebuild()
        {
            if (!_hasRead) return;

            var controller = GetComponent<BoatController>();
            BoatHullDef hull = controller != null ? controller.Hull : null;
            BoatVisualDef visual = hull != null ? hull.Visual : null;
            if (ReferenceEquals(visual, _builtFrom)) return;

            TearDown();
            Build();
        }

        /// <summary>
        /// Remove everything <see cref="Build"/> made.
        ///
        /// <para><b>Out through the cabin's own exit first</b>, so <c>CabinLeft</c> is published while
        /// the cutaway that closes the house on it still exists.</para>
        ///
        /// <para>⚠ <b><c>DestroyImmediate</c>, not <c>Destroy</c>.</b> <see cref="BoatInterior"/>,
        /// <see cref="BoatCutaway"/> and <see cref="BoatCabinDoor"/> are all
        /// <c>[DisallowMultipleComponent]</c>, and <c>Destroy</c> only marks them: the old ones would
        /// stand until the end of the frame, refusing the <c>AddComponent</c> of the build that follows
        /// in this same call, and a doorway search in between would find the old door.</para>
        /// </summary>
        private void TearDown()
        {
            if (Interior != null && Interior.IsInside) Interior.TryExit();

            if (Door != null) DestroyImmediate(Door.gameObject);
            for (int i = 0; i < _additionalDoors.Count; i++)
                if (_additionalDoors[i] != null) DestroyImmediate(_additionalDoors[i].gameObject);
            if (_room != null) DestroyImmediate(_room);
            if (Cutaway != null) DestroyImmediate(Cutaway);
            if (Interior != null) DestroyImmediate(Interior);

            Door = null;
            _additionalDoors.Clear();
            _room = null;
            Cutaway = null;
            Interior = null;
        }

        /// <summary>The door's own id, or a stable stand-in. Ids must be unique among live registrants,
        /// and two boats of one class are two live registrants — the def id in the prefix is what
        /// separates them. An unnamed ADDITIONAL door is named by its index, so it can never take the
        /// main door's stand-in.</summary>
        private static string DoorId(BoatInteriorDoor door, int additionalIndex)
            => !string.IsNullOrEmpty(door.Id) ? door.Id
             : additionalIndex < 0 ? "entry"
             : $"additional_{additionalIndex}";

        /// <summary>
        /// How close you must stand to work this door. Derived from the leaf the kit measured rather
        /// than typed: a slider's clear width is the opening you step through, and a threshold you have
        /// to line up on to the pixel is not cozy (the doorway-gap reasoning <c>BuildingInterior</c>
        /// already carries). Floored so a hull whose sidecar omits the width is still reachable.
        /// </summary>
        private static float ReachMetres(BoatInteriorDoor door)
            => Mathf.Max(1.2f, door.ClearWidthMeters + 0.5f);

        /// <summary>The elevation this hull's art is presented at, off her own def — 40° for the iso
        /// rigs. The mesh def states it; a sprite-only hull falls back to the compass's own field.
        ///
        /// <para><b>Public because it is the ONE derivation.</b> Anything that places a gameplay point on
        /// this hull must project it the way her art projects, and two callers reading the same two fields
        /// in two files is how a room and its own doorway come to disagree about where the door is. Both
        /// halves of the intro cabin — the walker on the sole and the anchor the door hangs off — ask
        /// here. ⚠ Null-safe now that a second caller exists: a hull with no visual def is presented in
        /// PLAN view, which is the identity projection and therefore exactly the placement an unmeasured
        /// hull already has.</para></summary>
        public static float BakeElevationDegrees(BoatVisualDef visual)
            => visual == null
                   ? DeckAreaMath.PlanViewElevationDegrees
                   : visual.HullMesh != null ? visual.HullMesh.ElevationDeg
                                             : visual.ArtBakeElevationDegrees;

        /// <summary>
        /// The MEASURED handedness of this hull's exterior art. The mesh def's is the authority when
        /// there is one — it is the number <c>RigAzimuthProbe</c> took off rendered pixels — and the
        /// sprite compass's own flag otherwise.
        ///
        /// <para>⚠ Never folded together with the INTERIOR sheets' handedness: they are different
        /// artwork and they genuinely differ (the lobster boat's compass runs clockwise where her
        /// interior runs counter-clockwise).</para>
        ///
        /// <para>Public for the same reason <see cref="BakeElevationDegrees"/> is: it is half of one
        /// projection, and the half that MIRRORS the hull end for end when it is answered twice. Null-safe
        /// — a hull with no def keeps the counter-clockwise convention <see cref="DeckAreaMath"/> itself
        /// assumes, so an unmeasured hull's placement is unchanged.</para>
        /// </summary>
        public static bool ExteriorAzimuthCounterClockwise(BoatVisualDef visual)
            => visual == null
                || (visual.HullMesh != null
                        ? visual.HullMesh.AzimuthCounterClockwise
                        : visual.FacingsAreCounterClockwise);

        /// <summary>Peak roll of this hull's baked rock cycle — an ART FACT off her own def, never a
        /// constant here (rule 6). 0 when she has no mesh def, which draws a still cabin rather than a
        /// guessed one.</summary>
        private static float RockRollDegrees(BoatVisualDef visual)
            => visual.HullMesh != null ? visual.HullMesh.RockRollDegrees : 0f;

        /// <summary>Peak heave of the same cycle, in the rig's own pixels.</summary>
        private static float RockHeavePixels(BoatVisualDef visual)
            => visual.HullMesh != null ? visual.HullMesh.RockHeavePixels : 0f;

        /// <summary>
        /// Screen-vertical travel at the peak of the PITCH, metres — what the rig's pitch amplitude
        /// (degrees) does to the picture at this bake elevation.
        ///
        /// <para>Derived rather than typed: the rigs state pitch as a rotation, and how much screen
        /// travel it causes depends on the camera. Small by construction — a couple of centimetres on a
        /// working boat — which is why <c>DeckRiderVisual</c> carries 0.02 as its own tuned default.</para>
        /// </summary>
        private static float PitchLiftMetres(BoatVisualDef visual)
        {
            if (visual.HullMesh == null) return 0f;
            float pitchRadians = visual.HullMesh.RockPitchDegrees * Mathf.Deg2Rad;
            float elevation = visual.HullMesh.ElevationDeg * Mathf.Deg2Rad;
            // The bow rises by sin(pitch) of the half-length; on screen that reads through the camera's
            // own cosine. Half-length is not on the def, so this uses the hull's own cell half-height in
            // metres, which is the same order and comes off the same bake.
            float halfLengthMetres = visual.HullMesh.PxPerMetre > 0
                ? (visual.HullMesh.CellH * 0.5f) / visual.HullMesh.PxPerMetre
                : 0f;
            return Mathf.Sin(pitchRadians) * halfLengthMetres * Mathf.Cos(elevation);
        }
    }
}
