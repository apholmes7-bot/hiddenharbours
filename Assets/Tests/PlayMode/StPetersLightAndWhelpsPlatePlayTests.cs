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
            new BoardFrame("ne_light.station.dusk", 190f, 224f, 53f, 74f, 19.5f, 0.63f),
            new BoardFrame("ne_light.head.dusk", 165f, 245f, 32.5f, 77.5f, 19.5f, 0.63f),
            new BoardFrame("ne_light.yard.noon", 183f, 205f, 44f, 62f, 12f, 0.63f),
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
            // The ground by the cottage and the Gap: the path past the door, the wet ground by the pond, the brook and its footbridge.
            new BoardFrame("whelps_lookout.cottage.dusk", 154f, 176f, -43f, -24f, 19.5f, 0.63f),
            new BoardFrame("whelps_lookout.gap.noon", 146f, 168f, -49f, -30f, 12f, 0.63f),
            new BoardFrame("whelps_lookout.gap.dusk", 146f, 168f, -49f, -30f, 19.5f, 0.63f),
            // The station's props (fog bell, clothesline, picket fences, the plank path) and the east cardinal off the bar.
            new BoardFrame("ne_props.station.noon", 196f, 226f, 52f, 70f, 12f, 0.63f),
            new BoardFrame("ne_props.yard.noon", 176f, 200f, 50f, 66f, 12f, 0.63f),
            new BoardFrame("ne_props.planks.noon", 164f, 212f, 38f, 64f, 12f, 0.63f),
            new BoardFrame("ne_props.east_buoy.high", 248f, 272f, 96f, 110f, 12f, 2.2f),
            new BoardFrame("ne_props.east_buoy.low", 248f, 272f, 96f, 110f, 12f, -1.4f),
            new BoardFrame("ne_props.east_buoy.spring_low", 248f, 272f, 96f, 110f, 12f, -2.2f),
            new BoardFrame("ne_props.east_buoy.2230", 248f, 272f, 96f, 110f, 22.5f, 0.63f),
            // One board per piece at noon, low water, high water and night: the station, the Gap footbridge, the
            // seals' skerries and the pier's south face with its fender strings.
            new BoardFrame("stp_round.station.noon", 190f, 224f, 53f, 74f, 12f, 0.63f),
            new BoardFrame("stp_round.station.low", 190f, 224f, 53f, 74f, 12f, -1.4f),
            new BoardFrame("stp_round.station.high", 190f, 224f, 53f, 74f, 12f, 2.2f),
            new BoardFrame("stp_round.station.2230", 190f, 224f, 53f, 74f, 22.5f, 0.63f),
            new BoardFrame("stp_round.gap.noon", 146f, 168f, -49f, -30f, 12f, 0.63f),
            new BoardFrame("stp_round.gap.low", 146f, 168f, -49f, -30f, 12f, -1.4f),
            new BoardFrame("stp_round.gap.high", 146f, 168f, -49f, -30f, 12f, 2.2f),
            new BoardFrame("stp_round.gap.2230", 146f, 168f, -49f, -30f, 22.5f, 0.63f),
            new BoardFrame("stp_round.skerries.noon", 172f, 200f, -57.75f, -42f, 12f, 0.63f),
            new BoardFrame("stp_round.skerries.low", 172f, 200f, -57.75f, -42f, 12f, -1.4f),
            new BoardFrame("stp_round.skerries.high", 172f, 200f, -57.75f, -42f, 12f, 2.2f),
            new BoardFrame("stp_round.skerries.2230", 172f, 200f, -57.75f, -42f, 22.5f, 0.63f),
            new BoardFrame("stp_round.pier_south.noon", 196f, 220f, -4f, 8f, 12f, 0.63f),
            new BoardFrame("stp_round.pier_south.low", 196f, 220f, -4f, 8f, 12f, -1.4f),
            new BoardFrame("stp_round.pier_south.high", 196f, 220f, -4f, 8f, 12f, 2.2f),
            new BoardFrame("stp_round.pier_south.2230", 196f, 220f, -4f, 8f, 22.5f, 0.63f),
        };

        const string EastCardinalNightFrame = "ne_props.east_buoy.2230";
        const string EastCardinalMark = "NavMark_aid.stp_ne_bar_east_cardinal";
        const string NineMileCreek = "NineMileCreek";

        /// <summary>
        /// The first hour at or after <paramref name="hour"/> that lands in the middle of one of the east cardinal's
        /// flashes (Q(3) 10s is dark most of its period), so the night plate shows her lamp burning.
        /// </summary>
        static float HourTheEastCardinalBurns(float hour)
        {
            GameObject mark = GameObject.Find(EastCardinalMark);
            Assert.IsNotNull(mark, EastCardinalMark + " is not in St Peters.");
            var lamp = mark.GetComponentInChildren<HiddenHarbours.Art.NavLight>(true);
            Assert.IsNotNull(lamp, "the east cardinal carries no lamp.");
            return HourTheLampBurns(lamp, hour);
        }

        /// <summary>The first hour at or after <paramref name="hour"/> in the middle of one of a lamp's flashes.</summary>
        static float HourTheLampBurns(HiddenHarbours.Art.NavLight lamp, float hour)
        {
            Assert.IsTrue(lamp.IsLit, lamp.name + " is unlit.");
            double spd = GameServices.Config.SecondsPerDay;
            const double step = 0.02;
            int first = -1;
            for (int k = 0; k < 1500; k++)
            {
                float h = hour + (float)(k * step / spd * 24.0);
                bool on = lamp.Character.IsOn((1.0 + h / 24.0) * spd, lamp.PhaseSeconds);
                if (on && first < 0) first = k;
                if (!on && first >= 0) return hour + (float)((first + k - 1) * 0.5 * step / spd * 24.0);
            }
            Assert.Fail(lamp.name + " never flashed in thirty seconds of clock.");
            return hour;
        }

        /// <summary>
        /// Every light that burns inside a frame (a scene light, a 2D light, a nav lamp), with where it hangs and how
        /// bright: the trace of a glow on a plate that no one placed on purpose.
        /// </summary>
        static void TraceTheGlows(BoardFrame frame)
        {
            const float margin = 6f;
            foreach (Behaviour b in Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                string type = b.GetType().Name;
                if (type != "SceneLight" && type != "Light2D" && type != "NavLight") continue;
                Vector3 p = b.transform.position;
                if (p.x < frame.X0 - margin || p.x > frame.X1 + margin || p.y < frame.YBottom - margin || p.y > frame.YTop + margin) continue;
                object intensity = b.GetType().GetProperty("intensity")?.GetValue(b);
                string path = b.name;
                for (Transform t = b.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
                Debug.Log($"[KeySceneBoards] glow in {frame.Id}: {type} {path} at ({p.x:F2}, {p.y:F2}) enabled {b.isActiveAndEnabled}" +
                          (intensity != null ? $" intensity {intensity}" : ""));
            }
        }

        /// <summary>The editor's render statistics for the last frame (UnityEditor.UnityStats), read by name; n/a in a player.</summary>
        static string EditorStat(string name)
        {
            var stats = System.Type.GetType("UnityEditor.UnityStats, UnityEditor");
            object v = stats?.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
            return v != null ? v.ToString() : "n/a";
        }

        /// <summary>Shoots one frame at its hour and tide into <paramref name="dir"/>, the follow and the pixel camera
        /// off, and logs the frame's draw calls.</summary>
        IEnumerator ShootBoard(BoardFrame frame, float hour, string dir, IEnvironmentService held, Behaviour[] pixelCameras,
                               Unity.Profiling.ProfilerRecorder drawCalls)
        {
            yield return PinTheHour(hour, ClearRunSeconds);

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
            Debug.Log($"[KeySceneBoards] plate written: {path} ({_w}x{_h}, hour {hour}, tide {frame.Tide}, day tint {tint.r:F3} {tint.g:F3} {tint.b:F3}, " +
                      $"draw calls {(drawCalls.Valid ? drawCalls.LastValue.ToString() : EditorStat("drawCalls"))}, batches {EditorStat("batches")}, set-pass {EditorStat("setPassCalls")})");
            if (frame.Id.Contains("2230")) TraceTheGlows(frame);
        }

        /// <summary>
        /// A lit mark at Nine Mile Creek at the east cardinal's night hour, in the middle of one of its flashes and
        /// framed as the east cardinal is (24 x 14 m): how bright a lit mark reads beside hers.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator NineMileCreek_ALitMark_AtTheEastCardinalsNightHour()
        {
            RequireAGraphicsDevice();
            string dir = System.Environment.GetEnvironmentVariable(KeySceneBoardPlatesVariable);
            if (string.IsNullOrEmpty(dir)) Assert.Ignore(KeySceneBoardPlatesVariable + " is unset: this comparison plate is shot on request.");
            yield return LoadRegion(StPeters);   // the persistent core and its play camera come up with St Peters
            yield return LoadRegion(NineMileCreek);
            EnsureTheFollow();
            HoldTheMood();
            var lamp = Object.FindObjectsByType<HiddenHarbours.Art.NavLight>(FindObjectsSortMode.None)
                .Where(l => l.IsLit).OrderBy(l => l.name, System.StringComparer.Ordinal).FirstOrDefault();
            Assert.IsNotNull(lamp, "Nine Mile Creek holds no lit mark.");
            Vector3 at = lamp.transform.position;
            var frame = new BoardFrame("ne_props.nmc_lit_mark.2230", at.x - 12f, at.x + 12f, at.y - 7f, at.y + 7f, 22.5f, 0.63f);
            Debug.Log($"[KeySceneBoards] the Nine Mile Creek mark: {lamp.name} at ({at.x:F2}, {at.y:F2})");

            IEnvironmentService held = GameServices.Environment;
            bool followEnabled = _follow.enabled;
            float aspect = _cam.aspect, ortho = _cam.orthographicSize;
            Vector3 camAt = _cam.transform.position;
            var pixelCameras = _cam.GetComponents<Behaviour>().Where(b => b.GetType().Name == "PixelPerfectCamera" && b.enabled).ToArray();
            var drawCalls = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Draw Calls Count");
            try
            {
                yield return ShootBoard(frame, HourTheLampBurns(lamp, frame.Hour), dir, held, pixelCameras, drawCalls);
            }
            finally
            {
                drawCalls.Dispose();
                GameServices.Environment = held;
                _follow.enabled = followEnabled;
                foreach (Behaviour ppc in pixelCameras) if (ppc != null) ppc.enabled = true;
                _cam.aspect = aspect;
                _cam.orthographicSize = ortho;
                _cam.transform.position = camAt;
                Time.timeScale = 1f;
            }
        }

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

        [UnityTest, Timeout(900000)]
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
            var drawCalls = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Draw Calls Count");
            try
            {
                string only = System.Environment.GetEnvironmentVariable(KeySceneBoardFramesVariable);
                foreach (BoardFrame frame in LightAndWhelpsFrames())
                {
                    if (!string.IsNullOrEmpty(only) && !frame.Id.Contains(only)) continue;
                    float hour = frame.Id == EastCardinalNightFrame ? HourTheEastCardinalBurns(frame.Hour) : frame.Hour;
                    yield return ShootBoard(frame, hour, dir, held, pixelCameras, drawCalls);
                }
                Debug.Log("[KeySceneBoards] PLATES_COMPLETE " + dir);
            }
            finally
            {
                drawCalls.Dispose();
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
