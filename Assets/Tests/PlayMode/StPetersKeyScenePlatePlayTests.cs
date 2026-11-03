using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.App;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // Shares the existing GPU/title-only camera harness and its complete teardown.
    public partial class WharfBuildingPass2PlatePlayTests
    {
        readonly struct KeyFrame
        {
            public readonly string Id;
            public readonly Vector2 Centre;
            public readonly float Height;
            public KeyFrame(string id, float x, float y, float height)
            { Id = id; Centre = new Vector2(x, y); Height = height; }
        }

        [UnityTest]
        public IEnumerator StPeters_Wave1b_LandingCanneryBenchAndEastEnd_ThroughTheGameCamera()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            EnsureTheFollow();
            _look = "wave1b";
            _lookEvidence = "key scenes, ground XY centres from CD's scene frames; independent props retain their game XY (ADR 0042)";
            HoldTheMood();
            yield return PinTheHour(Noon, ClearRunSeconds);
            string captions = StartFile("captions.txt");
            string numbers = StartFile("numbers.txt");
            var frames = new[]
            {
                new KeyFrame("landing.of_road", 181.5f, -1.5f, 9f),
                new KeyFrame("landing.of_deck", 199f, -1.6f, 9f),
                new KeyFrame("landing.of_disembark", 211.5f, -1.9f, 9f),
                new KeyFrame("cannery.of_door", 176.3f, 8.2f, 9f),
                new KeyFrame("cannery.of_shingle", 174.5f, 30f, 9f),
                new KeyFrame("cannery.wide_landing", 181f, 8f, 14f),
                new KeyFrame("whelps_lookout.of_bench", 166.9f, -39.3f, 9f),
                new KeyFrame("east_end.loft_corner", 183.2f, 23.8f, 14f),
                new KeyFrame("east_end.look_pass", 178f, 14f, 33.75f),
            };
            var newPieces = Object.FindObjectsByType<SpriteRenderer>().Where(r =>
                r.transform.parent != null && r.transform.parent.name.StartsWith("keyscene.stp_")).ToList();
            Assert.Greater(newPieces.Count, 20, "The regenerated key-scene root is missing.");
            File.AppendAllText(numbers, Census("wave 1b placed sheets", newPieces), Utf8);
            foreach (var frame in frames)
            {
                int zoom = CameraZoomPolicy.StepForWorldHeight(frame.Height, CameraFollow.AssetsPPU, CameraFollow.DesignScreenHeightPx);
                float height = CameraZoomPolicy.WorldHeightForStep(zoom, CameraFollow.AssetsPPU, CameraFollow.DesignScreenHeightPx);
                float pixel = CameraZoomPolicy.WorldUnitsPerRenderedPixel(zoom, CameraFollow.AssetsPPU, CameraFollow.DesignScreenHeightPx);
                Vector2 centre = new Vector2(Mathf.Round(frame.Centre.x / pixel) * pixel, Mathf.Round(frame.Centre.y / pixel) * pixel);
                yield return FrameOn(centre, height);
                yield return RunTheWorld(ClearRunSeconds, centre);
                yield return ShadersReady();
                SavePlate(frame.Id + ".png", Capture());
                File.AppendAllText(captions, Caption(frame.Id, "wave 1b", centre, ""), Utf8);
                File.AppendAllText(numbers, $"{frame.Id}: CD requested height={frame.Height} game ladder height={height} centre={centre.x:R},{centre.y:R}\n", Utf8);
                using var draws = OfferedCounter("Draw Calls Count");
                using var batches = OfferedCounter("Batches Count");
                using var sets = OfferedCounter("SetPass Calls Count");
                for (int i = 0; i < 5; i++) yield return null;
                long calls = draws.Valid ? draws.LastValue : -1;
                long passes = sets.Valid ? sets.LastValue : -1;
                string counterSource = "ProfilerRecorder";
#if UNITY_EDITOR
                if (calls <= 0)
                {
                    calls = UnityEditor.UnityStats.drawCalls;
                    passes = UnityEditor.UnityStats.setPassCalls;
                    counterSource = "UnityStats (editor Game view)";
                }
#endif
                Assert.Greater(calls, 0, "No rendered draw calls; this is not GPU evidence.");
                File.AppendAllText(numbers, $"{frame.Id}: drawCalls={calls} batches={(batches.Valid ? batches.LastValue : -1)} setPass={passes} source={counterSource}\n", Utf8);
            }
            foreach (GullPerch perch in Object.FindObjectsByType<GullPerch>())
                File.AppendAllText(numbers, $"perch {perch.Id}: ground={perch.GroundPoint} screen={perch.ScreenPoint} sortY={perch.SortY}\n", Utf8);
            // Village_stPetersCannery.json gives this ridge pixel in every cell, measured from top-left.
            // Keep CD's gull coordinates: this records their relation to the baked ridge, not a relocation.
            SpriteRenderer cannery = GameObject.Find("StPetersCannery").GetComponent<SpriteRenderer>();
            Sprite roof = cannery.sprite;
            Assert.AreEqual(new Vector2(892, 681), roof.rect.size, "Re-read the ridge anchor when the sheet changes.");
            Vector2 ridgeLocal = (new Vector2(446f, roof.rect.height - 169.8f) - roof.pivot) / roof.pixelsPerUnit;
            Vector2 ridgeWorld = cannery.transform.TransformPoint(ridgeLocal);
            GullPerch gullA = Object.FindObjectsByType<GullPerch>().Single(p => p.Id == "prop.stp_cannery_gull_a");
            Vector2 ridgeDelta = gullA.ScreenPoint - ridgeWorld;
            File.AppendAllText(numbers, $"gull_a vs baked ridge: ridgeWorld={ridgeWorld:F4} perchWorld={gullA.ScreenPoint:F4} " +
                $"deltaWorld={ridgeDelta:F4} deltaSheetPixels={ridgeDelta * roof.pixelsPerUnit:F3}\n", Utf8);
            Debug.Log("[O3Wave1b] PLATES_COMPLETE " + PlatePath());
        }

        static ProfilerRecorder OfferedCounter(string name)
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.Name == name) return ProfilerRecorder.StartNew(description.Category, name);
            }
            return default;
        }

