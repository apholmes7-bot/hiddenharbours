using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.App
{
    /// <summary>
    /// ⭐ <b>THE FIRST ROOM IN THE GAME IS THE INSIDE OF SOMEBODY ELSE'S BOAT.</b> A new game opens with
    /// the player BELOW DECKS on Armand's cape islander while he runs the marks — she can get up, walk
    /// about his cabin, and come out through the aft door when she wants to watch him come alongside.
    ///
    /// <para><b>Why this is a plain class and not a component.</b> Everything it holds is bookkeeping
    /// about ONE passage: which room she is in, where on its sole she is standing, and which way she was
    /// last walking. It never writes her transform — <see cref="ArrivalOpening"/> asks it where she should
    /// be and puts her there, in the one <c>LateUpdate</c> that already owns her position for the whole
    /// arrival. Two things placing the player is the defect this codebase has already paid for twice (the
    /// walk-in-place, and the seat that dragged her back off the wharf mid-step); one owner, one write.
    /// </para>
    ///
    /// <para><b>⭐ IT ADDS NO SECOND MECHANISM.</b> The room, the door, the cutaway and the level map are
    /// the ones <see cref="BoatInteriorInstaller"/> already grows on every hull that spawns; going below
    /// is <see cref="BoatInterior.TryEnter"/>, which publishes <c>CabinEntered</c> exactly as the door's
    /// own press does, so <see cref="BoatCutaway"/> opens her house through the seam it already listens
    /// on and nothing here knows the cutaway exists. Coming out is the DOOR — the real
    /// <see cref="BoatCabinDoor"/>, with its measured cue, its level resolution and its sheet load — never
    /// a second copy of it. What is new is only that a walker inside a moving hull has a POSITION on the
    /// sole, and that is what this holds.</para>
    ///
    /// <para><b>⚠ The passenger is not at the helm, and this is the whole of why the house may open.</b>
    /// <see cref="BoatCutaway"/> refuses the cut for whoever is steering (the occupancy law, #642), and
    /// the arrival never declares the player as piloting anything — she is carried. So a passenger below
    /// gets the cut and Armand keeps his wheel, which is the ruling read straight off the two facts that
    /// already say it.</para>
    ///
    /// <para><b>⚠ Def LEVELS are not sheet ROWS.</b> Nothing here indexes a cell array. The level is the
    /// room the door is cut into (<see cref="BoatCabinDoor.RoomLevelIndex"/>: the level whose outline its
    /// wall line is on), and for a door with no wall line the one its sill height resolves to through
    /// <see cref="BoatInterior.LevelIndexAtHeight"/> — the same call <see cref="BoatCabinDoor.TryUse"/>
    /// makes, and the one place that knows the def's levels, the sheet's rows and the −1 that means "an
    /// outdoor deck draws nothing".</para>
    ///
    /// <para><b>⭐ The doorway (owner ruling D1 (a), 2026-09-30).</b> Within one clear width of the door, a
    /// key held within 30° of its axis is bent toward the opening (<see cref="BoatCabinDoor.SteerHeld"/>),
    /// and a key the doorway carries walks her round the furniture just inside rather than into it
    /// (<see cref="BoatCabinWalkMath.StepRound"/>). Farther off, nothing is bent. Her pace, gait and
    /// facing come across the doorway with her (<see cref="SeedFromDeck"/>), and the pace eases to this
    /// floor's rather than switching on the sill.</para>
    /// </summary>
    internal sealed class ArrivalCabinWalk
    {
        private readonly BoatInterior _cabin;
        private readonly BoatCabinDoor _door;
        private readonly BoatCutaway _cutaway;
        private readonly BoatInteriorLevel _level;
        private readonly int _levelIndex;
        private readonly float _soleZMetres;
        private readonly float _bakeElevationDegrees;
        private readonly bool _azimuthCounterClockwise;
        private readonly float _walkSpeedMetresPerSecond;

        private Vector2 _local;
        private float _headingDegrees;
        private float _speedMetresPerSecond;
        private Vector2 _held;
        private float _paceMetresPerSecond;
        private float _paceFromMetresPerSecond;
        private float _sinceSeedSeconds = float.PositiveInfinity;

        private ArrivalCabinWalk(BoatInterior cabin, BoatCabinDoor door, BoatCutaway cutaway,
                                 int levelIndex, BoatInteriorLevel level,
                                 float bakeElevationDegrees, bool azimuthCounterClockwise,
                                 float walkSpeedMetresPerSecond)
        {
            _cabin = cabin;
            _door = door;
            _cutaway = cutaway;
            _levelIndex = levelIndex;
            _level = level;
            _soleZMetres = level.SoleZMeters;
            _bakeElevationDegrees = bakeElevationDegrees;
            _azimuthCounterClockwise = azimuthCounterClockwise;
            _walkSpeedMetresPerSecond = Mathf.Max(0f, walkSpeedMetresPerSecond);
            _paceMetresPerSecond = _walkSpeedMetresPerSecond;
            _local = BoatCabinWalkMath.StartPointFor(level, door != null ? door.Door : null);
        }

        // ---- what she is standing in -----------------------------------------------------------------

        /// <summary>The cabin itself — the thing that owns "is she inside", so nothing here keeps a second
        /// copy of that answer.</summary>
        public BoatInterior Cabin => _cabin;

        /// <summary>Her way out: the hull's own aft door, with the cue the sidecar measured.</summary>
        public BoatCabinDoor Door => _door;

        /// <summary>The cut this hull is being asked for, or null on a hull with no mesh to cut. Held for
        /// the fixture — the gate is <see cref="BoatCutaway"/>'s and this never writes to it.</summary>
        public BoatCutaway Cutaway => _cutaway;

        /// <summary>Which of the def's levels the aft door walks her in onto.</summary>
        public int LevelIndex => _levelIndex;

        /// <summary>Where she is standing, in the sole's own hull-local metres.</summary>
        public Vector2 LocalPosition => _local;

        /// <summary>The height of that sole in the hull's rig metres (up from the keel): the third
        /// coordinate of where she is standing, for a figure placed in the hull's own frame.</summary>
        public float SoleHeightMetres => _soleZMetres;

        /// <summary>Which way she was last walking, as a compass heading — held rather than measured for
        /// the reason <c>ArrivalOpening.PoseThePassenger</c> states: the transform she is drawn from
        /// carries the BOAT's motion as well as her own, so a drawer measuring it reads a fisher at five
        /// knots standing still.</summary>
        public float HeadingDegrees => _headingDegrees;

        /// <summary>Her honest travelling speed: metres of SOLE per second, which is the floor she
        /// actually crosses. Zero on a tick with no input, so she stands rather than moonwalks — and on the
        /// tick she comes in through the doorway, the pace she came through at (<see cref="SeedFromDeck"/>),
        /// so a figure walking through a doorway is never drawn standing.</summary>
        public float SpeedMetresPerSecond => _speedMetresPerSecond;

        /// <summary>The pace her keys walk her at, metres of sole per second: this floor's own walk speed, or
        /// on its way to it from the pace she came through the doorway at.</summary>
        public float PaceMetresPerSecond => _paceMetresPerSecond;

        /// <summary>The gait she carries out through a doorway (<c>ArrivalDeckWalk.SeedFromCabin</c>): her
        /// travelling speed, or the pace her key was asking for when the wall line cut the step short —
        /// the crossing tick's step stops at the line, and a figure walking through a doorway must not be
        /// drawn slowing in it.</summary>
        public float GaitThroughTheDoorwayMetresPerSecond
            => Mathf.Max(_speedMetresPerSecond, _paceMetresPerSecond * Mathf.Min(1f, _held.magnitude));

        /// <summary>Her key this tick in the hull's frame, as her step turned it and before any doorway bent
        /// it — what the doorway is asked with (<see cref="BoatCabinDoor.TryWalkThrough"/>). Zero for no key.
        /// </summary>
        public Vector2 HeldHullLocal => _held;

        /// <summary>True while the occupant is below on this hull.</summary>
        public bool IsBelow => _cabin != null && _cabin.IsInside;

        // ---- opening it ------------------------------------------------------------------------------

        /// <summary>
        /// <b>Find this hull's cabin, or say honestly that she has none.</b> Returns null for every hull
        /// the interiors kit has not measured, which is most of the fleet and is DATA rather than a fault —
        /// the arrival then opens on deck exactly as it did before, with nothing else changed.
        ///
        /// <para>⚠ <see cref="BoatInteriorInstaller.Build"/> is called rather than waited for. The
        /// installer builds in <c>Start</c>, which is the end of the frame the boat was activated in, and
        /// the arrival needs the room in the SAME call it spawns her — a player who spends one frame on
        /// deck before the cabin exists is a player the opening flickers at. Build is idempotent
        /// (<c>if (Interior != null) return;</c>), so calling it early costs nothing and the installer's
        /// own <c>Start</c> becomes a no-op.</para>
        /// </summary>
        public static ArrivalCabinWalk TryOpen(BoatController boat, float walkSpeedMetresPerSecond)
        {
            if (boat == null) return null;

            var installer = boat.GetComponent<BoatInteriorInstaller>();
            if (installer == null) return null;      // EditMode, or a controller built without the mount
            installer.Build();

            BoatInterior cabin = installer.Interior;
            if (cabin == null || !cabin.HasInterior) return null;
            if (!BoatInteriorEntryPolicy.MayOffer(cabin)) return null;

            BoatCabinDoor door = installer.Door;
            if (door == null || door.Door == null)
            {
                Debug.LogWarning($"[ArrivalCabinWalk] '{boat.name}' carries a measured interior with no " +
                                 "threshold, so there would be no way back out on deck. The opening " +
                                 "stays topside.", boat);
                return null;
            }

            // The room she walks is the room the doorway is cut into: the level whose outline its wall
            // line is on — the level the doorway's own crossing is measured against. A door with no wall
            // line falls back to the level its sill resolves to, the question the door asks at its own
            // press. −1 is a real answer (an outdoor deck draws nothing) and it means this hull has no
            // room to start the game in.
            int levelIndex = door.RoomLevelIndex;
            if (levelIndex < 0) levelIndex = cabin.LevelIndexAtHeight(door.Door.ThresholdPoint.z);
            if (levelIndex < 0 || !cabin.IsUsableLevel(levelIndex))
            {
                Debug.LogWarning($"[ArrivalCabinWalk] '{boat.name}': her door's sill at " +
                                 $"{door.Door.ThresholdPoint.z:F2} m resolves to no drawable level, so " +
                                 "the opening stays topside.", boat);
                return null;
            }

            BoatInteriorDef def = cabin.Def;
            BoatInteriorLevel level = def.Levels[levelIndex];
            BoatVisualDef visual = boat.Hull != null ? boat.Hull.Visual : null;

            return new ArrivalCabinWalk(cabin, door, boat.GetComponent<BoatCutaway>(),
                                        levelIndex, level,
                                        BoatInteriorInstaller.BakeElevationDegrees(visual),
                                        BoatInteriorInstaller.ExteriorAzimuthCounterClockwise(visual),
                                        walkSpeedMetresPerSecond);
        }

        /// <summary>
        /// <b>Put her below.</b> The sheets come in first — the door's press does this at its cue start and
        /// the cabin's own remark says why (megabytes that nothing references until somebody actually goes
        /// in) — and then the ordinary transition, which publishes <c>CabinEntered</c> and opens the house.
        /// </summary>
        public bool GoBelow()
        {
            if (_cabin == null || _cabin.IsInside) return false;
            _cabin.EnsureCells();
            return _cabin.TryEnter(_levelIndex);
        }

        // ---- walking about it ------------------------------------------------------------------------

        /// <summary>
        /// One step about the sole. <paramref name="moveInput"/> is the screen-axis walk input (the same
        /// vector <c>DeckWalkController</c> reads), <paramref name="drawnHeadingDegrees"/> the heading of
        /// the hull PICTURE she is inside.
        ///
        /// <para>Her FACING comes out of the WORLD travel the step produced rather than out of the input,
        /// because the picture is what the player is looking at. Her GAIT is metres of sole: the floor she
        /// crosses, and the same measure the deck walk reads its planking in, so the two readouts meet at
        /// the doorway.</para>
        ///
        /// <para>⭐ <b>Near the doorway (D1 (a)).</b> The key is bent toward the opening within its pull, and
        /// a key the doorway carries is stepped round the furniture just inside instead of into it. The
        /// pace eases from the one she came through at to this floor's over
        /// <paramref name="paceBlendSeconds"/>.</para>
        /// </summary>
        public void Step(Vector2 moveInput, float deltaSeconds, float drawnHeadingDegrees,
                         float paceBlendSeconds)
        {
            Vector2 before = _local;
            float dt = Mathf.Max(0f, deltaSeconds);
            _sinceSeedSeconds += dt;
            _paceMetresPerSecond = BlendedPace(_paceFromMetresPerSecond, _walkSpeedMetresPerSecond,
                                               _sinceSeedSeconds, paceBlendSeconds);

            _held = BoatCabinWalkMath.HeldOnTheSole(moveInput, drawnHeadingDegrees, _bakeElevationDegrees,
                                                    _azimuthCounterClockwise);
            // `!= null`, never `?.` — the door is a UnityEngine.Object (see ArrivalOpening.WalkTheCabin).
            Vector2 steered = _door != null ? _door.SteerHeld(_local, _held) : _held;
            _local = _door != null && _door.CarriesHeld(_local, _held)
                ? BoatCabinWalkMath.StepRound(_level, _local, steered, _paceMetresPerSecond, dt)
                : BoatCabinWalkMath.StepHeld(_level, _local, steered, _paceMetresPerSecond, dt);

            _speedMetresPerSecond = (_local - before).magnitude / Mathf.Max(1e-4f, deltaSeconds);

            Vector2 travel = WorldOffset(_local, drawnHeadingDegrees)
                             - WorldOffset(before, drawnHeadingDegrees);

            // ⚠ Her facing is KEPT when she stops rather than reset: a fisher who finishes a step facing
            // the stove and is then drawn facing north because nobody was pressing a key is the same
            // "resolved from a zero velocity" defect MooredBoat's skipper hold exists to prevent.
            if (travel.sqrMagnitude > 1e-8f) _headingDegrees = ArrivalPilot.CompassOf(travel);
        }

        /// <summary>Where she is standing, in the world — the hull's pivot plus her point on the sole,
        /// through the projection the hull's own art is drawn by. The SAME transform that puts her door on
        /// screen (<see cref="HullLocalAnchor"/>), which is what makes the threshold a place she can
        /// actually walk to rather than a coordinate that happens to be nearby.</summary>
        public Vector3 WorldPosition(Transform boatRoot, float drawnHeadingDegrees, float z)
        {
            if (boatRoot == null) return new Vector3(0f, 0f, z);
            Vector2 offset = WorldOffset(_local, drawnHeadingDegrees);
            return new Vector3(boatRoot.position.x + offset.x, boatRoot.position.y + offset.y, z);
        }

        /// <summary>
        /// <b>Seed her from where she is standing now</b> — used at the doorway, in both directions, so
        /// that crossing it moves nobody. Coming in, this reads the deck position she pressed the door
        /// from back onto the sole; the clamp then walks her the last few centimetres INSIDE, because a
        /// threshold is on the sole's edge by construction.
        ///
        /// <para>The same law <c>DeckWalkController.SeedDeckLocalFromTransform</c> keeps: when something
        /// other than this put her somewhere, read it rather than overrule it.</para>
        /// </summary>
        public void SeedFromWorld(Transform boatRoot, Vector3 world, float drawnHeadingDegrees)
        {
            if (boatRoot == null) return;
            Vector2 relative = (Vector2)world - (Vector2)boatRoot.position;
            Vector2 read = BoatCabinWalkMath.FromWorldOffset(relative, _soleZMetres, drawnHeadingDegrees,
                                                             _bakeElevationDegrees,
                                                             _azimuthCounterClockwise);
            _local = BoatCabinWalkMath.ClampToSole(_level, read, _local);
            _speedMetresPerSecond = 0f;
        }

        /// <summary>
        /// ⭐ <b>Seed her from the deck walk's own point</b> — the doorway's join going in, when the deck
        /// walk is what held her. Both walks speak the hull's own metres, and the doorway only takes her
        /// standing on its wall line (<see cref="BoatCabinThreshold.HasReachedTheWallLine"/>), which is the
        /// one place both floors can hold her: so the sole's clamp moves her by a hair at most, with no
        /// round trip through a world point drawn by last frame's hull.
        ///
        /// <para>Her pace, her gait and her facing come across with her. The pace then eases to this
        /// floor's in <see cref="Step"/> — the two speeds blended across the doorway rather than switched
        /// on its sill — and the gait is what she was making on the tick she crossed, so the figure is
        /// never drawn standing in the doorway she is walking through.</para>
        /// </summary>
        public void SeedFromDeck(Vector2 hullLocalMetres, float carriedPaceMetresPerSecond,
                                 float carriedSpeedMetresPerSecond, float compassHeadingDegrees)
        {
            _local = BoatCabinWalkMath.ClampToSole(_level, hullLocalMetres, _local);
            _paceFromMetresPerSecond = Mathf.Max(0f, carriedPaceMetresPerSecond);
            _paceMetresPerSecond = _paceFromMetresPerSecond;
            _sinceSeedSeconds = 0f;
            _speedMetresPerSecond = Mathf.Max(0f, carriedSpeedMetresPerSecond);
            _headingDegrees = compassHeadingDegrees;
        }

        /// <summary>
        /// The pace a walker is at <paramref name="sinceSeedSeconds"/> after coming through a doorway at
        /// <paramref name="fromMetresPerSecond"/>: a straight ease to <paramref name="toMetresPerSecond"/>
        /// over <paramref name="blendSeconds"/>, and that floor's own pace from then on (or at once, for a
        /// blend of 0). Both of the arrival's walks use it, so the two sides of the doorway agree.
        /// </summary>
        internal static float BlendedPace(float fromMetresPerSecond, float toMetresPerSecond,
                                          float sinceSeedSeconds, float blendSeconds)
            => blendSeconds > 0f && sinceSeedSeconds < blendSeconds
                ? Mathf.Lerp(fromMetresPerSecond, toMetresPerSecond, sinceSeedSeconds / blendSeconds)
                : toMetresPerSecond;

        private Vector2 WorldOffset(Vector2 local, float drawnHeadingDegrees)
            => BoatCabinWalkMath.ToWorldOffset(local, _soleZMetres, drawnHeadingDegrees,
                                               _bakeElevationDegrees, _azimuthCounterClockwise);
    }
}
