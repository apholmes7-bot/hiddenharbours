using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>Targeted authoring entry. The explicit JSON plan supplies proposed Def values;
    /// geometry, walks and paints still go through their production bakers and importers.</summary>
    public static class RecreationBoatsIntakeBake
    {
        public static void Cli()
        {
            try
            {
                string[] args = System.Environment.GetCommandLineArgs();
                int arg = Array.IndexOf(args, "-hh-intake-plan");
                if (arg < 0 || arg + 1 >= args.Length) throw new ArgumentException("-hh-intake-plan is required");
                var rows = HullLayoutJson.List(DeckSidecarJson.Parse(File.ReadAllText(args[arg + 1])), "intake");
                var log = new StringBuilder();
                foreach (object raw in rows)
                {
                    var row = HullLayoutJson.Object(raw, "intake");
                    string key = HullLayoutJson.Text(row, "key");
                    var fleet = HullMeshFleet.Get(key);
                    var mesh = RigMeshAssetBaker.BakeOne(key);
                    foreach (var value in HullLayoutJson.Child(row, "meshGameFields"))
                        Set(mesh, value.Key, value.Value);
                    EditorUtility.SetDirty(mesh);

                    string templatePath = "Assets/_Project/Data/Boats/" + HullLayoutJson.Text(row, "template") + ".asset";
                    string boatPath = "Assets/_Project/Data/Boats/" + HullLayoutJson.Text(row, "boatAsset");
                    var boat = AssetDatabase.LoadAssetAtPath<BoatHullDef>(boatPath);
                    bool fresh = boat == null;
                    if (fresh && File.Exists(boatPath)) throw new IOException(boatPath + ": existing hull did not load");
                    if (fresh)
                    {
                        var template = AssetDatabase.LoadAssetAtPath<BoatHullDef>(templatePath);
                        if (template == null) throw new IOException(templatePath + ": template missing");
                        boat = UnityEngine.Object.Instantiate(template);
                        boat.name = Path.GetFileNameWithoutExtension(boatPath);
                    }
                    foreach (var value in HullLayoutJson.Child(row, "values"))
                    {
                        var proposed = HullLayoutJson.Object(value.Value, "values." + value.Key);
                        string source = HullLayoutJson.Text(proposed, "source");
                        if (value.Key == "Visual" || source.StartsWith(templatePath + " / ", StringComparison.Ordinal) ||
                            source.StartsWith("BoatHullDef.cs initializer / ", StringComparison.Ordinal)) continue;
                        Set(boat, value.Key, proposed["value"]);
                    }
                    boat.Visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(fleet.VisualAssetPaths.Single());
                    if (boat.Visual == null) throw new IOException(key + ": visual missing after bake");
                    if (fresh) AssetDatabase.CreateAsset(boat, boatPath);
                    EditorUtility.SetDirty(boat);
                    // A newly created visual did not exist when Bake imported its walk.
                    using (var host = RigScriptHostFactory.Create())
                    {
                        var data = RigMeshExtractor.ExtractFrom(host, fleet.ScriptPath, fleet.GlobalName, hull: fleet.Extraction);
                        var imported = HullWalkSidecarImporter.Import(data, HullLayoutJson.Text(row, "meshStem"), mesh,
                            new[] { boat.Visual });
                        if (!imported.Ok) throw new FormatException(string.Join("; ", imported.Refusals));
                    }
                    int count = HullPaintSchemeBaker.Bake(HullPaintSchemeBaker.Fleet.Single(p => p.RigKey == key), log);
                    log.AppendLine(key + ": " + mesh.Mesh.vertexCount + " body vertices, " +
                        mesh.DriveFaceVertexRanges.Length + " declared drive faces, " + count + " schemes; " + boat.Id);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[rec-boats-intake] BAKE_OK\n" + log);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("[rec-boats-intake] BAKE_FAILED\n" + error);
                EditorApplication.Exit(1);
            }
        }

        static void Set(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field == null) throw new ArgumentException(target.GetType().Name + "." + name + ": field missing");
            field.SetValue(target, value == null ? null : field.FieldType.IsEnum
                ? Enum.ToObject(field.FieldType, Convert.ToInt32(value, CultureInfo.InvariantCulture))
                : Convert.ChangeType(value, field.FieldType, CultureInfo.InvariantCulture));
        }
    }
}
