using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>THE WORLD XY PLANE IS THE SQUASHED GROUND PLANE</b> — and this is where anything that needs a
    /// ground BEARING out of a world-space vector comes to get it.
    ///
    /// <para>The shared ¾ bake camera sits 40° above the horizon, so one metre of NORTHWARD ground travel
    /// draws <c>sin 40° ≈ 0.643</c> world units up the screen while one metre EASTWARD draws a full unit
    /// across. World XY is therefore not a compass rose: <c>atan2(dx, dy)</c> over a world delta answers a
    /// SCREEN angle, and the answer is wrong by up to <b>12.56°</b> off the cardinals — which is over a
    /// quarter of an eight-way facing cell.</para>
    ///
    /// <para><b>Why this lives in Core rather than reading the definition.</b>
    /// <c>HiddenHarbours.Art.SpriteLightMath.GroundDepthScale</c> IS the definition, but Core references
    /// nothing (rule 4), so it cannot see it. Same problem <c>BuildingInterior</c> has and the same answer:
    /// carry the number and let an EditMode test — <c>IsoGroundTests</c> — pin the two together, so a
    /// camera-elevation change can never leave one of them behind quietly.</para>
    ///
    /// <para><b>⚠️ Not every plane in this project is squashed, so do not reach for this reflexively.</b>
    /// The DECK frame (<c>DeckWalkController</c>: x abeam to starboard, y along the keel) is metres of real
    /// deck on BOTH axes, and un-squashing it would introduce exactly the error this file removes. The test
    /// is not "is it a Vector2" — it is "did this vector come off a world-space transform?".</para>
    ///
    /// <para><b>The measurement behind it (2026-08-13, ADR 0034).</b> A standalone V8 harness re-rendered
    /// the whole <c>idle</c> sheet at the baker's own options — row <c>d</c> at <c>dir = d</c> — and
    /// reproduced the committed <c>Fisher_idle.png</c> byte-for-byte (1 130 496 bytes identical), which
    /// on its own pins all eight rows at <c>45°·d</c> of ground azimuth. The first DIAGONAL row — where
    /// the two readings differ most — was then matched against a CONTINUOUS turntable angle (the rig takes
    /// a fractional <c>dir</c>). Row 1, the one labelled NE, matches at
    /// <b>exactly 45.00° of ground azimuth, at distance 0</b>, with a clean monotone bowl either side; the
    /// alternative reading, that the rows are evenly spaced in world-XY/screen bearings and the first
    /// diagonal is baked at 57.26°, scores 142 370. The character rigs' own projection says the same thing
    /// in one line (<c>characterIsoRig6.js</c>: <c>th = -dir*Math.PI/4</c>, a plain vertical-axis
    /// turntable, with the 0.643 squash applied afterwards at projection). <b>The eight rows are evenly
    /// spaced GROUND bearings.</b> So a heading handed to <c>CharacterVisualDef.FacingRowFor</c> must be a
    /// ground bearing too.</para>
    ///
    /// <para>Pure, static, allocation-free and deterministic — no engine state, no <c>Time</c>, no RNG.</para>
    /// </summary>
    public static class IsoGround
    {
        /// <summary>The shared bake camera's height above the horizon, in degrees (ADR 0006/0022). Every rig
        /// in the project renders at it. Mirrors <c>SpriteLightMath.CameraElevationDeg</c>.</summary>
        public const float CameraElevationDegrees = 40f;

        /// <summary>
        /// How far up the screen one metre of NORTHWARD ground travel draws (<c>sin 40° ≈ 0.643</c>) — i.e.
        /// how much the world's y axis is FORESHORTENED against its x axis. Mirrors
        /// <c>SpriteLightMath.GroundDepthScale</c>, which is the definition.
        /// </summary>
        public static readonly float GroundDepthScale =
            Mathf.Sin(CameraElevationDegrees * Mathf.Deg2Rad);

        /// <summary>
        /// A world-space delta re-expressed in GROUND metres — the depth axis stretched back out by
        /// <see cref="GroundDepthScale"/>, the across axis left alone because it was never squashed.
        ///
        /// <para>Note the magnitude is NOT preserved and is not meant to be: this exists to be handed to an
        /// <c>atan2</c>. Anything that wants a speed should measure it in the plane it actually cares
        /// about rather than reading a length off this.</para>
        /// </summary>
        public static Vector2 ToGround(Vector2 worldDelta) =>
            new Vector2(worldDelta.x, worldDelta.y / Mathf.Max(1e-6f, GroundDepthScale));

        /// <summary>
        /// The compass bearing (degrees, 0 = North, CLOCKWISE — the project's convention) a WORLD-space
        /// delta is really pointing across the ground. Zero-safe: a zero-length delta answers 0.
        ///
        /// <para>This is the same un-squash <c>Art.Editor.BuildingFacing.FacingToward</c> does before it
        /// compares a door's normal against a target, and for the same reason. That one is kept separate
        /// rather than routed here only because it is editor-side Art with its own measured door-normal
        /// story; the arithmetic is deliberately identical.</para>
        /// </summary>
        public static float BearingDegrees(Vector2 worldDelta)
        {
            if (worldDelta.sqrMagnitude < 1e-12f) return 0f;
            Vector2 g = ToGround(worldDelta);
            // Atan2(x, y): 0 = +Y (North), growing clockwise toward +X (East).
            return Mathf.Atan2(g.x, g.y) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The compass bearing from one WORLD position to another across the ground — what anything that
        /// STATES a facing (an NPC routine pointing a shopkeeper at their counter, a builder seeding a
        /// character's initial heading) should use, so that a stated stance and a measured walk agree
        /// rather than disagreeing by up to 12.5°.
        /// </summary>
        public static float BearingDegrees(Vector2 from, Vector2 to) => BearingDegrees(to - from);
    }
}
