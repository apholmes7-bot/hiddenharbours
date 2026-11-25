using System.Collections.Generic;
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
    /// <b>The rod is in his hand when he uses it, and slung on his back when he does not.</b>
    ///
    /// <list type="bullet">
    /// <item>In every in-hand state (the fishing performance and the free gait), on several frames and at
    /// all eight headings, the grip sits on the holding hand's tool socket within the shipped
    /// <see cref="GameConfig.HeldRodGripToleranceMetres"/>.</item>
    /// <item>On the free gait the rod is in the hand on the side of him nearer the camera (read off the
    /// shoulders, which do not swing), with the carried yaw mirrored in the left hand; it never changes
    /// hands mid-stride, and a change of hands is blended like the draw.</item>
    /// <item>While a mesh figure draws him, the old sprite rod never may.</item>
    /// <item>The free gait carries the rod at the Def's carried aim; rowing, the helm, a carry and the swim
    /// put it on the rig's back mount, diagonal across his back and off the torso.</item>
    /// <item>Busy hands stow it on a free gait too; fishing always puts it in his hand.</item>
    /// <item>A missing back mount is a refusal; a Def that does not sling keeps the old in-hand carry.</item>
    /// <item>The move between hand and back is a short blend from the Def, or a clean swap at zero.</item>
    /// <item>The hands report a slung rod and busy hands from their own state.</item>
    /// </list>
    /// </summary>
    public sealed class HeldRodStowTests
    {
        private const string RodPath = "Assets/_Project/Data/Tools/Rod.asset";
        private const string FisherPath = "Assets/_Project/Data/Characters/Skin/fisher.asset";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        private const float LineWidthPx = 1.5f;

        /// <summary>Every state the rod is drawn in his hand: the fishing performance and the free gait.</summary>
        private static readonly string[] InHand =
            { "hold", "cast", "bite", "strike", "reel", "land", "castBack", "castRelease", "idle", "walk", "run" };

        /// <summary>Upright clips that aim no rod and are not a free gait: the rod rides his back.</summary>
        private static readonly string[] UprightStowed =
            { "idle_oars", "row_oars", "idle_helm", "walk_helm", "walk_buckets", "idle_tray" };

        /// <summary>The swim band: stowed too, whatever the body's lean.</summary>
        private static readonly string[] SwimStowed = { "swim", "tread" };

        private static readonly MethodInfo s_figureOnDestroy =
            typeof(IsoCharacterFigureRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo s_propOnDestroy =
            typeof(IsoFacetHeldPropRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<Object> _made = new List<Object>();
        private readonly List<IsoCharacterFigureRenderer> _figures = new List<IsoCharacterFigureRenderer>();
        private readonly List<HeldRodMesh> _drivers = new List<HeldRodMesh>();
        private GameObject _home;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _home = new GameObject("HeldRodStowTestsHome");
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
                f.LeaveAshore();
                if (s_figureOnDestroy != null) s_figureOnDestroy.Invoke(f, null);
                Object.DestroyImmediate(f.gameObject);
            }
            _figures.Clear();
            GameServices.Reset();
            foreach (Object o in _made)
                if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // ================================================================ in his hand

        [Test]
        public void TheGrip_IsOnHisHandsToolSocket_InEveryInHandState_FrameAndHeading()
        {
            float margin = Load<GameConfig>(ConfigPath).HeldRodGripToleranceMetres;
            Assert.Greater(margin, 0f, "the shipped grip tolerance must be a positive number");
            ToolDef rod = Load<ToolDef>(RodPath);
            IsoCharacterFigureRenderer figure = Fisher();
            HeldRodMesh driver = Driver();
            int mainSocket = figure.SkinDef.IndexOfBone(rod.HeldMeshMountBone);
            int offSocket = figure.SkinDef.IndexOfBone(rod.CarriedOffHandMountBone);
            Assert.AreNotEqual(CharacterSkinDef.NoBone, offSocket, "the fisher has no other-hand tool mount");
            // A frame long enough to finish any move between hands, so each pose is checked where it settles.
            float settle = rod.StowBlendSeconds * 2f;
            int checkedPoses = 0;

            foreach (string state in InHand)
            {
                Assert.IsTrue(figure.SkinDef.TryGetClip(state, out CharacterSkinDef.SkinClip clip), $"harness: no '{state}'");
                foreach (int frame in new[] { 0, clip.FrameCount / 3, (2 * clip.FrameCount) / 3, clip.FrameCount - 1 })
                for (int heading = 0; heading < 8; heading++)
                {
                    figure.transform.rotation = Quaternion.Euler(0f, 0f, heading * 45f);
                    Assert.IsTrue(figure.SetPose(state, frame), $"harness: the fisher did not pose '{state}' {frame}");
                    HeldRodMesh.Use use = clip.Tool != null && clip.Tool.Length > 0 && clip.Tool[0].Held
                        ? HeldRodMesh.Use.Fishing : HeldRodMesh.Use.Free;
                    Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, use, settle),
                                  $"'{state}' {frame} heading {heading}: {driver.LastRefusal}");
                    Assert.IsFalse(driver.Stowed, $"'{state}' is an in-hand state, yet the rod is on his back");
                    if (!driver.Carried)
                        Assert.IsFalse(driver.CarriedInOffHand, $"'{state}': a clip that aims the rod grips it in the rig's hand");
                    int socket = driver.CarriedInOffHand ? offSocket : mainSocket;
                    Assert.IsTrue(figure.TryBonePoint(socket, out Vector3 socketLocal));
                    Vector3 socketWorld = figure.FacetTransform.TransformPoint(socketLocal);
                    float d = (driver.Renderer.transform.position - socketWorld).magnitude;
                    Assert.LessOrEqual(d, margin,
                                       $"'{state}' frame {frame} heading {heading * 45}: the grip is {d:0.000} m off his hand");
                    checkedPoses++;
                }
            }
            figure.transform.rotation = Quaternion.identity;
            Assert.Greater(checkedPoses, 0);
        }

        [Test]
        public void TheFreeGait_CarriesTheRodAtTheDefsAim_NotTheFishingAim()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            IsoCharacterFigureRenderer figure = AshoreFisher();
            HeldRodMesh driver = Driver();
            foreach (string state in rod.CarriedInHandClips)
            for (int heading = 0; heading < 8; heading++)
            {
                Face(figure, heading);
                Assert.IsTrue(figure.SetPose(state, 0), $"harness: no '{state}'");
                Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.Free, rod.StowBlendSeconds * 2f),
                              driver.LastRefusal.ToString());
                Assert.IsTrue(driver.Carried);
                Assert.IsFalse(driver.Stowed);
                Vector3 aim = driver.Renderer.transform.localRotation * HeldPropPose.ToolDirection(56f, 16f);
                float yaw = HeldRodMesh.CarriedYawFor(rod, driver.CarriedInOffHand);
                Assert.Less((aim - HeldPropPose.ToolDirection(rod.CarriedPitchDeg, yaw)).magnitude, 1e-3f,
                            $"'{state}' heading {heading * 45}: the carried rod is not at the Def's carried aim for that hand");
            }
        }

        // ================================================================ the hand you can see

        [Test]
        public void TheFreeGait_CarriesTheRodInTheHandNearerTheCamera_AtEveryHeading()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            Assert.IsTrue(rod.CarryInNearerHand, "the shipped rod is carried in the hand you can see");
            IsoCharacterFigureRenderer figure = AshoreFisher();
            // His sides, read off the bones the Def names for it (the shoulders: they do not swing).
            int main = figure.SkinDef.IndexOfBone(rod.CarriedSideBone);
            int off = figure.SkinDef.IndexOfBone(rod.CarriedOffSideBone);
            Assert.AreNotEqual(CharacterSkinDef.NoBone, main, "the fisher has no bone for the rod hand's side");
            Assert.AreNotEqual(CharacterSkinDef.NoBone, off, "the fisher has no bone for the other side");
            bool sawMain = false, sawOff = false;

            foreach (string state in rod.CarriedInHandClips)
            for (int heading = 0; heading < 8; heading++)
            {
                // A fresh driver: the choice from a standing start, with no earlier hand to hold on to.
                HeldRodMesh driver = Driver();
                Face(figure, heading);
                Assert.IsTrue(figure.SetPose(state, 0), $"harness: no '{state}'");
                Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.Free, 0f), driver.LastRefusal.ToString());
                Assert.IsTrue(figure.TryBonePoint(main, out Vector3 m));
                Assert.IsTrue(figure.TryBonePoint(off, out Vector3 o));
                float mainZ = figure.FacetTransform.TransformPoint(m).z;
                float offZ = figure.FacetTransform.TransformPoint(o).z;
                float facing = driver.FacingAway(figure);
                if (facing < rod.CarriedOffHandFacingAwayMin)
                {
                    // Facing the camera both hands show: the rod stays in the rig's hand, angled out clear of him.
                    Assert.IsFalse(driver.CarriedInOffHand, $"'{state}' heading {heading * 45}: facing the camera ({facing:0.00}), yet in the other hand");
                    sawMain = true;
                    continue;
                }
                float holding = driver.CarriedInOffHand ? offZ : mainZ;
                float other = driver.CarriedInOffHand ? mainZ : offZ;
                Assert.LessOrEqual(holding, other + rod.CarriedHandSwapDepthMetres + 1e-5f,
                                   $"'{state}' heading {heading * 45}: the rod is in the hand farther from the camera");
                if (offZ < mainZ - rod.CarriedHandSwapDepthMetres)
                    Assert.IsTrue(driver.CarriedInOffHand, $"'{state}' heading {heading * 45}: the right hand is hidden, yet holds the rod");
                if (driver.CarriedInOffHand) sawOff = true; else sawMain = true;
            }
            Assert.IsTrue(sawMain, "the rod never sat in his right hand on a free gait");
            Assert.IsTrue(sawOff, "the rod never changed to his left hand when his right was behind him");
        }

        [Test]
        public void ThroughAWholeStride_TheRodStaysInOneHand_AtEveryHeading()
        {
            // The hands swing through each other on the walk and the run; the rod must not change hands
            // with every step. Facing one way through two whole cycles, it never changes hands after the
            // first frame.
            ToolDef rod = Load<ToolDef>(RodPath);
            IsoCharacterFigureRenderer figure = AshoreFisher();
            foreach (string state in rod.CarriedInHandClips)
            {
                Assert.IsTrue(figure.SkinDef.TryGetClip(state, out CharacterSkinDef.SkinClip clip), $"harness: no '{state}'");
                for (int heading = 0; heading < 8; heading++)
                {
                    HeldRodMesh driver = Driver();
                    Face(figure, heading);
                    int changes = 0;
                    bool hand = false;
                    for (int i = 0; i < clip.FrameCount * 2; i++)
                    {
                        Assert.IsTrue(figure.SetPose(state, i % clip.FrameCount));
                        Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.Free, 1f / 60f));
                        if (i > 0 && driver.CarriedInOffHand != hand) changes++;
                        hand = driver.CarriedInOffHand;
                    }
                    Assert.AreEqual(0, changes, $"'{state}' heading {heading * 45}: the rod changed hands {changes} times in two strides");
                }
            }
        }

        [Test]
        public void TheHandChoice_HoldsWithinTheSwapDepth_AndSwapsPastIt()
        {
            const float swap = 0.05f;
            Assert.IsFalse(HeldRodMesh.CarryInOffHand(false, 0f, -0.04f, swap), "inside the margin: stays in the right hand");
            Assert.IsTrue(HeldRodMesh.CarryInOffHand(false, 0f, -0.06f, swap), "past the margin: the left hand takes it");
            Assert.IsTrue(HeldRodMesh.CarryInOffHand(true, -0.04f, 0f, swap), "inside the margin: stays in the left hand");
            Assert.IsFalse(HeldRodMesh.CarryInOffHand(true, -0.06f, 0f, swap), "past the margin: back to the right hand");
            Assert.IsTrue(HeldRodMesh.CarryInOffHand(false, 0f, -0.001f, 0f), "no margin: the nearer hand at once");
            Assert.IsFalse(HeldRodMesh.CarryInOffHand(false, 0f, -0.06f, float.PositiveInfinity), "an endless margin never swaps");
            Assert.IsTrue(HeldRodMesh.CarryInOffHand(false, 0f, -0.06f, -1f), "a negative margin reads as zero");

            // The facing gate: facing the camera past the Def's limit, the rig's hand keeps it.
            Assert.IsTrue(HeldRodMesh.CarryInOffHand(false, 0f, -0.3f, swap, 0f, -0.1f), "side-on: the nearer hand takes it");
            Assert.IsFalse(HeldRodMesh.CarryInOffHand(false, 0f, -0.3f, swap, -0.5f, -0.1f), "facing the camera: the rig's hand keeps it");
            Assert.IsFalse(HeldRodMesh.CarryInOffHand(true, 0f, -0.3f, swap, -0.5f, -0.1f), "turning to the camera hands it back");
            Assert.IsTrue(HeldRodMesh.CarryInOffHand(false, 0f, -0.3f, swap, -1f, -1f), "a limit of -1 never gates");
        }

        [Test]
        public void TheOtherHand_MirrorsTheCarriedYaw()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            Assert.AreEqual(rod.CarriedYawDeg, HeldRodMesh.CarriedYawFor(rod, false));
            Assert.AreEqual(-rod.CarriedYawDeg, HeldRodMesh.CarriedYawFor(rod, true));
            Assert.AreEqual(0f, HeldRodMesh.CarriedYawFor(null, true));
        }

        [Test]
        public void ADefThatDoesNotChooseAHand_KeepsTheRodInItsMountHand_AtEveryHeading()
        {
            ToolDef rod = Copy(Load<ToolDef>(RodPath));
            rod.CarryInNearerHand = false;
            IsoCharacterFigureRenderer figure = AshoreFisher();
            HeldRodMesh driver = Driver();
            Assert.IsTrue(figure.SetPose("walk", 0));
            for (int heading = 0; heading < 8; heading++)
            {
                Face(figure, heading);
                Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.Free, 0f));
                Assert.IsFalse(driver.CarriedInOffHand, $"heading {heading * 45}");
            }
        }

        [Test]
        public void ChangingHands_IsBlendedOverTheDefsSeconds()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            IsoCharacterFigureRenderer figure = AshoreFisher();
            Assert.IsTrue(figure.SetPose("walk", 0));
            HeldRodMesh driver = Driver();
            int swapped = -1;
            bool prev = false;
            for (int heading = 0; heading < 8 && swapped < 0; heading++)
            {
                Face(figure, heading);
                Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.Free, rod.StowBlendSeconds * 2f));
                if (heading > 0 && driver.CarriedInOffHand != prev) swapped = heading;
                prev = driver.CarriedInOffHand;
            }
            Assert.GreaterOrEqual(swapped, 0, "harness: the rod never changed hands across the headings");
            // The frame that changed hands was long enough to finish; replay it from the other side half way.
            HeldRodMesh half = Driver();
            for (int heading = 0; heading < swapped; heading++)
            {
                Face(figure, heading);
                Assert.IsTrue(half.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.Free, rod.StowBlendSeconds * 2f));
            }
            bool before = half.CarriedInOffHand;
            Face(figure, swapped);
            Assert.IsTrue(half.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.Free, rod.StowBlendSeconds * 0.5f));
            Assert.AreNotEqual(before, half.CarriedInOffHand, "harness: the replay did not change hands");
            Assert.IsTrue(half.Blending, "half way between his hands: still moving");
        }

        // ================================================================ never the old sprite

        [Test]
        public void TheOldSprite_NeverDrawsWhileAMeshFigureDrawsHim()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            Assert.IsTrue(rod.DrawHeldMeshOnFigure);
            Assert.IsFalse(HeldRodMesh.SpriteMayDraw(true, rod, true), "a mesh figure: the mesh is the rod's only look");
            Assert.IsTrue(HeldRodMesh.SpriteMayDraw(true, rod, false), "a sprite body keeps the sprite rod");
            Assert.IsTrue(HeldRodMesh.SpriteMayDraw(false, rod, true), "the switch off keeps the sprite everywhere");
            Assert.IsTrue(HeldRodMesh.SpriteMayDraw(true, null, true), "no tool: nothing to keep from the sprite");
            ToolDef spriteOnly = Copy(rod);
            spriteOnly.DrawHeldMeshOnFigure = false;
            Assert.IsTrue(HeldRodMesh.SpriteMayDraw(true, spriteOnly, true), "a Def that keeps its sprite keeps it");
        }

        // ================================================================ on his back

        [Test]
        public void RowingTheHelmAndACarry_SlingTheRodDiagonallyOnHisBack_OffTheTorso()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            Assert.IsTrue(rod.StowOnBack, "the shipped rod must ride his back when he is not using it");
            IsoCharacterFigureRenderer figure = Fisher();
            HeldRodMesh driver = Driver();
            int back = figure.SkinDef.IndexOfBone(rod.StowMountBone);
            int chest = figure.SkinDef.IndexOfBone(HeldRodMesh.ChestBoneId);
            Assert.AreNotEqual(CharacterSkinDef.NoBone, back, "the fisher has no back mount");
            Assert.AreNotEqual(CharacterSkinDef.NoBone, chest, "the fisher has no chest");

            foreach (string state in UprightStowed)
            {
                Assert.IsTrue(figure.SkinDef.TryGetClip(state, out CharacterSkinDef.SkinClip clip), $"harness: no '{state}'");
                foreach (int frame in new[] { 0, clip.FrameCount - 1 })
                {
                    Assert.IsTrue(figure.SetPose(state, frame));
                    Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.Free, 0f),
                                  $"'{state}' {frame}: {driver.LastRefusal}");
                    Assert.IsTrue(driver.Stowed, $"'{state}' {frame}: the rod is not on his back");
                    Assert.IsFalse(driver.Carried);

                    Assert.IsTrue(figure.TryBoneFrame(back, out Vector3 mount, out Quaternion frameRot));
                    Assert.IsTrue(figure.TryBoneFrame(chest, out Vector3 chestPoint, out Quaternion chestRot));
                    Vector3 axis = frameRot * Vector3.up;
                    Transform r = driver.Renderer.transform;
                    Vector3 drawnAxis = r.localRotation * HeldPropPose.ToolDirection(56f, 16f);
                    Assert.Less((drawnAxis - axis).magnitude, 1e-3f, $"'{state}': the rod is not along the back mount");

                    float tilt = Vector3.Angle(drawnAxis, Vector3.forward);
                    Assert.That(tilt, Is.InRange(8f, 50f),
                                $"'{state}' {frame}: slung {tilt:0.0} deg off upright; diagonal is the look");

                    Vector3 outward = chestRot * Vector3.down;
                    Vector3 want = mount + axis * rod.StowGripAlongMetres + outward.normalized * rod.StowStandOffMetres;
                    Assert.Less((r.localPosition - want).magnitude, 1e-4f, $"'{state}': the stowed grip is not the Def's");
                    Assert.Greater(Vector3.Dot(r.localPosition - chestPoint, outward), 0.05f,
                                   $"'{state}' {frame}: the slung rod's grip is not behind his chest");
                }
            }
        }

        [Test]
        public void Swimming_SlingsTheRodOnHisBack()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            IsoCharacterFigureRenderer figure = Fisher();
            HeldRodMesh driver = Driver();
            foreach (string state in SwimStowed)
            {
                Assert.IsTrue(figure.SetPose(state, 0), $"harness: no '{state}'");
                Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.HandsBusy, 0f), driver.LastRefusal.ToString());
                Assert.IsTrue(driver.Stowed, $"'{state}': the rod is not on his back");
            }
        }

        [Test]
        public void BusyHandsStowIt_OnAFreeGait_ButFishingAndAnAimingClipKeepItInHand()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            IsoCharacterFigureRenderer figure = Fisher();
            HeldRodMesh driver = Driver();

            Assert.IsTrue(figure.SetPose("walk", 0));
            Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.HandsBusy, 0f));
            Assert.IsTrue(driver.Stowed, "the other hand is busy: the rod rides his back");

            Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.Fishing, 0f));
            Assert.IsFalse(driver.Stowed, "fishing puts the rod in his hand");

            Assert.IsTrue(figure.SetPose("idle_oars", 0));
            Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.Fishing, 0f));
            Assert.IsFalse(driver.Stowed, "fishing beats the clip");

            Assert.IsTrue(figure.SetPose("hold", 0));
            Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.HandsBusy, 0f));
            Assert.IsFalse(driver.Stowed, "a clip that aims the rod always has it in his hand");
            Assert.IsFalse(driver.Carried);
        }

        [Test]
        public void NoBackMount_IsARefusal_AndADefThatDoesNotSling_KeepsTheRodInHand()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            IsoCharacterFigureRenderer figure = Fisher();
            HeldRodMesh driver = Driver();
            Assert.IsTrue(figure.SetPose("idle_oars", 0));

            ToolDef noMount = Copy(rod);
            noMount.StowMountBone = "no_such_bone";
            Assert.IsFalse(driver.TryDraw(true, noMount, figure, LineWidthPx, HeldRodMesh.Use.Free, 0f));
            Assert.AreEqual(HeldRodMesh.Refusal.NoMountBone, driver.LastRefusal);

            ToolDef noSling = Copy(rod);
            noSling.StowOnBack = false;
            Assert.IsTrue(driver.TryDraw(true, noSling, figure, LineWidthPx, HeldRodMesh.Use.HandsBusy, 0f));
            Assert.IsFalse(driver.Stowed);
            Assert.IsTrue(driver.Carried);
        }

        [Test]
        public void TheMoveBetweenHandAndBack_BlendsOverTheDefsSeconds_OrSwapsCleanAtZero()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            Assert.Greater(rod.StowBlendSeconds, 0f, "the shipped rod blends");
            IsoCharacterFigureRenderer figure = Fisher();
            HeldRodMesh driver = Driver();

            Assert.IsTrue(figure.SetPose("walk", 0));
            Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.Free, 0f));
            Vector3 hand = driver.Renderer.transform.localPosition;
            Assert.IsTrue(figure.SetPose("idle_oars", 0));
            Assert.IsTrue(driver.Resolve(true, rod, figure, HeldRodMesh.Use.Free, out Vector3 backGrip, out _) == HeldRodMesh.Refusal.None);

            Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.Free, rod.StowBlendSeconds * 0.5f));
            Assert.IsTrue(driver.Blending, "half way: still moving");
            Vector3 mid = driver.Renderer.transform.localPosition;
            Assert.Greater((mid - hand).magnitude, 1e-3f, "half way, yet still in his hand");
            Assert.Greater((mid - backGrip).magnitude, 1e-3f, "half way, yet already on his back");

            Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.Free, rod.StowBlendSeconds));
            Assert.IsFalse(driver.Blending, "the blend is over");
            Assert.Less((driver.Renderer.transform.localPosition - backGrip).magnitude, 1e-4f);

            ToolDef swap = Copy(rod);
            swap.StowBlendSeconds = 0f;
            HeldRodMesh clean = Driver();
            Assert.IsTrue(figure.SetPose("walk", 0));
            Assert.IsTrue(clean.TryDraw(true, swap, figure, LineWidthPx, HeldRodMesh.Use.Free, 0f));
            Assert.IsTrue(figure.SetPose("idle_oars", 0));
            Assert.IsTrue(clean.TryDraw(true, swap, figure, LineWidthPx, HeldRodMesh.Use.Free, 0.01f));
            Assert.IsFalse(clean.Blending, "zero seconds is a clean swap");
            Assert.Less((clean.Renderer.transform.localPosition - backGrip).magnitude, 1e-4f);
        }

        [Test]
        public void CarriesInHandOn_IsTheDefsList()
        {
            ToolDef rod = Load<ToolDef>(RodPath);
            Assert.IsTrue(rod.CarriesInHandOn("walk"));
            Assert.IsTrue(rod.CarriesInHandOn("idle"));
            Assert.IsFalse(rod.CarriesInHandOn("row_oars"));
            Assert.IsFalse(rod.CarriesInHandOn(""));
            Assert.IsFalse(rod.CarriesInHandOn(null));
        }

        // ================================================================ the hands' own state

        [Test]
        public void TheHands_ReportBusyHandsAndASlungRod_FromTheirOwnState()
        {
            CarryHands hands = ToolInHand.Holding(ToolDef.RodId, _made);
            Assert.IsNotNull(hands.HeldFightRod, "harness: the rod is not in his hand");
            Assert.IsFalse(hands.FightRodHandsBusy, "one rod in one hand: the other hand is free");
            Assert.IsNull(hands.SlungFightRod);

            var pailGo = new GameObject("HeldRodStowPail");
            _made.Add(pailGo);
            var pail = pailGo.AddComponent<CarriableBucket>();
            pail.Configure(20, pailGo.AddComponent<SpriteRenderer>(), "container.test.rodstow");
            Assert.AreEqual(CarryRefusal.None, hands.TryPickUp(pail), "harness: the pail would not share with the rod");
            Assert.IsTrue(hands.FightRodHandsBusy, "rod and pail: both hands are busy");

            var cod = new CatchItem("fish.atlantic_cod", "Atlantic cod", FishCategory.InshoreGroundfish,
                                    1.75f, 17, 0.23f, 0.42f, new FreshnessState(0.19f, 716.5d, StorageMode.Ambient));
            Assert.IsTrue(hands.TryPutInHand(cod), "harness: the catch did not go in his hand");
            Assert.IsNotNull(hands.SlungFightRod, "the rod was tucked away for the catch");
            Assert.IsNull(hands.HeldFightRod, "a slung rod is not in his hand");
            Assert.IsTrue(hands.FightRodHandsBusy);
        }

        // ================================================================ harness

        /// <summary>The fisher stood ashore as the game stands him (EnterAshore: the bake elevation's frame,
        /// so world z is depth toward the camera). Turn him with <see cref="Face"/>: the yaw the deck
        /// presenter writes (SetAshoreYaw). TearDown takes him back off the shore, which gives his id back.</summary>
        private IsoCharacterFigureRenderer AshoreFisher()
        {
            IsoCharacterFigureRenderer figure = Fisher();
            var sortGo = new GameObject("HeldRodStowTestsSort");
            _made.Add(sortGo);
            Assert.IsTrue(figure.EnterAshore(sortGo.AddComponent<SpriteRenderer>()), "harness: the fisher did not go ashore");
            return figure;
        }

        /// <summary>Face him <paramref name="heading"/> eighths of a turn.</summary>
        private static void Face(IsoCharacterFigureRenderer figure, int heading) => figure.SetAshoreYaw(heading * 45f);

        private IsoCharacterFigureRenderer Fisher()
        {
            var figure = new GameObject("HeldRodStowTestsFisher").AddComponent<IsoCharacterFigureRenderer>();
            _figures.Add(figure);
            figure.Configure(Load<CharacterSkinDef>(FisherPath));
            figure.Visible = true;
            Assert.IsTrue(figure.IsConfigured, "harness: the fisher figure did not configure");
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
    }
}
