#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>Wharf sections join at structural sockets, independent of the sprite crop and fittings.</summary>
    public static class WharfModules
    {
        public static string Key(string baseKey, int index, int count, bool forward = true)
        {
            if (count < 1 || index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            if (count == 1) return baseKey;
            if (!forward) index = count - 1 - index;
            return baseKey + (index == 0 ? "Start" : index == count - 1 ? "End" : "Middle");
        }

        /// <summary>CCW sprite index to Unity picture displacement: rotate, then squash ground Y once.</summary>
        public static Vector2 Step(float runMetres, int facing)
        {
            float angle = facing * 45f * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle) * SpriteLightMath.GroundDepthScale) * runMetres;
        }
    }

    /// <summary>Builds reusable prefab runs from the measured bake contract.</summary>
    public sealed class WharfModuleBuilder : EditorWindow
    {
        const string ContractPath = "Assets/_Project/Art/Sprites/Wharf/Iso/wharfIsoRig.contract.json";
        [Serializable] sealed class Module { public string @base; public string part; public float run; }
        [Serializable] sealed class Cell { public string key; public Module module; }
        [Serializable] sealed class Contract { public Cell[] cells; }
        Cell[] _modules;
        int _selected, _count = 3, _facing, _sortingOrder = SortingBands.WharfDeckMax - 1;

        [MenuItem("Hidden Harbours/Art/Build Modular Wharf Run")]
        static void Open() => GetWindow<WharfModuleBuilder>("Wharf modules");

        void OnEnable()
        {
            _modules = JsonUtility.FromJson<Contract>(File.ReadAllText(ContractPath)).cells
                .Where(c => c.module != null && c.module.part == "middle").ToArray();
        }

        void OnGUI()
        {
            if (_modules == null || _modules.Length == 0) { EditorGUILayout.HelpBox("Import the modular wharf bake first.", MessageType.Info); return; }
            _selected = EditorGUILayout.Popup("Construction", Mathf.Clamp(_selected, 0, _modules.Length - 1), _modules.Select(c => c.module.@base).ToArray());
            _count = EditorGUILayout.IntSlider("Sections", _count, 2, 64);
            _facing = EditorGUILayout.IntSlider("Facing", _facing, 0, 7);
            _sortingOrder = EditorGUILayout.IntField("Sorting order", _sortingOrder);
            EditorGUILayout.HelpBox("Creates a prefab with a start, repeatable middles and an end. Positions use structural length at 32 PPU. Float runs are level; connect the assembled run to one FloatingPlatform for tide motion.", MessageType.Info);
            if (GUILayout.Button("Create prefab and place in scene")) Build();
        }

        void Build()
        {
            var module = _modules[_selected].module;
            var sprites = Enumerable.Range(0, _count).Select(i => IsoPackSprites.Facing("wharfIso", WharfModules.Key(module.@base, i, _count), _facing)).ToArray();
            if (sprites.Any(s => s == null)) throw new InvalidOperationException("A module sprite is missing. Import the complete wharf bake before building.");
            var root = new GameObject($"{module.@base}_{_count}_Facing{_facing}");
            try
            {
                Vector2 step = WharfModules.Step(module.run, _facing);
                for (int i = 0; i < sprites.Length; i++)
                {
                    var section = new GameObject(WharfModules.Key(module.@base, i, _count));
                    section.transform.SetParent(root.transform, false);
                    section.transform.localPosition = step * (i - (_count - 1) * 0.5f);
                    var sr = section.AddComponent<SpriteRenderer>();
                    sr.sprite = sprites[i]; sr.sortingOrder = _sortingOrder;
                }
                const string folder = "Assets/_Project/Prefabs/WharfModules";
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "WharfModules");
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{root.name}.prefab");
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Place modular wharf");
                if (Selection.activeTransform != null) instance.transform.position = Selection.activeTransform.position;
                Selection.activeGameObject = instance;
            }
            finally { DestroyImmediate(root); }
        }
    }
}
#endif
