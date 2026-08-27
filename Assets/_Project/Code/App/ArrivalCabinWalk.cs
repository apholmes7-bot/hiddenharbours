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
    /// <para><b>⚠ Def LEVELS are not sheet ROWS.</b> Nothing here indexes a cell array. The level is
    /// resolved from the door's own sill height through <see cref="BoatInterior.LevelIndexAtHeight"/> —
    /// the same call <see cref="BoatCabinDoor.TryUse"/> makes — which is the one place that knows the def's
    /// levels, the sheet's rows and the −1 that means "an outdoor deck draws nothing".</para>
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

        /// <summary>Which way she was last walking, as a compass heading — held rather than measured for
        /// the reason <c>ArrivalOpening.PoseThePassenger</c> states: the transform she is drawn from
        /// carries the BOAT's motion as well as her own, so a drawer measuring it reads a fisher at five
        /// knots standing still.</summary>
        public float HeadingDegrees => _headingDegrees;

        /// <summary>Her honest travelling speed: metres of SOLE per second, which is the floor she
        /// actually crosses. Zero on a tick with no input, so she stands rather than moonwalks.</summary>
        public float SpeedMetresPerSecond => _speedMetresPerSecond;

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

            // The sill states which level you walk in ONTO — the same question the door asks at its own
            // press, asked of the same authority. −1 is a real answer (an outdoor deck draws nothing) and
            // it means this hull has no room to start the game in.
            int levelIndex = cabin.LevelIndexAtHeight(door.Door.ThresholdPoint.z);
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
        /// <para>Her facing and her gait come out of the WORLD travel the step produced rather than out of
        /// the input, because the picture is what the player is looking at: a metre of sole walked into the
        /// foreshortened axis is less than a metre of screen, and a fisher drawn striding at a pace she is
        /// not making is the walk-in-place defect with the sign flipped.</para>
        /// </summary>
        public void Step(Vector2 moveInput, float deltaSeconds, float drawnHeadingDegrees)
        {
            Vector2 before = _local;
            _local = BoatCabinWalkMath.Step(_level, _local, moveInput, _walkSpeedMetresPerSecond,
                                            deltaSeconds, drawnHeadingDegrees, _bakeElevationDegrees,
                                            _azimuthCounterClockwise);

            Vector2 travel = WorldOffset(_local, drawnHeadingDegrees)
                             - WorldOffset(before, drawnHeadingDegrees);

            float dt = Mathf.Max(1e-4f, deltaSeconds);
            _speedMetresPerSecond = travel.magnitude / dt;

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

        private Vector2 WorldOffset(Vector2 local, float drawnHeadingDegrees)
            => BoatCabinWalkMath.ToWorldOffset(local, _soleZMetres, drawnHeadingDegrees,
                                               _bakeElevationDegrees, _azimuthCounterClockwise);
    }
}
