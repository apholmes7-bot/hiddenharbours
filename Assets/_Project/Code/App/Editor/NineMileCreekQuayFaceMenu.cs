#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>ONE MENU ITEM: re-draw Nine Mile Creek's quay face in the scene that is open.</b>
    ///
    /// <para><b>Why it exists.</b> The courses in the committed scene were placed before the sea could
    /// climb them, so they carry no <see cref="HiddenHarbours.Art.TidalFaceWaterline"/> and draw the wall
    /// dry through every tide. The code fix cannot reach a placed object on its own, and the only
    /// existing way to re-place one was <c>Build Nine Mile Creek Scene</c>, which re-derives the whole
    /// region and takes the authored layer with it. The face is pure builder output — six runs derived
    /// off the wharf's own geometry, nothing hand-placed in it — so it is exactly the piece that may be
    /// dropped and re-derived on its own.</para>
    ///
    /// <para><b>It is also the A/B rig.</b> Re-place, shoot the plate, set every course's
    /// <c>SetRidesTheTide(false)</c>, shoot again: the same placed dressing with one term changed, which
    /// is the only pair that is about this change rather than about two months of other merges.</para>
    /// </summary>
    public static class NineMileCreekQuayFaceMenu
    {
        [MenuItem("Hidden Harbours/Nine Mile Creek/Re-draw the Quay Face (the sea climbs it)",
                  priority = 20)]
        public static void RedrawQuayFace()
        {
            GameObject root = GameObject.Find(NineMileCreekDressing.RootName);
            if (root == null)
            {
                Debug.LogError(
                    $"[NineMileCreekQuayFace] No '{NineMileCreekDressing.RootName}' in the open scene — " +
                    "this is Nine Mile Creek's dressing root, so either the wrong scene is open or the " +
                    "region has never been built. Nothing was changed.");
                return;
            }

            var terrain = Object.FindFirstObjectByType<MainlandTidalTerrain>();
            if (terrain == null)
            {
                Debug.LogError(
                    "[NineMileCreekQuayFace] No MainlandTidalTerrain in the open scene, and the face's " +
                    "waterline is measured off it — a wall's lip is whatever deck it holds up, never a " +
                    "constant. Nothing was changed.");
                return;
            }

            int placed = NineMileCreekDressing.ReplaceFace(root, terrain);
            EditorSceneManager.MarkSceneDirty(root.scene);

            Debug.Log(
                $"[NineMileCreekQuayFace] Re-drew {placed} course(s). Each one now carries its own lip " +
                "and the elevation of the deck it holds up, and stops drawing where the published sea " +
                "stands over it — so the wall bares 5.2 m at spring low and 0.8 m at spring high, and a " +
                "hull alongside meets the water on the line the wall meets it on. Save the scene to keep " +
                "it.");
        }
    }
}
#endif
