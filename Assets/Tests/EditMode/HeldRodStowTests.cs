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
    /// all eight headings, the grip sits on the right hand's tool socket within the shipped
    /// <see cref="GameConfig.HeldRodGripToleranceMetres"/>.</item>
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
            int socket = figure.SkinDef.IndexOfBone(rod.HeldMeshMountBone);
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
                    Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, use, 0f),
                                  $"'{state}' {frame} heading {heading}: {driver.LastRefusal}");
                    Assert.IsFalse(driver.Stowed, $"'{state}' is an in-hand state, yet the rod is on his back");
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
            IsoCharacterFigureRenderer figure = Fisher();
            HeldRodMesh driver = Driver();
            foreach (string state in rod.CarriedInHandClips)
            {
                Assert.IsTrue(figure.SetPose(state, 0), $"harness: no '{state}'");
                Assert.IsTrue(driver.TryDraw(true, rod, figure, LineWidthPx, HeldRodMesh.Use.Free, 0f), driver.LastRefusal.ToString());
                Assert.IsTrue(driver.Carried);
                Assert.IsFalse(driver.Stowed);
                Vector3 aim = driver.Renderer.transform.localRotation * HeldPropPose.ToolDirection(56f, 16f);
                Assert.Less((aim - HeldPropPose.ToolDirection(rod.CarriedPitchDeg, rod.CarriedYawDeg)).magnitude, 1e-3f,
                            $"'{state}': the carried rod is not at the Def's carried aim");
            }
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
