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
    /// <b>The pail is its facet mesh, hung by its bail from his carry socket</b> (ADR 0053): the bucket
    /// rig's baked state for the fill the sprite would show (the same band and catch group, from the same
    /// catch art table), mounted on the figure's facet so it sorts per pixel against his legs, and swung
    /// back from the way his hand moves. The clam hod has no baked mesh and keeps its sprite.
    ///
    /// <para>Measured on the SHIPPED fisher skin and the shipped carry row, every frame of the three bucket
    /// gaits, both hands, all eight headings.</para>
    /// </summary>
    public sealed class HeldPailMeshTests
    {
        private const string LibraryPath = "Assets/_Project/Data/Art/CatchItemLibrary.asset";
        private const string FisherPath = "Assets/_Project/Data/Characters/Skin/fisher.asset";
        private const string CarryHandsPath = "Assets/_Project/Code/Player/CarryHands.cs";
        private const string BucketDefId = "container.bucket";

        private static readonly float[] Headings = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

        private static readonly MethodInfo s_figureOnDestroy =
            typeof(IsoCharacterFigureRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo s_propOnDestroy =
            typeof(IsoFacetHeldPropRenderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<Object> _made = new List<Object>();
        private readonly List<IsoCharacterFigureRenderer> _figures = new List<IsoCharacterFigureRenderer>();
        private readonly List<HeldCatchMesh> _drivers = new List<HeldCatchMesh>();
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
            _home = new GameObject("HeldPailMeshTestsHome");
            _made.Add(_home);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (HeldCatchMesh d in _drivers)
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
        public void TheShippedBucketRow_NamesThePailTier_AndASwing()
        {
            CharacterCarryPoseDef.Row row = BucketRow();
            Assert.AreEqual("pail", row.MeshTier, "the bucket row must name the bucket rig's pail tier");
            Assert.Greater(row.MeshSwingDegPerMetrePerSecond, 0f);
            Assert.Greater(row.MeshSwingMaxDeg, 0f);
            Assert.IsTrue(_config.MeshPailInHand, "the pail mesh ships switched on");
        }

        [Test]
        public void TheShippedLibrary_HoldsEveryPailState_HungFromItsBail()
        {
            CatchItemLibrary library = Load<CatchItemLibrary>(LibraryPath);
            var states = new List<HeldPropMeshDef> { library.BucketMesh("pail", CatchFillBand.Empty, null) };
            foreach (CatchFillBand band in CatchFillMath.FilledBands)
                foreach (string group in CatchFillMath.BucketGroups)
                    states.Add(library.BucketMesh("pail", band, group));
            Assert.AreEqual(13, states.Count);
            foreach (HeldPropMeshDef def in states)
            {
                Assert.IsNotNull(def, "a pail state has no baked mesh");
                Assert.AreEqual(HeldPropMeshDef.BucketFamily, def.Family, def.Id);
                Assert.IsTrue(def.IsUsable(), $"{def.Id} is not usable");
                Assert.IsTrue(IsoFacetHeldPropMesh.TryBuild(def, new IsoFacetHeldPropMesh.Options { Scale = 1f },
                                                            out var built, out string why), why);
                Mesh mesh = built.Mesh;
                _made.Add(mesh);
                float top = float.MinValue;
                foreach (Vector3 v in mesh.vertices) top = Mathf.Max(top, v.z - def.GripLocalMeters.z);
                Assert.LessOrEqual(top, _config.HeldShovelGroundToleranceMetres,
                                   $"{def.Id}: the pail rises {top:0.000} m above its bail");
            }
        }

        [Test]
        public void ThePresenter_HandsOutTheMeshForTheFillItShows()
        {
            (CarriableBucket bucket, BucketFillPresenter presenter) = Pail(Load<CatchItemLibrary>(LibraryPath));
            CatchItemLibrary library = presenter.Library;

            Assert.AreSame(library.BucketMesh("pail", CatchFillBand.Empty, null), bucket.HeldMeshFor("pail"));

            // HeldMeshFor settles a pending fill first, so the band is read after it.
            presenter.SetContents(new[] { "cod" }, 0.05f);
            HeldPropMeshDef few = bucket.HeldMeshFor("pail");
            Assert.AreEqual(CatchFillBand.Few, presenter.Band);
            Assert.AreSame(library.BucketMesh("pail", CatchFillBand.Few, "fish"), few);

            presenter.SetContents(new[] { "lobster", "lobster", "cod" }, 1f);
            HeldPropMeshDef brim = bucket.HeldMeshFor("pail");
            Assert.AreEqual(CatchFillBand.Brim, presenter.Band);
            Assert.AreEqual("crust", presenter.Group);
            Assert.AreSame(library.BucketMesh("pail", CatchFillBand.Brim, "crust"), brim);

            presenter.SetContents(new[] { "cod" }, 0.5f);
            HeldPropMeshDef changed = bucket.HeldMeshFor("pail");
            Assert.AreNotEqual(CatchFillBand.Brim, presenter.Band);
            Assert.AreSame(library.BucketMesh("pail", presenter.Band, "fish"), changed,
                           "a changed fill must not hand out the last state's mesh");

            Assert.IsNull(bucket.HeldMeshFor("no_such_tier"), "an unknown tier keeps the sprite");
        }

        [Test]
        public void ABucketWithoutAPailPresenter_TheHod_HasNoMesh()
        {
            var go = new GameObject("Hod");
            _made.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            var bucket = go.AddComponent<CarriableBucket>();
            bucket.Configure(10, sr, "container.test.hod", BucketDefId);
            Assert.IsNull(bucket.HeldMeshFor("pail"), "the hod has no baked mesh and must keep its sprite");
        }

        [Test]
        public void DrawnElsewhere_HidesTheSprite_AndPlacingItDownShowsItAgain()
        {
            (CarriableBucket bucket, _) = Pail(Load<CatchItemLibrary>(LibraryPath));
            var sr = bucket.GetComponent<SpriteRenderer>();
            bucket.SetDrawnElsewhere(true);
            Assert.IsTrue(sr.forceRenderingOff);
            Assert.IsTrue(bucket.DrawnElsewhere);
            bucket.OnPlaced();
            Assert.IsFalse(sr.forceRenderingOff, "a pail set down must draw its own sprite again");
        }

        // ================================================================ hung from the socket, every frame

        [Test]
        public void TheBucketGaits_EveryFrameBothHandsEveryHeading_TheBailIsOnTheCarrySocket()
        {
            CharacterCarryPoseDef.Row row = BucketRow();
            HeldPropMeshDef def = Load<CatchItemLibrary>(LibraryPath).BucketMesh("pail", CatchFillBand.Half, "fish");
            IsoCharacterFigureRenderer figure = Fisher();
            HeldCatchMesh driver = Driver();
            var options = Options(def);
            float tolerance = _config.HeldRodGripToleranceMetres;
            float ground = _config.HeldShovelGroundToleranceMetres;
            var failures = new List<string>();
            int checkedFrames = 0;

            foreach (string state in BucketGaits(figure.SkinDef, row.Carry))
            {
                Assert.IsTrue(figure.SkinDef.TryGetClip(state, out CharacterSkinDef.SkinClip clip));
                foreach (CarryHandSide hand in new[] { CarryHandSide.Left, CarryHandSide.Right })
                {
                    string socket = row.SocketFor(hand);
                    int bone = figure.SkinDef.IndexOfBone(socket);
                    Assert.AreNotEqual(CharacterSkinDef.NoBone, bone, $"the fisher has no '{socket}'");
                    var world = new Matrix4x4[figure.SkinDef.BoneCount];
                    for (int f = 0; f < clip.FrameCount; f++)
                    {
                        Assert.IsTrue(figure.SetPose(state, f));
                        Assert.IsTrue(CarryMath.TryClipSocketVelocity(clip, figure.SkinDef.Bones, bone, figure.DrawnFrame,
                                                                      world, out Vector3 v));
                        Quaternion swing = CarryMath.HungSwing(v, row.MeshSwingDegPerMetrePerSecond, row.MeshSwingMaxDeg);
                        Assert.IsTrue(driver.TryDrawHung(true, def, options, socket, swing, figure),
                                      $"'{state}' {f} {hand}: {driver.LastRefusal}");
                        IsoFacetHeldPropRenderer r = driver.Renderer;
                        Assert.AreSame(figure.FacetTransform, r.transform.parent,
                                       "the pail must ride the figure's facet child to sort per pixel with him");
                        Assert.IsTrue(figure.TryBonePoint(bone, out Vector3 grip));
                        Vector3 bail = r.transform.localPosition + r.transform.localRotation * def.GripLocalMeters;
                        foreach (float heading in Headings)
                        {
                            Matrix4x4 m = FacetFrame(figure.SkinDef, heading).localToWorldMatrix;
                            float off = (m.MultiplyPoint3x4(r.transform.localPosition) - m.MultiplyPoint3x4(grip)).magnitude;
                            if (off > tolerance) failures.Add($"'{state}' {f} {hand} heading {heading}: bail {off:0.000} m off {socket}");
                        }
                        if ((bail - grip).magnitude > tolerance && (r.transform.localPosition - grip).magnitude > tolerance)
                            failures.Add($"'{state}' {f} {hand}: the pail is not hung at its bail");
                        float low = float.MaxValue, high = float.MinValue;
                        foreach (Vector3 p in r.DrawnMesh.vertices)
                        {
                            float z = (r.transform.localPosition + r.transform.localRotation * p).z;
                            low = Mathf.Min(low, z);
                            high = Mathf.Max(high, z);
                        }
                        if (high > grip.z + ground) failures.Add($"'{state}' {f} {hand}: the pail rises {high - grip.z:0.000} m above the hand");
                        if (low < -ground) failures.Add($"'{state}' {f} {hand}: the pail is {-low:0.000} m into the ground");
                        checkedFrames++;
                    }
                }
            }
            Assert.Greater(checkedFrames, 0, "harness: no bucket gait frames");
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void TheSwitchOff_OrNoMesh_HidesThePail_AndGivesBackItsReach()
        {
            HeldPropMeshDef def = Load<CatchItemLibrary>(LibraryPath).BucketMesh("pail", CatchFillBand.Empty, null);
            IsoCharacterFigureRenderer figure = Fisher();
            HeldCatchMesh driver = Driver();
            Assert.IsTrue(figure.SetPose("idle_buckets", 0));
            Assert.IsTrue(driver.TryDrawHung(true, def, Options(def), "carry_R", Quaternion.identity, figure),
                          driver.LastRefusal.ToString());
            Assert.Greater(figure.HeldReachMetres, 0f);
            Assert.IsFalse(driver.TryDrawHung(false, def, Options(def), "carry_R", Quaternion.identity, figure));
            Assert.AreEqual(HeldCatchMesh.Refusal.SwitchOff, driver.LastRefusal);
            Assert.IsFalse(driver.Renderer.Visible);
            Assert.AreEqual(0f, figure.HeldReachMetres);
            Assert.IsFalse(driver.TryDrawHung(true, null, Options(def), "carry_R", Quaternion.identity, figure));
            Assert.AreEqual(HeldCatchMesh.Refusal.NoMesh, driver.LastRefusal);
        }

        // ================================================================ the swing

        [Test]
        public void TheSwing_LagsTheHand_AndIsClamped()
        {
            Quaternion q = CarryMath.HungSwing(new Vector3(0.5f, 0f, 0f), 12f, 10f);
            Vector3 hangs = q * Vector3.back;   // the hanging end, -z in the figure frame
            Assert.Less(hangs.x, 0f, "the hanging end must trail the hand");
            Assert.AreEqual(6f, Quaternion.Angle(Quaternion.identity, q), 1e-3f);

            Assert.AreEqual(10f, Quaternion.Angle(Quaternion.identity, CarryMath.HungSwing(new Vector3(0f, 5f, 0f), 12f, 10f)), 1e-3f);
            Assert.AreEqual(Quaternion.identity, CarryMath.HungSwing(new Vector3(0f, 0f, 3f), 12f, 10f), "vertical motion swings nothing");
            Assert.AreEqual(Quaternion.identity, CarryMath.HungSwing(new Vector3(1f, 0f, 0f), 0f, 10f));
            Assert.AreEqual(Quaternion.identity, CarryMath.HungSwing(new Vector3(1f, 0f, 0f), 12f, 0f));
        }

        [Test]
        public void TheClipSocketVelocity_IsAFunctionOfTheFrame()
        {
            CharacterSkinDef fisher = Load<CharacterSkinDef>(FisherPath);
            Assert.IsTrue(fisher.TryGetClip("walk_buckets", out CharacterSkinDef.SkinClip clip));
            int bone = fisher.IndexOfBone("carry_R");
            var world = new Matrix4x4[fisher.BoneCount];
            bool moved = false;
            for (int f = 0; f < clip.FrameCount; f++)
            {
                Assert.IsTrue(CarryMath.TryClipSocketVelocity(clip, fisher.Bones, bone, f, world, out Vector3 a));
                Assert.IsTrue(CarryMath.TryClipSocketVelocity(clip, fisher.Bones, bone, f, world, out Vector3 b));
                Assert.AreEqual(a, b, $"frame {f}: the same frame gave two velocities");
                moved |= a.sqrMagnitude > 1e-8f;
            }
            Assert.IsTrue(moved, "the hand never moves on walk_buckets: the pail would never swing");
            Assert.IsFalse(CarryMath.TryClipSocketVelocity(clip, fisher.Bones, -1, 0, world, out _));
            Assert.IsFalse(CarryMath.TryClipSocketVelocity(clip, fisher.Bones, bone, 0, new Matrix4x4[1], out _));
        }

        // ================================================================ the carrier's wiring

        [Test]
        public void TheCarrier_HidesThePailSprite_ExactlyWhenTheMeshDraws()
        {
            // The carrier needs a live mesh figure presenter to run a frame, so its wiring is pinned in source.
            string src = File.ReadAllText(CarryHandsPath).Replace("\r\n", "\n");
            StringAssert.Contains("StatePailMesh(carried, row, socket, socketBone, figure);", src);
            StringAssert.Contains("bool drawn = _pailMesh.TryDrawHung(on, def, options, socket, swing, figure);\n            bucket.SetDrawnElsewhere(drawn);", src);
            StringAssert.Contains("HeldPropMeshDef def = on ? bucket.HeldMeshFor(row.MeshTier) : null;", src);
            StringAssert.Contains("new HeldCatchMesh(transform, \"HeldPailMesh\", IsoCharacterFigureRenderer.HeldReachCarry)", src);
        }

        // ================================================================ harness

        private static List<string> BucketGaits(CharacterSkinDef skin, string carry)
        {
            var found = new List<string>();
            foreach (string gait in new[] { "idle", "walk", "run" })
                if (skin.TryGetClip($"{gait}_{carry}", out _)) found.Add($"{gait}_{carry}");
            CollectionAssert.Contains(found, $"idle_{carry}", "the fisher has no idle bucket carry");
            return found;
        }

        private IsoFacetHeldPropMesh.Options Options(HeldPropMeshDef def) => new IsoFacetHeldPropMesh.Options
        {
            Scale = 1f,
            LineWidthMeters = def != null && def.PxPerMetre > 0 ? _config.HeldPropLineWidthPx / def.PxPerMetre : 0f,
        };

        private static CharacterCarryPoseDef.Row BucketRow()
        {
            var poses = Resources.Load<CharacterCarryPoseDef>(CharacterCarryPoseDef.ResourcesPath);
            Assert.IsNotNull(poses, "harness: no carry pose table");
            Assert.IsTrue(poses.TryRow(BucketDefId, out CharacterCarryPoseDef.Row row), "no bucket carry row");
            return row;
        }

        private (CarriableBucket, BucketFillPresenter) Pail(CatchItemLibrary library)
        {
            var go = new GameObject("Pail");
            _made.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            var presenter = go.AddComponent<BucketFillPresenter>();
            presenter.Configure(library, null, null);
            var bucket = go.AddComponent<CarriableBucket>();
            bucket.Configure(20, sr, "container.test.pail", BucketDefId);
            return (bucket, presenter);
        }

        private Transform FacetFrame(CharacterSkinDef skin, float headingDeg)
        {
            var root = new GameObject("HeldPailTest_Root");
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
            var figure = new GameObject("HeldPailMeshTestsFisher").AddComponent<IsoCharacterFigureRenderer>();
            _figures.Add(figure);
            figure.Configure(Load<CharacterSkinDef>(FisherPath));
            figure.Visible = true;
            Assert.IsTrue(figure.IsConfigured, "harness: the fisher figure did not configure");
            Assert.IsNotNull(figure.FacetTransform, "harness: the fisher figure has no facet child");
            return figure;
        }

        private HeldCatchMesh Driver()
        {
            var d = new HeldCatchMesh(_home.transform, "HeldPailMesh", IsoCharacterFigureRenderer.HeldReachCarry);
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
