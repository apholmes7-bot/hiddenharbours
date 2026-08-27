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

        public FoamInjection(Vector2 from, Vector2 to, float radius, float amount, float vigour)
        {
            From = from;
            To = to;
            Radius = radius;
            Amount = amount;
            Vigour = vigour;
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
        public static bool ShouldRun => s_Live.Count > 0 && LookStrength > 0f;

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
        public static void PublishDriftVelocity(Vector2 metresPerSecond)
        {
            DriftVelocity = metresPerSecond;
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
        /// </summary>
        static void EnsureFallbackBound()
        {
            if (s_BlackFallback != null) return;
            s_BlackFallback = new Texture2D(1, 1, TextureFormat.R8, false, true)
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
    }
}
