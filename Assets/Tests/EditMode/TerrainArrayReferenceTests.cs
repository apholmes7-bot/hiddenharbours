using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    public class TerrainArrayReferenceTests
    {
        [TestCase("StPeters")]
        [TestCase("NineMileCreek")]
        public void SceneDetailReference_ResolvesToImportedFullDepthArray(string scene)
        {
            string path = "Assets/_Project/Scenes/" + scene + ".unity";
            string id = scene == "StPeters" ? "1201741992" : "1396913515";
            string yaml = File.ReadAllText(path);
            Match block = Regex.Match(yaml, @"(?ms)^--- !u!114 &" + id + @"\r?\n(?<body>.*?)(?=^---|\z)");
            Assert.IsTrue(block.Success, path + ": ground component missing.");
            StringAssert.Contains("guid: 09c13e4d6f71605bc28d5e0f1a32b4c6", block.Value);
            MatchCollection fields = Regex.Matches(block.Value,
                @"(?m)^  _detailArray256: \{fileID: (?<id>-?\d+), guid: (?<guid>[a-f0-9]{32}), type: (?<type>\d+)\}\r?$");
            Assert.AreEqual(1, fields.Count, path + ": expected exactly one detail reference.");
            Match field = fields[0];
            var imported = TerrainArrayAssets.LoadDetailRequired();
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(imported, out string guid, out long localId));
            Assert.AreEqual("3", field.Groups["type"].Value, path + ": native reference was not migrated.");
            Assert.AreEqual(guid, field.Groups["guid"].Value, path);
            Assert.AreEqual(localId, long.Parse(field.Groups["id"].Value, System.Globalization.CultureInfo.InvariantCulture), path);
            string recipe = AssetDatabase.GUIDToAssetPath(guid);
            var resolved = AssetDatabase.LoadAllAssetsAtPath(recipe).OfType<Texture2DArray>()
                .SingleOrDefault(a => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out string g, out long f)
                    && g == guid && f == localId);
            Assert.IsNotNull(resolved, path + ": YAML identity does not resolve to a Texture2DArray.");
            Assert.AreEqual(256, resolved.width);
            Assert.AreEqual(256, resolved.height);
            Assert.AreEqual(TerrainTexArrayBuilder.Depth, resolved.depth);
            Assert.AreEqual(63, resolved.depth);
        }
    }
}
