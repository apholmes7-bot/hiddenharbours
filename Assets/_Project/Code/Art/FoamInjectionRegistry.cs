using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// One hull's deposit into the advected foam buffer this frame (ADR 0027 #6): the SEGMENT of sea
    /// it swept since the last frame, how wide a band it churns, and how hard.
    ///
    /// <para>A segment rather than a point on purpose — a boat at 3 m/s crosses about a cell and a
    /// half per frame at 60 fps, and several on a frame hitch, so a point deposit would lay a dashed
    /// trail that gets dashier the worse the frame rate. The capsule between last frame's position
    /// and this one is continuous by construction.</para>
    /// </summary>
    public readonly struct FoamInjection
    {
        /// <summary>Where the hull was at the previous injection tick (world m).</summary>
        public readonly Vector2 From;
        /// <summary>Where it is now (world m).</summary>
        public readonly Vector2 To;
        /// <summary>Half-width of the churned band about that segment (m).</summary>
        public readonly float Radius;
        /// <summary>How much foam to add at the centre of the band this frame, 0..1 (already
        /// dt-scaled by the injector, so the deposit is frame-rate independent).</summary>
        public readonly float Amount;
        /// <summary>
        /// How hard this hull is working the water right now, 0..1 — <c>FoamBuffer.Injection01</c>'s own
        /// output, <b>before</b> it is turned into a per-frame amount.
        ///
        /// <para>It is the freshness channel's <b>GATE</b>, not a scale: the advect pass asks only
        /// "is this hull working the water at all this frame", and marks the clock fully fresh if so
        /// (<see cref="FoamBuffer.Freshness"/> says why it must not be scaled). <see cref="Amount"/>
        /// cannot answer that question — it is this same number multiplied by the frame's dt, so at a
        /// high frame rate a gently-working hull's amount rounds toward nothing and the gate would
        /// flicker with the frame rate. A wake's colour must not depend on how fast the machine is.</para>
        /// </summary>
        public readonly float Vigour;

        /// <summary>
        /// This hull's DISPERSAL this frame (water fidelity PR 11b) — the widening edge that carries
        /// the share of her churn which leaves the band. <see cref="FoamDispersal.IsActive"/> is
        /// false at the shipped passthrough (the spread dial at 0, or a hull with no track behind her
        /// yet), and then this injection is bit-for-bit the one PR 11a shipped.
        /// </summary>
        public readonly FoamDispersal Dispersal;

        public FoamInjection(Vector2 from, Vector2 to, float radius, float amount, float vigour)
            : this(from, to, radius, amount, vigour, default)
        {
        }

        public FoamInjection(Vector2 from, Vector2 to, float radius, float amount, float vigour,
                             in FoamDispersal dispersal)
        {
            From = from;
            To = to;
            Radius = radius;
            Amount = amount;
            Vigour = vigour;
            Dispersal = dispersal;
        }
    }

    /// <summary>
    /// One hull's DISPERSAL EDGE this frame (water fidelity PR 11b): the outward-sweeping rim of her
    /// churn, sampled along her recent track.
    ///
    /// <para><b>The owner, 2026-09-04 and 09-06:</b> <i>"foam fades behind boat but doesnt disperse
    /// in width"</i> · <i>"the foam always stays to the original foam path, it doesnt widen over time
    /// and fade away."</i> The buffer cannot blur — its cell law makes every scroll an exact integer
    /// texel copy (<see cref="FoamBuffer.AdvectCells"/>), which is what stops a wake smudging under a
    /// camera pan — so a laid trail could fade but never widen. This is that width, evaluated at
    /// INJECTION against each deposit's own age, never as a filter over the target.</para>
    ///
    /// <para><b>What the shader is told, and what it derives.</b> Only the TRACK is sent. The edge's
    /// law is a SLOPE, <c>w = r₀ + slope·s</c>, so with the nodes spaced at equal arc length the
    /// half-width is linear in the node index and the shader computes it from
    /// <see cref="HalfWidth"/> and <see cref="MaxHalfWidth"/> alone; the age mark is exponential in
    /// the same parameter, so <see cref="TailMark"/> is enough for all six. That keeps this to four
    /// constant arrays and leaves nothing to drift between the two halves of the seam.</para>
    ///
    /// <para><b>Six nodes, spaced by DISTANCE astern, not by time.</b> The reach
    /// <c>S = (w_max − r₀)/slope</c> is the same at any speed, so the spacing is fixed. Six is what a
    /// hard turn needs: five sub-segments hold a 90°-over-the-reach arc to about 10 cm, three hold it
    /// to 27 cm, and a single straight extension astern of the transom misses the track by over a
    /// metre.</para>
    /// </summary>
    public readonly struct FoamDispersal
    {
        /// <summary>How many track nodes the edge is sampled at. Mirrored as
        /// <c>FOAM_DISPERSAL_NODES</c> and a COMPILE-TIME loop bound in the advect shader, for the
        /// same reason <see cref="FoamBuffer.MaxInjectors"/> is one.</summary>
        public const int Nodes = 6;

        /// <summary>The transom's track, newest first: node 0 is where she is now, node 5 the oldest
        /// water the edge has reached (world m). Equal arc length apart.</summary>
        public readonly Vector2 Node0, Node1, Node2, Node3, Node4, Node5;

        /// <summary>The churn band's own half-width (m) — where the edge starts, and what the
        /// envelope is measured from.</summary>
        public readonly float HalfWidth;

        /// <summary>The envelope this hull's churn spreads to (m): where the band's outer taper
        /// reaches exactly zero. Reduced to what her AVAILABLE track can reach, so conservation holds
        /// while a wake is still being born rather than only once it is full length.</summary>
        public readonly float MaxHalfWidth;

        /// <summary>The edge's per-frame amplitude (<see cref="FoamBuffer.EdgeGain"/>) — already
        /// dt-scaled, and already speed-free.</summary>
        public readonly float EdgeGain;

        /// <summary>The edge's soft width (m) — <see cref="FoamBuffer.EdgeWidth"/>.</summary>
        public readonly float EdgeWidth;

        /// <summary>The freshness the OLDEST node's water should carry
        /// (<see cref="FoamBuffer.AgeMark"/>). Without it the widening band would be born at the far
        /// end of #724's colour walk while the core beside it is white. Intermediate nodes are
        /// <c>TailMark^u</c>, which is exact at a steady speed and a phase error in the colour walk
        /// (never in the geometry) when she is accelerating.</summary>
        public readonly float TailMark;

        /// <summary>False at the shipped passthrough: the spread dial at 0, or a hull with no track
        /// behind her yet. Then nothing about her injection differs from PR 11a's.</summary>
        public bool IsActive => EdgeGain > 0f && EdgeWidth > 0f && MaxHalfWidth > HalfWidth
                                && HalfWidth > 0f;

        public FoamDispersal(Vector2 node0, Vector2 node1, Vector2 node2,
                             Vector2 node3, Vector2 node4, Vector2 node5,
                             float halfWidth, float maxHalfWidth,
                             float edgeGain, float edgeWidth, float tailMark)
        {
            Node0 = node0; Node1 = node1; Node2 = node2;
            Node3 = node3; Node4 = node4; Node5 = node5;
            HalfWidth = halfWidth;
            MaxHalfWidth = maxHalfWidth;
            EdgeGain = edgeGain;
            EdgeWidth = edgeWidth;
            TailMark = tailMark;
        }
    }

    /// <summary>
    /// The live set of foam INJECTORS (ADR 0027 #6) — the same shape, for the same reasons, as
    /// <see cref="ReflectionRegistry"/>:
    ///
    /// <list type="number">
    /// <item><b>The zero-cost guarantee.</b> <see cref="IsoFacetHullFeature"/> consults
    /// <see cref="ShouldRun"/> and enqueues the buffer pass only when something is actually churning
    /// AND the owner has dialled the look in. No hull near water, or the effect off, and the pass is
    /// never recorded — no render target, no blit, nothing (CLAUDE.md rule 7, the
    /// <c>IsoFacetUrpPassTests</c> contract).</item>
    /// <item><b>Membership is EXPLICIT and SELF-GATING.</b> A hull joins only by carrying a
    /// <see cref="FoamInjector"/>, and that component unregisters itself the moment it is not over
    /// water — so a boat hauled out on the hard costs nothing either.</item>
    /// <item><b>The set stays BOUNDED.</b> The advect shader loops over
    /// <see cref="FoamBuffer.MaxInjectors"/> slots as a COMPILE-TIME constant (never a runtime bound
    /// — <c>WaterShaderCompileGuardTests</c> names <c>[unroll]</c> over a runtime bound as a known
    /// magenta trap). Over the cap the extra injectors are dropped for the frame and it says so once,
    /// loudly, because a silent truncation reads as "every boat is churning" when it is not.</item>
    /// </list>
    ///
    /// <para><b>Determinism boundary.</b> Everything downstream of this registry is accumulated
    /// VISUAL state: it feeds no simulation, saves nothing, and is allowed to differ run-to-run with
    /// frame pacing exactly as particles are (rule 5 / ADR 0008). Only the water shader's foam
    /// compose may read it.</para>
    /// </summary>
    public static class FoamInjectionRegistry
    {
        static readonly List<FoamInjector> s_Live = new List<FoamInjector>();
        static Texture2D s_BlackFallback;
        static bool s_WarnedOverCap;
        static bool s_IdleBound;

        // ---- THE WAKE LIFT's publish (water PR F, register row 27) ------------------------------
        // Two global float4 arrays, one slot per live injector, uploaded by whichever injector runs
        // its LateUpdate last — the GrassFootstep pattern exactly: every writer owns its own slot and
        // uploads the whole buffer, so the last writer's upload already carries every writer's frame,
        // and there is no ordering assumption and no separate publisher host to keep alive.
        //
        // ⚠️ GLOBAL, not per-material: the water shader's VERTEX stage reads these, and the displaced
        // sea is drawn by chunk renderers that only ever receive a COPY of the flat renderer's
        // property block. A per-material write would reach one of the two passes and not the other.
        // (It is also why the two DIALS are material properties and these are not: a dial belongs to
        // the look, a hull's root belongs to the hull.)
        static readonly Vector4[] s_LiftRoot = new Vector4[FoamBuffer.MaxInjectors];
        static readonly Vector4[] s_LiftShape = new Vector4[FoamBuffer.MaxInjectors];
        // ⚠️ NOT a reservation table. The lift's slots are PACKED FRESH EVERY FRAME from the hulls
        // that actually hand a wave in: s_LiftFrame is the frame the current packing belongs to,
        // s_LiftCount how many of its slots are spoken for. Both reset on the first publish of a new
        // frame, so nothing is held between frames and there is no reservation to leak.
        static int s_LiftFrame = -1;
        static int s_LiftCount;
        static bool s_WarnedLiftOverCap;

        /// <summary>How many injectors are live and over water. The feature's cheap gate.</summary>
        public static int Count => s_Live.Count;

        /// <summary>
        /// The owner's look dial, mirrored out of the live water material's <c>_WakeFoamStrength</c>
        /// by <see cref="WaterSurface"/> every push. 0 (the shipped default) means the water shader
        /// would draw nothing from the buffer, so the feature does not bother filling one — the
        /// "effect off ⇒ nothing enqueued" half of the zero-cost contract.
        ///
        /// <para>Defaults to 0 and stays 0 in a scene with no <see cref="WaterSurface"/>: the fail
        /// state is OFF, never a buffer churning away for a sea nobody is drawing.</para>
        /// </summary>
        public static float LookStrength { get; private set; }

        /// <summary>Everything the feature needs to decide whether to record the pass at all.</summary>
        public static bool ShouldRun => LookStrength > 0f && (s_Live.Count > 0 || SurfDepositStrength > 0f);

        /// <summary>
        /// The velocity the whole buffer advects at (m/s) — the shared wind/current blend
        /// (<see cref="WaterSurface.FoamDriftDirection"/>) × the pushed flow speed, published by
        /// <see cref="WaterSurface"/> so the buffer travels along exactly the axis the shader's own
        /// foam and whitecaps already drift along.
        ///
        /// <para>⚠️ The GLOBAL blend, deliberately without the shoreward bias. The shader's
        /// <c>FoamDriftDir()</c> adds a per-position pull toward the coast; a rigid buffer scroll is
        /// uniform by construction and cannot carry a position-dependent term. So near a beach the
        /// buffered foam drifts on the wind/current axis while the shader's own fringe foam also
        /// leans shoreward — a small, stated divergence, not an oversight.</para>
        /// </summary>
        public static Vector2 DriftVelocity { get; private set; }

        /// <summary>Called by <see cref="WaterSurface"/> from the live material (see
        /// <see cref="LookStrength"/>). Not a mood float: this is an owner LOOK knob, not weather
        /// mood, so mood-easing it would be the double-drive trap <c>_RainRingStrength</c> already
        /// documents.</summary>
        public static void PublishLookStrength(float strength)
        {
            LookStrength = Mathf.Max(0f, strength);
        }

        /// <summary>Called by <see cref="WaterSurface"/> each push — see <see cref="DriftVelocity"/>.</summary>
        /// <summary>ADR 0040 rev 3: the bore's foam DEPOSIT dial, mirrored out of the live water material's
        /// <c>_SurfDepositStrength</c> by <c>WaterSurface</c> — the same read-not-push contract as
        /// <see cref="LookStrength"/>. 0 (the shipped value) = no deposit; with no hull churning either,
        /// the pass does not run at all.</summary>
        public static float SurfDepositStrength { get; private set; }

        /// <summary>The DRAWN wave scale the bore is evaluated at (<c>_OceanSwellScale</c> over the shader's
        /// legacy reference), published beside the deposit dial by <c>WaterSurface</c>: every C# reader of
        /// the drawn bore (the deposit pass, the lip spray) samples the field at this scale, or it would be
        /// reading a bore the water is not drawing.</summary>
        public static float DrawnWaveScale { get; private set; } = 1f;

        public static void PublishSurfDeposit(float strength, float drawnWaveScale)
        {
            SurfDepositStrength = Mathf.Max(0f, strength);
            DrawnWaveScale = Mathf.Max(1e-4f, drawnWaveScale);
        }

        public static void PublishDriftVelocity(Vector2 metresPerSecond)
        {
            DriftVelocity = metresPerSecond;
        }

        /// <summary>
        /// The FRESHNESS channel's half-life (s), mirrored out of <c>IsoFacetHullFeature</c> — the
        /// clock the wake's colour walks down (<c>WakeFoamAgeing</c>).
        ///
        /// <para>PR 11b needs it on the injection side: the dispersal lays foam on water the hull
        /// never touched, and that water has an AGE, so its freshness mark has to be the value a
        /// mark made now would have decayed to by then (<see cref="FoamBuffer.AgeMark"/>). Without
        /// it the widening band is born at the far end of the colour walk while the core beside it
        /// is white.</para>
        ///
        /// <para>The default matches the feature's own serialized default, so the very first frame —
        /// before any renderer feature has run — ages correctly rather than not at all.</para>
        /// </summary>
        public static float AgeHalfLifeSeconds { get; private set; } = 4f;

        /// <summary>Called by <c>IsoFacetHullFeature</c> each time it sets the pass up — the same
        /// read-not-push contract as <see cref="LookStrength"/>.</summary>
        public static void PublishAgeHalfLife(float halfLifeSeconds)
        {
            AgeHalfLifeSeconds = Mathf.Max(0f, halfLifeSeconds);
        }

        /// <summary>
        /// Bind the black fallback and a ZERO window, so a sea drawn while the pass is NOT running
        /// reads "no wake foam anywhere". Called by the feature the moment it decides not to record.
        ///
        /// <para>⚠️ Without this, a buffer that stops being filled — the boat moored ashore, the
        /// owner dialling the strength to 0 and back — would leave its last frame bound, and a
        /// frozen wake would hang on the sea for as long as the scene lived. Idempotent: the flag
        /// keeps it to one pair of calls per idle stretch, not one per camera per frame.</para>
        /// </summary>
        internal static void BindIdle()
        {
            if (s_IdleBound) return;
            EnsureFallbackBound();
            Shader.SetGlobalTexture(FoamShaderIds.BufferTex, s_BlackFallback);
            Shader.SetGlobalVector(FoamShaderIds.BufferWorld, Vector4.zero);
            s_IdleBound = true;
        }

        /// <summary>The feature says it has published a live buffer this frame, so the next idle
        /// stretch must rebind the fallback again.</summary>
        internal static void NotePublished()
        {
            s_IdleBound = false;
        }

        /// <summary>
        /// Zero every wake-lift slot and upload. ⚠️ The zeroing is the point: a hull hauled out,
        /// teleported or destroyed must take her wake with her, and an un-zeroed slot would leave one
        /// frame of stern wave standing in empty water for as long as the scene lived — the same
        /// frozen-last-frame trap <see cref="BindIdle"/> exists for on the foam buffer.
        ///
        /// <para>Clearing the WHOLE buffer is safe because the buffer is repacked from scratch every
        /// frame anyway (<see cref="PublishWakeLift"/>): every hull still making way is back in it on
        /// her next publish, so the living wakes lose at most the frame a boat left on.</para>
        /// </summary>
        internal static void ClearWakeLift()
        {
            System.Array.Clear(s_LiftRoot, 0, s_LiftRoot.Length);
            System.Array.Clear(s_LiftShape, 0, s_LiftShape.Length);
            s_LiftCount = 0;
            s_LiftFrame = -1;
            s_WarnedLiftOverCap = false;
            PublishLift();
        }

        /// <summary>
        /// Hand one hull's stern wave train to THIS FRAME'S packing (water PR F, register row 27),
        /// returning whether she got one of its slots — the
        /// transom root and heading it is drawn from, the wavelength that keeps station at her speed
        /// through the water, her churned half-beam, and her WAKE GATE.
        ///
        /// <para><b>Everything here is already computed.</b> <see cref="FoamInjector"/> works all four
        /// out each <c>LateUpdate</c> for the foam buffer — the transom (not the origin), the speed
        /// through the water (not over the ground), the half-beam from the hull's watertight width,
        /// and the wake channel's own gate. This publish adds no march, no history and no second
        /// field: it forwards the taps that exist.</para>
        ///
        /// <para><b>The gate is what makes at-rest exactly zero.</b> <paramref name="gate01"/> is the
        /// injector's <c>wake01</c> — <c>FoamBuffer.Shape01</c> on speed through the water — which is
        /// already 0 for a hull lying to her mooring, bobbing, or carried along by the stream. A hull
        /// making no way keeps station with no wave, so both this and
        /// <see cref="WakeLiftMath.TransverseWavelength"/> reach zero together — and a hull at zero
        /// TAKES NO SLOT, so she is absent from the frame's packing and the shader never reads her.
        ///
        /// <para>⚠️ A hull can also be refused because the frame already holds
        /// <see cref="FoamBuffer.MaxInjectors"/> STRONGER trains than hers: the cap is rationed by the
        /// gate, not by arrival order. Returning false therefore means "not drawn this frame", never
        /// "something went wrong", and a caller that needs to know must read the return value rather
        /// than assume the publish landed.</para></para>
        /// </summary>
        internal static bool PublishWakeLift(Vector2 rootXY, Vector2 heading,
                                             float wavelengthMetres, float halfBeamMetres,
                                             float gate01)
        {
            // 🔴 THE CAP IS RATIONED BY WHO IS LIFTING THE MOST WATER, NOT BY WHO ASKED FIRST.
            // Nothing is reserved and nothing is held between frames: the packing is built fresh every
            // frame from the hulls that hand a wave in.
            //
            // ⚠️ TWO EARLIER CUTS OF THIS CAP SHIPPED THE SAME SYMPTOM — the player's own boat drawing
            // no wake at all in a populated region — for two different reasons, so the history is worth
            // the lines it costs:
            //
            //   1. The first cut RESERVED: <c>ClaimLiftSlot</c> in the injector's OnEnable, held for the
            //      component's life. NineMileCreek has 31 injectors alive against 8 slots, so the resident
            //      fleet owned every slot at scene load and the one hull genuinely under way published
            //      nothing, all session, while the acceptance fixture read a moored boat's root.
            //   2. The second cut packed per frame in publish order — <see cref="CollectInjections"/>'s
            //      own law for the foam, "a hull that is live but not actually churning claims no slot" —
            //      and starved the very same hull for a subtler reason: SPEED THROUGH THE WATER IS NOT
            //      SPEED OVER THE GROUND. A moored boat has the stream running past her, so her wake is
            //      real, just tiny: NineMileCreek's 0.285 m/s of current measures as a 0.052 m train at
            //      gate 0.095, and she publishes it every frame like everybody else. Thirty-one of those
            //      ask before the player's boat does, and first-come-first-served gave them the whole cap
            //      while she ran at 6 m/s.
            //
            // So the ration is the wake's OWN STRENGTH. Slots fill in publish order while there is room;
            // once the cap is full a hull takes the WEAKEST slot, and only if her gate actually beats it.
            // That needs no dial, no threshold and no new number — the gate is already this feature's
            // amplitude, so "the eight biggest wakes in the frame are the eight that get drawn" is the
            // whole rule, and it is what the owner would expect if asked. A tie keeps the incumbent, which
            // is what stops a harbour full of identical bobbing hulls from trading slots every frame.
            int frame = Time.frameCount;
            bool rolled = s_LiftFrame != frame;
            if (rolled)
            {
                s_LiftFrame = frame;
                s_LiftCount = 0;
                System.Array.Clear(s_LiftRoot, 0, s_LiftRoot.Length);
                System.Array.Clear(s_LiftShape, 0, s_LiftShape.Length);
            }

            float gate = Mathf.Clamp01(gate01);
            float lambda = Mathf.Max(wavelengthMetres, 0f);

            // A hull making no way keeps station with no wave, so her gate and her wavelength reach zero
            // together and she takes no slot at all — a harbour full of moored boats costs the fleet
            // nothing. The upload is skipped unless THIS call is the one that rolled the frame, so a
            // fleet of thirty-one still costs one array upload a frame, not thirty-one (rule 7);
            // whoever rolled it has already pushed the cleared buffer.
            if (gate <= 0f || lambda <= 0f)
            {
                if (rolled) PublishLift();
                return false;
            }

            int slot;
            if (s_LiftCount < s_LiftShape.Length)
            {
                slot = s_LiftCount++;
            }
            else
            {
                // THE RATION. Find the weakest train standing in the frame and take its slot if this
                // hull's is stronger; `weakest` starts at her own gate so the comparison and the
                // "did she win" test are the same test, and an exact tie leaves the incumbent alone.
                slot = -1;
                float weakest = gate;
                for (int i = 0; i < s_LiftShape.Length; i++)
                {
                    if (s_LiftShape[i].x < weakest) { weakest = s_LiftShape[i].x; slot = i; }
                }

                // No silent caps: a truncation nobody is told about reads as "every boat lifts water".
                // Warn-once per domain load (unlike the foam's, which re-arms off the live count — the
                // number that overflows here is trains standing in ONE frame, which nothing tracks
                // between frames, and inventing a counter to re-arm a log line is not worth a field).
                // ⚠️ This fires in any populated harbour and that is not a fault: a moored fleet in a
                // current really does have more live trains than the shader has slots. It says which
                // ones got drawn, so the next reader does not have to discover the ration the hard way.
                if (!s_WarnedLiftOverCap)
                {
                    s_WarnedLiftOverCap = true;
                    Debug.LogWarning(
                        $"[FoamInjectionRegistry] more than {FoamBuffer.MaxInjectors} hulls have a live " +
                        "stern wave in the same frame. The strongest wakes own the slots and the weaker " +
                        "trains are NOT drawn this frame — a moored fleet in a current is the usual cause, " +
                        "and the boats actually making way outrank it. The cap is the water shader's " +
                        "HH_WAKE_LIFT_MAX compile-time unroll; raise FoamBuffer.MaxInjectors and " +
                        "HH_WAKE_LIFT_MAX together if a fleet ever needs more.");
                }

                if (slot < 0)
                {
                    if (rolled) PublishLift();
                    return false;
                }
            }

            s_LiftRoot[slot] = new Vector4(rootXY.x, rootXY.y, heading.x, heading.y);
            s_LiftShape[slot] = new Vector4(gate, lambda, Mathf.Max(halfBeamMetres, 0f), 0f);
            PublishLift();
            return true;
        }

        /// <summary>Upload both arrays whole. Cheap, and the reason no writer needs to know whether it
        /// ran first or last (<c>GrassFootstep.Publish</c>, same lesson: one array per instance made
        /// the last writer WIN instead of the last writer UPLOAD).</summary>
        static void PublishLift()
        {
            Shader.SetGlobalVectorArray(FoamShaderIds.WakeLiftRoot, s_LiftRoot);
            Shader.SetGlobalVectorArray(FoamShaderIds.WakeLiftShape, s_LiftShape);
        }

        /// <summary>
        /// The published wake-lift slots, for tests and for the acceptance fixture: the root/heading
        /// vector and the shape vector exactly as the shader will read them. Read-only by
        /// construction (the arrays are copied out), so no caller can write the sea's state.
        /// </summary>
        internal static void ReadWakeLift(Vector4[] root, Vector4[] shape)
        {
            if (root != null) System.Array.Copy(s_LiftRoot, root, Mathf.Min(root.Length, s_LiftRoot.Length));
            if (shape != null) System.Array.Copy(s_LiftShape, shape, Mathf.Min(shape.Length, s_LiftShape.Length));
        }

        internal static void Register(FoamInjector injector)
        {
            if (injector == null || s_Live.Contains(injector)) return;
            s_Live.Add(injector);
            EnsureFallbackBound();
        }

        internal static void Unregister(FoamInjector injector)
        {
            s_Live.Remove(injector);
            if (s_Live.Count <= FoamBuffer.MaxInjectors) s_WarnedOverCap = false;
            // ⚠️ Takes her stern wave off the sea in the same call. The per-frame packing already
            // drops a boat that stops publishing — but only once SOMEBODY ELSE publishes and rolls the
            // frame, and the last hull to leave a region leaves nobody. That is the frozen-last-frame
            // trap, and this is where it dies.
            ClearWakeLift();
        }

        /// <summary>
        /// Fill <paramref name="into"/> with this frame's deposits, newest-registered first, up to
        /// <see cref="FoamBuffer.MaxInjectors"/>. Returns how many slots were written. Allocation-free
        /// (the caller owns the array and reuses it every frame — rule 7).
        /// </summary>
        public static int CollectInjections(FoamInjection[] into)
        {
            if (into == null || into.Length == 0) return 0;
            int max = Mathf.Min(into.Length, FoamBuffer.MaxInjectors);
            int n = 0;
            int dropped = 0;
            for (int i = 0; i < s_Live.Count; i++)
            {
                FoamInjector injector = s_Live[i];
                if (injector == null) continue;
                // A hull that is live but not actually churning (moored on glass, drifting with the
                // stream) claims no slot — so the cap bounds BOATS THAT ARE MAKING FOAM, not boats.
                if (!injector.TryTakeInjection(out FoamInjection injection)) continue;
                if (n >= max) { dropped++; continue; }
                into[n++] = injection;
            }
            // ⚠️ Warn only when foam was ACTUALLY dropped — never merely because many injectors exist.
            // No silent caps: a truncation nobody is told about reads as "every boat is churning".
            if (dropped > 0 && !s_WarnedOverCap)
            {
                s_WarnedOverCap = true;
                Debug.LogWarning(
                    $"[FoamInjectionRegistry] {n + dropped} hulls are churning water but only " +
                    $"{FoamBuffer.MaxInjectors} slots exist, so {dropped} of them laid NO wake this " +
                    "frame. The cap is the advect shader's COMPILE-TIME loop bound (ADR 0027 #6); " +
                    "raise FoamBuffer.MaxInjectors and FOAM_MAX_INJECTORS together if a fleet needs it.");
            }
            return n;
        }

        /// <summary>
        /// Bind an opaque BLACK 1×1 as the global foam buffer so the water shader — which samples it
        /// every frame the owner has the look dialled in — reads "no wake foam anywhere" before the
        /// pass has ever run, instead of Unity's grey unbound-texture placeholder.
        ///
        /// <para>⚠️ Grey is not a cosmetic problem here: the mask is read as coverage, so an unbound
        /// sampler would smear a half-strength foam wash across the whole sea on the first frame of
        /// every scene. Black is exactly "no foam". Same lesson, same shape, as the interior guard's
        /// black 1×1 and the reflection target's clear one.</para>
        ///
        /// <para><b>TWO channels, matching the real buffer.</b> The water shader reads <c>.rg</c> —
        /// coverage and freshness — and the zero window bound alongside this makes it bail before it
        /// looks. Matching the CHANNELS anyway costs one byte and keeps "black means nothing here" a
        /// property of the texture rather than of a guard somewhere else that might move. The bit depth
        /// deliberately does not track the render target's (register row 28 widened that to 16): zero is
        /// zero at any depth, and this is a 1x1 that is never decayed.</para>
        /// </summary>
        static void EnsureFallbackBound()
        {
            if (s_BlackFallback != null) return;
            s_BlackFallback = new Texture2D(1, 1, TextureFormat.RG16, false, true)
            {
                name = "HHFoamBufferFallback",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            s_BlackFallback.SetPixel(0, 0, Color.black);
            s_BlackFallback.Apply(false, true);
            Shader.SetGlobalTexture(FoamShaderIds.BufferTex, s_BlackFallback);
            Shader.SetGlobalVector(FoamShaderIds.BufferWorld, Vector4.zero);
        }
    }

    /// <summary>Shader property ids shared by <see cref="FoamInjector"/>, the renderer feature, the
    /// advect shader and the water shader's foam compose.</summary>
    public static class FoamShaderIds
    {
        /// <summary>The advected foam buffer, published globally by the feature's pass and sampled by
        /// the water shader as a mask that ADDS to the existing foam. Single channel, point filtered:
        /// one texel IS one world cell (<see cref="FoamBuffer.CellsPerUnit"/>).</summary>
        public static readonly int BufferTex = Shader.PropertyToID("_HHFoamBufferTex");

        /// <summary>The buffer's world window: <c>xy</c> = the cell-snapped lower-left corner (m),
        /// <c>z</c> = the window extent (m), <c>w</c> = 1/extent. <c>z</c> ≤ 0 means "never
        /// published" and the water shader treats that as no foam. This is what makes the read
        /// WORLD-anchored — the crawl law (see <see cref="FoamBuffer.WorldCellOrigin"/>).</summary>
        public static readonly int BufferWorld = Shader.PropertyToID("_HHFoamBufferWorld");

        /// <summary>Advect pass: the previous frame's buffer (the ping-pong read side).</summary>
        public static readonly int Prev = Shader.PropertyToID("_HHFoamPrev");
        /// <summary>Advect pass: this frame's WHOLE-CELL content shift, in texels
        /// (camera scroll + wind drift, already reduced to integers by the cell law).</summary>
        public static readonly int Shift = Shader.PropertyToID("_HHFoamShift");
        /// <summary>Advect pass: this frame's exponential decay multiplier
        /// (<see cref="FoamBuffer.DecayFactor"/>).</summary>
        public static readonly int Decay = Shader.PropertyToID("_HHFoamDecay");
        /// <summary>Advect pass: the buffer resolution, <c>xy</c> = texels, <c>zw</c> = 1/texels.</summary>
        public static readonly int Resolution = Shader.PropertyToID("_HHFoamResolution");
        /// <summary>Advect pass: per-slot injection segment — <c>xy</c> = from, <c>zw</c> = to (world m).</summary>
        public static readonly int InjectSeg = Shader.PropertyToID("_HHFoamInjectSeg");
        /// <summary>Advect pass: per-slot injection shape — <c>x</c> = radius (m), <c>y</c> = amount
        /// (0 in an unused slot, which is what makes the fixed-length loop cost nothing),
        /// <c>z</c> = vigour 0..1 (the FRESHNESS gate; see <see cref="FoamInjection.Vigour"/>).</summary>
        public static readonly int InjectShape = Shader.PropertyToID("_HHFoamInjectShape");

        /// <summary>Advect pass: this frame's exponential decay multiplier for the FRESHNESS channel
        /// (<see cref="FoamBuffer.DecayFactor"/> on the age half-life — a different, shorter half-life
        /// than the coverage channel's, so the colour walk completes while the foam is still visible).
        /// </summary>
        public static readonly int AgeDecay = Shader.PropertyToID("_HHFoamAgeDecay");
        /// <summary>ADR 0040 rev 3: the bore's deposit — x = strength, y = the drawn wave scale, z = dt.</summary>
        public static readonly int SurfDeposit = Shader.PropertyToID("_HHSurfDeposit");

        /// <summary>PR 11b: per-slot dispersal track, nodes 0 and 1 — <c>xy</c> = the transom now,
        /// <c>zw</c> = one fifth of the reach astern (world m).</summary>
        public static readonly int DispersalTrackA = Shader.PropertyToID("_HHFoamDispTrackA");
        /// <summary>PR 11b: per-slot dispersal track, nodes 2 and 3 (world m).</summary>
        public static readonly int DispersalTrackB = Shader.PropertyToID("_HHFoamDispTrackB");
        /// <summary>PR 11b: per-slot dispersal track, nodes 4 and 5 (world m) — 5 is the oldest.</summary>
        public static readonly int DispersalTrackC = Shader.PropertyToID("_HHFoamDispTrackC");
        /// <summary>PR 11b: <c>x</c> = the edge's per-frame gain (0 in an unused slot and at the
        /// shipped passthrough — which is what makes the whole block cost nothing and stay
        /// bit-exact), <c>y</c> = the edge's soft width (m), <c>z</c> = the churn band's half-width
        /// (m), <c>w</c> = the envelope half-width (m). The tail's age mark rides
        /// <c>_HHFoamInjectShape[i].w</c>, which PR 11a left unused.</summary>
        public static readonly int DispersalShape = Shader.PropertyToID("_HHFoamDispShape");

        /// <summary>
        /// Water PR F (register row 27): per-slot WAKE LIFT root — <c>xy</c> = the transom in world
        /// metres (<c>FoamBuffer.SternWorld</c>, the same point the foam is shed from), <c>zw</c> = the
        /// hull's unit forward direction. Mirrored as <c>_HHWakeLiftRoot</c> in the water shader.
        /// </summary>
        public static readonly int WakeLiftRoot = Shader.PropertyToID("_HHWakeLiftRoot");

        /// <summary>
        /// Water PR F: per-slot WAKE LIFT shape — <c>x</c> = the wake gate 0..1 (0 at rest),
        /// <c>y</c> = the transverse wavelength (m) that keeps station at her speed through the water,
        /// <c>z</c> = her churned half-beam (m), <c>w</c> unused. <b>x or y at 0 is an unused slot</b>
        /// and the shader's loop skips it — which is also how a hull lying to her mooring costs
        /// nothing. Mirrored as <c>_HHWakeLiftShape</c> in the water shader.
        /// </summary>
        public static readonly int WakeLiftShape = Shader.PropertyToID("_HHWakeLiftShape");
    }
}
