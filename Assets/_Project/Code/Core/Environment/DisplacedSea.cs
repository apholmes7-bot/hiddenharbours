namespace HiddenHarbours.Core
{
    /// <summary>
    /// The live DISPLACED-SEA presentation state (ADR 0023 phase 3, step 2 — the SHARED HEAVE):
    /// the two numbers every water-riding visual needs to draw its lift on the same sea the
    /// displaced surface draws — the shared exaggeration and the derived shore-fade band, exactly
    /// as the ACTIVE surface is pushing them to its own vertex stage this tick.
    ///
    /// <para><b>Why a published state and not a config read.</b> The surface's effective values
    /// resolve per throttled tick (the wired <c>GameConfig.DisplacedWater</c> block when present,
    /// its serialized fallbacks otherwise; the band DERIVED from the live envelope — rule 6). A
    /// consumer that re-read the config on its own could disagree with the surface for a tick
    /// (different fallback, different envelope read) — the overlay-pose lesson, again. Publishing
    /// the surface's OWN effective values makes boat and sea agree by construction, including
    /// while the owner tunes the config in Play: the surface re-reads and re-publishes within one
    /// refresh (~8 Hz), and every consumer reads THIS, never a cached copy.</para>
    /// </summary>
    public readonly struct DisplacedSeaState
    {
        /// <summary>The SHARED displacement exaggeration (ADR 0023 §(2)) the surface is lifting
        /// with this tick — <c>GameConfig.WaveExaggeration</c> where a config is wired. Every
        /// consumer passes this to <see cref="ShoreFadeMath.DisplacedHeight"/>.</summary>
        public readonly float Exaggeration;

        /// <summary>The DERIVED shore-fade band (metres of depth) the surface is fading with this
        /// tick (<c>coefficient × live envelope × exaggeration × shore gradient</c>). Every
        /// consumer passes this to <see cref="ShoreFadeMath.DisplacedHeight"/>, so a boat nosing
        /// into the shallows settles exactly as the water under it does.</summary>
        public readonly float ShoreFadeBandMeters;

        /// <summary>
        /// The FREQUENCY scale the surface's vertex stage samples the shared wave field at —
        /// <c>_OceanSwellScale / 0.025</c>, which is <b>2.8</b> at the owner's tuned materials, not 1.
        ///
        /// <para>⚠️ <b>The third number, and it was the missing one.</b> A rider sampling the field
        /// at scale 1 while the surface DRAWS it at 2.8 is not riding a slightly different sea — it
        /// is riding a different WAVE: the same amplitude envelope at wavelengths 2.8× shorter. The
        /// two coincide only by accident, so the drawn water stands at a crest where the hull sits in
        /// a trough and climbs straight over it. That is the "boats are nearly submerged" defect
        /// (owner, 2026-07-27), and it is the same <c>_OceanSwellScale</c> incident that already bit
        /// the watertight clamp — fixed there (it scans at <c>WaterIsoDepthFrame.FreqScale</c>) and
        /// never fixed for the RIDE, because the ride's only route to the value is this seam and this
        /// seam did not carry it.</para>
        ///
        /// <para>1 means "sample the field as-is": the value for a surface at the shipped default
        /// scale, and the safe reading for any state constructed without one.</para>
        /// </summary>
        public readonly float FreqScale;

        public DisplacedSeaState(float exaggeration, float shoreFadeBandMeters, float freqScale = 1f)
        {
            Exaggeration = exaggeration;
            ShoreFadeBandMeters = shoreFadeBandMeters;
            FreqScale = UnityEngine.Mathf.Max(freqScale, 1e-3f);
        }
    }

    /// <summary>
    /// The Core seam between the Art-side displaced surface and its Boats-side riders
    /// (CLAUDE.md rule 4 — the two feature modules meet here, never each other). The ACTIVE
    /// <c>DisplacedWaterSurface</c> publishes its effective state each throttled uniform tick and
    /// clears it when it deactivates; <c>BoatWaveMotion</c> / <c>MeshHullDriver</c> read it every
    /// frame. Absent state (<see cref="IsActive"/> false) is the A/B contract's OFF side: no ride,
    /// no resting draft, the flat-water visuals byte-identical to before ADR 0023 phase 3.
    ///
    /// <para>Presentation-only, deliberately NOT part of the sim: nothing here feeds physics
    /// (seakeeping forces keep reading the unfaded sim height — the ADR's open see==feel
    /// question), nothing is saved, and the state is recomputed by its publisher from
    /// <c>(worldSeed, gameTime)</c>-derived inputs each tick (rule 5).</para>
    /// </summary>
    public static class DisplacedSea
    {
        private static object s_Owner;
        private static DisplacedSeaState s_State;

        /// <summary>True while an active displaced surface has published a state — the ONE gate
        /// every boat-side displaced visual (ride, resting draft) sits behind.</summary>
        public static bool IsActive => s_Owner != null;

        /// <summary>The live state; false (and <c>default</c>) when no displaced sea is active.</summary>
        public static bool TryGet(out DisplacedSeaState state)
        {
            state = s_State;
            return s_Owner != null;
        }

        /// <summary>Publish the active surface's effective state (each throttled tick — re-publish
        /// is how a live config edit reaches the riders). One sea per region: with multiple
        /// publishers the last one wins, exactly like the Art-side iso-depth frame.</summary>
        public static void Publish(object owner, in DisplacedSeaState state)
        {
            if (owner == null) return;
            s_Owner = owner;
            s_State = state;
        }

        /// <summary>Clear the state — only by its current owner (a stale publisher going away must
        /// not kill a newer sea's state). No active sea ⇒ no ride, no draft: the OFF contract.</summary>
        public static void Clear(object owner)
        {
            if (!ReferenceEquals(s_Owner, owner)) return;
            s_Owner = null;
            s_State = default;
        }
    }
}
