using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A MOORED BOAT DOES NOT SHOW SIDELIGHTS</b> (ADR 0016, boat-lights PR 2) — the rule of the
    /// road, through the real <c>IsoFacetHullPresentationService.Install</c> path, on the real
    /// committed defs.
    ///
    /// <para><b>Why this exists.</b> Until PR 2 the only lamp-bearing hull in the game was always under
    /// way, so "show every lamp the def declares" was accidentally correct. Giving the fleet its lamp
    /// tables turned that into a defect the size of a harbour: the seven boats made fast to the Nine
    /// Mile Creek wharf and every hull in the review anchorage would have burned sidelights, mastheads
    /// and searchlights all night — each one claiming, in the only language a navigation light has, to
    /// be under way.</para>
    ///
    /// <para><b>Why PlayMode.</b> The whole thing is lifecycle. <c>BoatLamps</c> builds its child lights
    /// in <c>OnEnable</c>, reads the regime off the boat root there, and <c>SceneLight</c> pools its
    /// quad in <c>OnDisable</c> — none of which runs in EditMode (no <c>[ExecuteAlways]</c>), so an
    /// EditMode test could only re-assert the array it just built. The pure half of the rule is pinned
    /// headless in <c>BoatLampRegimeTests</c>; this is the half that needs a scene.</para>
    /// </summary>
    public class BoatLampRegimePlayTests
    {
        const string CapeMeshPath = "Assets/_Project/Data/Boats/HullMeshes/CapeIslanderIsoHullMesh.asset";
        const string LobsterMeshPath = "Assets/_Project/Data/Boats/HullMeshes/LobsterBoatIsoHullMesh.asset";

        readonly List<Object> _spawned = new();

        /// <summary>A test's stand-in for a hull that is made fast — the one thing <c>MooredBoat</c>
        /// says about herself that the lamps care about. Spelled here rather than using
        /// <c>MooredBoat</c> so the fixture does not drag a builder, an owner Def and a skinner into a
        /// question about lamps; the seam is the interface, and this is a second implementor of it,
        /// which is itself worth having.</summary>
        sealed class LyingStill : MonoBehaviour, IVesselWay
        {
            public VesselWay Answer = VesselWay.Moored;
            public VesselWay Way => Answer;
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            foreach (Object o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        static HullMeshDef LoadCommitted(string path)
        {
#if UNITY_EDITOR
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(path);
            Assert.IsNotNull(def, $"{path} is missing");
            return def;
#else
            Assert.Ignore("Needs the AssetDatabase: these assert the REAL committed defs, not a mirror.");
            return null;
#endif
        }

        /// <summary>A hull built the way the game builds one: a boat ROOT with the mesh renderer
        /// installed on her visual CHILD, which is where the presentation service puts it and why
        /// anything that needs to speak about "this boat" has to climb first.</summary>
        (GameObject Root, BoatLamps Lamps, BoatSpotlight Beam) Build(HullMeshDef def, string name,
                                                                     VesselWay? way = null)
        {
            var root = new GameObject(name);
            _spawned.Add(root);
            if (way.HasValue) root.AddComponent<LyingStill>().Answer = way.Value;

            var host = new GameObject("FacetMesh");
            host.transform.SetParent(root.transform, false);

            IHullMeshRenderer installed = new IsoFacetHullPresentationService().Install(host, def);
            Assert.IsNotNull(installed, $"{name}: the install path refused the def");

            var lamps = host.GetComponent<BoatLamps>();
            Assert.IsNotNull(lamps, $"{name}: the def declares lamps, so Install must have mounted BoatLamps");
            return (root, lamps, root.GetComponent<BoatSpotlight>());
        }

        /// <summary>Which kinds are actually BURNING — the enabled lights, not the declared rows. The
        /// distinction is the whole feature: every hull declares her anchor light and her sidelights,
        /// and the regime decides which of them draw.</summary>
        static HashSet<HullLampKind> Burning(BoatLamps lamps)
        {
            var on = new HashSet<HullLampKind>();
            HullLamp[] rows = lamps.Lamps;
            SceneLight[] lights = lamps.Lights;
            Assert.IsNotNull(lights, "the lamps must have been built");
            for (int i = 0; i < rows.Length && i < lights.Length; i++)
                if (lights[i] != null && lights[i].enabled) on.Add(rows[i].Kind);

            // ⭐ AND HER CABIN, WHEREVER IT IS DRAWN. Since the owner's 2026-09-03 ruling a lit
            // wheelhouse is her WINDOWS (BoatWindowGlow), not a disc among these quads — so a walk of
            // the lamp table alone would report every cabin in the fleet as dark, and this file's four
            // assertions about a lit or unlit wheelhouse would have gone TWO red and TWO silently
            // true. The question these tests ask is "is her cabin lit", which is a fact about the boat
            // and not about which component draws it, so it is asked of both. A hull refused her
            // windows still burns the disc, and the loop above still finds her.
            var windows = lamps.GetComponent<BoatWindowGlow>();
            if (windows != null && windows.Lit) on.Add(HullLampKind.CabinGlow);
            return on;
        }

        // ---- the regime ------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AHullMadeFast_ShowsHerAnchorLightAndHerCabin_AndNothingThatNavigates()
        {
            var moored = Build(LoadCommitted(LobsterMeshPath), "MooredLobster", VesselWay.Moored);
            yield return null;

            Assert.AreEqual(VesselWay.Moored, moored.Lamps.Way,
                            "she read the way off her own root — the seam is the point");

            HashSet<HullLampKind> on = Burning(moored.Lamps);

            Assert.IsTrue(on.Contains(HullLampKind.AnchorLight),
                          "one all-round white, which is the whole of what a boat lying still may show");
            Assert.IsFalse(on.Contains(HullLampKind.CabinGlow),
                           "and her wheelhouse is DARK, because nobody has gone below her. The glow is " +
                           "not a navigation light — the regime has nothing to say about it — but seven " +
                           "identical lit wheelhouses along a wharf is a row of lanterns, not a harbour.");

            Assert.IsFalse(on.Contains(HullLampKind.PortSidelight),
                           "NO sidelights. A boat tied to a wall showing red and green is claiming to " +
                           "be under way — the one lie in this feature that could mislead somebody.");
            Assert.IsFalse(on.Contains(HullLampKind.StarboardSidelight));
            Assert.IsFalse(on.Contains(HullLampKind.SternLight));
            Assert.IsFalse(on.Contains(HullLampKind.Masthead));
        }

        [UnityTest]
        public IEnumerator AHullUnderWay_ShowsHerAspect_AndNoAnchorLight()
        {
            var running = Build(LoadCommitted(LobsterMeshPath), "RunningLobster", VesselWay.UnderWay);
            yield return null;

            HashSet<HullLampKind> on = Burning(running.Lamps);

            Assert.IsTrue(on.Contains(HullLampKind.PortSidelight), "red to port");
            Assert.IsTrue(on.Contains(HullLampKind.StarboardSidelight), "green to starboard");
            Assert.IsTrue(on.Contains(HullLampKind.SternLight), "white astern");
            Assert.IsTrue(on.Contains(HullLampKind.Masthead), "and the masthead that says under power");
            Assert.IsTrue(on.Contains(HullLampKind.CabinGlow));

            Assert.IsFalse(on.Contains(HullLampKind.AnchorLight),
                           "a boat making way is not at anchor, and showing both says both");
        }

        [UnityTest]
        public IEnumerator AHullThatAnswersNothing_IsUnderWay_WhichIsTheShippedBehaviour()
        {
            // ⚠️ The load-bearing default. The arrival's Cape Islander carries no IVesselWay of any
            // kind, and she is the hull the intro's whole light show runs on. If absence ever came to
            // mean "moored", the demo would go dark and nothing would say why.
            var cape = Build(LoadCommitted(CapeMeshPath), "ArrivingCape");
            yield return null;

            Assert.AreEqual(VesselWay.UnderWay, cape.Lamps.Way);
            HashSet<HullLampKind> on = Burning(cape.Lamps);
            Assert.IsTrue(on.Contains(HullLampKind.PortSidelight));
            Assert.IsTrue(on.Contains(HullLampKind.StarboardSidelight));
            Assert.IsTrue(on.Contains(HullLampKind.SternLight));
            Assert.IsTrue(on.Contains(HullLampKind.Masthead));
            Assert.IsTrue(on.Contains(HullLampKind.CabinGlow));
            Assert.IsFalse(on.Contains(HullLampKind.AnchorLight));
        }

        [UnityTest]
        public IEnumerator TheRegimeFlipsWhenSheIsLetGo_AndTheFlipIsBothWays()
        {
            var boat = Build(LoadCommitted(LobsterMeshPath), "CastingOff", VesselWay.Moored);
            yield return null;
            Assert.IsFalse(Burning(boat.Lamps).Contains(HullLampKind.PortSidelight), "precondition: made fast");

            boat.Root.GetComponent<LyingStill>().Answer = VesselWay.UnderWay;
            boat.Lamps.RefreshWay();
            yield return null;

            HashSet<HullLampKind> on = Burning(boat.Lamps);
            Assert.IsTrue(on.Contains(HullLampKind.PortSidelight), "let go, she shows her aspect");
            Assert.IsFalse(on.Contains(HullLampKind.AnchorLight), "and douses the anchor light");

            boat.Root.GetComponent<LyingStill>().Answer = VesselWay.Moored;
            boat.Lamps.RefreshWay();
            yield return null;

            on = Burning(boat.Lamps);
            Assert.IsFalse(on.Contains(HullLampKind.PortSidelight), "made fast again, she puts them out");
            Assert.IsTrue(on.Contains(HullLampKind.AnchorLight), "and hoists the anchor light back");
        }

        // ---- the searchlight -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AMooredBoatsSearchlightIsOut_NotMerelyDimmed()
        {
            // ⚠️ The way-gate (BoatSpotlight._dimWhenStationary) already fades a stationary beam toward
            // a FLOOR of 0.15, not to nothing — so a wharf of moored boats would have shown seven faint
            // cones burning all night. Dimming is a look; this is a rule, and it has to switch the lamp
            // off on both the surfaces a beam reaches.
            var moored = Build(LoadCommitted(LobsterMeshPath), "MooredWithABeam", VesselWay.Moored);
            yield return null;

            Assert.IsNotNull(moored.Beam, "the lobster boat declares a searchlight, so one was mounted");
            Assert.IsTrue(moored.Beam.MintedFromDef, "and it was minted from her def, not bolted on by a builder");
            Assert.IsFalse(moored.Beam.BeamOn, "a boat at her berth is not working her searchlight");
            Assert.IsFalse(moored.Beam.Light.enabled, "the LAND quad is off");
        }

        [UnityTest]
        public IEnumerator AnNpcUnderWayWorksHerSearchlight()
        {
            var running = Build(LoadCommitted(LobsterMeshPath), "RunningWithABeam", VesselWay.UnderWay);
            yield return null;

            Assert.IsNotNull(running.Beam);
            Assert.IsTrue(running.Beam.BeamOn,
                          "her skipper has the lamp going because that is his job — the same reason the " +
                          "cape's is burning as she comes into St Peters before dawn");
        }

        [UnityTest]
        public IEnumerator AWaySourceDESTROYEDUnderHer_ReadsAsGone_NotAsMoored()
        {
            // ⭐⭐ THE TRAP A LIVE PLATE CAUGHT, and it is the project's own banked one wearing a new
            // coat: an INTERFACE reference does not get Unity's fake-null operator. A destroyed
            // MonoBehaviour is still a perfectly good managed reference through IVesselWay and keeps
            // answering with whatever it last held — so a beam that cached one and asked
            // `_waySource != null` was talking to a component that no longer existed.
            //
            // Measured at Nine Mile Creek before this was fixed: twenty-five hulls let go, all
            // twenty-five sets of LAMPS flipped to under way (they re-resolve through GetComponent,
            // which does honour the operator) and all twenty-five SEARCHLIGHTS stayed out.
            var boat = Build(LoadCommitted(LobsterMeshPath), "LetGoByDestruction", VesselWay.Moored);
            yield return null;
            Assert.IsFalse(boat.Beam.BeamOn, "precondition: she is at her berth and her beam is out");

            Object.DestroyImmediate(boat.Root.GetComponent<LyingStill>());
            boat.Lamps.RefreshWay();
            yield return null;
            yield return null;

            Assert.AreEqual(VesselWay.UnderWay, boat.Lamps.Way,
                            "nothing answers for her any more, so she is under way — the default");
            Assert.IsTrue(boat.Beam.BeamOn,
                          "and her searchlight must reach the same conclusion. If this is false the " +
                          "beam is still asking a destroyed component and believing the answer.");
        }

        // ---- whose switch is it ----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheKeyReachesOnlyTheBoatThePlayerIsStandingOn()
        {
            var mine = Build(LoadCommitted(LobsterMeshPath), "PlayersLobster", VesselWay.UnderWay);
            var theirs = Build(LoadCommitted(LobsterMeshPath), "SkippersLobster", VesselWay.UnderWay);
            yield return null;

            Assert.IsFalse(mine.Beam.PlayerSwitchesThisBeam,
                           "with nothing declared, the slot is EMPTY and no beam answers — 'there is " +
                           "only one boat' is the guess that caused #642");
            Assert.IsFalse(theirs.Beam.PlayerSwitchesThisBeam);

            // The Player lane declares her boat by the controller token; Art can only name the ROOT, so
            // the slot is asked with the GameObject.
            GameServices.Helm.SetPlayersBoat(mine.Root);
            yield return null;

            Assert.IsTrue(mine.Beam.PlayerSwitchesThisBeam, "the boat she is standing on answers her key");
            Assert.IsFalse(theirs.Beam.PlayerSwitchesThisBeam,
                           "and the one two berths down does NOT — every live BoatSpotlight sees the " +
                           "same keyboard, so a beam that answered without asking would flip an NPC's " +
                           "searchlight every time the player reached for their own");

            GameServices.Helm.SetPlayersBoat(null);
            yield return null;
            Assert.IsFalse(mine.Beam.PlayerSwitchesThisBeam,
                           "she steps ashore and the key stops reaching the boat behind her");
        }

        [UnityTest]
        public IEnumerator AROWEDBoatIsStillHerBoat_AndHerBeamStillAnswers()
        {
            // ⭐⭐ THE TRAP THIS TEST EXISTS FOR. The obvious predicate is "is this the boat whose helm
            // the player holds" — and IsPlayerHelm carries HasHelm, which is the ENGINE question. The
            // boat the player owns at the opening is a ROWED DORY. Gating the switch on the helm would
            // have looked right, passed a fixture written against a powered hull, and silently killed
            // the L key on the starting boat. A searchlight is a boat's TACKLE, like her anchor: it
            // takes the wider declaration, at the wheel or on her deck.
            var dory = new GameObject("RowedDory");
            _spawned.Add(dory);
            var beam = dory.AddComponent<BoatSpotlight>();   // as PersistentCoreBuilder bolts one on
            yield return null;

            Assert.IsFalse(beam.MintedFromDef,
                           "the builder's beam is the PLAYER's, not the hull's — it must never be driven " +
                           "by the regime, or walking up the wharf would light her dory behind her");

            GameServices.Helm.SetPlayersBoat(dory);
            yield return null;

            Assert.IsTrue(beam.PlayerSwitchesThisBeam,
                          "no helm is registered and none is granted — she is rowing — and the switch " +
                          "still reaches her own boat");
            Assert.IsNull(GameServices.HelmControl,
                          "precondition: nothing holds the helm, so a helm predicate would have said no");
        }

        [UnityTest]
        public IEnumerator ThePlayersOwnBeamIsNeverRelitByTheRegime()
        {
            var dory = new GameObject("PlayersDory");
            _spawned.Add(dory);
            var beam = dory.AddComponent<BoatSpotlight>();
            GameServices.Helm.SetPlayersBoat(dory);
            yield return null;

            beam.SetBeam(true);
            yield return null;
            Assert.IsTrue(beam.BeamOn, "precondition: she switched it on");

            // She steps ashore. Her boat answers no IVesselWay, so the regime would call her UNDER WAY —
            // and if it drove a builder-bolted beam it would now hold her searchlight on for good, with
            // her switch no longer reaching it.
            GameServices.Helm.SetPlayersBoat(null);
            yield return null;
            yield return null;
            Assert.IsTrue(beam.BeamOn, "it stays exactly as she left it");

            beam.SetBeam(false);
            yield return null;
            yield return null;
            Assert.IsFalse(beam.BeamOn, "and off stays off — the regime never touches her lamp");
        }
        // ---- the CONTROL: her anchor light changes nothing she draws under way -------------------------

        static readonly int IdDayNightTint = Shader.PropertyToID("_DayNightTint");

        /// <summary>A night frame: luma ~ 0.12, so the additive-light shader's gate reads the cycle as
        /// ACTIVE and darkness sits above its full-on band. The same tint the interiors fixture uses,
        /// for the same reason — this is a PARITY claim, where both arms share it.</summary>
        static readonly Color NightTint = new Color(0.10f, 0.12f, 0.20f, 1f);

        static readonly float[] ParityHeadings = { 90f, 135f, 180f, 45f };

        [UnityTest]
        public IEnumerator TheCapeUnderWay_DrawsExactlyWhatSheDrewBeforeHerAnchorLightWasAdded()
        {
            // ⭐⭐ PR 2a's acceptance criterion (4), and the reason her anchor light is the LAST row on
            // her def. BoatLamps makes one child light per lamp IN ARRAY ORDER, and SceneLight's
            // deterministic flicker is seeded from the child's SIBLING INDEX — the trap that cost #702
            // five false reds. Appended last, every earlier lamp keeps the index it had; her cabin glow
            // keeps its seed; and the anchor light itself is disabled while she is under way. This
            // measures that claim instead of asserting it.
            //
            // The fixture shape is the one banked from #697/#702, and every clause of it is load-bearing:
            // time FROZEN (frame-time terms in the lit path move pixels between two captures), ONE host
            // re-installed in place (two hulls alive contaminate each other through scene-wide shader
            // globals), the searchlight OFF (its way-gate smoothing steps by a floored delta-time even
            // at timeScale 0), and the flicker FROZEN **and re-ticked** (a light pushes its flickered
            // intensity in OnEnable and its first Update, before any freeze, and with time frozen it
            // never ticks again — so a freeze without a re-tick is a no-op).
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device. This picture needs the local GPU.");

            HullMeshDef def = LoadCommitted(CapeMeshPath);
            Assert.AreEqual(HullLampKind.AnchorLight, def.Lamps[def.Lamps.Length - 1].Kind,
                            "precondition: the anchor light is her last row");

            // The control: the same def with the anchor row removed — the cape exactly as she shipped
            // in #686. Instantiated, so nothing on disk is touched.
            HullMeshDef before = Object.Instantiate(def);
            before.name = "CapeIslanderIsoHullMesh (as #686 shipped her)";
            _spawned.Add(before);
            var six = new List<HullLamp>();
            foreach (HullLamp l in def.Lamps) if (l.Kind != HullLampKind.AnchorLight) six.Add(l);
            Assert.AreEqual(def.Lamps.Length - 1, six.Count, "exactly one row removed");
            before.Lamps = six.ToArray();

            var listener = new GameObject("Listener");
            listener.AddComponent<AudioListener>();
            _spawned.Add(listener);

            Color tintBefore = Shader.GetGlobalColor(IdDayNightTint);
            float timeScaleBefore = Time.timeScale;
            Shader.SetGlobalColor(IdDayNightTint, NightTint);
            Time.timeScale = 0f;
            // Whatever a prior fixture left in the shared light globals would reach both shots, but a
            // beam that keeps publishing would reach only one of them.
            foreach (string gv in new[] { "_BoatLightPos", "_BoatLightDir", "_BoatLightParams", "_BoatLightParams2" })
                Shader.SetGlobalVector(gv, Vector4.zero);
            Shader.SetGlobalColor("_BoatLightColor", Color.black);

            RenderTexture rt = null;
            Camera cam = null;
            try
            {
                rt = new RenderTexture(def.CellW, def.CellH, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
                var camGo = new GameObject("ParityCam");
                _spawned.Add(camGo);
                cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = def.CellH / (2f * def.PxPerMetre);
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 400f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.clear;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.targetTexture = rt;

                var arm = Build(def, "CapeUnderWay");           // no IVesselWay: she is under way
                Assert.IsNotNull(arm.Beam, "she declares a searchlight");
                arm.Beam.SetBeam(false);                        // measured on its own elsewhere; see above

                var report = new System.Text.StringBuilder();
                report.AppendLine("THE CAPE'S ANCHOR LIGHT COSTS HER NOTHING UNDER WAY — full def vs the six rows #686 shipped");
                report.AppendLine("heading | inked | differing px | noise floor (same arm, next frame)");
                int worst = 0, worstNoise = 0;

                foreach (float heading in ParityHeadings)
                {
                    yield return Pose(arm, def, heading, cam);
                    byte[] withAnchor = Capture(cam, rt, def);
                    yield return Pose(arm, def, heading, cam);
                    byte[] noiseA = Capture(cam, rt, def);

                    yield return Pose(arm, before, heading, cam);
                    byte[] sixRows = Capture(cam, rt, def);

                    int differing = CountDiffering(withAnchor, sixRows);
                    int noise = CountDiffering(withAnchor, noiseA);
                    worst = Mathf.Max(worst, differing);
                    worstNoise = Mathf.Max(worstNoise, noise);

                    report.AppendLine($"{heading,7:F0} | {CountInked(withAnchor),5} | {differing,12} | {noise}");

                    // The noise floor is what separates "the same picture" from "a picture that is
                    // never the same twice" — a parity of 0 means nothing if two shots of the SAME arm
                    // also differ.
                    Assert.AreEqual(0, noise,
                        $"at heading {heading} two captures of the SAME arm differ by {noise} px — the " +
                        "fixture is not still, and no parity claim below it is worth anything");
                    Assert.AreEqual(0, differing,
                        $"at heading {heading} the cape draws {differing} px differently with her anchor " +
                        "light declared. She is the CONTROL for this PR: appended last, disabled under " +
                        "way, it must cost her nothing. A non-zero here most likely means a row moved " +
                        "in front of her cabin glow and re-seeded its flicker (the #702 trap).");

                    Assert.Greater(CountInked(withAnchor), 1000, "she must actually be in frame");
                }

                Debug.Log($"[boat-lamps-parity] {report}\nworst differing {worst} px, worst noise floor {worstNoise} px.");
            }
            finally
            {
                Shader.SetGlobalColor(IdDayNightTint, tintBefore);
                Time.timeScale = timeScaleBefore;
                if (cam != null) cam.targetTexture = null;
                if (rt != null) { rt.Release(); Object.Destroy(rt); }
            }
        }

        /// <summary>Re-install the def on the ONE host, pose her, and let real time pass so the hull's
        /// LateUpdate, the lamps' rebuild and SceneLight's property push have all landed — then freeze
        /// the flicker AND re-tick it.</summary>
        IEnumerator Pose(( GameObject Root, BoatLamps Lamps, BoatSpotlight Beam) arm, HullMeshDef d,
                         float heading, Camera cam)
        {
            var hull = arm.Lamps.GetComponent<IsoFacetHullRenderer>();
            IHullMeshRenderer installed = new IsoFacetHullPresentationService().Install(hull.gameObject, d);
            Assert.IsNotNull(installed, $"re-install of {d.name} refused");
            Assert.AreSame(arm.Lamps, hull.gameObject.GetComponent<BoatLamps>(),
                           "the re-install must keep the same BoatLamps component");
            arm.Beam.SetBeam(false);

            hull.HeadingDirUnits = HullMeshMath.HeadingToDirUnits(heading, 0f, d.AzimuthCounterClockwise);
            yield return null;

            foreach (SceneLight l in arm.Lamps.GetComponentsInChildren<SceneLight>(true)) l.FlickerAmount = 0f;
            if (arm.Lamps.LampsOn) { arm.Lamps.LampsOn = false; arm.Lamps.LampsOn = true; }
            yield return null;
            yield return new WaitForSecondsRealtime(0.12f);
            yield return null;
            yield return null;
        }

        /// <summary>Frame the cell over the origin and render NOW — no frame passes.</summary>
        static byte[] Capture(Camera cam, RenderTexture rt, HullMeshDef def)
        {
            float ppu = def.PxPerMetre;
            float ox = (def.PivotPx.x - def.CellW / 2f) / ppu;
            float oy = (def.CellH / 2f - def.PivotPx.y) / ppu;
            cam.transform.position = new Vector3(-ox, -oy, -100f);
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(def.CellW, def.CellH, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, def.CellW, def.CellH), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            byte[] bytes = tex.GetRawTextureData();
            var copy = new byte[bytes.Length];
            System.Array.Copy(bytes, copy, bytes.Length);
            Object.Destroy(tex);
            return copy;
        }

        static int CountInked(byte[] rgba)
        {
            int n = 0;
            for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] > 8) n++;
            return n;
        }

        /// <summary>Pixels differing AT ALL — no tolerance. A tolerance sitting on the number it
        /// tolerates is a boundary decided by noise, which is how #697 shipped a sweep-only red into
        /// every lane.</summary>
        static int CountDiffering(byte[] a, byte[] b)
        {
            Assert.AreEqual(a.Length, b.Length, "two shots of one cell");
            int n = 0;
            for (int i = 0; i + 3 < a.Length; i += 4)
                if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3]) n++;
            return n;
        }
    }
}
