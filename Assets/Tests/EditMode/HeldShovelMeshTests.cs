using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The clam shovel is its facet mesh in both of his hands</b> (ADR 0053): the same driver as the rod
    /// (<see cref="HeldRodMesh"/>), read off the shovel's own Def. While the mesh body digs, the grip is on
    /// the right hand's tool socket on every frame and the clip's tool track aims it, so the shaft runs
    /// through the left hand and the blade bottoms out where the clip's tool does, on the ground. On a
    /// free gait he carries it at the Def's carried aim, blade clear of the ground.
    ///
    /// <para>Measured on the SHIPPED fisher skin, through the ashore frame chain at all eight headings, so
    /// a rig re-bake that moves the hands or the blade fails here rather than in a picture.</para>
    /// </summary>
    public sealed class HeldShovelMeshTests
    {
        private const string ShovelPath = "Assets/_Project/Data/Tools/Shovel.asset";
        private const string ToolsFolder = "Assets/_Project/Data/Tools";
        private const string FisherPath = "Assets/_Project/Data/Characters/Skin/fisher.asset";
        private const string CarryHandsPath = "Assets/_Project/Code/Player/CarryHands.cs";
        private const float LineWidthPx = GameConfig.DefaultHeldPropLineWidthPx;

        private static readonly float[] Headings = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };
        private static readonly string[] CarriedClips = { "idle", "walk", "run" };

        private static readonly MethodInfo s_figureOnDestroy =
            typeof(IsoCharacterFigureRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo s_propOnDestroy =
            typeof(IsoFacetHeldPropRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<Object> _made = new List<Object>();
        private readonly List<IsoCharacterFigureRenderer> _figures = new List<IsoCharacterFigureRenderer>();
        private readonly List<HeldRodMesh> _drivers = new List<HeldRodMesh>();
        private GameConfig _previousConfig;
        private GameConfig _config;
        private GameObject _home;

        [SetUp]
        public void SetUp()
        {
            _previousConfig = GameServices.Config;
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _made.Add(_config);
            GameServices.Config = _config;
            _home = new GameObject("HeldShovelMeshTestsHome");
            _made.Add(_home);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (HeldRodMesh d in _drivers)
            {
                if (d.Renderer != null && s_propOnDestroy != null) s_propOnDestroy.Invoke(d.Renderer, null);
                d.Dispose();
            }
            _drivers.Clear();
            foreach (IsoCharacterFigureRenderer f in _figures)
            {
                if (f == null) continue;
                if (s_figureOnDestroy != null) s_figureOnDestroy.Invoke(f, null);
                Object.DestroyImmediate(f.gameObject);
            }
            _figures.Clear();
            GameServices.Config = _previousConfig;
            foreach (Object o in _made)
                if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // ================================================================ the shipped data

        [Test]
        public void TheShippedShovel_AsksForItsMesh_OnTheRightHandsToolSocket()
        {
            ToolDef shovel = Load<ToolDef>(ShovelPath);
            Assert.AreEqual(ToolDef.ShovelId, shovel.Id);
            Assert.IsTrue(shovel.DrawHeldMeshOnFigure, "the shovel must ask to be drawn as its mesh on the figure");
            Assert.IsTrue(shovel.DrawCarriedMeshOnFigure, "the shovel must ask for its carried mesh on a free gait");
            Assert.IsFalse(shovel.StowOnBack, "the shovel has no back mount: it stays in his hand");
            Assert.AreEqual(HeldPropSockets.RightHandTool, shovel.HeldMeshMountBone);
            HeldPropMeshDef mesh = shovel.HeldMeshFor(shovel.HeldMeshVariant);
            Assert.IsNotNull(mesh, $"the shovel names variant '{shovel.HeldMeshVariant}' and holds no such mesh");
            Assert.AreEqual("heldprop.shovel", mesh.Id);
            Assert.AreEqual(HeldPropMeshDef.ShovelFamily, mesh.Family);
            Assert.IsTrue(mesh.IsUsable(), "the shovel mesh is not usable: nothing to draw");
            Assert.IsTrue(HeldPropPose.TryParseSourcePose(mesh.SourcePose, out _, out _),
                          "the shovel's baked aim cannot be read off its provenance line");
            Assert.IsTrue(HeldPropPose.TryTipLocal(mesh, out _), "the shovel plots no shaft");
            CharacterSkinDef fisher = Load<CharacterSkinDef>(FisherPath);
            Assert.AreNotEqual(CharacterSkinDef.NoBone, fisher.IndexOfBone(HeldPropSockets.RightHandTool));
            Assert.AreNotEqual(CharacterSkinDef.NoBone, fisher.IndexOfBone(HeldPropSockets.LeftHandTool));
        }

        [Test]
        public void EveryClipThatAimsTheShovel_AimsItOnEveryFrame()
        {
            CharacterSkinDef fisher = Load<CharacterSkinDef>(FisherPath);
            List<string> clips = ClipsAiming(fisher, HeldPropMeshDef.ShovelFamily);
            CollectionAssert.Contains(clips, "dig", "the fisher's dig clip no longer aims the shovel");
            foreach (string state in clips)
            {
                Assert.IsTrue(fisher.TryGetClip(state, out CharacterSkinDef.SkinClip clip));
                Assert.AreEqual(clip.FrameCount, clip.Tool.Length, $"'{state}': one tool key per frame");
                for (int f = 0; f < clip.FrameCount; f++)
                    Assert.IsTrue(clip.Tool[f].Held && clip.Tool[f].Kind == HeldPropMeshDef.ShovelFamily,
                                  $"'{state}' frame {f} does not hold the shovel");
            }
        }

        // ================================================================ the dig, every frame, every heading

        [Test]
        public void TheDig_EveryFrameEveryHeading_TheGripIsOnTheRightHandsSocket_AimedByTheClip()
        {
            ToolDef shovel = Load<ToolDef>(ShovelPath);
            IsoCharacterFigureRenderer figure = Fisher();
            HeldRodMesh driver = Driver();
            HeldPropMeshDef mesh = shovel.HeldMeshFor(shovel.HeldMeshVariant);
            Assert.IsTrue(HeldPropPose.TryParseSourcePose(mesh.SourcePose, out float bp, out float by));
            int socket = figure.SkinDef.IndexOfBone(shovel.HeldMeshMountBone);
            float tolerance = _config.HeldRodGripToleranceMetres;
            var failures = new List<string>();

            foreach (string state in ClipsAiming(figure.SkinDef, HeldPropMeshDef.ShovelFamily))
            {
                Assert.IsTrue(figure.SkinDef.TryGetClip(state, out CharacterSkinDef.SkinClip clip));
                for (int f = 0; f < clip.FrameCount; f++)
                {
                    Assert.IsTrue(figure.SetPose(state, f), $"harness: the fisher did not pose '{state}' {f}");
                    Assert.IsTrue(driver.TryDraw(true, shovel, figure, LineWidthPx),
                                  $"'{state}' frame {f}: no mesh shovel ({driver.LastRefusal})");
                    Assert.IsFalse(driver.Carried, $"'{state}' aims the shovel: it is not the carried placement");
                    IsoFacetHeldPropRenderer r = driver.Renderer;
                    Assert.AreSame(figure.FacetTransform, r.transform.parent, "the shovel must ride the figure's facet child");
                    Assert.IsTrue(figure.TryBonePoint(socket, out Vector3 grip));

                    CharacterSkinDef.ToolKey key = clip.Tool[figure.DrawnFrame];
                    Vector3 aim = r.transform.localRotation * HeldPropPose.ToolDirection(bp, by);
                    if ((aim - HeldPropPose.ToolDirection(key.Pitch, key.Yaw)).magnitude > 1e-3f)
                        failures.Add($"'{state}' {f}: not aimed where the clip aims it");

                    foreach (float heading in Headings)
                    {
                        Matrix4x4 m = FacetFrame(figure.SkinDef, heading).localToWorldMatrix;
                        float off = (m.MultiplyPoint3x4(r.transform.localPosition) - m.MultiplyPoint3x4(grip)).magnitude;
                        if (off > tolerance) failures.Add($"'{state}' {f} heading {heading}: grip {off:0.000} m off tool_R");
                    }
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void TheDig_EveryFrame_TheShaftRunsThroughTheLeftHand()
        {
            ToolDef shovel = Load<ToolDef>(ShovelPath);
            IsoCharacterFigureRenderer figure = Fisher();
            HeldRodMesh driver = Driver();
            HeldPropMeshDef mesh = shovel.HeldMeshFor(shovel.HeldMeshVariant);
            // The whole plotted shaft, D-grip end to collar: the shovel's grip is the right hand partway
            // down it, and the rig's left hand is the top hand, up the shaft toward the D-grip.
            HeldPropMeshDef.Line shaft = System.Array.Find(mesh.Lines, l => l.Name == "shaft");
            Assert.IsNotNull(shaft.Points, "the shovel plots no 'shaft' line");
            Assert.GreaterOrEqual(shaft.Points.Length, 2);
            Vector3 shaftStart = shaft.Points[0].Position, shaftEnd = shaft.Points[shaft.Points.Length - 1].Position;
            int left = figure.SkinDef.IndexOfBone(HeldPropSockets.LeftHandTool);
            float tolerance = _config.HeldShovelShaftHandToleranceMetres;
            var failures = new List<string>();
            float worst = 0f;

            Assert.IsTrue(figure.SkinDef.TryGetClip("dig", out CharacterSkinDef.SkinClip clip));
            for (int f = 0; f < clip.FrameCount; f++)
            {
                Assert.IsTrue(figure.SetPose("dig", f));
                Assert.IsTrue(driver.TryDraw(true, shovel, figure, LineWidthPx), driver.LastRefusal.ToString());
                Transform t = driver.Renderer.transform;
                Vector3 a = t.localPosition + t.localRotation * shaftStart;
                Vector3 b = t.localPosition + t.localRotation * shaftEnd;
                Assert.IsTrue(figure.TryBonePoint(left, out Vector3 hand));
                float d = DistanceToSegment(hand, a, b);
                worst = Mathf.Max(worst, d);
                if (d > tolerance) failures.Add($"dig {f}: the left hand is {d:0.000} m off the shaft");
            }
            TestContext.WriteLine($"dig: worst left-hand distance from the shaft {worst:0.000} m");
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void TheDig_TheBladeIsLowestAtTheClipsLowPoint_AndNeverInTheGround()
        {
            (float lowest, int lowFrame, int keyLowFrame) = DigLowPoint();
            TestContext.WriteLine($"dig: the blade's lowest point is {lowest:0.000} m at frame {lowFrame} (the key's low frame {keyLowFrame})");
            Assert.AreEqual(keyLowFrame, lowFrame, "the mesh blade must bottom out where the clip's tool does");
            Assert.GreaterOrEqual(lowest, -_config.HeldShovelGroundToleranceMetres, "the blade goes into the ground");
        }

        /// <summary>
        /// The dig's stab drives the blade to the ground: at the clip's low point the lowest vertex of the
        /// baked shovel sits on the ground, within <see cref="GameConfig.HeldShovelGroundToleranceMetres"/>.
        /// The rig's dig keys (characterIsoRig10.poses.js, DIG) put the right hand low with the back and
        /// knees bent, so the blade reaches the ground with both hands on the shaft.
        /// </summary>
        [Test]
        public void TheDig_TheBladeMeetsTheGround_AtTheDigsLowPoint()
        {
            (float lowest, int lowFrame, _) = DigLowPoint();
            Assert.AreEqual(0f, lowest, _config.HeldShovelGroundToleranceMetres,
                            $"at the dig's low point (frame {lowFrame}) the blade is {lowest:0.000} m off the ground");
        }

        private (float lowest, int lowFrame, int keyLowFrame) DigLowPoint()
        {
            ToolDef shovel = Load<ToolDef>(ShovelPath);
            IsoCharacterFigureRenderer figure = Fisher();
            HeldRodMesh driver = Driver();
            int socket = figure.SkinDef.IndexOfBone(shovel.HeldMeshMountBone);
            Assert.IsTrue(figure.SkinDef.TryGetClip("dig", out CharacterSkinDef.SkinClip clip));
            float lowest = float.MaxValue, keyLowest = float.MaxValue;
            int lowFrame = -1, keyLowFrame = -1;
            for (int f = 0; f < clip.FrameCount; f++)
            {
                Assert.IsTrue(figure.SetPose("dig", f));
                Assert.IsTrue(driver.TryDraw(true, shovel, figure, LineWidthPx), driver.LastRefusal.ToString());
                float z = LowestZ(driver.Renderer);
                if (z < lowest) { lowest = z; lowFrame = f; }
                CharacterSkinDef.ToolKey k = clip.Tool[figure.DrawnFrame];
                Assert.IsTrue(figure.TryBonePoint(socket, out Vector3 grip));
                float keyTip = (grip + HeldPropPose.ToolDirection(k.Pitch, k.Yaw) * k.Length).z;
                if (keyTip < keyLowest) { keyLowest = keyTip; keyLowFrame = f; }
            }
            return (lowest, lowFrame, keyLowFrame);
        }

        // ================================================================ carried on a free gait

        [Test]
        public void OnAFreeGait_TheShovelIsCarriedAtTheDefsAim_GripOnTheSocket_BladeClearOfTheGround()
        {
            ToolDef shovel = Load<ToolDef>(ShovelPath);
            IsoCharacterFigureRenderer figure = Fisher();
            HeldRodMesh driver = Driver();
            HeldPropMeshDef mesh = shovel.HeldMeshFor(shovel.HeldMeshVariant);
            Assert.IsTrue(HeldPropPose.TryParseSourcePose(mesh.SourcePose, out float bp, out float by));
            float tolerance = _config.HeldRodGripToleranceMetres;
            float ground = _config.HeldShovelGroundToleranceMetres;
            var failures = new List<string>();

            foreach (string state in CarriedClips)
            {
                Assert.IsTrue(figure.SkinDef.TryGetClip(state, out CharacterSkinDef.SkinClip clip), $"harness: no '{state}'");
                foreach ((string socketId, string label) in new[] { (HeldPropSockets.RightHandTool, "right"),
                                                                    (HeldPropSockets.LeftHandTool, "left") })
                {
                    int socket = figure.SkinDef.IndexOfBone(socketId);
                    for (int f = 0; f < clip.FrameCount; f++)
                    {
                        Assert.IsTrue(figure.SetPose(state, f));
                        Assert.IsTrue(driver.TryDraw(true, shovel, figure, LineWidthPx, HeldRodMesh.Use.Free, 0f, socketId),
                                      $"'{state}' {f} {label}: {driver.LastRefusal}");
                        Assert.IsTrue(driver.Carried, $"'{state}' aims no tool, so the shovel is carried");
                        IsoFacetHeldPropRenderer r = driver.Renderer;
                        Assert.IsTrue(figure.TryBonePoint(socket, out Vector3 grip));
                        if ((r.transform.localPosition - grip).magnitude > tolerance)
                            failures.Add($"'{state}' {f} {label}: the carried grip is off the socket");
                        Vector3 aim = r.transform.localRotation * HeldPropPose.ToolDirection(bp, by);
                        if ((aim - HeldPropPose.ToolDirection(shovel.CarriedPitchDeg, shovel.CarriedYawDeg)).magnitude > 1e-3f)
                            failures.Add($"'{state}' {f} {label}: not at the Def's carried aim");
                        float low = LowestZ(r);
                        if (low < -ground) failures.Add($"'{state}' {f} {label}: the blade is {-low:0.000} m into the ground");
                    }
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        // ================================================================ coverage

        [Test]
        public void EveryHandToolWithAMesh_ResolvesToAHeldMesh_OnTheMeshFigure()
        {
            IsoCharacterFigureRenderer figure = Fisher();
            string[] guids = AssetDatabase.FindAssets("t:ToolDef", new[] { ToolsFolder });
            Assert.Greater(guids.Length, 0, "harness: no ToolDef assets");
            int covered = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ToolDef tool = Load<ToolDef>(path);
                if (tool.HeldMeshes == null || tool.HeldMeshes.Length == 0) continue;
                covered++;
                Assert.IsTrue(tool.DrawHeldMeshOnFigure, $"{tool.Id} has a held mesh but keeps its sprite on the mesh figure");
                Assert.IsTrue(tool.DrawCarriedMeshOnFigure, $"{tool.Id} has a held mesh but keeps its sprite when carried");
                HeldPropMeshDef mesh = tool.HeldMeshFor(tool.HeldMeshVariant);
                Assert.IsNotNull(mesh, $"{tool.Id} names variant '{tool.HeldMeshVariant}' and holds no such mesh");

                HeldRodMesh driver = Driver();
                Assert.IsTrue(figure.SetPose("idle", 0));
                Assert.IsTrue(driver.TryDraw(true, tool, figure, LineWidthPx), $"{tool.Id} carried: {driver.LastRefusal}");
                foreach (string state in ClipsAiming(figure.SkinDef, mesh.Family))
                {
                    Assert.IsTrue(figure.SetPose(state, 0));
                    Assert.IsTrue(driver.TryDraw(true, tool, figure, LineWidthPx), $"{tool.Id} on '{state}': {driver.LastRefusal}");
                }
            }
            Assert.GreaterOrEqual(covered, 2, "the rod and the shovel both carry held meshes");
        }

        // ================================================================ the carrier's wiring

        [Test]
        public void TheCarrier_DrawsAHandToolsMesh_AndHidesItsSprite_ExactlyWhenTheMeshDraws()
        {
            // The carrier needs a live mesh figure presenter to run a frame, so its wiring is pinned in source.
            string src = File.ReadAllText(CarryHandsPath).Replace("\r\n", "\n");
            StringAssert.Contains("StateDrawnByTheFight(carried);\n            StateToolMesh(carried, hand);", src);
            StringAssert.Contains("if (string.Equals(tool.DefId, _fightRodToolId, System.StringComparison.Ordinal)) return;", src);
            StringAssert.Contains("if (!_toolMesh.TryDraw(on, tool.Tool, LiveFigure(), width, use, Time.deltaTime, carriedSocket)) return;\n            tool.SetDrawnElsewhere(true);", src);
            StringAssert.Contains("if (!_toolMeshPosed && _toolMesh != null && _toolMesh.Drawing) _toolMesh.Hide();", src);
        }

        [Test]
        public void TheHandTool_AndTheRod_EachClaimTheirOwnReach()
        {
            ToolDef shovel = Load<ToolDef>(ShovelPath);
            IsoCharacterFigureRenderer figure = Fisher();
            var driver = new HeldRodMesh(_home.transform, "HeldToolMesh", IsoCharacterFigureRenderer.HeldReachHandTool);
            _drivers.Add(driver);
            Assert.IsTrue(figure.SetPose("dig", 0));
            figure.SetHeldReachMetres(IsoCharacterFigureRenderer.HeldReachTool, 0.01f);
            Assert.IsTrue(driver.TryDraw(true, shovel, figure, LineWidthPx), driver.LastRefusal.ToString());
            Assert.AreEqual(driver.Renderer.TipLocal.magnitude, figure.HeldReachMetres, 1e-5f);
            driver.Hide();
            Assert.AreEqual(0.01f, figure.HeldReachMetres, 1e-5f, "hiding the shovel took the rod's claim with it");
            Assert.AreEqual("HeldToolMesh", driver.Renderer.gameObject.name);
        }

        // ================================================================ harness

        internal static List<string> ClipsAiming(CharacterSkinDef skin, string family)
        {
            var found = new List<string>();
            foreach (CharacterSkinDef.SkinClip clip in skin.Clips)
            {
                if (clip.Tool == null || string.IsNullOrEmpty(clip.State) || found.Contains(clip.State)) continue;
                foreach (CharacterSkinDef.ToolKey k in clip.Tool)
                    if (k.Held && !k.Advisory && k.Kind == family) { found.Add(clip.State); break; }
            }
            return found;
        }

        private static float LowestZ(IsoFacetHeldPropRenderer r)
        {
            Transform t = r.transform;
            float low = float.MaxValue;
            foreach (Vector3 v in r.DrawnMesh.vertices)
                low = Mathf.Min(low, (t.localPosition + t.localRotation * v).z);
            return low;
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>The ashore figure's frame chain at one heading (as <c>CarrySideByHeadingTests</c>).</summary>
        private Transform FacetFrame(CharacterSkinDef skin, float headingDeg)
        {
            var root = new GameObject("HeldShovelTest_Root");
            _made.Add(root);
            var frame = new GameObject("AshoreFrame").transform;
            frame.SetParent(root.transform, false);
            frame.localRotation = IsoFacetMath.HullRotation(0d, skin.ElevationDeg);
            frame.localScale = IsoFacetMath.HullScale;
            var facet = new GameObject("Facet").transform;
            facet.SetParent(frame, false);
            float yaw = skin.AzimuthCounterClockwise ? -headingDeg : headingDeg;
            facet.localRotation = Quaternion.AngleAxis(yaw, Vector3.forward);
            return facet;
        }

        private IsoCharacterFigureRenderer Fisher()
        {
            var figure = new GameObject("HeldShovelMeshTestsFisher").AddComponent<IsoCharacterFigureRenderer>();
            _figures.Add(figure);
            figure.Configure(Load<CharacterSkinDef>(FisherPath));
            figure.Visible = true;
            Assert.IsTrue(figure.IsConfigured, "harness: the fisher figure did not configure");
            Assert.IsNotNull(figure.FacetTransform, "harness: the fisher figure has no facet child");
            return figure;
        }

        private HeldRodMesh Driver()
        {
            var d = new HeldRodMesh(_home.transform, "HeldToolMesh", IsoCharacterFigureRenderer.HeldReachHandTool);
            _drivers.Add(d);
            return d;
        }

        private static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"harness: no {typeof(T).Name} at {path}");
            return asset;
        }
    }
}
