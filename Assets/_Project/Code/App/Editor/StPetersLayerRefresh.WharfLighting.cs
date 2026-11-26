#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The St Peters pier lanterns' strength and pool, from the wharf lighting record</b>
    /// (<c>lighting.stp_wharf</c>, <see cref="WharfLightingPath"/>). The landing-dock step writes the lanterns
    /// (<c>DockLamps</c>) and reads the record; this applies just that step's lamp documents, so the record can be
    /// retuned without replaying the rest of the landing (the north pocket's ground write in particular).
    /// </summary>
    public static partial class StPetersLayerRefresh
    {
        public const string WharfLightingPath = "Assets/_Project/Data/Regions/StPeters/WharfLighting.asset";
        const string WharfLightingStep = "stp-wharf-lighting";

        /// <summary>The St Peters wharf's lighting record.</summary>
        public static WharfLightingDef WharfLighting() => AssetDatabase.LoadAssetAtPath<WharfLightingDef>(WharfLightingPath)
            ?? throw new Refusal(WharfLightingStep + ": no wharf lighting record at " + WharfLightingPath + ".");

        /// <summary>The landing-dock patch, refused unless every change it makes is an edit to a document under the
        /// pier's own <c>Lamps</c> root. Anything else means the landing has drifted from its plan, which is not this
        /// step's to settle.</summary>
        public static LayerPatch PlanWharfLighting(SceneYaml scene)
        {
            LayerPatch patch = LandingDock(scene);
            Doc root = scene.RootNamed(StPetersWharf.RootName);
            Doc lamps = scene.ChildNamed(scene.TransformOf(root), "Lamps");
            var owned = scene.SubtreeOf(lamps.FileId);
            foreach (var op in patch.Ops)
                if (op.Kind != OpKind.Edit || !owned.Contains(op.FileId))
                    throw new Refusal(WharfLightingStep + ": the landing-dock plan changes more than the pier lamps: " +
                                      op.Kind + " " + op.Name);
            return patch;
        }

        /// <summary>The batch entry: applies the lamp edits, then plans again on what it wrote, which must be empty.
        /// Exits 1 on any refusal, so a half-written scene never reports success.</summary>
        public static void WharfLightingApplyCli()
        {
            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).path == ScenePath)
                        throw new Refusal("StPeters is open. Close it before applying text patches.");
                string before = File.ReadAllText(ScenePath);
                LayerPatch patch = PlanWharfLighting(SceneYaml.Parse(before));
                Debug.Log("[" + WharfLightingStep + "] " + patch.Summary());
                string after = patch.ApplyTo(before);
                if (after != before) File.WriteAllText(ScenePath, after, new UTF8Encoding(false));
                if (!PlanWharfLighting(SceneYaml.Parse(after)).IsEmpty)
                    throw new Refusal(WharfLightingStep + ": a second plan is not empty.");
                Debug.Log("[" + WharfLightingStep + "] APPLIED; a second plan is empty.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
