#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Owner-facing tooling for the ADDITIVE 2D LIGHTS (ADR 0016). Two ways for Alex to SEE the night-lighting
    /// payoff with no hand-wiring, mirroring the SpriteShadow menu:
    ///
    /// <list type="bullet">
    /// <item><description><b>Hidden Harbours ▸ Lighting ▸ Add Light to Selection</b> — adds a light to every
    /// selected object. The TYPE is chosen by the sub-menu; today the built one is <b>Spotlight</b> (a
    /// <see cref="BoatSpotlight"/> cone — drop it on the boat). The other types (Worklight = radial,
    /// Window Glow = warm radial spill, Lightpost = static radial) are FOLLOW-UPS — their menu entries are
    /// present + structured (<see cref="LightPreset"/>) but route through the same generic radial
    /// <see cref="SceneLight"/> setup as stubs, not bespoke components yet.</description></item>
    /// <item><description><b>Hidden Harbours ▸ Build Light Test</b> — a REVERSIBLE demo (mirrors "Build Shadow
    /// Test"): drops a DARK ground plane and a couple of lights — a boat-spotlight-like CONE and a round
    /// RADIAL — into the current scene. Press Play, scrub the clock to NIGHT, and watch the beam CUT THROUGH
    /// the dark. Delete the spawned "LightTest" object to fully revert.</description></item>
    /// </list>
    ///
    /// It touches no committed scene / prefab / Data asset and writes nothing to disk — surgical + additive,
    /// exactly like the grass/shadow demos. Real-scene lights get added via the menu or by the owning lane later
    /// (this tool never edits the scene builders).
    /// </summary>
    public static class LightMenu
    {
        private const string AddSpotlightPath = "Hidden Harbours/Lighting/Add Light to Selection/Spotlight (boat)";
        private const string AddWorklightPath = "Hidden Harbours/Lighting/Add Light to Selection/Worklight (radial, stub)";
        private const string AddWindowPath    = "Hidden Harbours/Lighting/Add Light to Selection/Window Glow (radial, stub)";
        private const string AddLightpostPath = "Hidden Harbours/Lighting/Add Light to Selection/Lightpost (radial, stub)";
        private const string BuildMenuPath    = "Hidden Harbours/Build Light Test";
        private const string RootName         = "LightTest";

        /// <summary>
        /// The light TYPES the menu can add. Spotlight is the concrete, shipped one (a <see cref="BoatSpotlight"/>);
        /// the rest are structured FOLLOW-UPS (ADR 0016) — they currently configure a generic radial
        /// <see cref="SceneLight"/> as a sensible stub so the extension points are real and demonstrable, and a
        /// future bespoke component (Worklight / WindowGlow / Lightpost) slots in here.
        /// </summary>
        public enum LightPreset { Spotlight, Worklight, WindowGlow, Lightpost }

        // ---- Add to selection (by type) ------------------------------------------------------------------

        [MenuItem(AddSpotlightPath, priority = 40)]
        public static void AddSpotlight() => AddToSelection(LightPreset.Spotlight);

        [MenuItem(AddWorklightPath, priority = 41)]
        public static void AddWorklight() => AddToSelection(LightPreset.Worklight);

        [MenuItem(AddWindowPath, priority = 42)]
        public static void AddWindowGlow() => AddToSelection(LightPreset.WindowGlow);

        [MenuItem(AddLightpostPath, priority = 43)]
        public static void AddLightpost() => AddToSelection(LightPreset.Lightpost);

        private static void AddToSelection(LightPreset preset)
        {
            var targets = new List<GameObject>();
            foreach (var go in Selection.gameObjects)
                if (go != null) targets.Add(go);

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Add Light",
                    "Select one or more objects in the scene (e.g. the boat for a Spotlight), then run this again.",
                    "OK");
                return;
            }

            foreach (var go in targets)
                ConfigureLightOn(go, preset);

            Debug.Log($"[Light] Added a {preset} light to {targets.Count} object(s). Press Play and scrub the " +
                      "clock to NIGHT — the light cuts through the dark (it auto-gates: ~invisible by day, full " +
                      "at night). Tune colour / cone / intensity / range on the SceneLight (and BoatSpotlight) " +
                      "component, or on Resources/AdditiveLight.mat for the shared look.");
        }

        /// <summary>Attach + configure the right light component(s) on <paramref name="go"/> for the preset.</summary>
        private static void ConfigureLightOn(GameObject go, LightPreset preset)
        {
            if (preset == LightPreset.Spotlight)
            {
                // The concrete boat spotlight: a BoatSpotlight (which adds + drives a SceneLight cone).
                if (go.GetComponent<BoatSpotlight>() == null)
                    Undo.AddComponent<BoatSpotlight>(go);
            }
            else
            {
                // FOLLOW-UP stubs (Worklight / WindowGlow / Lightpost): a generic radial SceneLight, lightly
                // themed. Bespoke components land later; the extension point is here.
                var light = go.GetComponent<SceneLight>();
                if (light == null) light = Undo.AddComponent<SceneLight>(go);
                light.Shape = SceneLight.LightShape.Radial;
                switch (preset)
                {
                    case LightPreset.Worklight:
                        light.Color = new Color(1f, 0.97f, 0.9f, 1f); light.Intensity = 1.3f; light.Range = 5f;
                        light.EdgeSoftness = 0.7f; light.FlickerAmount = 0f;
                        break;
                    case LightPreset.WindowGlow:
                        light.Color = new Color(1f, 0.84f, 0.5f, 1f); light.Intensity = 0.9f; light.Range = 3.5f;
                        light.EdgeSoftness = 0.85f; light.FlickerAmount = 0.04f;
                        break;
                    case LightPreset.Lightpost:
                        light.Color = new Color(1f, 0.9f, 0.7f, 1f); light.Intensity = 1.1f; light.Range = 4.5f;
                        light.EdgeSoftness = 0.75f; light.FlickerAmount = 0.02f;
                        break;
                }
            }
            EditorUtility.SetDirty(go);
        }

        // Enable the Add entries only when something is selected.
        [MenuItem(AddSpotlightPath, validate = true)]
        [MenuItem(AddWorklightPath, validate = true)]
        [MenuItem(AddWindowPath, validate = true)]
        [MenuItem(AddLightpostPath, validate = true)]
        public static bool ValidateAddToSelection() => Selection.gameObjects.Length > 0;

        // ---- the "Build Light Test" demo -----------------------------------------------------------------

        [MenuItem(BuildMenuPath, priority = 44)]
        public static void BuildLightTest()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;

            // A DARK ground plane so the additive lights have something to brighten (a deep cold blue, like a
            // night sea/quay). The day/night overlay also darkens the whole frame at night on top of this.
            BuildGround(root.transform, new Color(0.05f, 0.07f, 0.12f, 1f), 22f, 14f);

            // 1) A boat-spotlight-like CONE: a small "boat" marker carrying a BoatSpotlight, thrown forward
            //    (its transform.up). Rotate it a touch so the beam rakes across the plane. The light carrier is
            //    kept at unit scale (a SCALED carrier would distort the light quad); the hull sprite is a scaled
            //    child instead.
            var boat = new GameObject("BoatMarker");
            boat.transform.SetParent(root.transform, false);
            boat.transform.position = new Vector3(-5f, -3f, 0f);
            boat.transform.rotation = Quaternion.Euler(0f, 0f, 20f);   // bow points up-left
            AddMarkerSpriteChild(boat.transform, "Hull", new Color(0.6f, 0.4f, 0.25f, 1f), 0.8f, 1.6f);
            boat.AddComponent<BoatSpotlight>();   // cone, warm, forward — adds its own SceneLight

            // 2) A round RADIAL glow (a lantern/worklight) so the owner sees the other shape too.
            var lantern = new GameObject("Lantern");
            lantern.transform.SetParent(root.transform, false);
            lantern.transform.position = new Vector3(5f, 2f, 0f);
            AddMarkerSpriteChild(lantern.transform, "Bulb", new Color(0.9f, 0.8f, 0.5f, 1f), 0.4f, 0.4f);
            var radial = lantern.AddComponent<SceneLight>();
            radial.Shape = SceneLight.LightShape.Radial;
            radial.Color = new Color(1f, 0.85f, 0.55f, 1f);
            radial.Intensity = 1.2f;
            radial.Range = 5f;
            radial.EdgeSoftness = 0.8f;
            radial.FlickerAmount = 0.08f;   // a living lantern wobble (deterministic)

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);

            Debug.Log(
                "[LightTest] Spawned 'LightTest' (a dark ground plane, a boat-marker with a forward CONE " +
                "spotlight, and a round RADIAL lantern). Press Play, then SCRUB THE CLOCK to NIGHT (Tide " +
                "Scrubber / DevFastTide / raise the clock TimeScale) and watch the beam + halo CUT THROUGH the " +
                "dark — they auto-gate, so they're ~invisible at noon and full at night. No clock in the scene? " +
                "They show anyway (the gate's no-cycle fallback) so you can tune. Tune colour / cone / intensity " +
                "/ range on the components. Delete 'LightTest' to fully revert.");
        }

        // ---- spawn helpers (in-memory greybox sprites; nothing saved to disk) -----------------------------

        private static void BuildGround(Transform parent, Color color, float w, float h)
        {
            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite(color);
            sr.sortingOrder = -32000;   // behind everything
            go.transform.localScale = new Vector3(w, h, 1f);
        }

        private static void AddMarkerSpriteChild(Transform parent, string name, Color color, float w, float h)
        {
            // A SCALED child sprite so the light-carrying parent stays at unit scale (a scaled carrier would
            // distort the light quad + bow offset). The sprite is 1x1 unit; the child's localScale sizes it.
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite(color);
            sr.sortingOrder = 10;
            go.transform.localScale = new Vector3(w, h, 1f);
        }

        private static Sprite SolidSprite(Color color)
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
