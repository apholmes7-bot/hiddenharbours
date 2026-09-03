using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The HEADLESS TWIN of <c>Assets/_Project/Art/Shaders/Include/SpriteLightResponse.hlsl</c> — the
    /// maths that lets a flat, rig-baked sprite <b>catch and rim with the colour of a light</b>
    /// (<c>docs/design/sprite-light-response.md</c>). Every function here is a line-for-line mirror of
    /// an HLSL function of the same name, lifted into C# so the response curve, the front/behind gate
    /// and the rim steer are unit-tested without a GPU (rule 5's determinism guard, and the
    /// HLSL-twin discipline the design doc §4.2 finding 2 demands "from the first commit").
    ///
    /// <para><b>The include is SHARED on purpose.</b> <c>Tree.mat</c> is the first adopter; shoreline
    /// plants and rocks consume the same mask contract next. Nothing in this file is tree-specific —
    /// the tree-specific part is which sheets get bound and at what strength.</para>
    ///
    /// <para><b>🔴 THE MASK CHANNEL ORDER IS OURS, NOT THE REFERENCE TECHNIQUE'S.</b> Every public
    /// description of this technique says <i>green = front, blue = rim</i>. Our rigs bake
    /// <b>R = key light · G = back rim · B = depth · A = coverage</b>
    /// (<c>TreeRig.packMask</c>; pinned at the bake by
    /// <c>TreeRigBakeTests.MaskChannels_AreKeyRimDepthCoverage_NotTheReferenceTechniquesOrder</c> and
    /// again HERE, at the point of consumption, by <see cref="MaskKey"/>..<see cref="MaskCoverage"/>
    /// and the tests that use them). A ported snippet swaps two channels and looks SUBTLY wrong
    /// rather than broken.</para>
    ///
    /// <para><b>🔴 THE NORMAL SHEET USED TO BE SMALLER THAN THE MASK.</b> The rig composited a 1 px
    /// keyline ring outside the volume: those pixels were opaque in the albedo (and so in the mask's
    /// A) but carried NO surface normal — measured at 611 px of Red Spruce's 7601 on pass 1, 722 of
    /// 6570 on pass 2. ADR 0031 wave 2 retired that ring at the rig, so a freshly baked sheet has all
    /// three channels covering exactly the geometry
    /// (<c>TreeRigBakeTests.ChannelCoverage_AlbedoMaskAndNormal_AllAgree_NowTheKeylineIsRetired</c>).
    /// <b>The sampling rule is unchanged and deliberate:</b> all three channels are still sampled
    /// through the <b>albedo's</b> mesh and uv, and every term that needs a normal still falls back
    /// to the baked mask where there is none — see <see cref="KeyResponse"/>'s <c>hasNormal</c>.
    /// That fallback is what keeps sheets baked before the retirement, and any other rig sharing
    /// this path (#428), from consuming a fabricated normal.</para>
    ///
    /// <para>Pure and allocation-free: no <see cref="Time"/>, no <see cref="Random"/>, no engine
    /// state. Visual-only — nothing here drives sim or save (rule 5).</para>
    /// </summary>
    public static class SpriteLightMath
    {
        // =================================================================================
        // 🔴 THE MASK CONTRACT, PINNED AT THE POINT OF CONSUMPTION
        // =================================================================================

        /// <summary>Mask <b>R</b> — the KEY-LIGHT channel: the rig's <c>max(0, N·K)^1.35</c> baked
        /// against its own fixed key (<see cref="RigKeyDirection"/>). NOT "front" in the reference
        /// technique's green-channel sense.</summary>
        public const int MaskKey = 0;

        /// <summary>Mask <b>G</b> — the BACK-RIM channel: the outer fringe of the silhouette, already
        /// gated on local mass thickness by the rig's rule 3. NOT the reference's blue.</summary>
        public const int MaskRim = 1;

        /// <summary>Mask <b>B</b> — DEPTH along the view axis, normalised per sprite.
        /// <b>1 = NEAR the camera, 0 = FAR</b> (the rig writes <c>(z − zmin) / zrange</c> and treats
        /// higher z as occluding).</summary>
        public const int MaskDepth = 2;

        /// <summary>Mask <b>A</b> — COVERAGE: the albedo's own alpha, keyline ring included.</summary>
        public const int MaskCoverage = 3;

        // =================================================================================
        // the camera — ADR 0006/0022, the same numbers the tree/boat/rock/shoreline bakes use
        // =================================================================================

        /// <summary>The shared bake camera: ¾ from the south, this many degrees above the horizon.
        /// Every rig in the project renders at it, which is why one light-direction transform serves
        /// all of them.</summary>
        public const float CameraElevationDeg = 40f;

        /// <summary>How far up the screen one world metre of NORTHWARD ground travel draws
        /// (<c>sin 40° ≈ 0.643</c> — the kit README's "ground depth ×0.643"). Also how much a
        /// northward light direction leans up-screen.</summary>
        public static readonly float GroundDepthScale = Mathf.Sin(CameraElevationDeg * Mathf.Deg2Rad);

        /// <summary>How tall one world metre of HEIGHT draws (<c>cos 40° ≈ 0.766</c> — the kit
        /// README's "Height ×0.766").</summary>
        public static readonly float HeightScale = Mathf.Cos(CameraElevationDeg * Mathf.Deg2Rad);

        // =================================================================================
        // the rig's own fixed key — the ORACLE the live key term is validated against
        // =================================================================================

        /// <summary>
        /// The rig's fixed key light in the SAME view space the normal sheet decodes to
        /// (<c>+x</c> right, <c>+y</c> UP, <c>+z</c> toward the camera): upper-left, slightly toward
        /// the viewer. <c>treeIsoRig.js</c> writes <c>LIGHT.key = nrm([-0.55, -0.66, 0.52])</c> in ITS
        /// frame, where <c>ny</c> is screen-DOWN; <c>normalView()</c> then encodes <c>-ny</c> into
        /// green, so the decoded normal is y-UP and the key's y flips sign with it.
        ///
        /// <para><b>Why this constant is load-bearing:</b> the rig's R channel is
        /// <c>max(0, N·K)^1.35</c>. Feed this direction to <see cref="KeyTerm"/> against a decoded
        /// normal and you must get the baked R back — which is exactly how the tests prove the whole
        /// decode convention (the y-flip above all) is right. Get the flip wrong and the tree lights
        /// from below.</para>
        /// </summary>
        public static readonly Vector3 RigKeyDirection = new Vector3(-0.55f, 0.66f, 0.52f).normalized;

        /// <summary>The exponent the rig raises its Lambert term to before writing mask R
        /// (<c>Math.pow(max(0, N·K), 1.35)</c>). Mirrored so the live key term is the SAME curve,
        /// evaluated for a different light.</summary>
        public const float KeyExponent = 1.35f;

        // =================================================================================
        // decode
        // =================================================================================

        /// <summary>
        /// Decode one texel of the NORMAL sheet to a unit view-space normal (<c>+x</c> right,
        /// <c>+y</c> up, <c>+z</c> toward the camera). The sheet imports with <b>sRGB OFF and no lossy
        /// compression</b> (pinned by <c>TreeSheetImportTests</c>) precisely so these bytes survive as
        /// data; do NOT "fix" an import setting to make a channel read right — if a channel reads
        /// wrong the bug is the sampling or the channel order.
        ///
        /// <para>Returns <see cref="Vector3.zero"/> when the texel has no coverage (the keyline ring),
        /// so callers can test <c>sqrMagnitude</c> instead of consuming a normalised
        /// <c>(-1,-1,-1)</c>.</para>
        /// </summary>
        public static Vector3 DecodeNormal(Color32 texel)
        {
            if (texel.a == 0) return Vector3.zero;
            var n = new Vector3(
                texel.r / 255f * 2f - 1f,
                texel.g / 255f * 2f - 1f,
                texel.b / 255f * 2f - 1f);
            float len = n.magnitude;
            return len > 1e-5f ? n / len : Vector3.zero;
        }

        // =================================================================================
        // where the light IS — the one transform every consumer shares
        // =================================================================================

        /// <summary>
        /// A light's direction in VIEW space, from its GROUND-PLANE direction and its elevation —
        /// the single place the bake camera's geometry is applied, so the sun, the moon and every
        /// local lamp agree about what "in front" means.
        ///
        /// <para><paramref name="groundDir"/> is the unit ground-plane direction TOWARD the light
        /// (<c>+x</c> east, <c>+y</c> north) — exactly what <c>_SunDir</c> publishes.
        /// <paramref name="elevation"/> is its height above the horizon as a sine, <c>1</c> at the
        /// zenith, <c>0</c> at the horizon, negative below it — exactly what <c>_SunElevation</c>
        /// publishes.</para>
        ///
        /// <para>Under the shared 40° camera looking north, world <c>+y</c> (north) maps to view
        /// <c>(0, sin40, −cos40)</c> and world <c>+z</c> (up) to <c>(0, cos40, sin40)</c>. So a light
        /// in the NORTH has a NEGATIVE view z — it is BEHIND the sprite — which is the whole basis of
        /// <see cref="Frontness"/>. Result is unit length (the basis is orthonormal); degenerate input
        /// falls back to straight at the viewer.</para>
        /// </summary>
        public static Vector3 LightViewDirection(Vector2 groundDir, float elevation)
        {
            float sa = Mathf.Clamp(elevation, -1f, 1f);            // sin(altitude)
            float ca = Mathf.Sqrt(Mathf.Max(0f, 1f - sa * sa));    // cos(altitude)

            Vector2 g = groundDir.sqrMagnitude > 1e-10f ? groundDir.normalized : Vector2.zero;
            float wx = g.x * ca, wy = g.y * ca, wz = sa;           // world direction toward the light

            var v = new Vector3(
                wx,
                wy * GroundDepthScale + wz * HeightScale,
                -wy * HeightScale + wz * GroundDepthScale);
            float len = v.magnitude;
            return len > 1e-5f ? v / len : Vector3.forward;
        }

        /// <summary>
        /// THE SUN, with the cycle-off fallback the additive lights already use, plus how much sun
        /// there is (<paramref name="sunAmount"/>, 0..1).
        ///
        /// <para><see cref="DayNightController"/> publishes <c>_SunDir</c>/<c>_SunElevation</c> at
        /// RUNTIME only, so in EDIT MODE — or a bare art scene, or before the controller installs —
        /// they are unset. A response that reads as dead in the scene view is a response the owner
        /// cannot tune, so an unset direction falls back to a plausible mid-morning sun. Mirrors the
        /// shader's <c>SpriteLightSunDirection</c>.</para>
        /// </summary>
        public static Vector3 SunDirection(Vector2 sunDirGround, float sunElevation, out float sunAmount)
        {
            bool unset = sunDirGround.sqrMagnitude < 1e-6f;
            Vector2 g = unset ? new Vector2(-0.45f, -0.89f) : sunDirGround;   // south and a little west
            float e = unset ? 0.72f : sunElevation;
            sunAmount = Mathf.Clamp01(e);
            return LightViewDirection(g, e);
        }

        /// <summary>
        /// THE WEATHER, taken from the ONE global that already carries it — the headless twin of the
        /// shader's <c>SpriteLightSunAmount</c>.
        ///
        /// <para>The <paramref name="sunAmount"/> that <see cref="SunDirection"/> hands back is
        /// <c>saturate(elevation)</c>: it takes the catch out at dusk, and that is all it knows. The
        /// CAST SHADOW knows more. <see cref="DayNightController"/> publishes <c>_ShadowStrength</c> =
        /// <see cref="DayNightMath.ShadowStrength"/> = <c>saturate(elevation) × (1 − weatherDim ×
        /// overcastFadesShadow)</c> — the SAME <c>saturate(elevation)</c> with the live weather folded
        /// in. So the published global already IS "how much sun there is, cloud included", and the sun
        /// catch reads the weather by TAKING that number rather than deriving one of its own: one
        /// publisher, one value, and no way for a lit side to disagree with the shadow beneath it.</para>
        ///
        /// <para>🔴 <b>Clear weather is BIT-identical</b> to the elevation-only reading this replaces:
        /// <c>weatherDim 0</c> makes the weather factor exactly <c>1f</c>, and <c>x * 1f</c> is the
        /// IEEE 754 multiplicative identity. Off the cycle nothing has published either global and
        /// <c>_ShadowStrength</c> reads 0, which would kill the catch the fallback sun exists to give —
        /// so an unset direction returns <paramref name="sunAmount"/> untouched, testing the SAME
        /// global <see cref="SunDirection"/> tests so the pair can never disagree about whether the
        /// cycle is running.</para>
        /// </summary>
        /// <param name="sunDirGround">What <c>_SunDir.xy</c> carries — zero when nothing has published.</param>
        /// <param name="sunAmount">The elevation-only amount out of <see cref="SunDirection"/>.</param>
        /// <param name="shadowStrength">What <c>_ShadowStrength</c> carries this tick.</param>
        public static float SunAmount(Vector2 sunDirGround, float sunAmount, float shadowStrength)
        {
            bool unset = sunDirGround.sqrMagnitude < 1e-6f;
            return unset ? sunAmount : Mathf.Clamp01(shadowStrength);
        }

        /// <summary>
        /// The same transform for a POINT light: the view-space direction from a sprite standing at
        /// <paramref name="spriteWorldXY"/> toward a lamp at <paramref name="lampWorldXY"/>, given the
        /// lamp's elevation above the horizon as seen from the sprite. Used for the boat spotlight —
        /// the one local light this project already publishes as shader globals (ADR 0016).
        /// </summary>
        public static Vector3 LampViewDirection(Vector2 spriteWorldXY, Vector2 lampWorldXY, float elevation)
            => LightViewDirection(lampWorldXY - spriteWorldXY, elevation);

        // =================================================================================
        // the two terms
        // =================================================================================

        /// <summary>
        /// The KEY term for a live light: <c>max(0, N·L)^1.35</c> — <b>the rig's own Lambert
        /// function</b>, evaluated for a different light direction.
        ///
        /// <para>That identity is the point. Pass <see cref="RigKeyDirection"/> and this reproduces
        /// the baked mask R channel; pass the live sun and you get what the rig WOULD have baked had
        /// the sun been its key. So the mask R is not merely consumed — it is the oracle that proves
        /// the normal decode, the exponent and the channel assignment are all correct together.</para>
        /// </summary>
        public static float KeyTerm(Vector3 normal, Vector3 lightDir)
            => Mathf.Pow(Mathf.Max(0f, Vector3.Dot(normal, lightDir)), KeyExponent);

        /// <summary>
        /// FRONTNESS: how much a light is in FRONT of the sprite (between it and the camera) rather
        /// than BEHIND it, <c>0</c> (fully behind) .. <c>1</c> (fully in front), from the light's view-space
        /// z. This is the decision the reference technique makes with a screen-space lookup against
        /// each light's own green-over-blue gradient sprite (design doc §4.1); we can compute it
        /// directly because <see cref="LightViewDirection"/> already knows the camera.
        ///
        /// <para><paramref name="band"/> is how soft the changeover is: a small band switches
        /// abruptly at the sprite's plane, a wide one cross-fades key into rim across a broad arc.
        /// <b>The shipped sun never goes fully behind</b> — <c>DayNightMath.SunDirection</c> keeps it
        /// in the south (toward the camera) all day, so its view z is positive from dawn to dusk
        /// (measured 0.215 at the horizon to 0.724 at noon). The band is therefore not a smoothing
        /// detail but the LOOK decision that buys the grazing dawn/dusk rim — see
        /// <see cref="DefaultFrontBand"/>.</para>
        /// </summary>
        public static float Frontness(float lightViewZ, float band)
        {
            float b = Mathf.Max(band, 1e-4f);
            return Mathf.Clamp01((lightViewZ + b) / (2f * b));
        }

        /// <summary>
        /// The shipped changeover width — the value <c>Tree.mat</c> carries in <c>_LightFrontBand</c>
        /// (asserted, so the two cannot drift). Chosen against the measured daylight arc: at 0.6 a
        /// HORIZON sun reads 68% front / 32% rim, so golden hour genuinely rims the crown, while a
        /// NOON sun still saturates to pure key and midday reads flat and front-lit — which is what a
        /// tree under a high sun should look like. A tighter band would make the sun pure key all day
        /// and leave the rim term inert on everything but the boat lamp.
        /// </summary>
        public const float DefaultFrontBand = 0.6f;

        /// <summary>
        /// RIM STEER: how much a rim pixel faces the light LATERALLY (in the screen plane),
        /// <c>0</c>..<c>1</c>. A rim lights the side of the silhouette that faces the light, and on a
        /// silhouette the normal lies in the screen plane — which is exactly the quantity the rig's
        /// own rim term uses (<c>back = max(0, n̂.xy · RS)</c>). Depth is deliberately ignored: the
        /// front/behind half of the decision is <see cref="Frontness"/>'s job.
        ///
        /// <para>Returns 1 when either lateral direction is degenerate (a normal pointing straight at
        /// the camera, or a light pointing straight away from it) so the baked band passes through
        /// unsteered rather than snapping to black. <b>An OVERHEAD light is not one of those cases</b>
        /// — the 40° camera projects world "up" onto screen-up, so an overhead light has a real
        /// lateral direction and rims the TOP of the silhouette.</para>
        /// </summary>
        public static float RimSteer(Vector3 normal, Vector3 lightDir)
        {
            var n = new Vector2(normal.x, normal.y);
            var l = new Vector2(lightDir.x, lightDir.y);
            if (n.sqrMagnitude < 1e-8f || l.sqrMagnitude < 1e-8f) return 1f;
            return Mathf.Clamp01(Vector2.Dot(n.normalized, l.normalized));
        }

        /// <summary>
        /// DEPTH attenuation from mask B (<b>1 = near, 0 = far</b>): <c>lerp(1, depth, bias)</c>. At
        /// <paramref name="bias"/> 0 the channel is unused; at 1 the far side of the canopy catches
        /// nothing, so a near light wraps the form instead of flooding it flat.
        /// </summary>
        public static float DepthAttenuation(float maskDepth, float bias)
            => Mathf.Lerp(1f, Mathf.Clamp01(maskDepth), Mathf.Clamp01(bias));

        // =================================================================================
        // the assembled response — what the fragment stage actually adds
        // =================================================================================

        /// <summary>
        /// The KEY response at one texel for one light, before colour and strength.
        ///
        /// <para><paramref name="relight"/> blends the BAKED catch (0) toward the LIVE recompute (1).
        /// At 0 the tree brightens where the rig's fixed key already put light; at 1 the catch swings
        /// with the real light and the crown's far side goes dark at dawn. <b>Where the texel has no
        /// normal it collapses to the baked mask regardless</b> — that is the keyline ring, which has
        /// coverage but no surface, and where a decoded <c>(0,0,0,0)</c> would otherwise be a
        /// fabricated normal pointing nowhere.</para>
        /// </summary>
        /// <param name="maskKey">Mask R at this texel, 0..1.</param>
        /// <param name="maskDepth">Mask B at this texel, 0..1 (1 = near).</param>
        /// <param name="maskCoverage">Mask A at this texel, 0..1.</param>
        /// <param name="normal">Decoded view-space normal, or zero where there is none.</param>
        /// <param name="lightDir">View-space unit direction toward the light.</param>
        /// <param name="relight">0 = baked catch, 1 = fully live recompute.</param>
        /// <param name="frontBand">Softness of the front/behind changeover (<see cref="Frontness"/>).</param>
        /// <param name="depthBias">How hard depth attenuates (<see cref="DepthAttenuation"/>).</param>
        public static float KeyResponse(float maskKey, float maskDepth, float maskCoverage,
                                        Vector3 normal, Vector3 lightDir,
                                        float relight, float frontBand, float depthBias)
        {
            bool hasNormal = normal.sqrMagnitude > 1e-8f;
            float live = hasNormal ? KeyTerm(normal, lightDir) : 0f;
            float catchTerm = Mathf.Lerp(Mathf.Clamp01(maskKey), live,
                                         hasNormal ? Mathf.Clamp01(relight) : 0f);
            return catchTerm
                   * Frontness(lightDir.z, frontBand)
                   * DepthAttenuation(maskDepth, depthBias)
                   * Mathf.Clamp01(maskCoverage);
        }

        /// <summary>
        /// The BACK-RIM response at one texel for one light, before colour and strength: the baked
        /// rim band, gated on the light being BEHIND, and steered to the side of the silhouette that
        /// faces it.
        ///
        /// <para><b>Measured limitation, stated rather than hidden:</b> mask G is already directional
        /// — the rig bakes it against a fixed back light at <c>nrm([0.48, -0.28, -0.83])</c> (behind
        /// and upper-RIGHT), so pixels the rig's rim never touched are 0 and no amount of live
        /// steering can rim them. A light behind and to the LEFT therefore dims the rim rather than
        /// moving it to the left edge. Fixing that needs an undirected rim band from the rig — an
        /// art-director change, flagged not made.</para>
        /// </summary>
        /// <param name="steer">0 = the baked band passes through unsteered, 1 = fully steered by
        /// <see cref="RimSteer"/>.</param>
        public static float RimResponse(float maskRim, float maskDepth, float maskCoverage,
                                        Vector3 normal, Vector3 lightDir,
                                        float steer, float frontBand, float depthBias)
        {
            bool hasNormal = normal.sqrMagnitude > 1e-8f;
            float lateral = hasNormal ? RimSteer(normal, lightDir) : 1f;
            float steered = Mathf.Lerp(1f, lateral, Mathf.Clamp01(steer));
            return Mathf.Clamp01(maskRim)
                   * steered
                   * (1f - Frontness(lightDir.z, frontBand))     // BEHIND, not in front
                   * DepthAttenuation(maskDepth, depthBias)
                   * Mathf.Clamp01(maskCoverage);
        }

        /// <summary>
        /// How much a texel is FORBIDDEN to rim, 0..1 — the twin of <c>SpriteLitDecorNoRim</c> in
        /// <c>Include/SpriteLitDecor.hlsl</c>, and the one piece of the shared path that did not exist
        /// when only the trees consumed it.
        ///
        /// <para><b>Why it exists.</b> A tree is all body and wood: every pixel of it may catch a back
        /// rim. The rig families that came after are not. The shoreline plants bake blades, culms and
        /// fronds as <i>strap</i> material and the shrubs bake a <i>veil</i>, and both committed
        /// contracts say the same thing in the same words about their state sheet — <i>"This is the
        /// branch. Read it, do not infer it."</i> A blade of eelgrass given a tree trunk's silhouette
        /// rim reads as a mistake, because it is one.</para>
        ///
        /// <para><b>Why a selector and not a channel index.</b> The two rigs put the flag in DIFFERENT
        /// channels — plants in their tide sheet's blue, shrubs in their calendar sheet's red — so the
        /// consumer must be told, not left to guess. A one-hot selector dotted against the texel is that
        /// instruction in the form the shader can act on without a per-pixel branch, and it is exact
        /// for a single channel.</para>
        ///
        /// <para><b>Belt and braces, deliberately.</b> Both contracts ALSO state that the rim channel is
        /// already identically 0 on those pixels, so an honest bake needs no gate at all. The gate costs
        /// one dot and one multiply, and it makes a bake that regressed — a channel swapped, a re-bake
        /// that forgot — fail safe instead of shipping a wrong rim.</para>
        /// </summary>
        /// <param name="gateTexel">The state-sheet texel, channels in 0..1.</param>
        /// <param name="selector">The one-hot channel selector. <see cref="Vector4.zero"/> means no gate
        /// is bound, which is the tree and which returns 0 (every pixel may rim) for any texel.</param>
        public static float RimGateFlag(Vector4 gateTexel, Vector4 selector) =>
            Mathf.Clamp01(Vector4.Dot(gateTexel, selector));
    }
}
