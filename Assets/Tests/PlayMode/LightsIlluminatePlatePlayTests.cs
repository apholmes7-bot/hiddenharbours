using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
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
        const string PlateDir = "lights-illuminate";

        private WharfNightStage _stage;

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            if (_stage != null) yield return _stage.TearDown();
            _stage = null;
            // Put the profile back: it is a Resources asset shared by every test that follows.
            if (LampPoolSystem.Instance != null) LampPoolSystem.Instance.Profile = null;
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
            Assert.Greater(shadowQuad.transform.position.z, poolQuad.transform.position.z,
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
    }
}
