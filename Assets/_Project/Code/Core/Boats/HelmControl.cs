namespace HiddenHarbours.Core
{
    /// <summary>
    /// Which diegetic piloting control the active hull shows (ADR 0025, S1 of the boat-UI arc). A
    /// motorised hull without a helm console earns the outboard <b>tiller</b> (the art director's
    /// <c>TillerRig</c> — the one control a motorised dory/punt shows); a hull with a
    /// <c>HelmConsoleDef</c> earns the binnacle <b>lever</b> (<c>LeverRig</c> — the moving part every
    /// helm composites). Oars hulls show <see cref="None"/> — a bare dory carries no instrument
    /// (docs/art/rigs/ui/README.md, "The diegetic rule").
    /// </summary>
    public enum HelmControlStyle
    {
        None   = 0,
        Tiller = 1,
        Lever  = 2,
    }

    /// <summary>
    /// The binnacle lever's housing finish — <c>LeverRig.SPECS</c>: <c>graphite</c> (matte console
    /// housing, the centre-console helm) or <c>chrome</c> (polished stainless — sport/novi/cape).
    /// Authored per console on <c>HelmConsoleDef</c> (content is data, rule 2).
    /// </summary>
    public enum HelmLeverFinish
    {
        Graphite = 0,
        Chrome   = 1,
    }

    /// <summary>
    /// The steering wheel's rim finish — <c>WheelRig.RIMS</c> (docs/art/rigs/ui/console-wheel):
    /// <c>rubber</c> (stock helm — moulded rim, graphite knobs), <c>teak</c> (turned teak rim and
    /// handles), <c>steel</c> (polished stainless, bright rim — the sport skiff's chrome destroyer
    /// wheel). Authored per console on <c>HelmConsoleDef</c> (content is data, rule 2).
    /// </summary>
    public enum HelmWheelRim
    {
        Rubber = 0,
        Teak   = 1,
        Steel  = 2,
    }

    /// <summary>
    /// The active boat's piloting-control seam (ADR 0025 S1): what control the helm shows, the live
    /// drive/steer to DRAW it with, and the input intents a presentation layer may send back. The
    /// Boats lane implements it (<c>HelmControlRelay</c>, riding the active <c>BoatController</c>);
    /// the UI overlay reads/writes it through <see cref="GameServices.HelmControl"/> WITHOUT
    /// referencing the Boats module (rule 4) — the same one-way, Core-mediated handoff as
    /// <see cref="IActiveBoatService"/>.
    ///
    /// <para><b>One state, one owner.</b> <see cref="Drive"/> is a read of the controller's own
    /// <c>_throttle</c> (the value <c>EngineThrust</c> consumes) — the lever draws exactly what the
    /// physics runs, never a UI-side copy (the flick-cast lesson: never two computations of one
    /// quantity). Every intent below lands in <c>BoatController.SetControl</c>; nothing here stores a
    /// second drive.</para>
    ///
    /// <para><b>Pull, not push</b> — sampled by the overlay per frame, like
    /// <see cref="IActiveBoatService"/>; the sim never pushes presentation events (ADR 0007). Held
    /// throttle is transient input state, never saved (rule 5).</para>
    /// FLAG lead-architect: new Core contract (the ADR 0025 S1 helm-control seam).
    /// </summary>
    public interface IHelmControl
    {
        /// <summary>True while the player is at the helm of a MOTORISED hull (controller enabled +
        /// Engine propulsion). False on foot, moored, or rowing — the overlay hides.</summary>
        bool HasHelm { get; }

        /// <summary>Which control the current hull shows (tiller vs lever; None while unmanned/oars).</summary>
        HelmControlStyle Style { get; }

        /// <summary>The lever's housing finish for this hull's console (Lever style only).</summary>
        HelmLeverFinish LeverFinish { get; }

        /// <summary>The steering wheel's rim finish for this hull's console (composed dashes, S2a).</summary>
        HelmWheelRim WheelRim { get; }

        /// <summary>
        /// The EFFECTIVE equipment fit of the active hull's helm (S2a of the boat-UI arc): which
        /// console rig draws the dash and which instruments are actually fitted. Derived data,
        /// recomputed per read (<c>BoatEquipment.EffectiveFit</c> — hull default + owned upgrades);
        /// <see cref="HelmFit.None"/> while unmanned or on a console-less hull. The overlay uses it
        /// to choose the composed dash vs the lone S1 instrument card.
        /// FLAG lead-architect: Core contract growth (the ADR 0025 S2a dash-composition seam).
        /// </summary>
        HelmFit Fit { get; }

        /// <summary>The signed drive in [-1..+1] the physics is running RIGHT NOW — the LeverRig
        /// <c>sig</c> / TillerRig throttle+gear source. Read-only: presentation draws it, intents move it.</summary>
        float Drive { get; }

        /// <summary>The helm steer in [-1..+1] (the tiller rotates with it; read-only).</summary>
        float Steer { get; }

        /// <summary>Step one detent toward AHEAD (a key/gamepad press EDGE; the drive HOLDS after).</summary>
        void StepAhead();

        /// <summary>Step one detent toward ASTERN.</summary>
        void StepAstern();

        /// <summary>Snap the drive straight to neutral (the dedicated neutral key).</summary>
        void SetNeutral();

        /// <summary>The mouse drag-to-set path: set a CONTINUOUS drive while the grip is dragged
        /// (LeverRig.sigFromOffset output, clamped). Live every frame of the drag.</summary>
        void DragDrive(float sig);

        /// <summary>The drag released: the lever HOLDS where it was left, except inside the neutral
        /// snap window (data — <c>HelmThrottleSettings.NeutralSnapWindow01</c>), which snaps to 0.</summary>
        void EndDrag();

        // ---- the wheel's steer session (S2a) ----------------------------------------------------
        // Steer stays ONE value with ONE owner (BoatController._steer). Keys write it momentarily
        // every frame (DevBoatInput); the focused wheel writes it through THIS session instead, and
        // the two never interleave: while a session is live the key layer PRESERVES the held steer
        // when its own read is zero, and a real key press ENDS the session (keys win — the single
        // decisive handover, no per-frame fight). The wheel overlay must watch
        // <see cref="SteerDragActive"/> and drop its grab when the session is broken under it.

        /// <summary>True while a wheel steer session is live (set by <see cref="DragSteer"/>, cleared
        /// by <see cref="EndSteerDrag"/> or by the key layer taking steer back).</summary>
        bool SteerDragActive { get; }

        /// <summary>The focused wheel's steer write: set the live steer in [-1..+1] and keep the
        /// steer session open. Called every frame the wheel is grabbed or coasting under focus.</summary>
        void DragSteer(float steer);

        /// <summary>End the wheel steer session. The steer VALUE is left as written — the momentary
        /// key layer resumes ownership next frame (centred unless a key is held), exactly the
        /// untouched S1 key semantics.</summary>
        void EndSteerDrag();
    }
}
