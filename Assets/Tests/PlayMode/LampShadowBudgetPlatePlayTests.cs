using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The lamp-shadow budget on a real wharf at night</b> (the 2026-11-25 shadow design, option A): pairs
    /// ranked by strength, each caster's strongest K, the strongest N overall, and an eased fade at the edge
    /// of both cuts. Photographed at St Peters at 02:00 through the game's own camera (<see cref="WharfNightStage"/>):
    /// a figure between two lamps, the bollards under the dock lamps, a boat between wharf lamps, a figure at
    /// the store, a walk across the budget's edge as a strip, and the pairing's cost. Plates and a frame-time
    /// table land in <c>temporaryCachePath/lamp-shadows-budget</c>.
    /// </summary>
    public class LampShadowBudgetPlatePlayTests
    {
        private const string PlateDir = "lamp-shadows-budget";
        private static readonly int IdShadowColor = Shader.PropertyToID("_ShadowColor");

        private WharfNightStage _stage;
        private LampShadowProfile _profileBefore;
        private readonly StringBuilder _notes = new StringBuilder();

        private void Note(string s) { _notes.AppendLine(s); Debug.Log("[" + PlateDir + "] " + s); }

        private string Dir()
        {
            string d = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(d);
            return d;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_profileBefore != null && LampShadowSystem.Instance != null) LampShadowSystem.Instance.Profile = _profileBefore;
            _profileBefore = null;
            if (_notes.Length > 0) File.WriteAllText(Path.Combine(Dir(), "notes.txt"), _notes.ToString());
            _notes.Clear();
            if (_stage != null) yield return _stage.TearDown();
            _stage = null;
        }

        private static float SlotAlpha(LampShadowSystem s, int i, MaterialPropertyBlock block)
        {
            MeshRenderer r = s.SlotRenderer(i);
            if (r == null || !r.enabled) return 0f;
            r.GetPropertyBlock(block);
            return block.GetColor(IdShadowColor).a;
        }

        /// <summary>How many drawn shadows belong to casters under <paramref name="root"/>, and from how many
        /// distinct lamps; the strongest alpha among them.</summary>
        private static int ShadowsOf(LampShadowSystem s, Transform root, MaterialPropertyBlock block,
                                     out int lamps, out float maxAlpha)
        {
            int n = 0;
            maxAlpha = 0f;
            var seen = new List<SceneLight>();
            for (int i = 0; i < s.ActiveShadowCount; i++)
            {
                var c = s.SlotCaster(i) as Component;
                if (c == null || !c.transform.IsChildOf(root)) continue;
                float a = SlotAlpha(s, i, block);
                if (a <= 0f) continue;
                n++;
                maxAlpha = Mathf.Max(maxAlpha, a);
                if (!seen.Contains(s.SlotLight(i))) seen.Add(s.SlotLight(i));
            }
            lamps = seen.Count;
            return n;
        }

        private void NoteSlots(LampShadowSystem s, string what, MaterialPropertyBlock block)
        {
            Note($"{what}: active {s.ActiveShadowCount} of pool {s.PoolSize}, cut {s.GlobalCut:0.000}");
            for (int i = 0; i < s.ActiveShadowCount; i++)
            {
                var c = s.SlotCaster(i) as Component;
                SceneLight l = s.SlotLight(i);
                Note($"   [{i}] {(l != null ? l.name : "-")} -> {(c != null ? c.transform.root.name + "/" + c.name : "-")} " +
                     $"score {s.SlotScore(i):0.000} alpha {SlotAlpha(s, i, block):0.000}");
            }
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator TheBudget_RanksByStrength_KeepsKPerCaster_AndFadesAtItsEdge()
        {
            WharfNightStage.RequireAGraphicsDevice();
            _stage = new WharfNightStage("StPeters", PlateDir);
            yield return _stage.Load();
            yield return _stage.SetNight(2f);

            LampShadowSystem sys = LampShadowSystem.Instance;
            Assert.IsNotNull(sys, "the lamp-shadow system installs itself");
            var block = new MaterialPropertyBlock();

            var walk = Object.FindFirstObjectByType<HiddenHarbours.Player.PlayerWalkController>();
            Assert.IsNotNull(walk, "St Peters has a player");
            Transform him = walk.transform;
            var rb = walk.GetComponent<Rigidbody2D>();
            if (rb != null) rb.simulated = false;
            void Put(Vector2 p) { him.position = new Vector3(p.x, p.y, him.position.z); if (rb != null) rb.position = p; }
            var headlamp = walk.GetComponentInChildren<Headlamp>(true);
            if (headlamp != null && headlamp.Light != null) headlamp.Light.CastsShadows = false;   // the placed lamps only

            // The placed lamps that throw shadows, and the two nearest each other with a point both reach.
            var lamps = new List<SceneLight>();
            foreach (var l in Object.FindObjectsByType<SceneLight>(FindObjectsSortMode.InstanceID))
                if (l.isActiveAndEnabled && l.CastsShadows && l.ReachMetres > 1f && l.transform.root != him.root) lamps.Add(l);
            Assert.GreaterOrEqual(lamps.Count, 2, "St Peters has at least two placed lamps with a reach");
            SceneLight a = null, b = null;
            float best = float.MaxValue;
            for (int i = 0; i < lamps.Count; i++)
            for (int j = i + 1; j < lamps.Count; j++)
            {
                float d = Vector2.Distance(lamps[i].WorldOrigin, lamps[j].WorldOrigin);
                if (d < 2f || d >= best) continue;
                if (d * 0.5f < Mathf.Min(lamps[i].ReachMetres, lamps[j].ReachMetres) * 0.9f) { best = d; a = lamps[i]; b = lamps[j]; }
            }
            Assert.IsNotNull(a, "two lamps whose reaches overlap");
            Vector2 mid = (a.WorldOrigin + b.WorldOrigin) * 0.5f + new Vector2(0f, -0.3f);
            Note($"lamps {lamps.Count}; pair {a.name} at {a.WorldOrigin:0.00} and {b.name} at {b.WorldOrigin:0.00}, {best:0.00} m apart");

            // 1. A figure between two lamps throws two shadows (K = 2), one from each.
            Put(mid);
            yield return _stage.FrameOn(mid);
            sys.PublishFrame(_stage.Camera, true);
            NoteSlots(sys, "figure between two lamps", block);
            int mine = ShadowsOf(sys, him, block, out int myLamps, out _);
            _stage.SavePlate("1-figure-between-two-lamps.png", _stage.Capture());
            Assert.AreEqual(2, myLamps, $"between {a.name} and {b.name} he throws one shadow from each ({mine} drawn)");
            Assert.LessOrEqual(mine, sys.Profile.MaxShadowsPerCaster, "never more than K for one caster");

            // 2. The bollards and posts under those lamps: each caster in reach throws at most K.
            for (int i = 0; i < sys.ActiveShadowCount; i++)
            {
                var c = sys.SlotCaster(i);
                int perCaster = 0;
                for (int j = 0; j < sys.ActiveShadowCount; j++) if (ReferenceEquals(sys.SlotCaster(j), c)) perCaster++;
                Assert.LessOrEqual(perCaster, sys.Profile.MaxShadowsPerCaster, "K per caster holds across the pool");
            }
            Vector2 aside = a.WorldOrigin + new Vector2(0f, 6f);
            Put(aside + new Vector2(0f, 40f));   // out of the way: the dock's own casters
            yield return _stage.ReFrameOn(a.WorldOrigin);
            sys.PublishFrame(_stage.Camera, true);
            NoteSlots(sys, "the dock without him", block);
            _stage.SavePlate("2-dock-casters.png", _stage.Capture());
            Assert.Greater(sys.ActiveShadowCount, 0, "the dock lamps throw the dock's casters");

            // 3. A boat between the wharf lamps: her hull is a caster too.
            HullLampShadowCaster hullNear = null;
            float hullDist = float.MaxValue;
            foreach (var h in Object.FindObjectsByType<HullLampShadowCaster>(FindObjectsSortMode.InstanceID))
            {
                float d = Mathf.Min(Vector2.Distance(h.transform.position, a.WorldOrigin), Vector2.Distance(h.transform.position, b.WorldOrigin));
                if (d < hullDist) { hullDist = d; hullNear = h; }
            }
            if (hullNear != null)
            {
                yield return _stage.ReFrameOn(hullNear.transform.position);
                sys.PublishFrame(_stage.Camera, true);
                NoteSlots(sys, $"boat {hullNear.name} {hullDist:0.0} m from the nearer lamp", block);
                _stage.SavePlate("3-boat-between-wharf-lamps.png", _stage.Capture());
            }
            else Note("no mesh hull in St Peters to photograph");

            // 4. A figure at the store under the doorstep lantern and the green walk post.
            SceneLight door = lamps.Find(l => l.name.Contains("doorstep_lantern_general_store"));
            SceneLight post = lamps.Find(l => l.name.Contains("lantern_post_green_walk"));
            if (door != null && post != null)
            {
                Vector2 store = Vector2.Lerp(door.WorldOrigin, post.WorldOrigin, 0.5f);
                Transform figure = him;
                var villager = Object.FindFirstObjectByType<HiddenHarbours.World.VillagerRoutine>();
                if (villager != null && villager.GetComponentInChildren<SpriteShadow>() != null)
                {
                    villager.enabled = false;
                    villager.transform.position = new Vector3(store.x, store.y, villager.transform.position.z);
                    figure = villager.transform;
                    Put(store + new Vector2(0f, 40f));
                }
                else Put(store);
                yield return _stage.ReFrameOn(store);
                sys.PublishFrame(_stage.Camera, true);
                NoteSlots(sys, $"{(figure == him ? "he" : "villager " + figure.name)} at the store", block);
                ShadowsOf(sys, figure, block, out int storeLamps, out _);
                Note($"   figure at the store throws from {storeLamps} lamp(s)");
                _stage.SavePlate("4-figure-at-the-store.png", _stage.Capture());
            }
            else Note("the store's two lamps were not found by name");

            // 5. Walking across the budget's edge, as a strip. The pool is cut to end exactly at his pair's rank
            // where the walk ends (under lamp A), so out at the edge of A's reach he is turned away and under the
            // lamp he holds the last slot: the walk crosses the edge whatever else the dock holds. Each step
            // re-pairs (as the throttled tick does) and poses; neither his shadow nor the one he displaces may jump.
            Vector2 from = a.WorldOrigin + (a.WorldOrigin - b.WorldOrigin).normalized * (a.ReachMetres * 0.95f);
            Vector2 to = a.WorldOrigin + new Vector2(0f, -0.4f);
            Put(to);
            sys.PublishFrame(_stage.Camera, true);
            int hisRank = -1;
            for (int i = 0; i < sys.ActiveShadowCount && hisRank < 0; i++)
                if (sys.SlotCaster(i) is Component c0 && c0 != null && c0.transform.IsChildOf(him)) hisRank = i;
            Assert.GreaterOrEqual(hisRank, 0, "under lamp A he throws a shadow inside the shipped pool");
            _profileBefore = sys.Profile;
            LampShadowProfile tight = Object.Instantiate(_profileBefore);
            _stage.Track(tight);
            tight.MaxShadows = hisRank + 1;
            sys.Profile = tight;
            int last = hisRank;
            Note($"walk: his pair ranks {hisRank} under {a.name}; pool cut to {tight.MaxShadows}");
            Put(from);
            yield return _stage.ReFrameOn(Vector2.Lerp(from, to, 0.5f));
            const int steps = 60;
            float prev = -1f, worstJump = 0f, worstTotalJump = 0f, prevTotal = -1f;
            int swaps = 0;
            bool wasIn = false, everOut = false, everIn = false;
            var csv = new StringBuilder("step,x,y,his_alpha,last_slot_alpha,last_slot_caster,cut\n");
            var strip = new List<byte[]>();
            for (int k = 0; k <= steps; k++)
            {
                Put(Vector2.Lerp(from, to, k / (float)steps));
                sys.PublishFrame(_stage.Camera, true);
                ShadowsOf(sys, him, block, out _, out float his);
                float total = SlotAlpha(sys, last, block);
                object caster = sys.SlotCaster(last);
                bool isIn = false;
                for (int i = 0; i < sys.ActiveShadowCount; i++)
                    if (sys.SlotCaster(i) is Component ci && ci != null && ci.transform.IsChildOf(him)) isIn = true;
                if (k > 0 && isIn != wasIn) swaps++;
                wasIn = isIn;
                everIn |= isIn;
                everOut |= !isIn;
                if (prev >= 0f) worstJump = Mathf.Max(worstJump, Mathf.Abs(his - prev));
                if (prevTotal >= 0f) worstTotalJump = Mathf.Max(worstTotalJump, Mathf.Abs(total - prevTotal));
                prev = his; prevTotal = total;
                var cc = caster as Component;
                csv.AppendLine($"{k},{him.position.x:0.000},{him.position.y:0.000},{his:0.0000},{total:0.0000}," +
                               $"{(cc != null ? cc.name : "-")},{sys.GlobalCut:0.0000}");
                if (k % 10 == 0) strip.Add(_stage.Capture());
                yield return null;
            }
            File.WriteAllText(Path.Combine(Dir(), "budget-edge-walk.csv"), csv.ToString());
            SaveStrip("5-budget-edge-walk-strip.png", strip);
            Note($"walk across the edge: {swaps} crossing(s) of the cut; worst step in his alpha {worstJump:0.0000}, " +
                 $"in the last slot's {worstTotalJump:0.0000}");
            Assert.IsTrue(everOut && everIn, "the walk crosses the budget's edge (turned away at the reach, kept under the lamp)");
            Assert.Less(worstJump, 0.08f, "his shadow eases across the edge, it does not pop");
            Assert.Less(worstTotalJump, 0.08f, "nor does the shadow it replaces");
            sys.Profile = _profileBefore;

            // 6. The cost: the pairing tick and the pose, timed on the CPU, and the frame time.
            Put(mid);
            yield return _stage.ReFrameOn(mid);
            var sw = new Stopwatch();
            var table = new StringBuilder("sample,publish_reselect_ms,publish_pose_ms\n");
            double sumSel = 0, sumPose = 0, maxSel = 0;
            for (int k = 0; k < 120; k++)
            {
                sw.Restart(); sys.PublishFrame(_stage.Camera, true); sw.Stop();
                double sel = sw.Elapsed.TotalMilliseconds;
                sw.Restart(); sys.PublishFrame(_stage.Camera, false); sw.Stop();
                double pose = sw.Elapsed.TotalMilliseconds;
                if (k >= 20) { sumSel += sel; sumPose += pose; maxSel = System.Math.Max(maxSel, sel); }
                table.AppendLine($"{k},{sel:0.0000},{pose:0.0000}");
            }
            float scaleBefore = Time.timeScale;
            Time.timeScale = 1f;
            var frames = new List<float>();
            for (int k = 0; k < 180; k++) { yield return null; if (k >= 30) frames.Add(Time.unscaledDeltaTime * 1000f); }
            Time.timeScale = scaleBefore;
            frames.Sort();
            float median = frames[frames.Count / 2], p95 = frames[(int)(frames.Count * 0.95f)];
            table.AppendLine($"# casters {LampShadowSystem.LiveCasterCount}, lamps {LampShadowSystem.LiveLightCount}, " +
                             $"active {sys.ActiveShadowCount} of {sys.PoolSize}");
            table.AppendLine($"# reselect mean {sumSel / 100:0.000} ms max {maxSel:0.000} ms; pose mean {sumPose / 100:0.000} ms");
            table.AppendLine($"# editor frame median {median:0.00} ms p95 {p95:0.00} ms over {frames.Count} frames");
            File.WriteAllText(Path.Combine(Dir(), "frame-time.csv"), table.ToString());
            Note($"cost: reselect mean {sumSel / 100:0.000} ms (max {maxSel:0.000}), pose mean {sumPose / 100:0.000} ms, " +
                 $"{LampShadowSystem.LiveCasterCount} casters x {LampShadowSystem.LiveLightCount} lamps; " +
                 $"editor frame median {median:0.00} ms, p95 {p95:0.00} ms");
            Assert.Less(sumSel / 100, 2.0, "the throttled pairing stays well inside a 60 fps frame");
            Assert.Less(sumPose / 100, 0.5, "the per-frame pose is cheap");
        }

        private void SaveStrip(string name, List<byte[]> frames)
        {
            if (frames.Count == 0) return;
            int w = _stage.Width, h = _stage.Height;
            // Each frame's middle third across, side by side.
            int cw = w / 3, x0 = w / 3;
            var tex = new Texture2D(cw * frames.Count, h, TextureFormat.RGBA32, false);
            var row = new Color32[cw];
            for (int f = 0; f < frames.Count; f++)
            {
                byte[] px = frames[f];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < cw; x++)
                    {
                        int i = (y * w + x0 + x) * 4;
                        row[x] = new Color32(px[i], px[i + 1], px[i + 2], 255);
                    }
                    tex.SetPixels32(f * cw, y, cw, 1, row);
                }
            }
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Dir(), name), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
