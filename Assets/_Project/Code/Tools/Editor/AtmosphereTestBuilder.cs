#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tools.Editor
{
    /// <summary>
    /// REVERSIBLE demo harness for the LIVING-COAST AMBIENT PARTICLES (mirrors <see cref="GrassTestBuilder"/>).
    /// ONE menu item drops a small backdrop + a cottage-with-chimney into the current scene so the owner can
    /// press Play and IMMEDIATELY see the coast come alive: sea mist drifting low over the water, hearth smoke
    /// bending downwind off the chimney, gulls wheeling over the harbour, and dust motes shimmering by day —
    /// all moving together on one shared wind.
    ///
    /// <para>The mist, gulls and motes are SELF-INSTALLING (they spawn their own hidden persistent hosts before
    /// the first scene), so they appear in Play with NO wiring — this demo just gives them a backdrop to read
    /// against and a dev wind so they move even with no sim. The chimney smoke is the one POSITIONED effect, so
    /// the demo drops a <see cref="ChimneySmoke"/> on the cottage chimney (the same component
    /// <c>StPetersBuilder</c> wires at the real cottage). It is ADDITIVE and surgical: it touches no committed
    /// scene/prefab/Data asset; delete the spawned "AtmosphereTest" object to fully revert.</para>
    ///
    /// Menu: <b>Hidden Harbours ▸ Build Atmosphere Test</b>.
    /// </summary>
    public static class AtmosphereTestBuilder
    {
        private const string MenuPath = "Hidden Harbours/Build Atmosphere Test";
        private const string RootName = "AtmosphereTest";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            // Remove a prior rig so re-running is idempotent.
            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;
            // A gentle veering test wind so the demo mist/smoke/gulls drift even with no environment sim.
            // The SAME GrassDevWind drives the shared _WindWorld the ambient effects read (cohesion), and it
            // stands down the moment a real sim is present (St Peters / Bootstrap).
            root.AddComponent<GrassDevWind>();

            // --- a sea backdrop so the mist + gulls read against something (in-memory sprite; not saved). ---
            BuildFill(root.transform, "SeaBackdrop", new Color(0.16f, 0.34f, 0.44f, 1f),
                      new Vector3(40f, 26f, 1f), new Vector3(0f, 0f, 0f), -100);
            // a strip of shore at the bottom so the shore-spray reads where water meets land
            BuildFill(root.transform, "Shore", new Color(0.74f, 0.68f, 0.52f, 1f),
                      new Vector3(40f, 5f, 1f), new Vector3(0f, -10f, 0f), -90);

            // --- the cottage with a chimney carrying live smoke (the one positioned ambient effect). ---
            var cottage = BuildFill(root.transform, "Cottage", new Color(0.55f, 0.40f, 0.33f, 1f),
                                    new Vector3(3.2f, 3f, 1f), new Vector3(-6f, -6f, 0f), 2);
            var roof = BuildFill(cottage.transform, "Roof", new Color(0.40f, 0.26f, 0.22f, 1f),
                                 new Vector3(1.2f, 0.35f, 1f), new Vector3(0f, 0.62f, 0f), 3);
            // the chimney: a small block, with the smoke component dropped at its flue
            var chimney = BuildFill(cottage.transform, "Chimney", new Color(0.45f, 0.30f, 0.26f, 1f),
                                    new Vector3(0.10f, 0.22f, 1f), new Vector3(0.18f, 0.62f, 0f), 4);
            chimney.AddComponent<ChimneySmoke>();   // smoke leaves the flue and bends downwind

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);

            Debug.Log(
                "[AtmosphereTest] Spawned 'AtmosphereTest'. Press Play, then watch:\n" +
                "  - SEA MIST drifts low over the water (self-installing; thickens with fog/sea-state in St Peters);\n" +
                "  - CHIMNEY SMOKE rises off the cottage flue and bends DOWNWIND on the shared wind;\n" +
                "  - GULLS wheel over the harbour on looping paths, occasionally (self-installing);\n" +
                "  - DUST MOTES shimmer by day and fade after dark (self-installing).\n" +
                "All ride the SAME wind the grass + water read; all dim/warm with the day/night cycle.\n" +
                "Tune each effect on its component (SeaMistEmitter / ChimneySmoke / GullFlock / DustMotes).\n" +
                "Delete the 'AtmosphereTest' object to fully revert. In St Peters: chimney smoke is wired at the " +
                "cottage by 'Build St Peters'; mist + gulls + motes self-install.");
        }

        // ---- helpers -------------------------------------------------------------------------------------

        private static GameObject BuildFill(Transform parent, string name, Color color,
                                            Vector3 scale, Vector3 localPos, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GenerateMarker(color);
            sr.sortingOrder = sortingOrder;
            return go;
        }

        /// <summary>A solid 1x1 marker sprite. In-memory; not saved.</summary>
        private static Sprite GenerateMarker(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.hideFlags = HideFlags.DontSave;
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
#endif
