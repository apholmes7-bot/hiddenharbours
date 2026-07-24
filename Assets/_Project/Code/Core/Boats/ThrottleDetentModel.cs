namespace HiddenHarbours.Core
{
    /// <summary>
    /// A persistent NOTCHED throttle for the engine helm — the model behind the diegetic single lever
    /// (the art director's <c>LeverRig</c>, <c>drive ∈ [-1..+1]</c>). It replaces the old binary
    /// keyboard throttle (<c>W?1:0 - S?1:0</c> in <c>DevBoatInput.ReadEngine</c>), which could only ever
    /// be full-ahead / neutral / full-astern because a key is on-or-off. Here Up/Down each STEP a
    /// <b>held</b> detent, so the player can sit at part-throttle — the thing a keyboard can't otherwise do.
    ///
    /// <para><b>One value, two consumers.</b> <see cref="Drive"/> is exactly the signed helm signal
    /// <c>BoatController.SetControl(throttle, steer)</c> already accepts (it clamps to ±1 and applies the
    /// weaker astern factor via <c>EngineThrust</c> — a held +0.5 already produces half-ahead thrust
    /// today), <i>and</i> exactly the <c>LeverRig</c> <c>drive</c> the console draws. So the same field
    /// feeds physics and the picture; last input wins whether it came from a key edge or a lever drag
    /// (<c>LeverRig.sigFromOffset → SnapToDrive</c>).</para>
    ///
    /// <para><b>Pure C# by design.</b> No <c>UnityEngine</c> — <see cref="System.Math"/> and integers only,
    /// so it is unit-testable without the Unity runtime (mirrors the pure-static precedent of
    /// <c>DevBoatInput.OarStateFor</c> and <c>BoatController.EngineThrust</c>). It carries exactly one
    /// field of state (<see cref="Notch"/>) because a detent must <b>hold its position across frames</b> —
    /// the one thing the old per-frame recompute could not do. Notch counts are constructor params
    /// (fed from <c>GameConfig.HelmThrottle</c>), so the model stays policy-agnostic (no magic numbers,
    /// rule 6). Held throttle is transient input state — never saved (rule 5).</para>
    /// </summary>
    public sealed class ThrottleDetentModel
    {
        private readonly int _aheadNotches;    // detents from neutral to FULL AHEAD  (e.g. 4)
        private readonly int _asternNotches;   // detents from neutral to FULL ASTERN (e.g. 2 — reverse travel is shorter)
        private int _notch;                    // current detent in [-_asternNotches .. +_aheadNotches]; 0 = neutral

        /// <param name="aheadNotches">Detents between neutral and full ahead (≥ 1).</param>
        /// <param name="asternNotches">Detents between neutral and full astern (≥ 1); usually fewer than
        /// ahead, so reverse has shorter travel like a real single-lever binnacle.</param>
        public ThrottleDetentModel(int aheadNotches = 4, int asternNotches = 2)
        {
            _aheadNotches  = aheadNotches  < 1 ? 1 : aheadNotches;
            _asternNotches = asternNotches < 1 ? 1 : asternNotches;
            _notch = 0;
        }

        /// <summary>Current detent, in <c>[-AsternNotches .. +AheadNotches]</c>; <c>0</c> = neutral.</summary>
        public int Notch => _notch;
        public int AheadNotches => _aheadNotches;
        public int AsternNotches => _asternNotches;

        /// <summary>True at the neutral detent (no thrust, F/N/R shift centred).</summary>
        public bool IsNeutral => _notch == 0;

        /// <summary>
        /// The signed helm signal in <c>[-1..+1]</c> — full astern = exactly <c>-1</c>, neutral = <c>0</c>,
        /// full ahead = exactly <c>+1</c> — even when astern has fewer notches than ahead. Feed this to
        /// <c>BoatController.SetControl</c> AND draw the lever with it.
        /// </summary>
        public float Drive => DriveFor(_notch, _aheadNotches, _asternNotches);

        /// <summary>Step one detent toward AHEAD (call on the Up key EDGE, not while held). Clamps at full ahead.</summary>
        public void StepAhead() => _notch = Clamp(_notch + 1, _asternNotches, _aheadNotches);

        /// <summary>Step one detent toward ASTERN (call on the Down key EDGE). Clamps at full astern.</summary>
        public void StepAstern() => _notch = Clamp(_notch - 1, _asternNotches, _aheadNotches);

        /// <summary>Snap straight to the neutral detent (the "chop to neutral" key — e.g. a tap of X/space).</summary>
        public void ToNeutral() => _notch = 0;

        /// <summary>
        /// Reset to neutral — call when the helm is left unmanned so re-taking it never surges from a stale
        /// drive (mirrors <c>BoatController.Stop()</c>, which <c>ControlSwitcher.LeaveHelm/Disembark</c>
        /// already call).
        /// </summary>
        public void Reset() => _notch = 0;

        /// <summary>Set the detent nearest a raw drive value — the mouse / <c>LeverRig.sigFromOffset</c> drag path.</summary>
        public void SnapToDrive(float drive) => _notch = NearestNotch(drive, _aheadNotches, _asternNotches);

        /// <summary>Set an explicit detent, clamped to range (restore / test seam).</summary>
        public void SetNotch(int notch) => _notch = Clamp(notch, _asternNotches, _aheadNotches);

        // ---- pure static arithmetic (no state, no UnityEngine — the unit-tested core) ----------------

        /// <summary>Clamp a notch into <c>[-asternNotches .. +aheadNotches]</c>.</summary>
        public static int Clamp(int notch, int asternNotches, int aheadNotches)
            => notch < -asternNotches ? -asternNotches
             : notch >  aheadNotches  ?  aheadNotches
             : notch;

        /// <summary>
        /// Drive for a notch: ahead notches normalise over <paramref name="aheadNotches"/>, astern notches
        /// over <paramref name="asternNotches"/>, so BOTH endpoints reach ±1 exactly regardless of the
        /// (asymmetric) notch counts. Neutral → 0.
        /// </summary>
        public static float DriveFor(int notch, int aheadNotches, int asternNotches)
        {
            if (notch == 0) return 0f;
            if (notch > 0)  return aheadNotches  > 0 ? (float)notch / aheadNotches  : 0f;
            return                 asternNotches > 0 ? (float)notch / asternNotches : 0f;   // notch < 0 → negative
        }

        /// <summary>
        /// The notch nearest a raw drive in <c>[-1..+1]</c> (clamped), rounding halves away from zero so a
        /// lever dragged just past a detent settles onto the nearer one. Inverse of <see cref="DriveFor"/>
        /// on exact detent values.
        /// </summary>
        public static int NearestNotch(float drive, int aheadNotches, int asternNotches)
        {
            if (drive >  1f) drive =  1f;
            else if (drive < -1f) drive = -1f;
            if (drive >= 0f)
                return (int)System.Math.Round((double)drive * aheadNotches, System.MidpointRounding.AwayFromZero);
            return -(int)System.Math.Round((double)(-drive) * asternNotches, System.MidpointRounding.AwayFromZero);
        }
    }
}
