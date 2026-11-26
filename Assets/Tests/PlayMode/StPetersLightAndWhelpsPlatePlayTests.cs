using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // The north-east light and the Whelps, shot at their art boards' own frames, hours and tides, so a
    // plate lies over its board pixel for pixel (32 px a metre). Shares the GPU/title-only harness.
    public partial class WharfBuildingPass2PlatePlayTests
    {
        /// <summary>Where these plates go. Unset, they land beside the harness's other plates.</summary>
        const string KeySceneBoardPlatesVariable = "HH_KEY_SCENE_PLATES";

        /// <summary>Set to shoot only the frames whose id holds this text. Unset, every frame is shot.</summary>
        const string KeySceneBoardFramesVariable = "HH_KEY_SCENE_FRAMES";

        /// <summary>The boards are drawn at 32 px a metre; so are these.</summary>
        const float BoardPixelsPerMetre = 32f;

        readonly struct BoardFrame
        {
            public readonly string Id;
            public readonly float X0, X1, YBottom, YTop, Hour, Tide;
            public BoardFrame(string id, float x0, float x1, float yBottom, float yTop, float hour, float tide)
            { Id = id; X0 = x0; X1 = x1; YBottom = yBottom; YTop = yTop; Hour = hour; Tide = tide; }
            public Vector2 Centre => new Vector2((X0 + X1) * 0.5f, (YBottom + YTop) * 0.5f);
            public float Width => X1 - X0;
            public float Height => YTop - YBottom;
        }

        // The boards' frames, from the package's tools/boards.json. A board's v is y + cos(40 deg) * z; the
        // game draws its ground flat, so the light's frame is the board's, let down by the head's 11 m.
        const float HeadTopMetres = 11f;
        static readonly float HeadRise = HeadTopMetres * Mathf.Cos(40f * Mathf.Deg2Rad);

        static BoardFrame[] LightAndWhelpsFrames() => new[]
        {
            new BoardFrame("ne_light.wide_station.0600", 195.055f, 219.945f, 67.026f - HeadRise, 81.026f - HeadRise, 6f, -0.85f),
            new BoardFrame("ne_light.wide_station.2230", 195.055f, 219.945f, 67.026f - HeadRise, 81.026f - HeadRise, 22.5f, -0.85f),
            new BoardFrame("ne_light.station.noon", 190f, 224f, 53f, 74f, 12f, 0.63f),
            new BoardFrame("ne_light.station.2230", 190f, 224f, 53f, 74f, 22.5f, 0.63f),
            new BoardFrame("ne_light.head.noon", 165f, 245f, 32.5f, 77.5f, 12f, 0.63f),
            new BoardFrame("ne_light.head.2230", 165f, 245f, 32.5f, 77.5f, 22.5f, 0.63f),
            new BoardFrame("whelps_lookout.lookout.noon", 154f, 199f, -50.893f, -26.893f, 12f, 0.63f),
            new BoardFrame("whelps_lookout.lookout.2230", 154f, 199f, -50.893f, -26.893f, 22.5f, 0.63f),
            new BoardFrame("whelps_lookout.skerries.low", 172f, 200f, -57.75f, -42f, 12f, -1.4f),
            new BoardFrame("ne_light.keeper.1000", 197f, 215f, 52f, 63f, 10f, 0.63f),
            new BoardFrame("whelps_lookout.cottage.0800", 154f, 176f, -43f, -24f, 8f, 0.63f),
            new BoardFrame("whelps_lookout.bench.1130", 154f, 176f, -43f, -24f, 11.5f, 0.63f),
            new BoardFrame("whelps_lookout.cottage.2230", 154f, 176f, -43f, -24f, 22.5f, 0.63f),
            new BoardFrame("whelps_lookout.skerries.half", 172f, 200f, -57.75f, -42f, 12f, -0.8f),
            new BoardFrame("whelps_lookout.skerries.half.2230", 172f, 200f, -57.75f, -42f, 22.5f, -0.8f),
            new BoardFrame("whelps_lookout.skerries.low.2230", 172f, 200f, -57.75f, -42f, 22.5f, -1.4f),
            new BoardFrame("whelps_lookout.rocks_close.half", 180f, 190f, -49.5f, -43.5f, 12f, -0.8f),
            new BoardFrame("whelps_lookout.rocks_close.low", 180f, 190f, -49.5f, -43.5f, 12f, -1.4f),
            new BoardFrame("whelps_lookout.lookout.dusk", 154f, 199f, -50.893f, -26.893f, 19.5f, 0.63f),
            new BoardFrame("whelps_lookout.stack.noon", 181f, 201f, -45f, -27f, 12f, 0.63f),
            new BoardFrame("whelps_lookout.stack.dusk", 181f, 201f, -45f, -27f, 19.5f, 0.63f),
            new BoardFrame("whelps_lookout.stack.2230", 181f, 201f, -45f, -27f, 22.5f, 0.63f),
            new BoardFrame("whelps_lookout.stack.low", 181f, 201f, -45f, -27f, 12f, -1.4f),
        };

        /// <summary>The north-east light in the loaded region: its lantern is where the clock says, at any hour.</summary>
        [UnityTest]
        public IEnumerator StPeters_TheNorthEastLight_TurnsWithTheClockAndNothingElse()
        {
            yield return LoadRegion(StPeters);
            var lamp = Object.FindAnyObjectByType<HiddenHarbours.World.LightStationLamp>();
            Assert.IsNotNull(lamp, "St Peters holds no light station lantern.");
            HiddenHarbours.World.LightStationDef def = lamp.Def;
            Assert.IsNotNull(def, "the lantern has no light list entry.");
            Assert.AreEqual("lightstation.stp_ne_light", def.Id);
            Assert.AreEqual(def.Beams, lamp.BeamTransforms.Length);
            Assert.AreEqual(3 + def.Beams, lamp.GetComponentsInChildren<HiddenHarbours.Art.SceneLight>(true).Length,
                "the lantern glow, the flash, the keeper's lamp and one lamp a beam");

            IGameClock clock = GameServices.Clock;
            Assert.IsNotNull(clock, "the region registered no clock.");
            foreach (float hour in new[] { Night, Noon, 3.25f })
            {
                yield return PinTheHour(hour, ClearRunSeconds);
                yield return null;
                lamp.Turn();
                float want = HiddenHarbours.World.LightSweep.BearingDegrees(clock.FlowSeconds, def.TurnSeconds, def.PhaseSeconds);
                Assert.AreEqual(0f, Mathf.DeltaAngle(want, lamp.BearingDegrees), 0.01f,
                    $"at {hour} h the lens is not where the clock puts it");
                Assert.AreEqual(HiddenHarbours.World.LightSweep.IsFlashing(want, def.Beams, def.FlashHalfAngleDegrees), lamp.IsFlashing);
                Vector2 up = lamp.BeamTransforms[0].up;
                float drawn = HiddenHarbours.World.LightSweep.DrawnDegrees(want, IsoGround.GroundDepthScale);
                Assert.AreEqual(0f, Mathf.DeltaAngle(Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg, drawn), 0.05f);
            }
            GameServices.Clock.TimeScale = 1f;
        }

        /// <summary>The two keepers who live away from the village are where their days put them, by the clock alone.</summary>
        [UnityTest]
        public IEnumerator StPeters_TheKeepers_AreAtTheirPostsByDay_AndIndoorsByNight()
        {
            yield return LoadRegion(StPeters);
            var keepers = new[]
            {
                (name: "AlbanDoucet", npc: "npc.alban_doucet", anchor: "anchor.stp_ne_light.tower_door", hour: 9f),
                (name: "OdileMarchand", npc: "npc.odile_marchand", anchor: "anchor.stp_whelps.cottage_door", hour: 8.5f),
            };
            foreach (var k in keepers)
            {
                GameObject who = GameObject.Find(k.name), where = GameObject.Find(k.anchor);
                Assert.IsNotNull(who, k.name + " is not on the island.");
                Assert.IsNotNull(where, "no " + k.anchor + " to be sent to.");
                var talk = who.GetComponent<HiddenHarbours.World.Interactable>();
                var routine = who.GetComponent<HiddenHarbours.World.KeeperRound>();
                Assert.IsNotNull(talk); Assert.IsNotNull(routine);
                Assert.AreEqual(k.npc, talk.Npc != null ? talk.Npc.Id : null);

                yield return PinTheHour(k.hour, ClearRunSeconds);
                for (int i = 0; i < 4; i++) yield return null;
                Assert.Less(Vector2.Distance(who.transform.position, where.transform.position), 0.75f,
                    $"at {k.hour} h {k.name} is not at {k.anchor}");
                Assert.IsTrue(who.GetComponent<SpriteRenderer>().enabled, k.name + " is at the post but not drawn");
                Assert.IsTrue(talk.enabled, k.name + " is at the post but cannot be spoken to");

                yield return PinTheHour(2f, ClearRunSeconds);
                for (int i = 0; i < 4; i++) yield return null;
                Assert.IsFalse(who.GetComponent<SpriteRenderer>().enabled, k.name + " is indoors at two in the morning");
            }
            GameServices.Clock.TimeScale = 1f;
        }

        [UnityTest]
        public IEnumerator StPeters_TheLightAndTheWhelps_AtTheirBoardsFramesHoursAndTides()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            EnsureTheFollow();
            HoldTheMood();
            _look = "key-scene-boards";
            _lookEvidence = "the boards' own frames (tools/boards.json), 32 px a metre, the follow and the pixel camera off";

            string dir = System.Environment.GetEnvironmentVariable(KeySceneBoardPlatesVariable);
            if (string.IsNullOrEmpty(dir)) dir = PlatePath();
            Directory.CreateDirectory(dir);

            IEnvironmentService held = GameServices.Environment;
            bool followEnabled = _follow.enabled;
            float aspect = _cam.aspect, ortho = _cam.orthographicSize;
            Vector3 camAt = _cam.transform.position;
            var pixelCameras = _cam.GetComponents<Behaviour>()
                .Where(b => b.GetType().Name == "PixelPerfectCamera" && b.enabled).ToArray();
            try
            {
                string only = System.Environment.GetEnvironmentVariable(KeySceneBoardFramesVariable);
                foreach (BoardFrame frame in LightAndWhelpsFrames())
                {
                    if (!string.IsNullOrEmpty(only) && !frame.Id.Contains(only)) continue;
                    yield return PinTheHour(frame.Hour, ClearRunSeconds);

                    GameServices.Environment = new LandingBoardTide(held, frame.Tide);
                    Time.timeScale = 1f;
                    float until = Time.realtimeSinceStartup + 20f;
                    do { yield return null; }
                    while (Time.realtimeSinceStartup < until &&
                           (Shader.GetGlobalVector("_HHSeaLevelWorld").w < .5f ||
                            Mathf.Abs(Shader.GetGlobalVector("_HHSeaLevelWorld").x - frame.Tide) > .01f));

                    _follow.enabled = false;
                    foreach (Behaviour ppc in pixelCameras) ppc.enabled = false;
                    int w = Mathf.RoundToInt(frame.Width * BoardPixelsPerMetre);
                    int h = Mathf.RoundToInt(frame.Height * BoardPixelsPerMetre);
                    if (_rt == null || _rt.width != w || _rt.height != h)
                    {
                        if (_rt != null) { _cam.targetTexture = null; _rt.Release(); Object.DestroyImmediate(_rt); }
                        _w = w; _h = h;
                        _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf);
                        _rt.Create();
                    }
                    _cam.targetTexture = _rt;
                    _cam.transform.position = new Vector3(frame.Centre.x, frame.Centre.y, _cam.transform.position.z);
                    _cam.orthographicSize = frame.Height * 0.5f;
                    _cam.aspect = frame.Width / frame.Height;

                    float run = Time.unscaledTime + ClearRunSeconds * 2f;
                    while (Time.unscaledTime < run) yield return null;
                    yield return ShadersReady();
                    Time.timeScale = 0f;
                    for (int i = 0; i < 2; i++) yield return null;

                    var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
                    tex.LoadRawTextureData(Capture());
                    tex.Apply();
                    string path = Path.Combine(dir, frame.Id + ".png");
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    Color tint = Shader.GetGlobalColor("_DayNightTint");
                    Debug.Log($"[KeySceneBoards] plate written: {path} ({_w}x{_h}, hour {frame.Hour}, tide {frame.Tide}, day tint {tint.r:F3} {tint.g:F3} {tint.b:F3})");
                }
                Debug.Log("[KeySceneBoards] PLATES_COMPLETE " + dir);
            }
            finally
            {
                GameServices.Environment = held;
                _follow.enabled = followEnabled;
                foreach (Behaviour ppc in pixelCameras) if (ppc != null) ppc.enabled = true;
                _cam.aspect = aspect;
                _cam.orthographicSize = ortho;
                _cam.transform.position = camAt;
                Time.timeScale = 1f;
            }
        }
    }
}
