#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine.SceneManagement;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    /// <summary>Bring the committed berth data up to the builder's existing plan without rebuilding
    /// the scene. Each patch owns one root; every other serialized document keeps its bytes.</summary>
    public static class NineMileCreekBerthRefresh
    {
        public const string TrenchStep = "03-berth-trench";
        public static LayerPatch Trench(SceneYaml scene)
        {
            Doc root = scene.RootNamed("TidalTerrain");
            Doc ground = scene.ComponentsOf(root).Single(d => d.IsScript("HiddenHarbours.World.MainlandTidalTerrain"));
            const string marker = "  _channels:";
            int start = ground.Text.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) throw new Refusal("Berth trench: terrain has no channels field.");
            string old = ground.Text.Substring(start);
            MainlandChannel[] parsed = ParseChannels(old);
            MainlandChannel[] expected = parsed.Length == 1
                ? new[] { NineMileCreekMainland.HarbourChannel } : NineMileCreekMainland.Channels;
            RequireChannels(old, expected);
            var patch = new LayerPatch(TrenchStep, "TidalTerrain", root.FileId);
            if (parsed.Length == 1)
                patch.Edit(ground, ground.Text + Channels(NineMileCreekMainland.BerthTrench).Substring(marker.Length),
                    "TidalTerrain/MainlandTidalTerrain");
            return patch.Seal(scene);
        }

        /// <summary>Read the precise serialized schema; only numeric spelling may differ.</summary>
        public static MainlandChannel[] ParseChannels(string block)
        {
            string[] lines = block.Split('\n');
            int at = 0;
            void RequireLine(string expected)
            {
                if (at >= lines.Length || lines[at++] != expected)
                    throw new Refusal("Berth channels: field names, order or entry shape differ.");
            }
            float Number(string value)
            {
                if (!float.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float n) || float.IsNaN(n) || float.IsInfinity(n))
                    throw new Refusal("Berth channels: invalid finite float.");
                return n;
            }
            float Scalar(string field)
            {
                string prefix = "    " + field + ": ";
                if (at >= lines.Length || !lines[at].StartsWith(prefix, StringComparison.Ordinal))
                    throw new Refusal("Berth channels: expected " + field + " in schema order.");
                return Number(lines[at++].Substring(prefix.Length));
            }
            RequireLine("  _channels:");
            var result = new System.Collections.Generic.List<MainlandChannel>();
            while (at < lines.Length)
            {
                RequireLine("  - Waypoints:");
                var points = new System.Collections.Generic.List<UnityEngine.Vector2>();
                while (at < lines.Length && lines[at].StartsWith("    - ", StringComparison.Ordinal))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(lines[at++],
                        @"^    - \{x: ([^,]+), y: ([^}]+)\}$");
                    if (!match.Success) throw new Refusal("Berth channels: waypoint fields must be x then y.");
                    points.Add(new UnityEngine.Vector2(Number(match.Groups[1].Value), Number(match.Groups[2].Value)));
                }
                float bed = Scalar("BedElevation"), width = Scalar("HalfWidthMetres"), ceiling = Scalar("CuttableCeiling");
                result.Add(new MainlandChannel(points.ToArray(), bed, width, ceiling));
            }
            return result.ToArray();
        }

        public static void RequireChannels(string block, MainlandChannel[] expected)
        {
            MainlandChannel[] actual = ParseChannels(block);
            if (actual.Length != expected.Length) throw new Refusal("Berth channels: entry count differs.");
            for (int i = 0; i < actual.Length; i++)
            {
                var a = actual[i]; var e = expected[i];
                if (a.Waypoints.Length != e.Waypoints.Length || a.BedElevation != e.BedElevation ||
                    a.HalfWidthMetres != e.HalfWidthMetres || a.CuttableCeiling != e.CuttableCeiling)
                    throw new Refusal("Berth channels: exact plan values differ at entry " + i);
                for (int j = 0; j < a.Waypoints.Length; j++)
                    if (a.Waypoints[j].x != e.Waypoints[j].x || a.Waypoints[j].y != e.Waypoints[j].y)
                        throw new Refusal("Berth channels: exact waypoint differs at entry " + i + ", point " + j);
            }
        }

        static string Channels(params MainlandChannel[] channels)
        {
            var text = new StringBuilder("  _channels:");
            foreach (var c in channels)
            {
                text.Append("\n  - Waypoints:");
                foreach (var p in c.Waypoints)
                    text.Append($"\n    - {{x: {F(p.x)}, y: {F(p.y)}}}");
                text.Append($"\n    BedElevation: {F(c.BedElevation)}\n    HalfWidthMetres: {F(c.HalfWidthMetres)}\n    CuttableCeiling: {F(c.CuttableCeiling)}");
            }
            return text.ToString();
        }

        [MenuItem("Hidden Harbours/Nine Mile Creek Layers/Apply berth trench")]
        public static void ApplyTrench() => Apply(Trench);
        static void Apply(Func<SceneYaml, LayerPatch> plan)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path == NineMileCreekLayerRefresh.ScenePath)
                    throw new Refusal("Close NineMileCreek before applying its text patch.");
            string before = File.ReadAllText(NineMileCreekLayerRefresh.ScenePath);
            LayerPatch patch = plan(SceneYaml.Parse(before));
            string after = patch.ApplyTo(before);
            Directory.CreateDirectory(NineMileCreekLayerRefresh.PatchFolder);
            File.WriteAllText(Path.Combine(NineMileCreekLayerRefresh.PatchFolder, patch.Step + ".patch.yaml"), patch.ToYaml(), new UTF8Encoding(false));
            if (after != before) File.WriteAllText(NineMileCreekLayerRefresh.ScenePath, after, new UTF8Encoding(false));
            UnityEngine.Debug.Log(patch.Summary());
        }
    }
}
#endif
