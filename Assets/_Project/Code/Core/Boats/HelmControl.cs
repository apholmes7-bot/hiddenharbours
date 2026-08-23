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
        /// <summary>
        /// <b>This hull has an ENGINE HELM being driven</b> — her controller is enabled and her
        /// propulsion is motorised. False for a rowed hull, a hull nobody is driving, and a hull
        /// ashore.
        ///
        /// <para><b>⚠ It does NOT mean the player is the one driving</b>, and reading it as if it did
        /// is the defect the ownership seam was built to end: it is equally true of a skipper's boat
        /// bringing the player in as a passenger (the owner's 2026-08-21 playtest, which drew that
        /// boat's wheel and gauges to somebody who was not steering). It is the ENGINE question — the
        /// gate the relay's own intents and instruments use, because a throttle step means nothing on
        /// a dory. <b>Presentation asks <see cref="IsPlayerHelm"/> instead.</b></para>
        /// </summary>
        bool HasHelm { get; }

        /// <summary>
        /// <b>The player is at THIS hull's helm, driving her</b> — <see cref="HasHelm"/> AND this relay
        /// is the one <c>HelmSlot</c> has granted the helm to (the hull the Player lane declared the
        /// player is piloting). This is the question every piece of helm PRESENTATION is actually
        /// asking: draw the wheel, the lever, the gauges, suppress the HUD's nav cluster.
        ///
        /// <para>With the slot arbitrated by occupancy, a consumer reading it through
        /// <see cref="GameServices.HelmControl"/> is already reading the player's own relay — so this
        /// says the same thing twice on purpose. It is what makes the reader's INTENT legible at the
        /// call site, and it stays correct for anything holding a relay reference directly.</para>
        /// </summary>
        bool IsPlayerHelm { get; }

        /// <summary>Which control the current hull shows (tiller vs lever; None while unmanned/oars).</summary>
        HelmControlStyle Style { get; }

        // ---- the ground tackle (the dash's anchor switch) ---------------------------------------
        //
        // ⚠ This is the BOAT's tackle, not the helm's. The three members below are answered for the
        // hull this relay rides whenever she is the player's boat — which is WIDER than "the player is
        // at her helm", because a rowed dory has no helm at all and her hook still answers to the
        // player. The dash switch happens to be a helm surface, so it reads them here; the key that
        // works the same tackle from a rowed hull never touches this seam at all (it presses
        // BoatAnchor directly, gated on GameServices.Helm.IsPlayersBoat). One tackle, two controls,
        // and neither is the other's fallback.

        /// <summary>
        /// <b>This hull carries ground tackle</b> — her <c>BoatHullDef.HasAnchor</c>, straight through.
        /// A DATA question, not a live one: it does not change while you are aboard, and it is what
        /// decides whether an anchor switch is drawn on the dash at all. False = no hook, no switch,
        /// and nothing to press.
        /// </summary>
        bool HasAnchor { get; }

        /// <summary>
        /// Where her hook stands right now — the live <c>BoatAnchor</c> state the switch shows.
        /// <see cref="AnchorState.Stowed"/> on a hull with no tackle, on a hull nobody is aboard, and
        /// before the sim has run. Read-only: the switch draws it, <see cref="ToggleAnchor"/> moves it.
        /// </summary>
        AnchorState AnchorState { get; }

        /// <summary>
        /// <b>Flick the anchor switch:</b> let go if she is catted, weigh if the hook is over the side
        /// (holding or dragging alike). The single verb an anchor control needs — the same
        /// <c>BoatAnchor.Toggle()</c> the key presses, so the switch and the key can never put the
        /// tackle in two different places. A no-op when this hull carries no anchor or is not the
        /// player's boat.
        /// </summary>
        void ToggleAnchor();

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
