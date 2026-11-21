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
    /// <b>The rod is its facet mesh in the mesh figure's hand</b> (ADR 0053): while the mesh body plays a
    /// fishing clip, the rod is drawn as the baked cane mesh on the right hand's tool socket, aimed by
    /// the clip's own tool track, and the sprite rod stands down. Anything missing hands the rod back to
    /// the sprite.
    ///
    /// <list type="bullet">
    /// <item>The aim maths restates the rig's tool direction, and is the identity at the baked aim.</item>
    /// <item>The shipped rod asks for the cane on <c>tool_R</c>, and the fisher has that bone.</item>
    /// <item>Every covered fishing clip aims the rod on every frame.</item>
    /// <item>On a posed fisher, every covered clip draws the mesh on the socket, its tip where the line
    /// starts, and the overlay padded to hold it.</item>
    /// <item>Switch off, tool flag off, no baked mesh, no figure, a clip with no rod: no mesh (the sprite
    /// draws), and a mesh that was up is taken down.</item>
    /// <item>The line's anchor is the mesh tip exactly when the mesh draws.</item>
    /// </list>
    /// </summary>
    public sealed class HeldRodMeshTests
    {
        private const string RodPath = "Assets/_Project/Data/Tools/Rod.asset";
        private const string FisherPath = "Assets/_Project/Data/Characters/Skin/fisher.asset";
        private const string CanePath = "Assets/_Project/Data/Characters/HeldProps/RodCaneHeldMesh.asset";
        private const string PresenterPath = "Assets/_Project/Code/Player/RodFightPresenter.cs";
        private const float LineWidthPx = 1.5f;

        /// <summary>The states this slice covers: the fishing performance the mesh body plays.</summary>
        private static readonly string[] Covered =
            { "hold", "cast", "bite", "strike", "reel", "land", "castBack", "castRelease" };

        private static readonly MethodInfo s_figureOnDestroy =
            typeof(IsoCharacterFigureRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo s_propOnDestroy =
            typeof(IsoFacetHeldPropRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<Object> _made = new List<Object>();
        private readonly List<IsoCharacterFigureRenderer> _figures = new List<IsoCharacterFigureRenderer>();
        private readonly List<HeldRodMesh> _drivers = new List<HeldRodMesh>();
        private GameConfig _previousConfig;
        private GameObject _home;

        [SetUp]
        public void SetUp()
        {
            _previousConfig = GameServices.Config;
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _made.Add(config);
            GameServices.Config = config;
            _home = new GameObject("HeldRodMeshTestsHome");
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

        // ================================================================ the aim maths

        [Test]
        public void ToolRotation_TurnsUpOntoTheRigsToolDirection()
        {
            float[] pitches = { -30f, 0f, 22f, 56f, 80f };
            float[] yaws = { -90f, -16f, 0f, 16f, 135f };
            foreach (float p in pitches)
            foreach (float y in yaws)
            {
                Vector3 got = HeldPropPose.ToolRotation(p, y) * Vector3.up;
                Vector3 want = HeldPropPose.ToolDirection(p, y);
                Assert.Less((got - want).magnitude, 1e-4f, $"pitch {p}, yaw {y}: {got} is not the rig's {want}");
            }
        }

        [Test]
        public void FromBakedPose_IsTheIdentityAtTheBakedAim_AndCarriesTheBakedAimOntoTheClipsAim()
        {
            Quaternion same = HeldPropPose.FromBakedPose(56f, 16f, 56f, 16f);
            Assert.Less(Quaternion.Angle(same, Quaternion.identity), 1e-3f, "the baked pose must draw as baked");

            Quaternion turn = HeldPropPose.FromBakedPose(56f, 16f, 10f, -40f);
            Vector3 got = turn * HeldPropPose.ToolDirection(56f, 16f);
            Assert.Less((got - HeldPropPose.ToolDirection(10f, -40f)).magnitude, 1e-4f);
        }

        [Test]
        public void SourcePose_IsReadOffTheProvenanceLine_AndAMissingAimIsRefused()
        {
            Assert.IsTrue(HeldPropPose.TryParseSourcePose("held: pitch 56.0000 deg, yaw 16.0000 deg, bend 0, zOff 0",
                                                          out float p, out float y));
            Assert.AreEqual(56f, p, 1e-4f);
            Assert.AreEqual(16f, y, 1e-4f);
            Assert.IsTrue(HeldPropPose.TryParseSourcePose("pitch -12.5 deg, yaw -3 deg", out p, out y));
            Assert.AreEqual(-12.5f, p, 1e-4f);
            Assert.AreEqual(-3f, y, 1e-4f);
            Assert.IsFalse(HeldPropPose.TryParseSourcePose("held: pitch 56 deg", out _, out _), "no yaw");
            Assert.IsFalse(HeldPropPose.TryParseSourcePose("", out _, out _));
            Assert.IsFalse(HeldPropPose.TryParseSourcePose(null, out _, out _));
        }

        [Test]
        public void NearestHeldKey_IsTheFramesOwn_ElseTheNearest_EarlierFirst()
        {
            var track = new[]
            {
                Key(false, "rod", 0f), Key(true, "rod", 1f), Key(false, "", 0f),
                Key(true, "shovel", 3f), Key(true, "rod", 4f),
            };
            Assert.IsTrue(HeldPropPose.TryNearestHeldKey(track, 1, "rod", out var k)); Assert.AreEqual(1f, k.Pitch);
            Assert.IsTrue(HeldPropPose.TryNearestHeldKey(track, 0, "rod", out k)); Assert.AreEqual(1f, k.Pitch);
            Assert.IsTrue(HeldPropPose.TryNearestHeldKey(track, 2, "rod", out k));
            Assert.AreEqual(1f, k.Pitch, "equal distance: the earlier frame wins");
            Assert.IsTrue(HeldPropPose.TryNearestHeldKey(track, 3, "rod", out k));
            Assert.AreEqual(4f, k.Pitch, "a held shovel is not a rod");
            Assert.IsTrue(HeldPropPose.TryNearestHeldKey(track, 99, "rod", out k)); Assert.AreEqual(4f, k.Pitch);
            Assert.IsFalse(HeldPropPose.TryNearestHeldKey(track, 0, "knife", out _));
            Assert.IsFalse(HeldPropPose.TryNearestHeldKey(null, 0, "rod", out _));
        }

        // ================================================================ the shipped data

        [Test]
        public void TheShippedRod_AsksForTheCane_OnTheRightHandsToolSocket_AndTheFisherHasIt()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            Assert.IsTrue(rod.DrawHeldMeshOnFigure, "the rod must ask to be drawn as its mesh on the figure");
            Assert.AreEqual(HeldPropSockets.RightHandTool, rod.HeldMeshMountBone);
            HeldPropMeshDef cane = rod.HeldMeshFor(rod.HeldMeshVariant);
            Assert.IsNotNull(cane, $"the rod names variant '{rod.HeldMeshVariant}' and holds no such mesh");
            Assert.AreEqual("heldprop.rod_cane", cane.Id);
            Assert.IsTrue(cane.IsUsable(), "the cane mesh is not usable: nothing to draw");
            Assert.IsTrue(HeldPropPose.TryParseSourcePose(cane.SourcePose, out float p, out float y),
                          "the cane's baked aim cannot be read off its provenance line");
            Assert.AreEqual(56f, p, 1e-3f);
            Assert.AreEqual(16f, y, 1e-3f);

            CharacterSkinDef fisher = Load<CharacterSkinDef>(FisherPath);
            Assert.AreNotEqual(CharacterSkinDef.NoBone, fisher.IndexOfBone(rod.HeldMeshMountBone),
                               "the fisher has no tool socket to hold the rod on");
        }

        [Test]
        public void TheCanesTip_LiesAlongItsBakedAim()
        {
            HeldPropMeshDef cane = Load<HeldPropMeshDef>(CanePath);
            Assert.IsTrue(HeldPropPose.TryTipLocal(cane, out Vector3 tip), "the cane plots no blank");
            Assert.Greater(tip.magnitude, 0.5f, "a rod tip this close to the grip is not a rod");
            float along = Vector3.Dot(tip.normalized, HeldPropPose.ToolDirection(56f, 16f));
            Assert.Greater(along, 0.98f,
                           $"the cane's tip {tip} does not lie along the rig's aim (56, 16): the frame the aim is " +
                           "turned in is not the mesh's");
        }

        [Test]
        public void EveryCoveredFishingClip_AimsTheRod_OnEveryFrame()
        {
            CharacterSkinDef fisher = Load<CharacterSkinDef>(FisherPath);
            foreach (string state in Covered)
            {
                Assert.IsTrue(fisher.TryGetClip(state, out CharacterSkinDef.SkinClip clip), $"no clip '{state}'");
                Assert.IsNotNull(clip.Tool, $"'{state}' carries no tool track");
                Assert.AreEqual(clip.FrameCount, clip.Tool.Length, $"'{state}': one tool key per frame");
                for (int f = 0; f < clip.FrameCount; f++)
                    Assert.IsTrue(clip.Tool[f].Held && clip.Tool[f].Kind == "rod",
                                  $"'{state}' frame {f} does not hold the rod");
            }
        }

        [Test]
        public void TheCaneBuilds_WithItsBlankAsTubes_InsideTheSlotLaw()
        {
            HeldPropMeshDef cane = Load<HeldPropMeshDef>(CanePath);
            float width = LineWidthPx / cane.PxPerMetre;
            Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(cane, width, out IsoFacetHeldPropMesh.Built built, out string why), why);
            _made.Add(built.Mesh);

            var colours = new HashSet<long>();
            int segments = 0;
            foreach (HeldPropMeshDef.Line line in cane.Lines)
            {
                if (line.Dots || line.Points == null) continue;
                for (int i = 0; i + 1 < line.Points.Length; i++)
                {
                    segments++;
                    colours.Add(((long)line.Points[i].Ramp << 32) | (uint)line.Points[i].Index);
                }
            }
            Assert.Greater(built.TubeSegments, 0, "the blank was not drawn");
            Assert.LessOrEqual(built.TubeSegments, segments);
            // One slot per face ramp, and at most one per line colour (a colour on a zero-length step only
            // takes no slot).
            Assert.Greater(built.Ramps.Length, cane.Ramps.Length, "the blank's colours took no slot");
            Assert.LessOrEqual(built.Ramps.Length, cane.Ramps.Length + colours.Count);
            Assert.LessOrEqual(built.Ramps.Length, HeldPropMeshDef.RampSlots);
            Assert.AreEqual(built.Ramps.Length, built.RampOffsets.Length);
            Assert.AreEqual(built.Ramps.Length, built.RampDitherWeights.Length);

            Assert.IsTrue(HeldPropPose.TryTipLocal(cane, out Vector3 tip));
            Bounds b = built.Mesh.bounds;
            b.Expand(width * 2f);
            Assert.IsTrue(b.Contains(tip), "the drawn mesh does not reach the blank's tip");
        }

        // ================================================================ on a posed fisher

        [Test]
        public void EachCoveredState_DrawsTheMeshOnTheSocket_WithTheTipWhereTheLineStarts()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            IsoCharacterFigureRenderer figure = Fisher();
            HeldRodMesh driver = Driver();
            int socket = figure.SkinDef.IndexOfBone(rod.HeldMeshMountBone);

            foreach (string state in Covered)
            {
                Assert.IsTrue(figure.SkinDef.TryGetClip(state, out CharacterSkinDef.SkinClip clip));
                int[] frames = { 0, clip.FrameCount / 2, clip.FrameCount - 1 };
                foreach (int frame in frames)
                {
                    Assert.IsTrue(figure.SetPose(state, frame), $"harness: the fisher did not pose '{state}' {frame}");

                    Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx),
                                  $"'{state}' frame {frame}: no mesh rod ({driver.LastRefusal})");
                    Assert.AreEqual(HeldRodMesh.Refusal.None, driver.LastRefusal);
                    IsoFacetHeldPropRenderer r = driver.Renderer;
                    Assert.IsNotNull(r);
                    Assert.AreSame(figure.FacetTransform, r.transform.parent, "the rod must ride the figure's facet child");
                    Assert.IsTrue(r.Visible, "drawing, yet the rod's renderer is off");
                    Assert.AreEqual("heldprop.rod_cane", r.Def.Id);

                    Assert.IsTrue(figure.TryBonePoint(socket, out Vector3 grip));
                    Assert.Less((r.transform.localPosition - grip).magnitude, 1e-5f, "the grip is not on the socket");

                    CharacterSkinDef.ToolKey key = clip.Tool[figure.DrawnFrame];
                    Vector3 aim = r.transform.localRotation * (HeldPropPose.ToolRotation(56f, 16f) * Vector3.up);
                    Assert.Less((aim - HeldPropPose.ToolDirection(key.Pitch, key.Yaw)).magnitude, 1e-3f,
                                $"'{state}' frame {figure.DrawnFrame}: the rod is not aimed where the clip aims it");

                    Vector2 tipWorld = r.transform.TransformPoint(r.TipLocal);
                    Assert.Less((driver.TipWorld - tipWorld).magnitude, 1e-5f, "the line's anchor is not the mesh tip");
                    Assert.AreEqual(r.TipLocal.magnitude, figure.HeldReachMetres, 1e-5f,
                                    "the figure's overlay is not padded to hold the rod");
                }
            }

            driver.Hide();
            Assert.IsFalse(driver.Renderer.Visible);
            Assert.AreEqual(0f, figure.HeldReachMetres, "the overlay pad was not given back");
        }

        [Test]
        public void TheLean_TurnsTheRodInThePicture_AboutTheGrip()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            IsoCharacterFigureRenderer figure = Fisher();
            HeldRodMesh driver = Driver();
            Assert.IsTrue(figure.SetPose("reel", 0));
            Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx), driver.LastRefusal.ToString());
            Vector2 grip = driver.GripWorld, tip = driver.TipWorld;

            driver.Lean(20f);

            Assert.Less((driver.GripWorld - grip).magnitude, 1e-5f, "the lean moved the grip");
            float turned = Vector2.SignedAngle(tip - grip, driver.TipWorld - grip);
            Assert.AreEqual(20f, turned, 0.05f, "the picture-space lean is not the presenter's");
        }

        // ================================================================ the fallback

        [Test]
        public void TheSwitchOff_KeepsTheSprite_AndTakesAMeshThatWasUpDown()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            IsoCharacterFigureRenderer figure = Fisher();
            HeldRodMesh driver = Driver();
            Assert.IsTrue(figure.SetPose("hold", 0));
            Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx), driver.LastRefusal.ToString());

            Assert.IsFalse(driver.TryDraw(false, rod, figure, LineWidthPx));
            Assert.AreEqual(HeldRodMesh.Refusal.SwitchOff, driver.LastRefusal);
            Assert.IsFalse(driver.Drawing);
            Assert.IsFalse(driver.Renderer.Visible, "switched off, the mesh rod is still drawn");
            Assert.AreEqual(0f, figure.HeldReachMetres);
        }

        [Test]
        public void EachMissingPiece_IsARefusal_AndTheSpriteKeepsTheRod()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            IsoCharacterFigureRenderer figure = Fisher();
            Assert.IsTrue(figure.SetPose("hold", 0));

            ToolDef keepsSprite = Copy(rod);
            keepsSprite.DrawHeldMeshOnFigure = false;
            ToolDef notBaked = Copy(rod);
            notBaked.HeldMeshVariant = "not_baked";
            ToolDef noMeshes = Copy(rod);
            noMeshes.HeldMeshes = System.Array.Empty<HeldPropMeshDef>();
            ToolDef noSocket = Copy(rod);
            noSocket.HeldMeshMountBone = "no_such_bone";

            Refuses(null, figure, HeldRodMesh.Refusal.NoTool);
            Refuses(keepsSprite, figure, HeldRodMesh.Refusal.ToolKeepsSprite);
            Refuses(notBaked, figure, HeldRodMesh.Refusal.NoUsableMesh);
            Refuses(noMeshes, figure, HeldRodMesh.Refusal.NoUsableMesh);
            Refuses(noSocket, figure, HeldRodMesh.Refusal.NoMountBone);
            Refuses(rod, null, HeldRodMesh.Refusal.NoMeshFigure);

            figure.Visible = false;
            Refuses(rod, figure, HeldRodMesh.Refusal.NoMeshFigure);
            figure.Visible = true;

            ToolDef noCarry = Copy(rod);
            noCarry.DrawCarriedMeshOnFigure = false;
            Assert.IsTrue(figure.SetPose("idle", 0), "harness: the fisher did not stand idle");
            Refuses(noCarry, figure, HeldRodMesh.Refusal.ClipAimsNoTool);
        }

        [Test]
        public void AClipThatAimsNoTool_HoldsTheRodAtTheDefsCarriedAim_OnTheSocket()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            Assert.IsTrue(rod.DrawCarriedMeshOnFigure, "the shipped rod must ask for its carried mesh");
            IsoCharacterFigureRenderer figure = Fisher();
            HeldRodMesh driver = Driver();
            int mainSocket = figure.SkinDef.IndexOfBone(rod.HeldMeshMountBone);
            int offSocket = figure.SkinDef.IndexOfBone(rod.CarriedOffHandMountBone);
            foreach (string state in new[] { "idle", "walk" })
            {
                Assert.IsTrue(figure.SkinDef.TryGetClip(state, out CharacterSkinDef.SkinClip clip), $"harness: no '{state}'");
                foreach (int frame in new[] { 0, clip.FrameCount - 1 })
                {
                    Assert.IsTrue(figure.SetPose(state, frame), $"harness: the fisher did not pose '{state}' {frame}");
                    // A frame long enough to finish any move between his hands, so the grip is checked settled.
                    Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.Free, rod.StowBlendSeconds * 2f),
                                  $"'{state}' {frame}: {driver.LastRefusal}");
                    Assert.IsTrue(driver.Carried, $"'{state}' aims no tool, so the rod is carried");
                    IsoFacetHeldPropRenderer r = driver.Renderer;
                    Assert.AreSame(figure.FacetTransform, r.transform.parent);
                    // The hand nearer the camera carries it (ToolDef.CarryInNearerHand), at the yaw for that hand.
                    Assert.IsTrue(figure.TryBonePoint(driver.CarriedInOffHand ? offSocket : mainSocket, out Vector3 grip));
                    Assert.Less((r.transform.localPosition - grip).magnitude, 1e-5f, "the carried grip is not on the socket");
                    Assert.IsFalse(driver.Stowed, $"'{state}' is a free-handed gait: the rod stays in his hand");
                    Vector3 aim = r.transform.localRotation * HeldPropPose.ToolDirection(56f, 16f);
                    float yaw = HeldRodMesh.CarriedYawFor(rod, driver.CarriedInOffHand);
                    Assert.Less((aim - HeldPropPose.ToolDirection(rod.CarriedPitchDeg, yaw)).magnitude, 1e-3f,
                                "the carried rod must be aimed at the Def's carried aim (D1), not the fishing aim");
                }
            }
            Assert.IsTrue(figure.SetPose("hold", 0));
            Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx));
            Assert.IsFalse(driver.Carried, "a clip that aims the rod is not the carried placement");
        }

        [Test]
        public void ALineAnchor_IsTheMeshTipExactlyWhenTheMeshDraws()
        {
            var mesh = new Vector2(1f, 2f);
            var sprite = new Vector2(-3f, 4f);
            Assert.AreEqual(mesh, RodPresenterMath.LineAnchor(true, mesh, sprite));
            Assert.AreEqual(sprite, RodPresenterMath.LineAnchor(false, mesh, sprite));
        }

        [Test]
        public void ThePresenter_TurnsTheSpriteRodOff_ExactlyWhenTheMeshDraws()
        {
            // The presenter's frame needs a live fishing state to run, so its wiring is pinned in its
            // source: the sprite rod goes off on a mesh frame, the mesh is taken down on any other, the
            // line starts at the drawn rod's tip, and the lean turns the rod that is drawn.
            string src = File.ReadAllText(PresenterPath).Replace("\r\n", "\n");
            StringAssert.Contains("DrawsMeshRod = TryDrawMeshRod();", src);
            StringAssert.Contains("if (!DrawsMeshRod) _meshRod?.Hide();", src);
            StringAssert.Contains("if (DrawsMeshRod)\n            {\n                _rodSr.enabled = false;", src);
            StringAssert.Contains("RodPresenterMath.LineAnchor(DrawsMeshRod,", src);
            StringAssert.Contains("if (DrawsMeshRod) _meshRod.Lean(lean);", src);
            // A mesh figure never shows the old sprite rod: not the fight's, not the carried one.
            StringAssert.Contains("else if (MeshFigureOwnsRod)\n            {", src);
            StringAssert.Contains("MeshFigureOwnsRod = !HeldRodMesh.SpriteMayDraw(on, carried, figure != null);", src);
            StringAssert.Contains("public bool DrawsRod => DrawsMeshRod || MeshFigureOwnsRod || (_rodSr != null && _rodSr.enabled);", src);
        }

        // ================================================================ harness

        private void Refuses(ToolDef tool, IsoCharacterFigureRenderer figure, HeldRodMesh.Refusal want)
        {
            HeldRodMesh driver = Driver();
            ToolDef rod = Load<ToolDef>(RodPath);
            IsoCharacterFigureRenderer live = Fisher();
            Assert.IsTrue(live.SetPose("hold", 0));
            Assert.IsTrue(driver.TryDraw(true, rod, live, LineWidthPx), "harness: the mesh rod did not draw first");

            Assert.IsFalse(driver.TryDraw(true, tool, figure, LineWidthPx), $"expected {want}, but the mesh drew");
            Assert.AreEqual(want, driver.LastRefusal);
            Assert.IsFalse(driver.Drawing);
            Assert.IsFalse(driver.Renderer.Visible, $"{want}: the mesh rod that was up is still drawn");
            Assert.AreEqual(0f, live.HeldReachMetres, $"{want}: the overlay pad was not given back");
        }

        private IsoCharacterFigureRenderer Fisher()
        {
            var figure = new GameObject("HeldRodMeshTestsFisher").AddComponent<IsoCharacterFigureRenderer>();
            _figures.Add(figure);
            figure.Configure(Load<CharacterSkinDef>(FisherPath));
            figure.Visible = true;
            Assert.IsTrue(figure.IsConfigured, "harness: the fisher figure did not configure");
            Assert.IsNotNull(figure.FacetTransform, "harness: the fisher figure has no facet child");
            return figure;
        }

        private HeldRodMesh Driver()
        {
            var d = new HeldRodMesh(_home.transform);
            _drivers.Add(d);
            return d;
        }

        private ToolDef Copy(ToolDef src)
        {
            ToolDef c = Object.Instantiate(src);
            _made.Add(c);
            return c;
        }

        private static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"harness: no {typeof(T).Name} at {path}");
            return asset;
        }

        private static CharacterSkinDef.ToolKey Key(bool held, string kind, float pitch) =>
            new CharacterSkinDef.ToolKey { Held = held, Kind = kind, Pitch = pitch };
    }
}
