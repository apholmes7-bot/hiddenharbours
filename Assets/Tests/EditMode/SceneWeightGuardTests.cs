using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE TRIPWIRE THIS EPISODE EARNED.</b> On 2026-08-09 a rebuilt <c>StPeters.unity</c> came back at
    /// <b>114 MB</b> against main's 15.8 MB — ~27,000 grass tufts serialized as GameObjects, four objects
    /// and about 3.1 KB of YAML each. Nothing in the repo noticed. What noticed was <b>GitHub's 100 MB file
    /// limit</b>, at push time, after the work was done; rule 7 had condemned it long before that and had no
    /// way to say so.
    ///
    /// <para>So this walks every committed scene and weighs it — on disk in bytes, and in serialized
    /// objects — against ceilings with real headroom over what the project legitimately carries today. It is
    /// deliberately a DUMB test: it parses no scene, loads no asset, needs no graphics device, and runs in
    /// milliseconds, because a guard that is expensive is a guard somebody turns off.</para>
    ///
    /// <para><b>⚠ It is a ceiling, not a target.</b> A scene creeping toward these numbers is not "fine
    /// because the test is green" — it is the same failure a year earlier. The numbers below are quoted from
    /// what main actually carries so a reader can see the headroom rather than trust an adjective.</para>
    ///
    /// <para><b>⚠ And it is proved on a FIXTURE, not on the owner's file.</b> The 114 MB scene is not in the
    /// repo and must never be — so the last test here synthesizes a scene of exactly that shape in a temp
    /// directory and requires the guard to reject it. A tripwire nobody has seen trip is a tripwire nobody
    /// knows is connected.</para>
    /// </summary>
    public class SceneWeightGuardTests
    {
        // =====================================================================================
        // the ceilings — quoted against what main carries, 2026-08-09
        // =====================================================================================

        /// <summary>
        /// Most bytes a committed scene may weigh. <b>32 MiB.</b>
        ///
        /// <para>Main's heaviest legitimate scene is <c>StPeters.unity</c> at <b>15,818,001 bytes</b>
        /// (15.1 MiB) — an island of trees, shrubs, shore plants, flowers and the village. This is a shade
        /// over 2× that, so the scene the project already has can roughly double before anyone is stopped,
        /// and it is about a third of GitHub's 100 MB hard limit — which means a scene that trips this fails
        /// in CI, at review, rather than at <c>git push</c> with the work already done.</para>
        /// </summary>
        public const long MaxSceneBytes = 32L * 1024 * 1024;

        /// <summary>
        /// Most SERIALIZED OBJECTS a committed scene may hold — every <c>--- !u!</c> document, so
        /// GameObjects, Transforms, renderers and components alike. <b>50,000.</b>
        ///
        /// <para>Main's heaviest is <c>StPeters.unity</c> at <b>20,193</b>. The owner's 114 MB rebuild was
        /// roughly 128,000. This sits at ~2.5× today's worst and well under half of the failure, which is
        /// where a ceiling belongs: high enough that legitimate growth does not trip it, low enough that the
        /// failure cannot sneak under it.</para>
        /// </summary>
        public const int MaxSceneObjects = 50_000;

        /// <summary>
        /// Most GameObjects a committed scene may hold. <b>12,000.</b>
        ///
        /// <para>Main's heaviest is <b>4,766</b>. Carried alongside the object count because it is the
        /// number a human recognises — "how many things are in this level" — and because a decor explosion
        /// shows up here first and most legibly.</para>
        /// </summary>
        public const int MaxSceneGameObjects = 12_000;

        // =====================================================================================
        // the measurement (pure — this is what the fixture proves)
        // =====================================================================================

        public struct Weight
        {
            public string Path;
            public long Bytes;
            public int Objects;
            public int GameObjects;
            public int GrassChunks;

            public override string ToString() =>
                $"{Path}: {Bytes / 1024f / 1024f:F2} MiB, {Objects:N0} objects, " +
                $"{GameObjects:N0} GameObjects";
        }

        /// <summary>
        /// Weigh one scene from its TEXT. Counting document markers rather than loading the scene is the
        /// point: it needs no editor, no graphics device and no import, so the guard costs nothing and
        /// cannot itself be the reason somebody deletes it.
        ///
        /// <para>Unity's Force-Text serialization (CLAUDE.md rule 9) writes one <c>--- !u!&lt;class&gt;
        /// &amp;&lt;id&gt;</c> line per serialized object, and class 1 is <c>GameObject</c>. That is the
        /// whole parser.</para>
        /// </summary>
        public static Weight Measure(string path, string text, long bytes)
        {
            var w = new Weight { Path = path, Bytes = bytes };
            if (string.IsNullOrEmpty(text)) return w;

            int at = 0;
            while (true)
            {
                at = text.IndexOf("--- !u!", at, System.StringComparison.Ordinal);
                if (at < 0) break;
                // Only count a marker that begins a line — the string can legitimately appear inside a
                // serialized string field, and a guard that miscounts is a guard nobody trusts.
                if (at == 0 || text[at - 1] == '\n')
                {
                    w.Objects++;
                    if (string.CompareOrdinal(text, at + 7, "1 &", 0, 3) == 0) w.GameObjects++;
                }
                at += 7;
            }

            // Belt and braces on the new path: GrassField's chunks carry HideFlags.DontSave, so they
            // CANNOT be written to a scene. If one ever appears, that guarantee has been broken and the
            // 114 MB failure has a road back.
            at = 0;
            while (true)
            {
                at = text.IndexOf("m_Name: GrassChunk_", at, System.StringComparison.Ordinal);
                if (at < 0) break;
                w.GrassChunks++;
                at += 19;
            }
            return w;
        }

        /// <summary>Weigh a scene file on disk.</summary>
        public static Weight MeasureFile(string path) =>
            Measure(path, File.ReadAllText(path), new FileInfo(path).Length);

        /// <summary>Why this scene fails, or null if it passes. One sentence per breach, each naming the
        /// number and the ceiling, because "scene too big" sends the reader back to the source.</summary>
        public static string Breach(in Weight w)
        {
            var sb = new StringBuilder();
            if (w.Bytes > MaxSceneBytes)
                sb.Append($"{w.Bytes / 1024f / 1024f:F1} MiB exceeds the {MaxSceneBytes / 1024 / 1024} MiB " +
                          "ceiling. ");
            if (w.Objects > MaxSceneObjects)
                sb.Append($"{w.Objects:N0} serialized objects exceeds the {MaxSceneObjects:N0} ceiling. ");
            if (w.GameObjects > MaxSceneGameObjects)
                sb.Append($"{w.GameObjects:N0} GameObjects exceeds the {MaxSceneGameObjects:N0} ceiling. ");
            if (w.GrassChunks > 0)
                sb.Append($"{w.GrassChunks:N0} GrassChunk objects were SAVED into the scene — those carry " +
                          "HideFlags.DontSave and cannot be serialized, so that guarantee is broken. ");
            return sb.Length == 0 ? null : sb.ToString().TrimEnd();
        }

        static List<string> CommittedScenes()
        {
            var scenes = new List<string>(
                Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories));
            scenes.Sort(System.StringComparer.Ordinal);
            return scenes;
        }

        // =====================================================================================
        // the guard
        // =====================================================================================

        [Test]
        public void EveryCommittedScene_StaysUnderTheWeightCeilings()
        {
            var scenes = CommittedScenes();
            Assert.Greater(scenes.Count, 0,
                "No .unity file was found under Assets/. This guard would then be guarding nothing — " +
                "treat it as a failure, not a pass.");

            var report = new StringBuilder();
            var breaches = new StringBuilder();
            long heaviest = 0;
            int mostObjects = 0;

            foreach (string path in scenes)
            {
                var w = MeasureFile(path);
                report.AppendLine("  " + w);
                heaviest = System.Math.Max(heaviest, w.Bytes);
                mostObjects = System.Math.Max(mostObjects, w.Objects);

                string breach = Breach(w);
                if (breach != null) breaches.AppendLine($"  {path} — {breach}");
            }

            Debug.Log($"[SceneWeightGuard] {scenes.Count} committed scene(s); heaviest " +
                      $"{heaviest / 1024f / 1024f:F2} MiB, most objects {mostObjects:N0}. Ceilings: " +
                      $"{MaxSceneBytes / 1024 / 1024} MiB, {MaxSceneObjects:N0} objects, " +
                      $"{MaxSceneGameObjects:N0} GameObjects.\n{report}");

            Assert.IsEmpty(breaches.ToString(),
                "A committed scene is too heavy:\n" + breaches +
                "\nThis is the guard that the 114 MB StPeters.unity of 2026-08-09 did not have. The cause " +
                "then was ~27,000 grass tufts saved as GameObjects; the fix was to store the FIELD and " +
                "derive the tufts at load (see GrassField). If a pass is emitting an object per scattered " +
                "thing, that is the bug — do not raise these ceilings to make this green.");
        }

        [Test]
        public void TheHeaviestCommittedScene_StillHasRealHeadroom()
        {
            // A ceiling nothing is near is a ceiling nobody has checked. This states the headroom as a
            // number so a reviewer can see the guard is calibrated rather than aspirational — and it fails
            // if a scene creeps up on the limit while still technically passing above.
            long heaviest = 0;
            string worst = "(none)";
            foreach (string path in CommittedScenes())
            {
                long bytes = new FileInfo(path).Length;
                if (bytes > heaviest) { heaviest = bytes; worst = path; }
            }

            Assert.Less(heaviest, MaxSceneBytes * 3 / 4,
                $"The heaviest committed scene ({worst}, {heaviest / 1024f / 1024f:F1} MiB) is within a " +
                $"quarter of the {MaxSceneBytes / 1024 / 1024} MiB ceiling. Either something is being " +
                "serialized that should be derived, or the ceiling needs a deliberate, argued raise — not " +
                "a quiet one.");
        }

        // =====================================================================================
        // the fixture — proving the tripwire is connected
        // =====================================================================================

        /// <summary>
        /// Write a synthetic scene of the shape that broke: N decor objects, each a GameObject +
        /// Transform + SpriteRenderer + one MonoBehaviour, padded toward the byte weight those objects
        /// really carried.
        ///
        /// <para>Streamed rather than built in memory: the shape being reproduced is tens of thousands of
        /// objects, and a test that allocates a hundred-megabyte string to prove a point is a test somebody
        /// deletes.</para>
        /// </summary>
        static void WriteSyntheticDecorScene(TextWriter w, int decorObjects, int bytesPerObject)
        {
            w.WriteLine("%YAML 1.1");
            w.WriteLine("%TAG !u! tag:unity3d.com,2011:");
            string padding = new string('x', Mathf.Max(0, bytesPerObject / 4 - 64));

            for (int i = 0; i < decorObjects; i++)
            {
                int id = 1000 + i * 4;
                w.WriteLine($"--- !u!1 &{id}");
                w.WriteLine("GameObject:");
                w.WriteLine("  m_Name: GrassTuft_MeadowShortA");
                w.WriteLine($"  m_Padding: {padding}");
                w.WriteLine($"--- !u!4 &{id + 1}");
                w.WriteLine("Transform:");
                w.WriteLine($"  m_Padding: {padding}");
                w.WriteLine($"--- !u!212 &{id + 2}");
                w.WriteLine("SpriteRenderer:");
                w.WriteLine($"  m_Padding: {padding}");
                w.WriteLine($"--- !u!114 &{id + 3}");
                w.WriteLine("MonoBehaviour:");
                w.WriteLine($"  m_Padding: {padding}");
            }
        }

        static string SyntheticDecorScene(int decorObjects, int bytesPerObject)
        {
            var sb = new StringBuilder(decorObjects * bytesPerObject + 1024);
            using (var w = new StringWriter(sb)) WriteSyntheticDecorScene(w, decorObjects, bytesPerObject);
            return sb.ToString();
        }

        [Test]
        public void TheGuard_RejectsASceneShapedLikeTheOneThatBroke()
        {
            // ~27,000 tufts at four objects each — the OBJECT SHAPE of the 114 MB StPeters.unity,
            // synthesized so the guard can be seen to trip WITHOUT that file ever entering the repo.
            //
            // ⚠ Padded to a little past the size ceiling rather than all the way to 114 MB. The shape is
            // the real one and all three ceilings are genuinely breached; writing 114 MB of temp file to
            // demonstrate a comparison would be a slow test with no extra evidence in it.
            const int Tufts = 27_000;

            string path = Path.Combine(Path.GetTempPath(),
                                       "hh_scene_weight_fixture_" + System.Guid.NewGuid().ToString("N") + ".unity");
            try
            {
                using (var file = new StreamWriter(path))
                    WriteSyntheticDecorScene(file, Tufts, bytesPerObject: 1600);

                var w = MeasureFile(path);

                Assert.AreEqual(Tufts, w.GameObjects, "the fixture did not synthesize the GameObjects it claims");
                Assert.AreEqual(Tufts * 4, w.Objects, "the fixture did not synthesize four objects per tuft");

                string breach = Breach(w);
                Assert.IsNotNull(breach,
                    $"THE TRIPWIRE IS NOT CONNECTED. A synthetic scene of {Tufts:N0} tufts " +
                    $"({w.Bytes / 1024f / 1024f:F1} MiB, {w.Objects:N0} objects) passed the guard. That is " +
                    "the exact shape of the 114 MB StPeters.unity this test exists to catch.");

                Assert.IsTrue(w.Bytes > MaxSceneBytes, "the fixture should breach the byte ceiling");
                Assert.IsTrue(w.Objects > MaxSceneObjects, "the fixture should breach the object ceiling");
                Assert.IsTrue(w.GameObjects > MaxSceneGameObjects,
                    "the fixture should breach the GameObject ceiling");

                Debug.Log($"[SceneWeightGuard] fixture rejected as expected — {breach}");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void TheGuard_PassesASceneOfLegitimateSize()
        {
            // The other half of a working tripwire: it must not fire on the project's real content. A
            // guard that fails on everything gets deleted, not fixed.
            string text = SyntheticDecorScene(1_000, bytesPerObject: 3100);
            var w = Measure("(fixture)", text, text.Length);

            Assert.IsNull(Breach(w),
                $"The guard rejected a modest 1,000-object scene ({w.Bytes / 1024f / 1024f:F1} MiB, " +
                $"{w.Objects:N0} objects). The ceilings are cut too tight to survive normal authoring.");
        }

        [Test]
        public void TheGuard_CountsOnlyDocumentMarkersAtTheStartOfALine()
        {
            // A serialized string field can legitimately contain "--- !u!". Miscounting it would make the
            // guard fire on innocent scenes, and a guard that cries wolf is a guard that gets disabled.
            const string text =
                "%YAML 1.1\n" +
                "--- !u!1 &1\n" +
                "GameObject:\n" +
                "  m_Note: this string mentions --- !u!1 &999 and must not be counted\n" +
                "--- !u!4 &2\n" +
                "Transform:\n";

            var w = Measure("(fixture)", text, text.Length);
            Assert.AreEqual(2, w.Objects, "the guard counted a document marker embedded in a string field");
            Assert.AreEqual(1, w.GameObjects, "the guard miscounted GameObjects");
        }

        [Test]
        public void TheGuard_CatchesSavedGrassChunks_EvenUnderTheSizeCeilings()
        {
            // GrassField's chunks carry HideFlags.DontSave, so this cannot happen. If it ever does, the
            // structural guarantee has broken and the failure would otherwise stay invisible until the
            // scene got big enough to trip a size ceiling.
            const string text =
                "%YAML 1.1\n" +
                "--- !u!1 &1\n" +
                "GameObject:\n" +
                "  m_Name: GrassChunk_c3_r17\n";

            var w = Measure("(fixture)", text, text.Length);
            Assert.AreEqual(1, w.GrassChunks);
            StringAssert.Contains("HideFlags.DontSave", Breach(w));
        }
    }
}
