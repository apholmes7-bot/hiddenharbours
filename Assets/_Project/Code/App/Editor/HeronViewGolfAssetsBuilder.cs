using System;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static class HeronViewGolfAssetsBuilder
    {
        public const string Builds = "Assets/_Project/Data/Characters/Builds";
        public const string Dialogues = "Assets/_Project/Data/NPCs/Dialogue";

        // Only baked structures; colour keys carry the nearest available tones.
        public static readonly (string stem, string hair, string outfit, string shirt, string hat)[] Looks = {
            ("Hand", "grey", "cream", "moss", "oat"),
            ("Hand", "black", "slate", "red", "char"),
            ("Cutter", "ginger", "char", "sky", "navy"),
            ("Skipper", "grey", "navy", "navy", "oat"),
            ("Ginny", "black", "plum", "cream", "char"),
            ("Hand", "black", "char", "moss", "char"),
            ("Nan", "white", "khaki", "ochre", "oat"),
            ("Hand", "sand", "cream", "cream", "navy"),
        };

        [MenuItem("Hidden Harbours/Regions/Cumberland/Build Approved Golf Assets (create missing)")]
        public static void Build()
        {
            var people = HeronViewRoster.Visitors;
            var bases = new CharacterBuildDef[people.Length];
            for (int i = 0; i < people.Length; i++)
            {
                bases[i] = AssetDatabase.LoadAssetAtPath<CharacterBuildDef>(Builds + "/" + Looks[i].stem + "Build.asset");
                if (bases[i] == null || !bases[i].HasBakedBody)
                    throw new InvalidOperationException("Missing baked structure: " + Looks[i].stem);
            }
            for (int i = 0; i < people.Length; i++)
            {
                var person = people[i]; var look = Looks[i]; var basis = bases[i];
                var build = Missing<CharacterBuildDef>(Builds + "/" + person.AssetName + "Build.asset", def => {
                    def.Id = person.BuildId; def.Preset = basis.Preset; def.Visual = basis.Visual;
                    def.Hair = look.hair; def.Outfit = look.outfit; def.Shirt = look.shirt; def.HatCol = look.hat;
                });
                var dialogue = Missing<DialogueDef>(Dialogues + "/" + person.AssetName + "First.asset", def => {
                    def.Id = person.DialogueId; def.FirstLines = new[] { person.Line };
                });
                Missing<NpcDef>(HeronViewRoutinesBuilder.NpcFolder + "/" + person.AssetName + ".asset", def => {
                    def.Id = person.NpcId; def.DisplayName = person.Name; def.Build = build;
                    def.Dialogue = dialogue; def.Kind = InteractKind.Talk;
                });
            }
            AssetDatabase.SaveAssets();
            HeronViewRoutinesBuilder.Build();
            Debug.Log("[HeronViewGolf] ASSETS OK: eight golfers; existing assets kept.");
        }

        static T Missing<T>(string path, Action<T> fill) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            T def = ScriptableObject.CreateInstance<T>(); fill(def);
            AssetDatabase.CreateAsset(def, path);
            return def;
        }

        public static void BuildCli() => HeronViewRoutinesBuilder.RunCli(Build);
    }
}
