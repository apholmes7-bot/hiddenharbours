using System;
using System.IO;
using System.Linq;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.RigBaking
{
    public sealed class HullWalkImportWiringTests
    {
        [Test]
        public void Import_LinksTheCruiserOnly_AndRefusesADifferentInterior()
        {
            foreach (string key in new[] { "goFast", "bowrider", "expressCruiser" })
            {
                var visual = ScriptableObject.CreateInstance<BoatVisualDef>();
                try
                {
                    var result = Import(key, visual);
                    Assert.That(result.Ok, Is.True, key + ": " + string.Join("; ", result.Refusals));
                    if (key == "expressCruiser")
                    {
                        var saved = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(
                            "Assets/_Project/Data/Boats/Interiors/ExpressCruiserIso.asset");
                        Assert.That(saved, Is.Not.Null);
                        Assert.That(saved.Id, Is.EqualTo("interior.express_cruiser_iso"));
                        Assert.That(saved.HasInterior(), Is.True);
                        Assert.That(visual.Interior, Is.SameAs(saved));
                    }
                    else Assert.That(visual.Interior, Is.Null, key + ": open or held-shut room remains unlinked");
                }
                finally { Object.DestroyImmediate(visual); }
            }

            var conflicting = ScriptableObject.CreateInstance<BoatVisualDef>();
            try
            {
                var planted = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(
                    "Assets/_Project/Data/Boats/Interiors/GoFastIso.asset");
                Assert.That(planted, Is.Not.Null, "the planted different interior must be a saved asset");
                conflicting.Interior = planted;
                var result = Import("expressCruiser", conflicting);
                Assert.That(result.Ok, Is.False);
                Assert.That(result.Refusals.Count, Is.EqualTo(1));
                StringAssert.Contains("different interior asset", result.Refusals.Single());
                Assert.That(conflicting.Interior, Is.SameAs(planted), "a different existing interior is never overwritten");
            }
            finally { Object.DestroyImmediate(conflicting); }
        }

        static HullWalkImportResult Import(string key, BoatVisualDef visual)
        {
            FleetHull hull = HullMeshFleet.Get(key);
            using var host = RigScriptHostFactory.Create();
            var data = RigMeshExtractor.ExtractFrom(host, hull.ScriptPath, hull.GlobalName, hull: hull.Extraction);
            var mesh = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
            if (mesh == null) throw new IOException(key + ": existing mesh missing; import never bakes one");
            string stem = Path.GetFileName(hull.MeshAssetPath).Replace("HullMesh.asset", "");
            return HullWalkSidecarImporter.Import(data, stem, mesh, new[] { visual });
        }

        // The authorized native repair uses the same importer, with no mesh bake or replacement.
        public static void ImportOnlyCli()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, "-hh-import-keys");
                if (at < 0 || at + 1 >= args.Length) throw new ArgumentException("-hh-import-keys required");
                foreach (string key in args[at + 1].Split(','))
                {
                    FleetHull hull = HullMeshFleet.Get(key);
                    var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(hull.VisualAssetPaths.Single());
                    if (visual == null) throw new IOException(key + ": existing visual missing");
                    var result = Import(key, visual);
                    if (!result.Ok) throw new FormatException(string.Join("; ", result.Refusals));
                    Debug.Log("[rec-intake-import] " + key + ": Interior=" +
                        (visual.Interior == null ? "null" : visual.Interior.Id));
                }
                Debug.Log("[rec-intake-import] IMPORT_ONLY_OK");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("[rec-intake-import] IMPORT_ONLY_FAILED " + error);
                EditorApplication.Exit(1);
            }
        }
    }
}
