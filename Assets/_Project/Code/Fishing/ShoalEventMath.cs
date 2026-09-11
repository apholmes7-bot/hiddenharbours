using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>What one fish is doing instead of plain swimming (<see cref="ShoalEventMath"/>).</summary>
    public enum ShoalEventKind
    {
        /// <summary>Swimming. The overwhelming majority of the time, for every fish.</summary>
        None = 0,

        /// <summary>The rig's <c>roll</c> — a surface roll, belly flash. Any fish may do it.</summary>
        Roll = 1,

        /// <summary>The rig's <c>thrash</c> — the head pitched through the surface. Any fish may do it.</summary>
        Thrash = 2,

        /// <summary>The rig's <c>jump</c> — clears the water entirely. Only for species the owner has
        /// ruled jump (<see cref="FishFlags.Jumps"/>); a flounder never does this.</summary>
        Jump = 3,

        /// <summary>The rig's <c>dart</c> — the scatter, and the only event that is not on a schedule at
        /// all: it is the school's reaction to a bobber landing on it. See
        /// <see cref="ShoalEventMath.ScatterAt"/>.</summary>
        Dart = 4
    }

    /// <summary>
    /// WHEN A FISH JUMPS — the deterministic schedule behind the owner's 2026-09-05 ruling
    /// (<i>"They will jump, thrash, roll, go deep"</i>).
    ///
    /// <para><b>A hash per fish would clump.</b> The tempting shape — roll a hash for every fish every
    /// tick and jump when it clears a bar — gives a UNIFORM distribution, which is not the same thing as
    /// a SPREAD: over a five-fish school it produces stretches with nothing happening and instants where
    /// three fish jump together, because nothing in it knows what the other fish are doing. So the
    /// schedule is shared out in SLOTS instead: the school's time is cut into periods, and each period
    /// belongs to exactly ONE fish, chosen by hashing the period. A school of five with a 20 s period
    /// shows a fish doing something every 20 s, a different fish each time, and never two at once.</para>
    ///
    /// <para><b>Recomputed, never scheduled</b> (rule 5). Nothing is queued and nothing ticks: the event
    /// under way at an instant is a pure function of <c>(worldSeed, schoolKey, t)</c>, so a jump at
    /// 07:14:20 on this seed happens at 07:14:20 on every load, on every machine, and a region streaming
    /// in mid-jump lands the fish mid-jump.</para>
    ///
    /// <para><b>The durations are the rig's, not invented.</b> Each event lasts exactly as long as the
    /// anim the rig baked for it (<c>ANIMS</c>: jump 6 frames x 95 ms, roll 4 x 130, thrash 4 x 110,
    /// dart 3 x 80), so an event can never be cut off mid-arc or held past its last frame.</para>
    /// </summary>
    public static class ShoalEventMath
    {
        /// <summary>Rig <c>ANIMS.jump</c>: 6 frames at 95 ms. <b>Forwarded from
        /// <see cref="FishJumpArc"/></b>, which is where the jump law lives now that the hooked fish
        /// jumps too — the fight drawer is in Player and cannot see this class, and one law transcribed
        /// twice is one law that drifts (owner's ruling 2026-09-06).</summary>
        public const int JumpFrames = FishJumpArc.Frames;
        public const double JumpFrameMs = FishJumpArc.FrameMs;

        /// <summary>Rig <c>ANIMS.roll</c>: 4 frames at 130 ms.</summary>
        public const int RollFrames = 4;
        public const double RollFrameMs = 130.0;

        /// <summary>Rig <c>ANIMS.thrash</c>: 4 frames at 110 ms.</summary>
        public const int ThrashFrames = 4;
        public const double ThrashFrameMs = 110.0;

        /// <summary>Rig <c>ANIMS.dart</c>: 3 frames at 80 ms.</summary>
        public const int DartFrames = 3;
        public const double DartFrameMs = 80.0;

        /// <summary>Rig <c>MOTION.jump.travel</c> — metres the fish carries forward over the six frames.</summary>
        public const float JumpTravelMetres = FishJumpArc.TravelMetres;

        /// <summary>Frames in one anim, and how long each is held (ms).</summary>
        public static void AnimShape(ShoalEventKind kind, out int frames, out double frameMs)
        {
            switch (kind)
            {
                case ShoalEventKind.Jump: frames = JumpFrames; frameMs = JumpFrameMs; return;
                case ShoalEventKind.Roll: frames = RollFrames; frameMs = RollFrameMs; return;
                case ShoalEventKind.Thrash: frames = ThrashFrames; frameMs = ThrashFrameMs; return;
                case ShoalEventKind.Dart: frames = DartFrames; frameMs = DartFrameMs; return;
                default: frames = ShoalMath.SwimFrames; frameMs = ShoalMath.SwimFrameMs; return;
            }
        }

        /// <summary>How long one whole event lasts, in seconds — its anim played once.</summary>
        public static double DurationSeconds(ShoalEventKind kind)
        {
            AnimShape(kind, out int frames, out double frameMs);
            return frames * frameMs / 1000.0;
        }

        /// <summary>
        /// The event under way in this school at this instant, if any.
        ///
        /// <para>One period, one actor, one event — see the type doc on why this is slotted rather than
        /// rolled per fish. Returns <see cref="ShoalEventKind.None"/> (and <c>actor = -1</c>) for the
        /// stretch of every period after the event has finished, which is most of it.</para>
        /// </summary>
        /// <param name="worldSeed">The world seed — the same one the schools are built from.</param>
        /// <param name="schoolKey">The school's <c>(cell, slot)</c> key.</param>
        /// <param name="gameSeconds">The clock's <c>TotalSeconds</c>.</param>
        /// <param name="markCount">How many fish the school is showing.</param>
        /// <param name="speciesJumps">Whether this school's fish jump (<see cref="FishFlags.Jumps"/>).</param>
        /// <param name="periodSeconds">Seconds per event slot — how often SOMETHING happens in this
        /// school. Owner-tunable; must be positive.</param>
        /// <param name="actor">Which swimmer slot is doing it, or -1 for none.</param>
        /// <param name="startSeconds">When the event began (its anim's frame 0).</param>
        public static ShoalEventKind At(int worldSeed, uint schoolKey, double gameSeconds, int markCount,
                                        bool speciesJumps, double periodSeconds,
                                        out int actor, out double startSeconds)
        {
            actor = -1;
            startSeconds = 0.0;
            if (markCount <= 0 || periodSeconds <= 0.0) return ShoalEventKind.None;

            long period = (long)System.Math.Floor(gameSeconds / periodSeconds);
            double periodStart = period * periodSeconds;

            uint h = Hash(worldSeed, schoolKey, period);

            // Which fish owns this period. One actor per period is the whole anti-clump property.
            actor = (int)(h % (uint)markCount);

            ShoalEventKind kind = KindFor(h, speciesJumps);
            double duration = DurationSeconds(kind);

            // Where inside the period it happens. The event must finish before the period does, or two
            // periods could overlap and put two fish in the air at once.
            double room = periodSeconds - duration;
            if (room <= 0.0)
            {
                // The owner has tuned the period shorter than the anim: start it on the period boundary
                // and let it run. Still exactly one actor, still deterministic.
                startSeconds = periodStart;
            }
            else
            {
                double frac = ((h >> 8) & 0xFFFFu) / 65536.0;
                startSeconds = periodStart + frac * room;
            }

            if (gameSeconds < startSeconds || gameSeconds >= startSeconds + duration)
            {
                actor = -1;
                return ShoalEventKind.None;
            }

            return kind;
        }

        /// <summary>
        /// The frame of <paramref name="kind"/> showing at <paramref name="gameSeconds"/>, for an event
        /// that began at <paramref name="startSeconds"/>. Clamped to the last frame rather than wrapped —
        /// an event plays ONCE; a wrapped jump would loop a fish through the air forever.
        /// </summary>
        public static int FrameAt(ShoalEventKind kind, double startSeconds, double gameSeconds)
        {
            AnimShape(kind, out int frames, out double frameMs);
            if (frames <= 1) return 0;
            double elapsedMs = (gameSeconds - startSeconds) * 1000.0;
            if (elapsedMs <= 0.0) return 0;
            int f = (int)(elapsedMs / frameMs);
            return f >= frames ? frames - 1 : f;
        }

        /// <summary>
        /// How high above the water the jumping fish is, 0..1 of its arc — a half-sine over the anim, so
        /// it leaves and re-enters the water at the surface. The rig bakes the pitch and the z arc INTO
        /// the frames; this is only the world-space lift the presenter adds on top so the fish actually
        /// crosses the surface it was drawn leaving.
        /// </summary>
        public static float JumpArc01(double startSeconds, double gameSeconds)
        {
            // The shared law (FishJumpArc.Arc01) — the SAME half-sine the hooked fish leaves the water
            // on, which is the whole of the owner's "a hooked bass jumps the way a free one does".
            return FishJumpArc.Arc01(startSeconds, gameSeconds);
        }

        /// <summary>
        /// THE SCATTER — the one event that is not scheduled. A bobber landing on the school makes the
        /// fish nearest it dart, which is the owner's <i>"able to be cast at"</i> read from the fish's
        /// side: the fish that scatter are the fish that were about to bite.
        ///
        /// <para>Driven by the caller's <see cref="SchoolInfluence"/> read, so the scatter and the bite
        /// come from ONE query of ONE model — a fish cannot flinch from a cast the resolver did not
        /// notice, and cannot ignore one it did.</para>
        /// </summary>
        /// <param name="influenceStrength01">The cast's <c>SchoolInfluence.Strength01</c> on this school.</param>
        /// <param name="secondsSinceSplash">How long ago the bobber landed; negative for "no cast".</param>
        /// <returns>True while the school is still scattering.</returns>
        public static bool ScatterAt(float influenceStrength01, double secondsSinceSplash)
            => influenceStrength01 > 0f
               && secondsSinceSplash >= 0.0
               && secondsSinceSplash < DurationSeconds(ShoalEventKind.Dart);

        /// <summary>
        /// <b>THE SCATTER'S REACH</b> — how hard a splash at <c>(splashX, splashY)</c> hits this school,
        /// 0..1, the term <see cref="ScatterAt"/> consumes. The owner's two knobs
        /// (<c>GameConfig.FishSchools.GullSplashScatterRadiusMetres</c> and
        /// <c>...ScatterStrength01</c>) are <paramref name="reachMetres"/> and
        /// <paramref name="maxStrength01"/>; nothing here is a number of its own.
        ///
        /// <para><b>Measured to the school's EDGE, not its anchor.</b> A school is a disc 8-14 m across
        /// and its fish are drawn spread over the whole of it, so a bird that came down on the rim landed
        /// ON the fish even though the anchor is ten metres away. Distance to the anchor would say
        /// otherwise and the picture would contradict itself.</para>
        ///
        /// <para>Falls off linearly from full strength at the rim to nothing at <paramref name="reachMetres"/>
        /// past it, so a splash across the cove is not a soft nudge — it is no event at all, and the
        /// school runs its ordinary schedule.</para>
        /// </summary>
        public static float ScatterStrength01(double splashX, double splashY,
                                              double centreX, double centreY,
                                              float schoolRadiusMetres, float reachMetres,
                                              float maxStrength01)
        {
            if (!(reachMetres > 0f) || !(maxStrength01 > 0f)) return 0f;

            double dx = splashX - centreX;
            double dy = splashY - centreY;
            double toEdge = Math.Sqrt(dx * dx + dy * dy) - Math.Max(0.0, schoolRadiusMetres);
            if (toEdge >= reachMetres) return 0f;

            float peak = maxStrength01 > 1f ? 1f : maxStrength01;
            if (toEdge <= 0.0) return peak;
            return peak * (float)(1.0 - toEdge / reachMetres);
        }

        /// <summary>
        /// How far through its bolt the school is, 0..1 — a half-sine over the <c>dart</c>, the same shape
        /// <see cref="JumpArc01"/> lifts a jumping fish on. Out and back: nothing at the splash, furthest
        /// at the middle of the anim, home again as the last frame ends.
        ///
        /// <para>That it returns to zero is the whole reason it is a sine and not a ramp. The shoal's
        /// position is closed-form in <c>(worldSeed, schoolKey, t)</c> with nothing to integrate, so an
        /// offset that ended anywhere but zero would snap the fish back the instant the dart expired.</para>
        /// </summary>
        public static float ScatterArc01(double startSeconds, double gameSeconds)
        {
            double duration = DurationSeconds(ShoalEventKind.Dart);
            double t = gameSeconds - startSeconds;
            if (t <= 0.0 || t >= duration) return 0f;
            return (float)Math.Sin(t / duration * Math.PI);
        }

        /// <summary>
        /// Where one fish of a scattering school is, relative to where it would have been — straight away
        /// from the splash, on the arc above.
        ///
        /// <para><b>The distance is the shoal's own spread</b> (<c>FishSpeciesDef.ShoalSpreadMetres</c>,
        /// the length the swimmers are solved over), scaled by <paramref name="strength01"/>. A startled
        /// fish bolts about a shoal-width and no further, which keeps every displaced fish inside the
        /// school's own disc — a scatter must never move a school, because a <c>(cell, slot)</c> holds
        /// exactly one and re-placing it would put two in a cell (#802).</para>
        ///
        /// <para>A fish sitting exactly under the splash has no "away" to run: it takes one fixed bearing
        /// rather than a drawn one, so the whole law stays a pure function of its arguments (rule 5).</para>
        /// </summary>
        public static void ScatterOffset(double splashX, double splashY, double fishX, double fishY,
                                         float strength01, float spreadMetres,
                                         double startSeconds, double gameSeconds,
                                         out float offsetX, out float offsetY)
        {
            offsetX = 0f; offsetY = 0f;

            float arc = ScatterArc01(startSeconds, gameSeconds);
            if (arc <= 0f || !(strength01 > 0f) || !(spreadMetres > 0f)) return;

            double ax = fishX - splashX;
            double ay = fishY - splashY;
            double len = Math.Sqrt(ax * ax + ay * ay);
            if (len < 1e-4) { ax = 1.0; ay = 0.0; len = 1.0; }

            double travel = (strength01 > 1f ? 1f : strength01) * spreadMetres * arc;
            offsetX = (float)(ax / len * travel);
            offsetY = (float)(ay / len * travel);
        }

        /// <summary>Which event this period holds. Jumpers jump a third of the time; everything else is
        /// split between the roll and the thrash, both of which any fish may do.</summary>
        private static ShoalEventKind KindFor(uint h, bool speciesJumps)
        {
            uint pick = (h >> 24) % 3u;
            if (pick == 0u) return speciesJumps ? ShoalEventKind.Jump : ShoalEventKind.Roll;
            return pick == 1u ? ShoalEventKind.Roll : ShoalEventKind.Thrash;
        }

        /// <summary>Stable 32-bit mix of <c>(worldSeed, schoolKey, period)</c> — the same shape of hash the
        /// school model itself uses, so an event is as reproducible as the school it happens in.</summary>
        private static uint Hash(int worldSeed, uint schoolKey, long period)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)worldSeed) * 16777619u;
                h = (h ^ schoolKey) * 16777619u;
                h = (h ^ (uint)(period & 0xFFFFFFFFL)) * 16777619u;
                h = (h ^ (uint)((period >> 32) & 0xFFFFFFFFL)) * 16777619u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
