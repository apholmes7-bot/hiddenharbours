#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The editor-side JSON reader the rod-kit importer runs on (<see cref="MiniJson"/>): the JSON
    /// grammar it must accept (the rig bakes' output), the failures it must throw on, and a live parse
    /// of the REAL committed anchor sidecar so the reader can never drift from the data it exists for.
    /// </summary>
    public class MiniJsonTests
    {
        [Test]
        public void Parses_TheWholeGrammar()
        {
            var root = MiniJson.Parse(
                "{ \"a\": 1.5, \"b\": [1, -2, 3e2], \"c\": {\"d\": \"x\\ny\"}, \"e\": true, " +
                "\"f\": false, \"g\": null, \"h\": \"\\u0041\" }") as Dictionary<string, object>;
            Assert.IsNotNull(root);
            Assert.AreEqual(1.5, (double)root["a"], 1e-9);
            var b = root["b"] as List<object>;
            Assert.AreEqual(3, b.Count);
            Assert.AreEqual(-2.0, (double)b[1], 1e-9);
            Assert.AreEqual(300.0, (double)b[2], 1e-9);
            Assert.AreEqual("x\ny", (MiniJson.Dict(root, "c"))["d"]);
            Assert.AreEqual(true, root["e"]);
            Assert.AreEqual(false, root["f"]);
            Assert.IsNull(root["g"]);
            Assert.AreEqual("A", root["h"]);
        }

        [Test]
        public void Parses_EmptyContainers_AndNestedArrays()
        {
            var root = MiniJson.Parse("{\"o\": {}, \"a\": [], \"n\": [[{\"x\": 1}]]}")
                as Dictionary<string, object>;
            Assert.AreEqual(0, (root["o"] as Dictionary<string, object>).Count);
            Assert.AreEqual(0, (root["a"] as List<object>).Count);
            var inner = (root["n"] as List<object>)[0] as List<object>;
            Assert.AreEqual(1.0, (double)((inner[0] as Dictionary<string, object>)["x"]), 1e-9);
        }

        [Test]
        public void Malformed_Throws_NeverHangs()
        {
            Assert.Throws<System.FormatException>(() => MiniJson.Parse("{\"a\": }"));
            Assert.Throws<System.FormatException>(() => MiniJson.Parse("{\"a\": 1"));
            Assert.Throws<System.FormatException>(() => MiniJson.Parse("[1, 2,"));
            Assert.Throws<System.FormatException>(() => MiniJson.Parse("\"unterminated"));
            Assert.Throws<System.FormatException>(() => MiniJson.Parse("{} trailing"));
            Assert.Throws<System.FormatException>(() => MiniJson.Parse(null));
        }

        [Test]
        public void TypedHelpers_DefaultOnMissing_NeverThrow()
        {
            object node = MiniJson.Parse("{\"n\": 3, \"s\": \"x\"}");
            Assert.AreEqual(3, MiniJson.Int(node, "n"));
            Assert.AreEqual(3f, MiniJson.Float(node, "n"), 1e-6f);
            Assert.AreEqual(7, MiniJson.Int(node, "missing", 7));
            Assert.IsNull(MiniJson.Dict(node, "missing"));
            Assert.IsNull(MiniJson.List(node, "missing"));
            Assert.IsNull(MiniJson.Dict(null, "anything"), "null nodes read as empty");
        }

        [Test]
        public void TheRealRodAnchorSidecar_Parses_AndCarriesTheKitShape()
        {
            // The reader exists to read THIS file — parse the committed sidecar and assert the shape
            // the importer depends on, so a re-bake that changes the schema fails here first.
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(RodKitImporter.RodAnchorsPath);
            Assert.IsNotNull(text, $"the committed sidecar must exist at {RodKitImporter.RodAnchorsPath}");
            object root = MiniJson.Parse(text.text);

            Assert.AreEqual(8, MiniJson.Int(root, "dirs"), "the kit is an 8-direction bake");
            var grips = MiniJson.Dict(root, "grips");
            var tiers = MiniJson.Dict(root, "tiers");
            Assert.IsNotNull(grips, "grips table");
            Assert.IsNotNull(tiers, "tiers table");
            Assert.IsNotNull(MiniJson.Dict(tiers, "cane"), "the cane tier ships");
            foreach (string state in RodKitImporter.RodStateOrder)
                Assert.IsNotNull(MiniJson.Dict(grips, state), $"grips.{state} — the importer's order " +
                    "must exist in the bake (a renamed state breaks the rod overlay silently otherwise)");
        }
    }
}
#endif
