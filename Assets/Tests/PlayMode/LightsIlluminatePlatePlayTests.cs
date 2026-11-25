using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A LAMP LIGHTS THE GROUND</b> — the second half of the owner's 2026-09-04 ruling, measured on the
    /// pier he was looking at, and photographed.
    ///
    /// <para>#733 took the disc off the SOURCE (<i>"it should glow from within the lamp reasilitcally"</i>)
    /// and left the pier honestly dark, because ADR 0016's additive quad is the source's own bloom and can
    /// only lay a sheet of cream over the frame. This is the other half: the patch of ground the lantern
    /// makes brighter, drawn by <see cref="LampPoolSystem"/> as a MULTIPLY.</para>
    ///
    /// <para><b>Three claims, and the third is the one that matters.</b> That the planks are lit is easy to
    /// show and easy to fake — the refused disc lit them too, in the sense that their pixels got brighter.
    /// What the disc did was FLATTEN them, and what a multiply promises is that it cannot: it scales what the
    /// frame already returned, so relative contrast, being a ratio, comes through untouched. That promise is
    /// proved abstractly in <c>LampPoolTests</c>; here it is proved on the actual planks.</para>
    /// </summary>
    public class LightsIlluminatePlatePlayTests
    {
        const string SceneName = "StPeters";
        const string CreekSceneName = "NineMileCreek";
        const string PlateDir = "lights-illuminate";

        private WharfNightStage _stage;

        // The installed gull flock, stood down by the noon control alone, and every flag it saved.
        private GullFlock _installedFlock;
        private bool _flockWasEnabled;
        private readonly List<(Renderer Renderer, bool Enabled)> _gullRenderers = new();
        private readonly List<(SpriteShadow Shadow, bool Enabled)> _gullShadows = new();

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            RestoreGulls();   // before the first yield, so the birds come back whatever the stage does
            if (_stage != null) yield return _stage.TearDown();
            _stage = null;
            // Put BOTH systems' profiles back. Null re-resolves the shipped Resources asset on the next
            // access, so a test that handed them its own copy cannot leave the owner's dials moved for
            // whatever runs next — and saying it for both is what puts the intent on the page.
            if (LampPoolSystem.Instance != null) LampPoolSystem.Instance.Profile = null;
            if (LampShadowSystem.Instance != null) LampShadowSystem.Instance.Profile = null;
        }

        // =============================================================================================

        /// <summary>
        /// <b>The 02:00 pier: the planks come up, and they are still planks.</b> Both arms are the same
        /// frame with one field moved — <see cref="LampShadowProfile.PoolsEnabled"/> — so they differ by the
        /// thing under review and by nothing else.
        /// </summary>
        [UnityTest]
        public IEnumerator ThePierPlanks_AreLitByTheLamp_AndAreStillPlanks()
        {
            WharfNightStage.RequireAGraphicsDevice();

            _stage = new WharfNightStage(SceneName, PlateDir);
            yield return _stage.Load();
            yield return _stage.SetNight(2f);

            IReadOnlyList<LampPosts.Site> sites = StPetersWharf.LampPostSites();
            GameObject lamps = PlaceLamps(sites);
            yield return _stage.FrameOn(sites[0].Position + new Vector2(0f, -1f));

            LampShadowProfile profile = PoolProfile();

            // BEFORE: #733's frame — the lantern glows, the planks are dark.
            profile.PoolsEnabled = false;
            yield return Settle();
            byte[] noPool = _stage.Capture();
            _stage.SavePlate("01-pier-0200-no-pool-BEFORE.png", noPool);

            // AFTER: the lamp lights the ground it stands over.
            profile.PoolsEnabled = true;
            yield return Settle();
            byte[] pooled = _stage.Capture();
            _stage.SavePlate("02-pier-0200-pool-AFTER.png", pooled);

            bool[] mask = WharfNightStage.LitMask(noPool, pooled, out int lit);
            float before = WharfNightStage.MeanLuma(noPool, mask);
            float after = WharfNightStage.MeanLuma(pooled, mask);
            float contrastBefore = _stage.RelativeLocalContrast(noPool, mask);
            float contrastAfter = _stage.RelativeLocalContrast(pooled, mask);
            int px = _stage.Width * _stage.Height;

            DumpPoolState();
            Debug.Log($"[{PlateDir}] {_stage.Width}x{_stage.Height}  pools {LampPoolSystem.Instance?.ActivePoolCount} " +
                      $"|  lit {lit} px ({100f * lit / px:0.00} %)  |  mean luma there {before:0.0000} -> " +
                      $"{after:0.0000} ({after / Mathf.Max(before, 1e-6f):0.00}x)  |  relative local contrast " +
                      $"{contrastBefore:0.0000} -> {contrastAfter:0.0000} " +
                      $"({contrastAfter / Mathf.Max(contrastBefore, 1e-6f):0.000}x)");

            Assert.Greater(lit, px / 200,
                $"the pool lit only {lit} px of {px}. The whole point of PR 2c is that the ground under a " +
                "lamp stops being dark; a pool nobody can see is #733's frame with more code in it.");

            Assert.Greater(after, before * 1.15f,
                $"the planks came up only {after / Mathf.Max(before, 1e-6f):0.00}x. A lamp that cannot lift " +
                "the ground it stands over by a sixth is not lighting it.");

            // ⭐ THE CLAIM THE DESIGN RESTS ON, on real planks. A multiply scales both terms of a ratio, so
            // relative contrast must come through essentially untouched — which is exactly what the refused
            // disc could not do (it drove the same measure from 0.21 down to 0.01, #733's plate 01).
            Assert.AreEqual(contrastBefore, contrastAfter, contrastBefore * 0.25f,
                $"the pool moved relative local contrast from {contrastBefore:0.0000} to {contrastAfter:0.0000}. " +
                "A multiply cannot flatten what it lights — if this drifts, something is ADDING, and the disc " +
                "the owner refused is coming back through the other door.");
        }

        /// <summary>
        /// <b>A lamp's shadow and a lamp's pool are two halves of one picture — and the thing that had
        /// stopped being true is that the lamp could find the bollard at all.</b>
        ///
        /// <para>The shadows (#698) draw AFTER the pool and multiply back down, so the planks a bollard
        /// blocks come back darker than the lit planks around them. Two things have to hold for that, and
        /// this asserts both ON THE REAL PIER: the lamp must PAIR with a caster inside the ground it lights,
        /// and the shadow quads must sort above the pool quads.</para>
        ///
        /// <para><b>⚠️ The pairing half is the one that was broken, and it was broken by #733.</b> The shadow
        /// system pairs by a radius that used to be <c>light.Range</c> — right, while Range was the pool.
        /// When Range became the BLOOM a lantern post's fell to 0.14 m, and a bollard three and a half
        /// metres away stopped being found: measured here, ZERO pairs. That is fixed on the light's own
        /// <see cref="SceneLight.ReachMetres"/>, and this is the plate-side proof that it took.</para>
        ///
        /// <para><b>⚠️ What this does NOT assert, honestly: the PIXELS.</b> The obvious measurement — shoot
        /// the pool with the shadows off and on and require the difference to be darker — comes back with
        /// the frames identical, and the cause is not the ladder: the one pair this pier offers draws a
        /// silhouette this fixture has not managed to make land in frame. The ordering itself is pinned
        /// exactly, by depth constant, in <c>LampPoolTests.TheDepthPins_PutThePoolUnderTheBloom…</c>. The
        /// photographic version is owed and is named in the PR body rather than quietly dropped.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ALampFindsTheBollardsInTheGroundItLights_AndItsShadowSortsOverThePool()
        {
            WharfNightStage.RequireAGraphicsDevice();

            _stage = new WharfNightStage(SceneName, PlateDir);
            yield return _stage.Load();
            yield return _stage.SetNight(2f);

            IReadOnlyList<LampPosts.Site> sites = StPetersWharf.LampPostSites();
            GameObject lamps = PlaceLamps(sites);
            yield return _stage.FrameOn(sites[0].Position + new Vector2(0f, -1f));

            LampShadowProfile profile = PoolProfile();
            profile.PoolsEnabled = true;
            profile.Strength = 0.8f;
            yield return Settle();
            _stage.SavePlate("03-pier-0200-pool-and-shadows.png", _stage.Capture());

            var shadows = LampShadowSystem.Instance;
            var pools = LampPoolSystem.Instance;
            Assert.IsNotNull(shadows); Assert.IsNotNull(pools);

            Debug.Log($"[{PlateDir}] shadow system: lights={LampShadowSystem.LiveLightCount} " +
                      $"casters={LampShadowSystem.LiveCasterCount} pairs={shadows.ActiveShadowCount}  |  " +
                      $"pool system: pools={pools.ActivePoolCount}");

            Assert.Greater(shadows.ActiveShadowCount, 0,
                $"the lamp paired with nothing, out of {LampShadowSystem.LiveCasterCount} casters on this " +
                "pier. That is the #733 regression exactly: the shadow system is looking inside the lamp's " +
                "BLOOM instead of the ground it lights.");

            SceneLight paired = shadows.SlotLight(0);
            Assert.IsNotNull(paired);
            Assert.Greater(paired.ReachMetres, paired.Range,
                "and it paired by the REACH, which is the number that is bigger than the fitting");

            // The ladder, on the live quads rather than on the constants: both draw at the ceiling order,
            // and the tie is broken by depth — the shadows NEARER the camera, so they draw last and
            // multiply the light the pool laid.
            MeshRenderer poolQuad = pools.SlotRenderer(0);
            MeshRenderer shadowQuad = shadows.SlotRenderer(0);
            Assert.IsNotNull(poolQuad); Assert.IsNotNull(shadowQuad);
            Assert.AreEqual(poolQuad.sortingOrder, shadowQuad.sortingOrder,
                "both are compositing elements at the ceiling; the ladder is the depth pin, not the order");
            Assert.Less(shadowQuad.transform.position.z, poolQuad.transform.position.z,
                $"the shadow quad sits at z={shadowQuad.transform.position.z:0.000} and the pool at " +
                $"{poolQuad.transform.position.z:0.000}. The camera looks along +Z, so the shadow must be " +
                "NEARER to draw last — a shadow under the light it is supposed to cut is two pictures of a " +
                "lamp instead of one.");
        }

        /// <summary>
        /// <b>By day the pool does not exist.</b> The night gate is the shared additive machinery's, and no
        /// profile, preset or pool may reach around it — so a change this large must be exactly invisible at
        /// noon.
        ///
        /// <para>Against the scene's own noise floor, because the sea keeps moving however hard the game
        /// clock is frozen and by day it is bright enough to clear any threshold. The floor reads zero here
        /// only because <c>WharfNightStage</c> stops engine time as well; before that it read 639,757.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AtNoon_TheGroundIsUntouched()
        {
            WharfNightStage.RequireAGraphicsDevice();

            _stage = new WharfNightStage(SceneName, PlateDir);
            yield return _stage.Load();
            yield return _stage.SetNight(12f);

            IReadOnlyList<LampPosts.Site> sites = StPetersWharf.LampPostSites();
            GameObject lamps = PlaceLamps(sites);
            yield return _stage.FrameOn(sites[0].Position + new Vector2(0f, -1f));

            // The gulls fly on UNSCALED time, so the stage's stopped clock does not hold them between the
            // captures below, and a bird that moves between two of them is counted against the pools.
            // Stood down for this control alone; the teardown puts them back.
            StandDownGulls();

            LampShadowProfile profile = PoolProfile();

            profile.PoolsEnabled = false;
            yield return Settle();
            byte[] offA = _stage.Capture();
            yield return Settle();
            int noiseFloor = WharfNightStage.Footprint(offA, _stage.Capture());

            profile.PoolsEnabled = true;
            yield return Settle();
            byte[] on = _stage.Capture();
            _stage.SavePlate("04-pier-noon-control.png", on);

            int changed = WharfNightStage.Footprint(offA, on);
            Debug.Log($"[{PlateDir}] noon: pools on changed {changed} px against a {noiseFloor} px floor " +
                      $"({_stage.Width}x{_stage.Height}); active pools {LampPoolSystem.Instance?.ActivePoolCount}");

            Assert.LessOrEqual(changed, Mathf.Max(noiseFloor, 2),
                $"a lamp lit the ground at noon: {changed} px changed against a {noiseFloor} px floor. The " +
                "gate is the shared additive machinery's and reads the published tint — if this fails, the " +
                "pool is bypassing it.");
        }

        // =============================================================================================
        //  the charter's other two acceptance plates
        // =============================================================================================

        /// <summary>
        /// <b>The Nine Mile Creek mooring wall at night, with the fleet lying against it.</b> The charter's
        /// second acceptance location, and a different question from the pier: NMC's lamps are 4.48 m
        /// `streetLamp`s rather than 2.46 m lantern posts, so their pools should read BROADER and FLATTER —
        /// which is `h/√(h²+d²)` and nothing else, since both lamps carry the same preset and the same
        /// reach. If the two regions' pools looked alike, the shape would be decoration rather than
        /// geometry.
        ///
        /// <para>It also puts the pass over the one receiver the pier has none of: <b>a mesh hull</b>. The
        /// fleet moored along that wall is `IsoFacetHullRenderer` geometry, not sprites, and a screen-space
        /// multiply lights her exactly as it lights the quay — with the facet path untouched. That is worth
        /// a measurement rather than an assurance.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheCreekWallAndTheFleetAlongsideAreLit_AndTheTallerLampPoolsFlatter()
        {
            WharfNightStage.RequireAGraphicsDevice();

            _stage = new WharfNightStage(CreekSceneName, PlateDir);
            yield return _stage.Load();
            yield return _stage.SetNight(2f);

            ITidalTerrain terrain = GameServices.TidalTerrain;
            if (terrain == null)
            {
                Assert.Ignore("SKIPPED — Nine Mile Creek registered no ITidalTerrain, so the lamp sites " +
                              "cannot be asked whether they stand on dry ground and the builder would " +
                              "decline every one of them.");
                yield break;
            }

            IReadOnlyList<LampPosts.Site> sites = NineMileCreekDressing.Lamps(terrain);
            var host = _stage.Track(new GameObject("PlateLamps"));
            int placed = LampPosts.Place(host.transform, sites, terrain,
                                         NineMileCreekMainland.SpringHighWater, "[lights-illuminate]");
            Assert.Greater(placed, 0, "the builder placed no lamp at the creek at all");
            foreach (SceneLight l in host.GetComponentsInChildren<SceneLight>(true)) l.FlickerAmount = 0f;

            // Frame the wall between its two lamps — the stretch a crew steps off a boat onto.
            var wall = new Vector2(NineMileCreekDressing.AtStation(4), NineMileCreekDressing.LampRowY);
            yield return _stage.FrameOn(wall + new Vector2(0f, -1.5f));

            LampShadowProfile profile = PoolProfile();

            profile.PoolsEnabled = false;
            yield return Settle();
            byte[] noPool = _stage.Capture();
            _stage.SavePlate("05-creek-wall-0200-no-pool-BEFORE.png", noPool);

            profile.PoolsEnabled = true;
            yield return Settle();
            byte[] pooled = _stage.Capture();
            _stage.SavePlate("06-creek-wall-0200-pool-AFTER.png", pooled);

            bool[] mask = WharfNightStage.LitMask(noPool, pooled, out int lit);
            float before = WharfNightStage.MeanLuma(noPool, mask);
            float after = WharfNightStage.MeanLuma(pooled, mask);
            // In LINEAR light (changed from the gamma read-back's measure, same 25% and the same live
            // expectation). The round-lamp fix lights the creek's very dark south ground (about 8 of 255);
            // there the sRGB curve lifts the mean more than the steps, so the gamma ratio fell 0.271 -> 0.205
            // while the same detail in linear light ROSE 0.295 -> 0.311, with 0 clipped pixels. A multiply is
            // exactly a no-op in linear light, which is the promise this line asks of.
            float contrastBefore = _stage.RelativeLocalContrastLinear(noPool, mask);
            float contrastAfter = _stage.RelativeLocalContrastLinear(pooled, mask);
            float gammaBefore = _stage.RelativeLocalContrast(noPool, mask);
            float gammaAfter = _stage.RelativeLocalContrast(pooled, mask);
            int px = _stage.Width * _stage.Height;

            Debug.Log($"[{PlateDir}] creek wall gamma relative local contrast {gammaBefore:0.0000} -> {gammaAfter:0.0000}");
            Debug.Log($"[{PlateDir}] creek wall {_stage.Width}x{_stage.Height}  lamps placed {placed}, pools " +
                      $"{LampPoolSystem.Instance?.ActivePoolCount}  |  lit {lit} px ({100f * lit / px:0.00} %)" +
                      $"  |  mean luma {before:0.0000} -> {after:0.0000} " +
                      $"({after / Mathf.Max(before, 1e-6f):0.00}x)  |  relative local contrast " +
                      $"{contrastBefore:0.0000} -> {contrastAfter:0.0000} " +
                      $"({contrastAfter / Mathf.Max(contrastBefore, 1e-6f):0.000}x)");

            Assert.Greater(lit, px / 200, $"the wall's lamps lit only {lit} px of {px}");
            Assert.Greater(after, before * 1.15f, "the quay a crew steps onto must come up");
            Assert.AreEqual(contrastBefore, contrastAfter, contrastBefore * 0.25f,
                "and the wall must still read as a wall — the multiply's promise holds here or it holds " +
                "nowhere");

            // ⭐ THE SHAPE IS GEOMETRY, NOT DECORATION. Same preset, same reach, different head height:
            // a 4.48 m streetLamp must strike the ground more squarely at a given distance than the pier's
            // 2.46 m lantern post. Asked of the maths both the shader and the plate share.
            float creek = LightMath.GroundIncidence(4.48f, 3f);
            float pier = LightMath.GroundIncidence(2.46f, 3f);
            Assert.Greater(creek, pier,
                $"at three metres out the creek's taller lamp reads {creek:0.000} against the pier's " +
                $"{pier:0.000} — if these were equal the pool would be a decal with a soft edge");
        }

        /// <summary>
        /// <b>A boat behind the dock is not lit through it</b> (the owner, 2026-11-25: "the dory which is
        /// visually behind the dock from camera is showing now with the lamp on").
        ///
        /// <para>A lamp lights a mesh pixel by its own FACE. Done in the ceiling pool from the hull pass's
        /// texture, it lit her wherever she is in that texture, which is also where the dock is drawn over her,
        /// so her lit silhouette showed on the planks. Every pixel where no hull is SEEN (the frame with the
        /// hulls hidden equals the frame with them shown) must be the same with the face lighting on and off:
        /// the dock in front of her is ground. Where she IS seen, the faces must still change her, or the
        /// face lighting was simply switched off.</para>
        ///
        /// <para>Shot at 02:00 at St Peters on the scene's own lamps that have a hull within reach.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AHullBehindTheDock_IsNotLitThroughIt()
        {
            WharfNightStage.RequireAGraphicsDevice();

            _stage = new WharfNightStage(SceneName, PlateDir);
            yield return _stage.Load();
            yield return _stage.SetNight(2f);

            var hulls = Object.FindObjectsByType<IsoFacetHullRenderer>(FindObjectsSortMode.None);
            var lamps = new List<SceneLight>();
            foreach (SceneLight l in Object.FindObjectsByType<SceneLight>(FindObjectsSortMode.None))
            {
                if (!l.isActiveAndEnabled || l.ReachMetres <= 0f || l.LampHeightMeters <= 0f) continue;
                if (l.GetComponentInParent<IsoFacetHullRenderer>() != null) continue;   // a shore lamp
                foreach (IsoFacetHullRenderer h in hulls)
                    if (h.isActiveAndEnabled &&
                        ((Vector2)h.transform.position - l.WorldOrigin).magnitude < l.ReachMetres + 4f)
                    { lamps.Add(l); break; }
            }
            lamps.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            Assert.Greater(lamps.Count, 0, "no shore lamp at St Peters has a hull within reach");
            foreach (SceneLight l in lamps) l.FlickerAmount = 0f;

            // ⭐ HE STANDS WELL OUT OF EVERY MEASURED LAMP'S REACH, for the reference frames and the measured
            // ones alike. His drawn figure cannot be hidden from a frame, he is lit on his own faces (rightly),
            // and his own headlamp is a lamp too; moved away, the count is hulls only.
            GameObject him = GameObject.FindWithTag("Player");
            Rigidbody2D hisBody = him != null ? him.GetComponent<Rigidbody2D>() : null;
            bool hisBodyWas = hisBody != null && hisBody.simulated;
            if (him != null)
            {
                Vector2 away = PointAwayFromEvery(lamps, PlayerClearanceMetres);
                if (hisBody != null) { hisBody.simulated = false; hisBody.position = away; }
                him.transform.position = new Vector3(away.x, away.y, him.transform.position.z);
                for (int i = 0; i < 10; i++) yield return null;
                foreach (SceneLight l in lamps)
                    Assert.Greater(((Vector2)him.transform.position - l.WorldOrigin).magnitude, l.ReachMetres + PlayerClearanceMetres * 0.5f,
                        $"premise: he stands out of {l.name}'s reach");
            }

            yield return _stage.FrameOn(lamps[0].WorldOrigin);
            LampShadowProfile profile = PoolProfile();
            Assert.IsTrue(profile.PoolsLightMeshFaces, "the shipped default lights mesh faces");

            int through = 0, faces = 0, hidden = 0, framed = 0;
            for (int k = 0; k < lamps.Count && k < 3; k++)
            {
                if (k > 0) yield return _stage.ReFrameOn(lamps[k].WorldOrigin);

                // Off, on, off again, all in ONE frame: nothing in the world moves between them, and a pixel
                // the two OFF frames disagree on is the renderer's own render-to-render motion (the sea's
                // accumulated foam and ripple), which no lamp setting made. Only a pixel both OFF frames
                // agree on can say whether the faces lit it.
                profile.PoolsLightMeshFaces = false;
                yield return Settle();
                LampPoolSystem.Instance.PublishFrame(_stage.Camera);
                byte[] off = _stage.Capture();
                profile.PoolsLightMeshFaces = true;
                LampPoolSystem.Instance.PublishFrame(_stage.Camera);
                byte[] on = _stage.Capture();
                profile.PoolsLightMeshFaces = false;
                LampPoolSystem.Instance.PublishFrame(_stage.Camera);
                byte[] off2 = _stage.Capture();
                // "Seen" means drawn by her boat: every hull with all of her boat's own renderers (gunwale,
                // oars and lamps are sprites, not the hull mesh). Hide them; what still shows is ground, dock,
                // buildings and figures.
                var hidden0 = new List<(Behaviour b, bool was)>();
                var hiddenR = new List<(Renderer r, bool was)>();
                foreach (IsoFacetHullRenderer h in hulls)
                {
                    if (h == null) continue;
                    hidden0.Add((h, h.enabled)); h.enabled = false;
                    Transform root = BoatLamps.BoatRootOf(h.transform);
                    if (root == null) root = h.transform;
                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) { hiddenR.Add((r, r.forceRenderingOff)); r.forceRenderingOff = true; }
                }
                yield return Settle();
                byte[] none = _stage.Capture();
                for (int j = hiddenR.Count - 1; j >= 0; j--) if (hiddenR[j].r != null) hiddenR[j].r.forceRenderingOff = hiddenR[j].was;
                for (int j = hidden0.Count - 1; j >= 0; j--) if (hidden0[j].b != null) hidden0[j].b.enabled = hidden0[j].was;
                profile.PoolsLightMeshFaces = true;
                yield return Settle();

                // ⭐ FIGURES ARE SET ASIDE, NOT HIDDEN. He and the villagers are mesh figures, lit on their own
                // faces, which is right, and their drawn figure cannot be hidden from this frame (it is drawn
                // by a pass that no renderer switch reaches). So each figure's box (every renderer under its
                // root, plus a margin) is left out of the count, and the box is asked to hold no hull pixel,
                // so leaving it out can never hide a boat lit through the dock.
                bool[] figure = FigureBoxes(_stage, FigureMarginMetres);

                int t = 0, f = 0, n = 0, moving = 0, setAside = 0, hullInFigure = 0;
                for (int i = 0, p = 0; i + 3 < on.Length; i += 4, p++)
                {
                    if (Diff(off, off2, i) > 3) { moving++; continue; }
                    bool seen = Diff(off, none, i) > 3;
                    if (figure[p]) { setAside++; if (seen) hullInFigure++; continue; }
                    int d = Diff(on, off, i);
                    if (!seen) { n++; if (d > 3) t++; }
                    else if (d > 3) f++;
                }
                Debug.Log($"[{PlateDir}] faces seen: lamp {lamps[k].name} at {lamps[k].WorldOrigin}  |  pixels with " +
                          $"no hull seen {n}, of them changed by her faces {t}  |  hull pixels her faces changed {f}" +
                          $"  |  pixels that moved between the two OFF renders {moving}  |  figure pixels set aside " +
                          $"{setAside}, hull pixels among them {hullInFigure}");
                Assert.Less(moving, on.Length / 4 / 10,
                    "a tenth of the frame moved between two OFF renders of one frame: nothing here can be read");
                Assert.AreEqual(0, hullInFigure,
                    $"{hullInFigure} hull pixels fall inside a figure's set-aside box at {lamps[k].name}: the box " +
                    "would hide a boat from the count, so it cannot be set aside here");
                if (t > 0)
                {
                    _stage.SavePlate($"20-faces-through-{lamps[k].name}-ON.png", on);
                    var mask = (byte[])on.Clone();
                    int firstThrough = -1;
                    for (int i = 0; i + 3 < on.Length; i += 4)
                        if (!figure[i / 4] && Diff(off, off2, i) <= 3 && Diff(off, none, i) <= 3 && Diff(on, off, i) > 3)
                        { mask[i] = 255; mask[i + 1] = 0; mask[i + 2] = 0; if (firstThrough < 0) firstThrough = i / 4; }
                    // Name what stands at the first counted pixel, so a failure says WHAT was lit, not only how much.
                    Vector3 w = _stage.Camera.ScreenToWorldPoint(new Vector3(
                        firstThrough % _stage.Width + 0.5f, firstThrough / _stage.Width + 0.5f, 10f));
                    foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    {
                        Bounds b = r.bounds;
                        if (!r.enabled || w.x < b.min.x || w.x > b.max.x || w.y < b.min.y || w.y > b.max.y) continue;
                        Debug.Log($"[{PlateDir}] at the first lit pixel {(Vector2)w}: {r.GetType().Name} '{r.name}' " +
                                  $"under '{(r.transform.parent != null ? r.transform.parent.name : "-")}' " +
                                  $"root '{r.transform.root.name}' shader " +
                                  $"'{(r.sharedMaterial != null ? r.sharedMaterial.shader.name : "-")}'");
                    }
                    _stage.SavePlate($"22-faces-through-{lamps[k].name}-MASK.png", mask);
                    _stage.SavePlate($"23-faces-{lamps[k].name}-NOHULLS.png", none);
                }
                _stage.SavePlate($"21-faces-{lamps[k].name}-OFF.png", off);
                through += t; faces += f; hidden += n; framed++;
            }

            if (hisBody != null) hisBody.simulated = hisBodyWas;
            Assert.Greater(hidden, 0, "every pixel was a hull: nothing was asked");
            Assert.LessOrEqual(through, 20,
                $"{through} pixels where no hull is seen changed when her faces were lit: a lamp lit a boat " +
                "through the dock (or the shed, or the pile) standing in front of her");
            Assert.Greater(faces, 0, "where she is seen, lighting her faces changed nothing: the face path is off");
        }

        /// <summary>Metres added round a figure's renderers when its box is set aside (a keyline and a
        /// held rod reach a little past the body's bounds).</summary>
        const float FigureMarginMetres = 0.25f;
        /// <summary>How far past every measured lamp's reach he is moved for the dock check, metres.</summary>
        const float PlayerClearanceMetres = 30f;

        /// <summary>A point at least <paramref name="clearance"/> metres past every lamp's reach: the first of
        /// a fixed ring of candidates round the first lamp that clears them all (deterministic).</summary>
        static Vector2 PointAwayFromEvery(List<SceneLight> lamps, float clearance)
        {
            Vector2 c = lamps[0].WorldOrigin;
            for (float r = 40f; r <= 200f; r += 40f)
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI * 0.25f;
                    Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    bool clear = true;
                    foreach (SceneLight l in lamps)
                        if ((p - l.WorldOrigin).magnitude < l.ReachMetres + clearance) { clear = false; break; }
                    if (clear) return p;
                }
            return c + new Vector2(0f, 400f);
        }
        /// <summary>A standing figure's box when none of its renderers is live: width and height, metres.</summary>
        const float FigureWidthMetres = 0.8f, FigureHeightMetres = 2.0f;

        /// <summary>One flag per plate pixel (bottom-left origin): inside a figure's box. A figure is every
        /// <see cref="IsoCharacterFigureRenderer"/> and the player, and its box is the union of the bounds of
        /// every renderer under its root (the figure's own transform when the root is a big container),
        /// grown by <paramref name="margin"/> metres and projected through the plate's camera.</summary>
        static bool[] FigureBoxes(WharfNightStage stage, float margin)
        {
            var mask = new bool[stage.Width * stage.Height];
            var roots = new List<Transform>();
            foreach (IsoCharacterFigureRenderer fig in Object.FindObjectsByType<IsoCharacterFigureRenderer>(FindObjectsSortMode.None))
                roots.Add(fig.transform.root.GetComponentsInChildren<Renderer>(true).Length <= 64 ? fig.transform.root : fig.transform);
            GameObject him = GameObject.FindWithTag("Player");
            if (him != null) roots.Add(him.transform.root);
            foreach (Transform root in roots)
            {
                bool any = false; Bounds box = default;
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled) continue;
                    if (!any) { box = r.bounds; any = true; } else box.Encapsulate(r.bounds);
                }
                // No live renderer under the root (his body sprite is hidden while his mesh is drawn): a
                // standing figure's box at his feet, a figure's width and height.
                if (!any) box = new Bounds(root.position + new Vector3(0f, FigureHeightMetres * 0.5f, 0f),
                                           new Vector3(FigureWidthMetres, FigureHeightMetres, 0f));
                box.Expand(new Vector3(margin * 2f, margin * 2f, 0f));
                // Through the VIEWPORT, scaled to the plate: a screen point is in whatever the camera targets
                // at the moment of the call, which between captures is not the plate.
                Vector3 a = stage.Camera.WorldToViewportPoint(new Vector3(box.min.x, box.min.y, 0f));
                Vector3 b = stage.Camera.WorldToViewportPoint(new Vector3(box.max.x, box.max.y, 0f));
                int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x) * stage.Width), 0, stage.Width);
                int x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x) * stage.Width), 0, stage.Width);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y) * stage.Height), 0, stage.Height);
                int y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y) * stage.Height), 0, stage.Height);
                if (x1 > x0 && y1 > y0)
                    Debug.Log($"[{PlateDir}] figure box '{root.name}' at {(Vector2)box.center}: plate px x {x0}..{x1}, y {y0}..{y1}");
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++) mask[y * stage.Width + x] = true;
            }
            return mask;
        }

        static int Diff(byte[] a, byte[] b, int i) =>
            System.Math.Abs(a[i] - b[i]) + System.Math.Abs(a[i + 1] - b[i + 1]) + System.Math.Abs(a[i + 2] - b[i + 2]);

        /// <summary>
        /// <b>The searchlight lights the dock it sweeps</b> — the charter's third acceptance, and the half of
        /// the owner's sentence that #733 could only half answer.
        ///
        /// <para>He said the beam <i>"doesnt read on water or enviroement"</i>. #733 fixed the WATER by
        /// pulling the additive quad back to a source glow so the sea's own N·L relief could read through
        /// it (#691). The ENVIRONMENT is this: a cone lamp is a point lamp with an angular gate, so the same
        /// pool machinery puts a wedge of light on the planks the beam is pointed at — and the beam's reach
        /// is its FULL throw, not the shortened quad.</para>
        ///
        /// <para>Shot at 06:13, the hour he was playing, with the beam aimed at the pier from the water.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheSearchlightLightsTheDockItSweeps()
        {
            WharfNightStage.RequireAGraphicsDevice();

            _stage = new WharfNightStage(SceneName, PlateDir);
            yield return _stage.Load();
            yield return _stage.SetNight(2f);

            // Off the mooring face, aimed NORTH at the planks: transform.up is the beam axis, so a zero
            // rotation throws +Y, which is at the pier from seaward.
            Rect deck = StPetersWharf.DeckFootprint();
            var from = new Vector2(StPetersWharf.LadderPosition().x,
                                   deck.yMin - BoatSpotlight.DefaultRangeMetres * 0.55f);
            var boat = _stage.Track(new GameObject("PlateSearchlight"));
            boat.transform.position = new Vector3(from.x, from.y, 0f);
            boat.transform.rotation = Quaternion.identity;
            var spot = boat.AddComponent<BoatSpotlight>();
            spot.KeyTogglesBeam = false;
            spot.SetBeam(true);
            // She is stationary by construction here, and a searchlight dims when not making way.
            var dim = typeof(BoatSpotlight).GetField("_dimWhenStationary",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(dim);
            dim.SetValue(spot, false);

            yield return _stage.FrameOn(from + new Vector2(0f, BoatSpotlight.DefaultRangeMetres * 0.5f));

            LampShadowProfile profile = PoolProfile();

            profile.PoolsEnabled = false;
            yield return Settle();
            byte[] noPool = _stage.Capture();
            _stage.SavePlate("07-beam-on-the-dock-no-pool-BEFORE.png", noPool);

            profile.PoolsEnabled = true;
            yield return Settle();
            byte[] pooled = _stage.Capture();
            _stage.SavePlate("08-beam-on-the-dock-pool-AFTER.png", pooled);

            Assert.Greater(spot.Light.ReachMetres, spot.Light.Range,
                "the beam's POOL is its full throw while its bloom is the shortened quad — if these were " +
                "equal, #733's source-glow dial would be shortening the illumination too");

            bool[] mask = WharfNightStage.LitMask(noPool, pooled, out int lit);
            float before = WharfNightStage.MeanLuma(noPool, mask);
            float after = WharfNightStage.MeanLuma(pooled, mask);
            int px = _stage.Width * _stage.Height;

            Debug.Log($"[{PlateDir}] searchlight on the dock: reach {spot.Light.ReachMetres:0.0} m vs bloom " +
                      $"{spot.Light.Range:0.0} m  |  lit {lit} px ({100f * lit / px:0.00} %)  |  mean luma " +
                      $"{before:0.0000} -> {after:0.0000} ({after / Mathf.Max(before, 1e-6f):0.00}x)");

            Assert.Greater(lit, px / 500,
                $"the beam lit only {lit} px of the dock. A cone is a point lamp with an angular gate; if " +
                "this is empty the gate is closing on everything or the beam is not aimed at the planks.");
            Assert.Greater(after, before * 1.15f, "and what it sweeps must actually come up");
        }

        // =============================================================================================
        //  scaffolding
        // =============================================================================================

        /// <summary>The pier's lamps, placed by the BUILDER's own code path — so a lamp that only pools when
        /// hand-placed cannot pass.</summary>
        GameObject PlaceLamps(IReadOnlyList<LampPosts.Site> sites)
        {
            var host = _stage.Track(new GameObject("PlateLamps"));
            int placed = LampPosts.Place(host.transform, sites, null, 0f, "[lights-illuminate]");
            Assert.AreEqual(sites.Count, placed,
                "the builder declined to place a lamp, so this fixture is photographing a pier the game does " +
                "not have");
            foreach (SceneLight l in host.GetComponentsInChildren<SceneLight>(true)) l.FlickerAmount = 0f;
            return host;
        }

        /// <summary>
        /// A profile of this fixture's OWN, handed to the pool system — never the shipped Resources asset.
        /// A test that moved the owner's dials would leave them moved for every test after it and, worse,
        /// for whoever opened the project next.
        /// </summary>
        LampShadowProfile PoolProfile()
        {
            LampShadowProfile shipped = LampPoolSystem.Instance != null
                ? LampPoolSystem.Instance.Profile
                : LampShadowProfile.CreateDefault();

            var mine = LampShadowProfile.CreateDefault();
            mine.PoolStrength = shipped.PoolStrength;
            mine.PoolEdgeSoftness = shipped.PoolEdgeSoftness;
            mine.MaxPools = shipped.MaxPools;
            mine.Strength = shipped.Strength;
            _stage.Track(mine);

            Assert.IsNotNull(LampPoolSystem.Instance,
                "LampPoolSystem never installed — it self-installs BeforeSceneLoad, so this means the play " +
                "session did not start it and nothing below would be measuring a pool");
            LampPoolSystem.Instance.Profile = mine;
            if (LampShadowSystem.Instance != null) LampShadowSystem.Instance.Profile = mine;
            return mine;
        }

        /// <summary>What the pool system actually built — the diagnostic that separates "no lamp qualified"
        /// from "the quad drew nothing".</summary>
        void DumpPoolState()
        {
            var sys = LampPoolSystem.Instance;
            if (sys == null) { Debug.Log($"[{PlateDir}] NO LampPoolSystem"); return; }
            var sb = new System.Text.StringBuilder($"[{PlateDir}] poolSize={sys.PoolSize} active={sys.ActivePoolCount}");
            for (int i = 0; i < sys.PoolSize; i++)
            {
                MeshRenderer r = sys.SlotRenderer(i);
                SceneLight l = sys.SlotLight(i);
                sb.Append($" | [{i}] mat={(r == null ? "noRenderer" : (r.sharedMaterial == null ? "NULL" : r.sharedMaterial.shader?.name))}")
                  .Append($" on={(r != null && r.enabled)} order={(r == null ? 0 : r.sortingOrder)}");
                if (l != null)
                    sb.Append($" lamp={l.gameObject.name} reach={l.ReachMetres:0.00} h={l.LampHeightMeters:0.00} I={l.Intensity:0.00}");
                if (r != null) sb.Append($" pos={r.transform.position} scale={r.transform.localScale}");
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>Let both lamp systems re-select and re-pose against the frozen frame. Engine time is
        /// stopped, so their own throttles never fire — the fixture drives them, which is what
        /// <c>PublishFrame</c> is public for.</summary>
        IEnumerator Settle()
        {
            for (int i = 0; i < 2; i++)
            {
                LampPoolSystem.Instance?.PublishFrame(_stage.Camera);
                LampShadowSystem.Instance?.PublishFrame(_stage.Camera);
                yield return null;
            }
        }

        /// <summary>Stands the installed gull flock down for one control, as <c>MeshInteriorLampsPlayTests</c>
        /// does: the flock stops advancing and every bird and pooled shadow under it stops drawing, with each
        /// flag saved for <see cref="RestoreGulls"/>.</summary>
        void StandDownGulls()
        {
            // Match SeagullLandingPlayTests: the hidden DontDestroyOnLoad host is not a scene find.
            foreach (GullFlock flock in Resources.FindObjectsOfTypeAll<GullFlock>())
            {
                if (flock == null || flock.gameObject.name != "GullFlock" ||
                    (flock.gameObject.hideFlags & HideFlags.HideAndDontSave) != HideFlags.HideAndDontSave)
                    continue;
                _installedFlock = flock;
                break;
            }
            if (_installedFlock == null) return; // The gulls' fixture owns the installation assertion.

            _flockWasEnabled = _installedFlock.enabled;
            // Save all flags BEFORE any OnDisable can change a pooled renderer's flag.
            foreach (Renderer renderer in _installedFlock.GetComponentsInChildren<Renderer>(true))
                _gullRenderers.Add((renderer, renderer.enabled));
            foreach (SpriteShadow shadow in _installedFlock.GetComponentsInChildren<SpriteShadow>(true))
                _gullShadows.Add((shadow, shadow.enabled));

            _installedFlock.enabled = false;
            foreach (var saved in _gullShadows) saved.Shadow.enabled = false;
            foreach (var saved in _gullRenderers) saved.Renderer.enabled = false;
        }

        /// <summary>Puts back exactly what <see cref="StandDownGulls"/> saved; nothing when no case stood the
        /// flock down.</summary>
        void RestoreGulls()
        {
            // OnEnable can re-stamp a shadow renderer: restore its component first, then the exact
            // renderer flags saved before stand-down. Do not destroy or replace the installed host.
            foreach (var saved in _gullShadows)
                if (saved.Shadow != null) saved.Shadow.enabled = saved.Enabled;
            foreach (var saved in _gullRenderers)
                if (saved.Renderer != null) saved.Renderer.enabled = saved.Enabled;
            if (_installedFlock != null) _installedFlock.enabled = _flockWasEnabled;
            _gullShadows.Clear();
            _gullRenderers.Clear();
            _installedFlock = null;
        }
    }
}
