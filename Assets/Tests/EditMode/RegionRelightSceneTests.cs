using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// PR 5a: St Peters and Nine Mile Creek draw their ground relit (terrain pass 9's TerrainLight6, under the
    /// day/night cycle's sun). Each region's TerrainSplatSurface holds the four relight maps as scene lines. A
    /// line lost, pointed at the wrong object, or naming a map that does not fit the region's detail array
    /// leaves the region unrelit, and quietly: the surface keeps the albedo and says so once in the console
    /// (TerrainSplatSurface.RelightLoaded). This guard catches it from the scene files' text and opens no scene,
    /// like TerrainArrayReferenceTests (ARRAYS' guard for the detail line, ADR 0047).
    ///
    /// <para>The bars are not the lines under test. The references are the ones the AssetDatabase holds for the
    /// shared loader's four maps (TerrainArrayAssets.LoadRelightRequired). The fit is the shader include's,
    /// written out by hand as in TerrainRelightBindingTests: slices TL6_TILE texels square, a ramp
    /// TL6_RAMP_PARAMS + 1 wide, and the depth of the detail array the region's own <c>_detailArray256</c> line
    /// names. The last four tests are the guard's own controls: engine-free fixtures that each lose a line or a
    /// fit, so the regions' pass is not a reader that sees nothing.</para>
    /// </summary>
    public class RegionRelightSceneTests
    {
        const string ScenesRoot = "Assets/_Project/Scenes/";
        const string SurfaceScript = "09c13e4d6f71605bc28d5e0f1a32b4c6";   // TerrainSplatSurface.cs.meta
        const int Tile = 256;      // TL6_TILE
        const int RampWidth = 81;  // sixteen palettes of five, then the parameters (TL6_RAMP_PARAMS = 80)

        /// <summary>The surface's relight fields in its own order (TerrainSplatSurface.cs:62-65), which is also
        /// the loader's: Normal, Light, Detail, Ramp.</summary>
        static readonly string[] RelightFields = { "_relightNormal", "_relightLight", "_relightDetail", "_relightRamp" };

        // ============================ THE REGIONS ============================

        [TestCase("StPeters", "1201741992")]
        [TestCase("NineMileCreek", "1396913515")]
        public void TheRegionsGround_NamesTheFourRelightMaps_AndEachFitsItsDetailArray(string scene, string surfaceId)
        {
            string path = ScenesRoot + scene + ".unity";
            string yaml = File.ReadAllText(path);

            TerrainArrayAssets.RelightSet set = TerrainArrayAssets.LoadRelightRequired();
            var want = new[] { Identity(set.Normal), Identity(set.Light), Identity(set.Detail), Identity(set.Ramp) };
            List<string> faults = LineFaults(yaml, surfaceId, want);
            Assert.IsEmpty(faults, path + ": the region would draw unrelit.\n  " + string.Join("\n  ", faults));

            // Each map loaded as the scene loads it, by its own line's guid and local id, and fitted to the
            // detail array the region's own line names.
            string block = SurfaceBlock(yaml, surfaceId);
            var detail = Resolve<Texture2DArray>(block, "_detailArray256", path);
            var normal = Resolve<Texture2DArray>(block, RelightFields[0], path);
            var light = Resolve<Texture2DArray>(block, RelightFields[1], path);
            var marks = Resolve<Texture2DArray>(block, RelightFields[2], path);
            var ramp = Resolve<Texture2D>(block, RelightFields[3], path);
            faults = FitFaults(detail.depth, Shape(normal), Shape(light), Shape(marks), (ramp.width, ramp.height));
            Assert.IsEmpty(faults, path + ": the surface would refuse the maps and keep the albedo.\n  " +
                                   string.Join("\n  ", faults));
        }

        // ============================ THE GUARD'S OWN CONTROLS ============================
        // A region in miniature: the surface's block between two other documents, the second of which holds a
        // relight line of its own that must not count. Pure text and numbers, so these run anywhere.

        const string FixtureId = "42";
        const string FixtureGuid = "0123456789abcdef0123456789abcdef";
        const string FixtureDetailGuid = "fedcba9876543210fedcba9876543210";
        static readonly (string Guid, long FileId)[] FixtureWant =
            { (FixtureGuid, -11), (FixtureGuid, -22), (FixtureGuid, -33), (FixtureGuid, 44) };

        static string[] FixtureLines() => new[]
        {
            "  _relightNormal: {fileID: -11, guid: " + FixtureGuid + ", type: 3}",
            "  _relightLight: {fileID: -22, guid: " + FixtureGuid + ", type: 3}",
            "  _relightDetail: {fileID: -33, guid: " + FixtureGuid + ", type: 3}",
            "  _relightRamp: {fileID: 44, guid: " + FixtureGuid + ", type: 3}",
        };

        static string Fixture(IEnumerable<string> relightLines, string script = SurfaceScript) => string.Join("\n",
            new[]
            {
                "--- !u!1 &41", "GameObject:", "  m_Name: Ground",
                "--- !u!114 &" + FixtureId, "MonoBehaviour:", "  m_GameObject: {fileID: 41}",
                "  m_Script: {fileID: 11500000, guid: " + script + ", type: 3}",
                "  _detailArray256: {fileID: -9, guid: " + FixtureDetailGuid + ", type: 3}",
                "  _splatE: {fileID: 0}",
            }
            .Concat(relightLines)
            .Concat(new[]
            {
                "  _floorPaint: -2.6",
                "--- !u!114 &43", "MonoBehaviour:",
                "  _relightNormal: {fileID: -11, guid: " + FixtureGuid + ", type: 3}",
                "",
            }));

        [Test]
        public void TheLineCheck_PassesTheFourLines_AndRedsOnEachOneLost()
        {
            Assert.IsEmpty(LineFaults(Fixture(FixtureLines()), FixtureId, FixtureWant),
                "premise: the fixture's four lines are the ones wanted, so each red below is its edit's");
            for (int lost = 0; lost < RelightFields.Length; lost++)
            {
                int dropped = lost;
                List<string> faults = LineFaults(Fixture(FixtureLines().Where((line, i) => i != dropped)),
                                                 FixtureId, FixtureWant);
                AssertOneFault(faults, lost, "the line lost (the next block's own line must not stand in for it)");
            }
        }

        [Test]
        public void TheLineCheck_RedsOnAWrongOrDoubledReference()
        {
            // Each edit spoils one line of the four; the check must name that line and no other.
            var edits = new (int Line, string From, string To, string Why)[]
            {
                (0, "guid: " + FixtureGuid, "guid: " + FixtureDetailGuid, "the detail recipe's guid"),
                (1, "fileID: -22", "fileID: -33", "another map's local id"),
                (2, "type: 3", "type: 2", "a native asset's reference, not an imported object's"),
                (3, "{fileID: 44, guid: " + FixtureGuid + ", type: 3}", "{fileID: 0}", "a null reference"),
            };
            foreach (var edit in edits)
            {
                string[] lines = FixtureLines();
                StringAssert.Contains(edit.From, lines[edit.Line], "premise: the edit applies");
                lines[edit.Line] = lines[edit.Line].Replace(edit.From, edit.To);
                AssertOneFault(LineFaults(Fixture(lines), FixtureId, FixtureWant), edit.Line, edit.Why);
            }

            string[] doubled = FixtureLines().Concat(new[] { FixtureLines()[1] }).ToArray();
            AssertOneFault(LineFaults(Fixture(doubled), FixtureId, FixtureWant), 1, "a doubled line");
        }

        [Test]
        public void TheLineCheck_RedsWhenTheBlockIsGoneOrNotTheSurfaces()
        {
            List<string> gone = LineFaults(Fixture(FixtureLines()), "40", FixtureWant);
            Assert.AreEqual(1, gone.Count, string.Join("; ", gone));
            StringAssert.Contains("&40", gone[0]);

            List<string> foreign = LineFaults(Fixture(FixtureLines(), script: FixtureDetailGuid), FixtureId, FixtureWant);
            Assert.AreEqual(1, foreign.Count, string.Join("; ", foreign));
            StringAssert.Contains("not the TerrainSplatSurface", foreign[0]);
        }

        [Test]
        public void TheFitCheck_PassesMapsThatFit_AndRedsOnEachMisfit()
        {
            int depth = 63;   // any depth: the fit is the maps' against the detail array's
            (int, int, int) fits = (Tile, Tile, depth);
            Assert.IsEmpty(FitFaults(depth, fits, fits, fits, (RampWidth, depth)), "premise: maps that fit pass");

            AssertOneFault(FitFaults(depth, (Tile, Tile, depth - 1), fits, fits, (RampWidth, depth)), 0,
                "a normal map one slice short");
            AssertOneFault(FitFaults(depth, fits, (Tile / 2, Tile, depth), fits, (RampWidth, depth)), 1,
                "a light map half as wide");
            AssertOneFault(FitFaults(depth, fits, fits, (Tile, Tile / 2, depth), (RampWidth, depth)), 2,
                "a detail map half as tall");
            AssertOneFault(FitFaults(depth, fits, fits, fits, (RampWidth - 1, depth)), 3,
                "a ramp with no column for the parameters");
            AssertOneFault(FitFaults(depth, fits, fits, fits, (RampWidth, depth - 1)), 3, "a ramp one row short");
            Assert.AreEqual(4, FitFaults(depth + 1, fits, fits, fits, (RampWidth, depth)).Count,
                "a detail array one slice deeper than all four maps");
        }

        // ============================ the guard ============================

        /// <summary>The surface's document, found by its object id as ARRAYS' guard finds it; null when the
        /// scene holds no such document.</summary>
        internal static string SurfaceBlock(string yaml, string surfaceId)
        {
            Match block = Regex.Match(yaml, @"(?ms)^--- !u!114 &" + Regex.Escape(surfaceId) + @"\r?\n(?<body>.*?)(?=^---|\z)");
            return block.Success ? block.Value : null;
        }

        /// <summary>What is wrong with a region's relight lines, one fault a line; none when the surface's block
        /// names each map once, as an imported object (type 3), by the wanted guid and local id.</summary>
        internal static List<string> LineFaults(string yaml, string surfaceId,
                                                IReadOnlyList<(string Guid, long FileId)> want)
        {
            var faults = new List<string>();
            string block = SurfaceBlock(yaml, surfaceId);
            if (block == null)
            {
                faults.Add("no document &" + surfaceId + ": the ground's component is gone");
                return faults;
            }
            if (!block.Contains("  m_Script: {fileID: 11500000, guid: " + SurfaceScript + ", type: 3}"))
            {
                faults.Add("&" + surfaceId + " is not the TerrainSplatSurface");
                return faults;
            }
            for (int i = 0; i < RelightFields.Length; i++)
            {
                string field = RelightFields[i];
                MatchCollection lines = RefLines(block, field);
                if (lines.Count != 1)
                {
                    faults.Add(field + (lines.Count == 0 ? ": no reference line, so the map is not bound"
                                                         : ": " + lines.Count + " reference lines"));
                    continue;
                }
                string guid = lines[0].Groups["guid"].Value, type = lines[0].Groups["type"].Value;
                long fileId = long.Parse(lines[0].Groups["id"].Value, CultureInfo.InvariantCulture);
                if (guid != want[i].Guid) faults.Add(field + ": guid " + guid + ", not the recipe's " + want[i].Guid);
                else if (fileId != want[i].FileId) faults.Add(field + ": local id " + fileId + ", not the map's " + want[i].FileId);
                if (type != "3") faults.Add(field + ": type " + type + ", not an imported object's 3");
            }
            return faults;
        }

        /// <summary>What would make the surface refuse the maps, one fault a map: each array as deep as the
        /// detail array with slices <see cref="Tile"/> texels square, and the ramp <see cref="RampWidth"/> wide
        /// with a row per slice.</summary>
        internal static List<string> FitFaults(int depth, (int Width, int Height, int Depth) normal,
                                               (int Width, int Height, int Depth) light,
                                               (int Width, int Height, int Depth) detail, (int Width, int Height) ramp)
        {
            var faults = new List<string>();
            var arrays = new[] { normal, light, detail };
            for (int i = 0; i < arrays.Length; i++)
                if (arrays[i].Width != Tile || arrays[i].Height != Tile || arrays[i].Depth != depth)
                    faults.Add($"{RelightFields[i]}: {arrays[i].Width}x{arrays[i].Height}x{arrays[i].Depth}, " +
                               $"not {Tile}x{Tile}x{depth}");
            if (ramp.Width != RampWidth || ramp.Height != depth)
                faults.Add($"{RelightFields[3]}: {ramp.Width}x{ramp.Height}, not {RampWidth}x{depth}");
            return faults;
        }

        static MatchCollection RefLines(string block, string field) => Regex.Matches(block,
            @"(?m)^  " + field + @": \{fileID: (?<id>-?\d+), guid: (?<guid>[0-9a-f]{32}), type: (?<type>\d+)\}\r?$");

        /// <summary>The object a reference line names, loaded as the scene loads it: by the line's guid and
        /// local id, among the objects imported at that guid's path.</summary>
        static T Resolve<T>(string block, string field, string path) where T : Object
        {
            MatchCollection lines = RefLines(block, field);
            Assert.AreEqual(1, lines.Count, path + ": " + field + " is not one reference line");
            string guid = lines[0].Groups["guid"].Value;
            long localId = long.Parse(lines[0].Groups["id"].Value, CultureInfo.InvariantCulture);
            string asset = AssetDatabase.GUIDToAssetPath(guid);
            Assert.IsNotEmpty(asset, path + ": " + field + "'s guid " + guid + " is no asset in this project");
            T found = AssetDatabase.LoadAllAssetsAtPath(asset).OfType<T>()
                .SingleOrDefault(o => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string g, out long f)
                                      && g == guid && f == localId);
            Assert.IsNotNull(found, path + ": " + field + " (" + localId + " in " + asset + ") is no " + typeof(T).Name);
            return found;
        }

        static (string Guid, long FileId) Identity(Object map)
        {
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(map, out string guid, out long localId),
                map.name + " has no identity in the AssetDatabase");
            return (guid, localId);
        }

        static (int Width, int Height, int Depth) Shape(Texture2DArray map) => (map.width, map.height, map.depth);

        static void AssertOneFault(List<string> faults, int field, string why)
        {
            Assert.AreEqual(1, faults.Count, why + ": " + string.Join("; ", faults));
            StringAssert.StartsWith(RelightFields[field] + ":", faults[0], why);
        }
    }
}