#if UNITY_EDITOR
        sealed class LandingBoardTide : IEnvironmentService
        {
            readonly IEnvironmentService _inner;
            readonly float _level;
            public LandingBoardTide(IEnvironmentService inner, float level) { _inner = inner; _level = level; }
            public int WorldSeed => _inner.WorldSeed;
            public TideProfile ActiveTideProfile { get => _inner.ActiveTideProfile; set => _inner.ActiveTideProfile = value; }
            public EnvironmentSample Sample()
            {
                var sample = _inner.Sample();
                return new EnvironmentSample(sample.WindVector, sample.CurrentVector, _level,
                    sample.SeaState, sample.Visibility, sample.SeaState01);
            }
            public float TideHeightAt(double seconds) => _level;
            public float WaterLevelAt(double seconds) => _level;
        }

        [UnityTest]
        public IEnumerator StPeters_LandingPassTwo_AtBothBoardHoursAndPinnedTides_OffBesideOn()
        {
            RequireAGraphicsDevice();
            string baseline = Path.GetFullPath("Evidence~/stp-kits/k0/plates");
            if (!File.Exists(Path.Combine(baseline, "landing.wide_approach.1431-off.png")))
                Assert.Ignore("The recorded atlas-deck OFF frames are required for this local comparison.");
            R17RunScopedContracts();
            yield return LoadRegion(StPeters);
            EnsureTheFollow(); HoldTheMood();
            _look = "k0-dock";
            _lookEvidence = "measured picture floor, derived placements, retained-height sea clip and local north pocket";
            string captions = StartFile("captions.txt"), numbers = StartFile("numbers.txt");
            var wharf = GameObject.Find("StPetersWharf");
            Assert.IsNotNull(wharf);
            var floor = wharf.GetComponent<StandablePlatform>();
            var migrated = wharf.transform.Find("Deck/WharfPass2Deck").GetComponent<SpriteRenderer>();
            if (!System.Environment.GetCommandLineArgs().Contains("-stpR18")) R17RecordPlantBeforeAndAfter(migrated);
            var face = migrated.GetComponent<HiddenHarbours.Art.TidalFaceWaterline>();
            Assert.IsNotNull(face);
            Assert.IsTrue(face.IsConfigured);
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            Vector3 originalPlayer = player.transform.position;
            var body = player.GetComponent<Rigidbody2D>();
            IEnvironmentService held = GameServices.Environment;
            bool followEnabled = _follow.enabled;
            float previousAspect = _cam.aspect;
            var pixelCameras = _cam.GetComponents<Behaviour>().Where(b => b.GetType().Name == "PixelPerfectCamera" && b.enabled).ToArray();
            var boards = new[]
            {
                new { Id = "landing.wide_approach.1431", Day = 29, Minute = 871, Tide = 2.18f, X = 203f, Y = 2.649f },
                new { Id = "cannery.wide_landing.0600", Day = 39, Minute = 360, Tide = -0.85f, X = 181f, Y = 12.596f },
                new { Id = "landing.wide_approach.noon", Day = 29, Minute = 720, Tide = 2.18f, X = 203f, Y = 2.649f },
                new { Id = "landing.wide_approach.1730", Day = 29, Minute = 1050, Tide = 2.18f, X = 203f, Y = 2.649f },
                new { Id = "cannery.wide_landing.noon", Day = 39, Minute = 720, Tide = -0.85f, X = 181f, Y = 12.596f },
                new { Id = "cannery.wide_landing.1730", Day = 39, Minute = 1050, Tide = -0.85f, X = 181f, Y = 12.596f },
            };
            IEnumerator HoldTide(float level)
            {
                GameServices.Environment = new LandingBoardTide(held, level);
                Time.timeScale = 1f;
                float until = Time.realtimeSinceStartup + 20f;
                do { yield return null; }
                while (Time.realtimeSinceStartup < until &&
                    (Shader.GetGlobalVector("_HHSeaLevelWorld").w < .5f || Mathf.Abs(Shader.GetGlobalVector("_HHSeaLevelWorld").x - level) > .01f));
                Assert.That(Shader.GetGlobalVector("_HHSeaLevelWorld").x, Is.EqualTo(level).Within(.01f));
                Time.timeScale = 0f;
            }
            void CameraAt(Vector2 at, bool boardAspect = false)
            {
                _follow.enabled = false;
                foreach (var ppc in pixelCameras) ppc.enabled = false;
                if (_rt == null || _rt.width != 796 || _rt.height != 448)
                {
                    if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }
                    _w = 796; _h = 448;
                    _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf); _rt.Create();
                }
                _cam.targetTexture = _rt;
                _cam.transform.position = new Vector3(at.x, at.y, _cam.transform.position.z);
                _cam.orthographicSize = 7f;
                _cam.aspect = boardAspect ? 24.89f / 14f : 796f / 448f;
            }
            void Pose(Vector2 at)
            {
                player.transform.position = new Vector3(at.x, at.y, 0);
                if (body != null) { body.position = at; body.linearVelocity = Vector2.zero; }
            }
            void Numbers(string id)
            {
                var dory = GameObject.Find("Dory");
                var ride = dory.GetComponent<HiddenHarbours.Boats.HullTideRide>();
                float sea = Shader.GetGlobalVector("_HHSeaLevelWorld").x;
                var image = HiddenHarbours.App.Editor.TerrainPlanPng.Read(File.ReadAllBytes(HiddenHarbours.App.Editor.StPetersLandingLayout.PicturePath));
                var heights = HiddenHarbours.App.Editor.TerrainPlanPng.Read(File.ReadAllBytes(HiddenHarbours.App.Editor.StPetersLandingLayout.HeightPath));
                byte[] alpha = image.Channel8(3), h = heights.Channel8(0);
                var contract = HiddenHarbours.App.Editor.StPetersLandingLayout.Picture;
                float lowest = float.PositiveInfinity, visible = float.PositiveInfinity;
                for (int row = 0; row < image.Height; row++)
                for (int x = 0; x < image.Width; x++)
                {
                    int i = row * image.Width + x;
                    if (alpha[i] == 0) continue;
                    float y = contract.originY + (contract.pivotY - row - .5f) / contract.pixelsPerUnit;
                    lowest = Mathf.Min(lowest, y);
                    float z = HiddenHarbours.Art.TidalFaceWaterline.DecodeHeight(h[i] / 255f, contract.heightDecode);
                    if (z >= sea) visible = Mathf.Min(visible, y);
                }
                File.AppendAllText(numbers, $"{id}: floor={floor.Footprint} elevation={floor.DeckElevation:R}; " +
                    $"drawn lips={HiddenHarbours.App.Editor.StPetersWharf.MooringFaceY:R},{HiddenHarbours.App.Editor.StPetersWharf.NorthFaceY:R}; " +
                    $"sea={sea:R}; lowestPileInkY={lowest:R} lowestAboveSeaPileInkY={visible:R}; doryPlan={dory.transform.position} " +
                    $"doryPictureRootY={dory.transform.position.y + ride.ScreenRiseNow():R}; playerFoot={player.transform.position}; shell={ShellFlow.Phase}\n", Utf8);
            }
            var railFrames = new Dictionary<string, Vector3>();
            IEnumerator AlongsideRailPlates(GameObject vessel, HiddenHarbours.Boats.BoatHullDef hullDef, string kind)
            {
                var ride = vessel.GetComponent<HiddenHarbours.Boats.HullTideRide>();
                var facet = vessel.GetComponentInChildren<HiddenHarbours.Art.IsoFacetHullRenderer>(true);
                Assert.IsNotNull(ride); Assert.IsNotNull(facet); Assert.IsTrue(facet.IsConfigured);
                Vector3 oldPosition = vessel.transform.position;
                float oldDatum = ride.BakedWaterlineElevation;
                var vesselBody = vessel.GetComponent<Rigidbody2D>();
                Vector2 oldVelocity = vesselBody != null ? vesselBody.linearVelocity : Vector2.zero;
                float halfBeam = hullDef.Visual.HullMesh.WatertightHalfBeamMeters;
                float lip = HiddenHarbours.App.Editor.StPetersWharf.MooringFaceY;
                float rootY = lip - HiddenHarbours.App.Editor.StPetersBuilder.AlongsideFenderGapMetres - halfBeam;
                var at = new Vector3(HiddenHarbours.App.Editor.StPetersWharf.LadderPosition().x, rootY, oldPosition.z);
                try
                {
                    vessel.transform.position = at;
                    if (vesselBody != null) { vesselBody.position = at; vesselBody.linearVelocity = Vector2.zero; }
                    // Read the committed scene datum; positioning this measurement hull does not rebase its ride.
                    Assert.That(ride.BakedWaterlineElevation, Is.EqualTo(HiddenHarbours.App.Editor.StPetersLandingLayout.DatumElevation));
                    foreach (float level in new[] { -2.2f, 2.2f })
                    {
                        yield return HoldTide(level);
                        string id = kind + (level < 0 ? "-low" : "-high");
                        var posed = facet.PosedMesh;
                        var mesh = posed.GetComponent<MeshFilter>().sharedMesh;
                        var bounds = posed.GetComponent<MeshRenderer>().bounds;
                        string frameKey = kind + (level < 0 ? "-low" : "-high");
                        if (!railFrames.TryGetValue(frameKey, out Vector3 lens))
                        {
                            float bottom = Mathf.Min(bounds.min.y, migrated.bounds.min.y);
                            float top = Mathf.Max(bounds.max.y, lip);
                            lens = new Vector3(at.x, .5f * (bottom + top), Mathf.Max(7f, .5f * (top - bottom) + 1f));
                            railFrames.Add(frameKey, lens);
                        }
                        CameraAt(new Vector2(lens.x, lens.y));
                        _cam.orthographicSize = lens.z;
                        yield return null;
                        var vertices = mesh.vertices;
                        var rows = new System.Text.StringBuilder("index,rigX,rigY,rigZ,worldX,worldY,pixelX,pixelY\n");
                        int railIndex = -1; Vector3 rail = Vector3.zero;
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            Vector3 v = vertices[i], world = posed.TransformPoint(v);
                            Vector3 pixel = _cam.WorldToViewportPoint(world);
                            rows.AppendLine(System.FormattableString.Invariant($"{i},{v.x:R},{v.y:R},{v.z:R},{world.x:R},{world.y:R},{pixel.x * 796:R},{(1 - pixel.y) * 448:R}"));
                            if (Mathf.Abs(v.x) >= .8f * halfBeam && Mathf.Abs(v.y) <= hullDef.LengthMeters * .25f &&
                                (railIndex < 0 || world.y > rail.y)) { railIndex = i; rail = world; }
                        }
                        Assert.GreaterOrEqual(railIndex, 0, "the midship outer hull has a north-rail sample");
                        File.WriteAllText(Path.Combine(PlatePath(), "alongside-" + id + "-vertices.csv"), rows.ToString(), Utf8);
                        Vector3 railPixel = _cam.WorldToViewportPoint(rail);
                        Vector3 lipPixel = _cam.WorldToViewportPoint(new Vector3(rail.x, lip, 0));
                        float draft = hullDef.Visual.ResolveDesignWaterlineMeters();
                        Vector3 waterA = posed.TransformPoint(new Vector3(halfBeam, 0, draft));
                        Vector3 waterB = posed.TransformPoint(new Vector3(-halfBeam, 0, draft));
                        Vector3 northWater = waterA.y >= waterB.y ? waterA : waterB;
                        Vector3 waterPixel = _cam.WorldToViewportPoint(northWater);
                        Vector3 lo = _cam.WorldToViewportPoint(bounds.min), hi = _cam.WorldToViewportPoint(bounds.max);
                        Assert.That(lo.x, Is.InRange(0f, 1f)); Assert.That(hi.x, Is.InRange(0f, 1f));
                        Assert.That(lo.y, Is.InRange(0f, 1f)); Assert.That(hi.y, Is.InRange(0f, 1f));
                        SavePlate("alongside-" + id + ".png", Capture());
                        File.AppendAllText(numbers, System.FormattableString.Invariant($"alongside-{id}: sea={level:R} waterline={ride.WaterlineNow():R} rootY={rootY:R} lipY={lip:R} halfBeam={halfBeam:R} fender={HiddenHarbours.App.Editor.StPetersBuilder.AlongsideFenderGapMetres:R} datum={ride.BakedWaterlineElevation:R} datumElevation={HiddenHarbours.App.Editor.StPetersLandingLayout.DatumElevation:R} heightScale={IsoGround.HeightScale:R} pictureRootY={rootY + ride.ScreenRiseNow():R} railVertex={railIndex} railX={rail.x:R} railY={rail.y:R} railPixelX={railPixel.x * 796:R} railPixelY={(1 - railPixel.y) * 448:R} waterlineY={northWater.y:R} waterPixelX={waterPixel.x * 796:R} waterPixelY={(1 - waterPixel.y) * 448:R} climbImageMetres={lip - rail.y:R} climbVerticalEquivalentMetres={(lip - rail.y) / IsoGround.HeightScale:R} lipPixelY={(1 - lipPixel.y) * 448:R} cameraX={_cam.transform.position.x:R} cameraY={_cam.transform.position.y:R} cameraHeight={2 * _cam.orthographicSize:R} cameraAspect={_cam.aspect:R} hullMinX={bounds.min.x:R} hullMinY={bounds.min.y:R} hullMaxX={bounds.max.x:R} hullMaxY={bounds.max.y:R}\n"), Utf8);
                        File.AppendAllText(captions, $"alongside-{id}: True height: the committed scene datum is read without a fixture override. The north dory is temporarily sampled alongside and restored; the cape reads the scene arrival datum. Root Y and its half-beam/fender separation remain in the picture. North-rail marker is the northmost posed outer-hull vertex in the middle half of the hull; all vertices are recorded.\n", Utf8);
                    }
                }
                finally
                {
                    vessel.transform.position = oldPosition;
                    if (vesselBody != null) { vesselBody.position = oldPosition; vesselBody.linearVelocity = oldVelocity; }
                    ride.Configure(oldDatum);
                }
            }
            try
            {
                Pose(HiddenHarbours.App.Editor.StPetersBuilder.DisembarkPos);
                foreach (var board in boards)
                {
                    yield return PinTheHour(board.Minute / 60f, ClearRunSeconds);
                    GameServices.Clock.SeekTo((board.Day + board.Minute / 1440.0) * GameServices.Config.SecondsPerDay);
                    GameServices.Clock.TimeScale = 0;
                    _follow.enabled = followEnabled;
                    foreach (var ppc in pixelCameras) ppc.enabled = true;
                    _cam.targetTexture = null; _cam.ResetAspect();
                    if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
                    int step = CameraZoomPolicy.StepForWorldHeight(14, CameraFollow.AssetsPPU, CameraFollow.DesignScreenHeightPx);
                    float setupHeight = CameraZoomPolicy.WorldHeightForStep(step, CameraFollow.AssetsPPU, CameraFollow.DesignScreenHeightPx);
                    float setupPixel = CameraZoomPolicy.WorldUnitsPerRenderedPixel(step, CameraFollow.AssetsPPU, CameraFollow.DesignScreenHeightPx);
                    Vector2 setupCentre = new Vector2(Mathf.Round(board.X / setupPixel) * setupPixel, Mathf.Round(board.Y / setupPixel) * setupPixel);
                    yield return FrameOn(setupCentre, setupHeight);
                    yield return RunTheWorld(2f, setupCentre); // retain the shared millimetre alignment guard
                    yield return HoldTide(board.Tide);
                    Assert.That(GameServices.Clock.DayIndex, Is.EqualTo(board.Day));
                    Assert.That(GameServices.Clock.HourOfDay, Is.EqualTo(board.Minute / 60f).Within(.001f));
                    yield return ShadersReady();
                    CameraAt(new Vector2(board.X, board.Y), true);
                    yield return null;
                    SavePlate(board.Id + "-on.png", Capture());
                    File.Copy(Path.Combine(baseline, board.Id + "-off.png"), Path.Combine(PlatePath(), board.Id + "-off.png"), true);
                    File.AppendAllText(captions, $"{board.Id}: ON current dock, day {board.Day}, minute {board.Minute}, tide {board.Tide:R}, " +
                        $"centre {board.X:R},{board.Y:R}, 796x448, height 14, width 24.89; OFF is the unchanged recorded atlas-deck capture from the earlier lease at the same frame/hour/tide.\n", Utf8);
                    Numbers(board.Id);
                    using var draws = OfferedCounter("Draw Calls Count");
                    for (int f = 0; f < 5; f++) yield return null;
                    long calls = draws.Valid && draws.LastValue > 0 ? draws.LastValue : UnityEditor.UnityStats.drawCalls;
                    Assert.Greater(calls, 0);
                    long albedoBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(migrated.sprite.texture);
                    var heightTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(HiddenHarbours.App.Editor.StPetersLandingLayout.HeightPath);
                    long heightBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(heightTexture);
                    File.AppendAllText(numbers, $"{board.Id}: on drawCalls={calls} deckAlbedoTextureBytes={albedoBytes} deckHeightTextureBytes={heightBytes} deckTotalTextureBytes={albedoBytes + heightBytes}\n", Utf8);
                }
                yield return PinTheHour(12, ClearRunSeconds);
                GameServices.Clock.TimeScale = 0;
                yield return HoldTide(2.2f);
                Rect r = floor.Footprint;
                Pose(r.center); CameraAt(r.center); yield return null;
                if (!System.Environment.GetCommandLineArgs().Contains("-stpR18")) yield return R17FloorShadowProof(migrated);
                var poses = new[] {
                    new {Name="corner-sw", At=new Vector2(r.xMin+.1f,r.yMin+.1f)}, new {Name="corner-se", At=new Vector2(r.xMax-.1f,r.yMin+.1f)},
                    new {Name="corner-nw", At=new Vector2(r.xMin+.1f,r.yMax-.1f)}, new {Name="corner-ne", At=new Vector2(r.xMax-.1f,r.yMax-.1f)},
                    new {Name="middle", At=r.center}, new {Name="edge-west", At=new Vector2(r.xMin+.1f,r.center.y)},
                    new {Name="edge-east", At=new Vector2(r.xMax-.1f,r.center.y)}, new {Name="edge-south", At=new Vector2(r.center.x,r.yMin+.1f)},
                    new {Name="edge-north", At=new Vector2(r.center.x,r.yMax-.1f)} };
                foreach (var pose in poses)
                {
                    Pose(pose.At); CameraAt(pose.At); yield return null;
                    Assert.That(HiddenHarbours.Player.TidalWalkability.DepthNow(pose.At), Is.LessThanOrEqualTo(0f), pose.Name + " is dry floor");
                    SavePlate("walk-" + pose.Name + ".png", Capture()); Numbers("walk-" + pose.Name);
                    if (pose.Name.StartsWith("edge-"))
                    {
                        Vector2 outward = pose.Name.EndsWith("west") ? Vector2.left : pose.Name.EndsWith("east") ? Vector2.right :
                            pose.Name.EndsWith("south") ? Vector2.down : Vector2.up;
                        var c = GameServices.Config;
                        Vector2 resolved = HiddenHarbours.Player.PlayerWalkController.ApplyWaterEdge(outward, pose.At,
                            HiddenHarbours.Player.TidalWalkability.DepthNow, .25f, c.WadeDepth, c.SwimLimit, c.WadeSlowFactor, c.SwimSlowFactor);
                        File.AppendAllText(numbers, $"{pose.Name}: resting pose on planks; outward live-depth probe={resolved}; dry shore remains traversable\n", Utf8);
                    }
                }
                int west = 0;
                foreach (Vector2 at in new[] { new Vector2(181,3), new Vector2(182,4), new Vector2(183,4.4f), HiddenHarbours.App.Editor.StPetersLandingLayout.Entry })
                {
                    Pose(at); CameraAt(at); yield return null;
                    Assert.That(HiddenHarbours.Player.TidalWalkability.DepthNow(at), Is.LessThanOrEqualTo(0f), "dry west entry");
                    SavePlate("west-entry-" + west + ".png", Capture()); Numbers("west-entry-" + west++);
                }
                Pose(HiddenHarbours.App.Editor.StPetersBuilder.DisembarkPos);
                var owner = UnityEditor.AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatOwnerDef>(HiddenHarbours.App.Editor.StPetersArrivalOpening.SkipperPath);
                Assert.IsNotNull(owner);
                var cape = new GameObject("LandingAlongsideProof"); cape.SetActive(false); _spawned.Add(cape);
                cape.transform.position = HiddenHarbours.App.Editor.StPetersBuilder.DockZonePos;
                var committedArrival = GameObject.Find("StPetersArrivalOpening").GetComponent<HiddenHarbours.App.ArrivalOpening>();
                var committedArrivalData = new UnityEditor.SerializedObject(committedArrival);
                cape.AddComponent<HiddenHarbours.Boats.HullTideRide>().Configure(committedArrivalData.FindProperty("_hullPictureDatum").floatValue);
                var moored = cape.AddComponent<HiddenHarbours.Boats.MooredBoat>();
                moored.Configure(owner, HiddenHarbours.App.Editor.StPetersArrivalOpening.BerthHeadingDegrees());
                cape.SetActive(true);
                Time.timeScale = 1f;
                for (int f=0; f<60 && !moored.IsPresented; f++) yield return null;
                Assert.IsTrue(moored.IsPresented);
                foreach (float tide in new[] { -2.2f, 0f, 2.2f })
                {
                    yield return HoldTide(tide);
                    string id = tide < -1 ? "low" : tide > 1 ? "high" : "middle";
                    CameraAt(cape.transform.position); yield return null;
                    SavePlate("tide-" + id + ".png", Capture()); Numbers("tide-" + id);
                    File.AppendAllText(numbers, $"tide-{id}: capePlan={cape.transform.position} capePictureRootY={cape.transform.position.y + cape.GetComponent<HiddenHarbours.Boats.HullTideRide>().ScreenRiseNow():R}\n", Utf8);
                    if (tide != 0)
                    {
                        CameraAt(HiddenHarbours.App.Editor.StPetersBuilder.DoryMooredPos); yield return null;
                        SavePlate("north-pocket-" + id + ".png", Capture()); Numbers("north-pocket-" + id);
                    }
                }
                var playerDory = GameObject.Find("Dory");
                var doryController = playerDory.GetComponent<HiddenHarbours.Boats.BoatController>();
                Assert.IsNotNull(doryController); Assert.IsNotNull(doryController.Hull);
                bool capeWasActive = cape.activeSelf;
                cape.SetActive(false);
                try
                {
                    yield return AlongsideRailPlates(playerDory, doryController.Hull, "dory");
                }
                finally { cape.SetActive(capeWasActive); }
                yield return null;
                yield return AlongsideRailPlates(cape, owner.Boat, "cape");
                Assert.IsTrue(ShellFlow.AtTitle, "the plate never leaves the title or enables saving");
            }
            finally
            {
                player.transform.position = originalPlayer;
                if (body != null) { body.position = originalPlayer; body.linearVelocity = Vector2.zero; }
                GameServices.Environment = held;
                _follow.enabled = followEnabled; _cam.aspect = previousAspect;
                foreach (var ppc in pixelCameras) ppc.enabled = true;
                Time.timeScale = 1f;
            }
            Debug.Log("[StPetersLandingDeck] DOCK_PLATES_COMPLETE " + PlatePath());
        }
#endif
    }
}
