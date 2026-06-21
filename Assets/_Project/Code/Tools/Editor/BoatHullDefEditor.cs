#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;   // SeaState
using HiddenHarbours.Boats;  // BoatHullDef, PropulsionType

namespace HiddenHarbours.Tools.Editor
{
    /// <summary>
    /// A friendly authoring inspector for <see cref="BoatHullDef"/> so the owner can tune how a hull
    /// *feels on the water* — above all the dory's hand-rowing — without reading code or guessing at
    /// raw numbers. Mirrors <see cref="FishSpeciesDefEditor"/>: every widget reads and writes the SAME
    /// serialized fields the runtime already uses (via <see cref="SerializedObject"/>, so Undo,
    /// multi-edit and prefab overrides all work for free). It adds NO data and changes NO runtime
    /// behaviour — it only groups the existing fields, labels them in plain English, gives the feel
    /// tunables sliders, and puts the *active* propulsion's controls front-and-centre.
    ///
    /// EDITOR-ONLY (Tools.Editor assembly, wrapped in <c>#if UNITY_EDITOR</c>); never ships in a build.
    /// The "Authoring hints" box is DISPLAY-ONLY and explicitly NOT the source of truth — the content
    /// validator (qa-test) is. These hints just catch the obvious "she won't move" mistakes at a glance.
    /// </summary>
    [CustomEditor(typeof(BoatHullDef))]
    [CanEditMultipleObjects]
    public class BoatHullDefEditor : UnityEditor.Editor
    {
        // Local palette (reads the same in light/dark skins; matches the other Tools editors).
        private static readonly Color PanelColor = new Color(0.16f, 0.18f, 0.20f);
        private static readonly Color TurnColor  = new Color(1f, 0.78f, 0.30f);

        private SerializedProperty _id, _displayName, _sprite;
        private SerializedProperty _length, _draught, _mass;
        private SerializedProperty _hold, _crew;
        private SerializedProperty _propulsion;
        private SerializedProperty _enginePower, _rudder;
        private SerializedProperty _oarPower, _oarOffset, _oarBrace;
        private SerializedProperty _fwdDrag, _latDrag, _windExposure;
        private SerializedProperty _maxSeaState, _cameraHeight;

        private void OnEnable()
        {
            _id          = serializedObject.FindProperty("Id");
            _displayName = serializedObject.FindProperty("DisplayName");
            _sprite      = serializedObject.FindProperty("Sprite");

            _length  = serializedObject.FindProperty("LengthMeters");
            _draught = serializedObject.FindProperty("DraughtMeters");
            _mass    = serializedObject.FindProperty("MassKg");

            _hold = serializedObject.FindProperty("HoldUnits");
            _crew = serializedObject.FindProperty("CrewSlots");

            _propulsion  = serializedObject.FindProperty("Propulsion");
            _enginePower = serializedObject.FindProperty("EnginePower");
            _rudder      = serializedObject.FindProperty("RudderAuthority");
            _oarPower    = serializedObject.FindProperty("OarPower");
            _oarOffset   = serializedObject.FindProperty("OarLateralOffset");
            _oarBrace    = serializedObject.FindProperty("OarBraceDrag");

            _fwdDrag      = serializedObject.FindProperty("ForwardDrag");
            _latDrag      = serializedObject.FindProperty("LateralDrag");
            _windExposure = serializedObject.FindProperty("WindExposure");

            _maxSeaState  = serializedObject.FindProperty("MaxSafeSeaState");
            _cameraHeight = serializedObject.FindProperty("CameraWorldHeightMeters");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawIdentity();
            DrawArt();
            DrawPropulsion();
            DrawRowingFeel();
            DrawEngineHelm();
            DrawHullAndMass();
            DrawHandling();
            DrawCapacity();
            DrawSeaworthiness();
            DrawCamera();
            DrawAuthoringHints();

            serializedObject.ApplyModifiedProperties();
        }

        // ------------------------------------------------------------------ identity & art

        private void DrawIdentity()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_id, new GUIContent(
                    "Id", "Stable, append-only id (e.g. boat.dory). Save & fleet state key off it — " +
                          "rename the display name freely, but never reuse or change an id."));
                EditorGUILayout.PropertyField(_displayName, new GUIContent("Display Name", "Player-facing name (e.g. \"The Dory\")."));
            }
        }

        private void DrawArt()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Art", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(_sprite, new GUIContent(
                        "Hull Sprite", "Optional hull sprite. The fleet swaps the renderer to this when the " +
                                       "boat is granted (null-safe). Attached by art-pipeline; never required during greybox."));
                    var sprite = _sprite.objectReferenceValue as Sprite;
                    if (sprite != null && sprite.texture != null)
                    {
                        Rect r = GUILayoutUtility.GetRect(48f, 48f, GUILayout.Width(48f), GUILayout.Height(48f));
                        DrawSpritePreview(r, sprite);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ propulsion

        private void DrawPropulsion()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("How she's driven", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_propulsion, new GUIContent(
                    "Propulsion",
                    "Oars = differential hand-rowing (the starting dory). Engine = throttle + rudder helm " +
                    "(boats you buy up the ladder). This picks which handling block below is live."));

                if (_propulsion.hasMultipleDifferentValues)
                {
                    EditorGUILayout.LabelField("   (mixed selection — both handling blocks are shown below)", EditorStyles.miniLabel);
                }
                else
                {
                    bool oars = _propulsion.enumValueIndex == (int)PropulsionType.Oars;
                    EditorGUILayout.LabelField("   " + (oars
                        ? "Hand-rowed: pull both oars to drive ahead, one oar to spin. Tune her under \"Rowing feel\"."
                        : "Engine helm: throttle + rudder. Tune her under \"Engine helm\"."),
                        EditorStyles.miniLabel);
                }
            }
        }

        private void DrawRowingFeel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawHandlingHeader("Rowing feel — oars", PropulsionType.Oars,
                    activeNote: "Live for this hull — this is the dory's hand-rowing feel.",
                    idleNote: "Ignored while Propulsion = Engine (kept for any hull that rows).");

                FeelSlider(_oarPower, "Oar power",
                    "Per-oar pull at a full stroke (design units). Both oars together drive ahead; a one-sided " +
                    "stroke gives thrust plus a turn the other way.", 0f, 800f,
                    v => v <= 0f ? "Zero — she won't pull anywhere." : $"Both oars ≈ {2f * v:0} ahead; one oar = thrust + a turn.");

                FeelSlider(_oarOffset, "Oar reach (m)",
                    "How far each oar bites off the centreline — the moment arm. Wider = a sharper turn from a " +
                    "one-sided stroke.", 0f, 2f);

                if (!_oarPower.hasMultipleDifferentValues && !_oarOffset.hasMultipleDifferentValues)
                    DrawTurnBiteBar(_oarPower.floatValue, _oarOffset.floatValue);

                FeelSlider(_oarBrace, "Brace drag",
                    "Extra water drag when you brace the oars (Space) to brake. Forgiving — it just eases her down, " +
                    "never snaps her to a halt.", 0f, 1200f,
                    v => v <= 1f ? "No braking bite — bracing won't slow her." : "Bracing eases her toward a stop.");
            }
        }

        private void DrawEngineHelm()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawHandlingHeader("Engine helm", PropulsionType.Engine,
                    activeNote: "Live for this hull — throttle + rudder.",
                    idleNote: "Ignored while Propulsion = Oars (set this up for boats you buy later).");

                FeelSlider(_enginePower, "Engine power", "Thrust at full throttle (design units).", 0f, 4000f,
                    v => v <= 0f ? "Zero — no thrust." : "Higher = more top speed and quicker acceleration.");
                FeelSlider(_rudder, "Rudder authority",
                    "Turning authority (design units). Effective turn scales up with speed.", 0f, 2000f,
                    v => "Higher = she answers the helm faster.");
            }
        }

        // ------------------------------------------------------------------ hull, mass, handling

        private void DrawHullAndMass()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Hull & mass", EditorStyles.boldLabel);
                FeelSlider(_length, "Length (m)", "Hull length. Mostly footprint and framing; longer hulls carry more way.", 1f, 60f);
                FeelSlider(_draught, "Draught (m)",
                    "How deep she sits. She grounds when draught exceeds the local water depth — mind the low-tide flats.",
                    0f, 6f, v => $"Needs ≥ {v:0.0} m of water under her.");
                FeelSlider(_mass, "Mass (kg)",
                    "Heavier hulls carry more momentum: slower to get going, slower to stop, slower to turn.", 0f, 2000f,
                    v => v <= 0f ? "Mass must be > 0 — the physics needs it." :
                         v < 600f ? "Light and lively." : v > 50000f ? "Ponderous — lots of way to lose." : "Moderate.");
            }
        }

        private void DrawHandling()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Handling in the water (shared)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("   How she glides, tracks and takes the wind — applies whether rowed or driven.", EditorStyles.miniLabel);

                FeelSlider(_fwdDrag, "Forward drag",
                    "Drag along the hull. Low = she glides and carries her way; high = she loses speed quickly when you ease off.",
                    0f, 200f, v => v < 20f ? "Glides — carries her way." : v > 120f ? "Draggy — stops quickly." : "Moderate glide.");
                FeelSlider(_latDrag, "Sideways grip",
                    "Beam-on drag. High = she tracks forward and turns crisply instead of skidding sideways.",
                    0f, 600f, v => "Higher = less skid, crisper turns.");
                FeelSlider(_windExposure, "Wind exposure",
                    "How hard the wind shoves her. Small craft are high; big ships low.", 0f, 3f,
                    v => v < 0.5f ? "Barely feels the wind." : v > 1.6f ? "Blown about — mind the gusts (P1)." : "Noticeable in a breeze.");
            }
        }

        private void DrawCapacity()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Capacity", EditorStyles.boldLabel);
                IntFeelSlider(_hold, "Hold (HU)", "Cargo capacity in hold units.", 0, 24);
                IntFeelSlider(_crew, "Crew slots", "How many crew she berths (drives automation later, P4).", 0, 12);
            }
        }

        private void DrawSeaworthiness()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Seaworthiness", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_maxSeaState, new GUIContent(
                    "Max safe sea state",
                    "The worst sea she can work safely. Above it the danger climbs (P5). Scale, calm→storm: " +
                    "Glass, Calm, Light, Moderate, Lively, Rough, Gale, Storm."));
                if (!_maxSeaState.hasMultipleDifferentValues)
                {
                    string name = _maxSeaState.enumDisplayNames[_maxSeaState.enumValueIndex];
                    EditorGUILayout.LabelField($"   Safe up to and including {name} — rougher than that is a gamble.", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawCamera()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);
                FeelSlider(_cameraHeight, "Framing height (m)",
                    "How tall a slice of the world the camera frames for this hull. Bigger boat = more water on screen.",
                    4f, 40f, v => $"Frames about {v:0} m of world, top to bottom.");
            }
        }

        // ------------------------------------------------------------------ authoring hints (display-only)

        private void DrawAuthoringHints()
        {
            EditorGUILayout.Space(2);
            if (serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.HelpBox(
                    "Select a single hull to see authoring hints. (Hints are display-only; the content " +
                    "validator is the source of truth.)", MessageType.None);
                return;
            }

            var warnings = new List<string>();
            var notes = new List<string>();

            if (string.IsNullOrWhiteSpace(_id.stringValue)) warnings.Add("No Id — give it a stable id like boat.dory.");
            if (_mass.floatValue <= 0f) warnings.Add("Mass is 0 — physics needs mass > 0 or she won't behave.");
            if (_length.floatValue <= 0f) warnings.Add("Length should be greater than 0.");
            if (_draught.floatValue < 0f) warnings.Add("Draught can't be negative.");

            bool oars = _propulsion.enumValueIndex == (int)PropulsionType.Oars;
            if (oars && _oarPower.floatValue <= 0f) warnings.Add("Rows with oars but Oar power is 0 — she won't pull anywhere.");
            if (!oars && _enginePower.floatValue <= 0f) warnings.Add("Engine boat but Engine power is 0 — no thrust.");

            if (_latDrag.floatValue <= _fwdDrag.floatValue)
                notes.Add("Sideways grip ≤ forward drag — she'll skid in turns; usually lateral drag is the larger.");
            if (_sprite.objectReferenceValue == null) notes.Add("No hull sprite yet — fine during greybox; art lands later.");

            string body;
            MessageType type;
            if (warnings.Count > 0)
            {
                body = "• " + string.Join("\n• ", warnings);
                if (notes.Count > 0) body += "\n• " + string.Join("\n• ", notes);
                type = MessageType.Warning;
            }
            else if (notes.Count > 0)
            {
                body = "• " + string.Join("\n• ", notes);
                type = MessageType.Info;
            }
            else
            {
                body = "Nothing obviously off — pull her out and see how she feels.";
                type = MessageType.Info;
            }

            EditorGUILayout.HelpBox(
                "Authoring hints (display-only — the content validator is the source of truth):\n" + body, type);
        }

        // ------------------------------------------------------------------ shared widgets

        /// <summary>
        /// Header for a propulsion-specific block: bold title plus a one-line note saying whether it is
        /// live for the current hull (or both, when multiple hulls are selected). The block stays
        /// editable either way — an idle block is just set-up for another hull, not forbidden.
        /// </summary>
        private void DrawHandlingHeader(string title, PropulsionType forType, string activeNote, string idleNote)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (_propulsion.hasMultipleDifferentValues)
                EditorGUILayout.LabelField("   (mixed selection — shown for whichever hulls use it)", EditorStyles.miniLabel);
            else
            {
                bool active = _propulsion.enumValueIndex == (int)forType;
                EditorGUILayout.LabelField("   " + (active ? activeNote : idleNote), EditorStyles.miniLabel);
            }
        }

        /// <summary>
        /// A friendly slider for a non-negative "feel" float, with an optional plain-English readout under it.
        /// The slider bounds expand to contain (and give headroom above) the current value, so dragging never
        /// clamps authored data — a tanker's big numbers still fit the same control as the dory's small ones.
        /// </summary>
        private void FeelSlider(SerializedProperty p, string label, string tooltip,
                                float softMin, float softMax, Func<float, string> readout = null)
        {
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            float v = p.floatValue;
            float lo = Mathf.Min(softMin, Mathf.Floor(v));
            float hi = Mathf.Max(softMax, Mathf.Ceil(v * 1.25f)); // headroom so you can push above the current value
            EditorGUI.BeginChangeCheck();
            float nv = EditorGUILayout.Slider(new GUIContent(label, tooltip), v, lo, hi);
            if (EditorGUI.EndChangeCheck()) p.floatValue = Mathf.Max(0f, nv);
            EditorGUI.showMixedValue = false;

            if (readout != null && !p.hasMultipleDifferentValues)
                EditorGUILayout.LabelField("   " + readout(p.floatValue), EditorStyles.miniLabel);
        }

        /// <summary>Integer twin of <see cref="FeelSlider"/> for capacity counts.</summary>
        private void IntFeelSlider(SerializedProperty p, string label, string tooltip, int softMin, int softMax)
        {
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            int v = p.intValue;
            int lo = Mathf.Min(softMin, v);
            int hi = Mathf.Max(softMax, Mathf.CeilToInt(v * 1.25f));
            EditorGUI.BeginChangeCheck();
            int nv = EditorGUILayout.IntSlider(new GUIContent(label, tooltip), v, lo, hi);
            if (EditorGUI.EndChangeCheck()) p.intValue = Mathf.Max(0, nv);
            EditorGUI.showMixedValue = false;
        }

        /// <summary>
        /// A small relative gauge for "how bitey is a one-sided stroke" = oar power × reach (the yaw-impulse
        /// scale). Purely a feel comparison across hulls/settings — not a physics readout; the controller owns
        /// the real maths.
        /// </summary>
        private void DrawTurnBiteBar(float power, float reach)
        {
            float bite = Mathf.Max(0f, power) * Mathf.Max(0f, reach);
            EditorGUILayout.LabelField($"   Turning bite, one-sided ≈ {bite:0} (power × reach) — compare across hulls.",
                EditorStyles.miniLabel);

            Rect r = EditorGUILayout.GetControlRect(false, 6f);
            r = new RectOffset((int)EditorGUIUtility.labelWidth + 4, 4, 0, 0).Remove(r);
            EditorGUI.DrawRect(r, PanelColor);
            float frac = Mathf.Clamp01(bite / 1200f); // 1200 ≈ a strong one-sided bite, for relative scale
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width * frac, r.height), TurnColor);
        }

        private static void DrawSpritePreview(Rect r, Sprite sprite)
        {
            Texture tex = sprite.texture;
            Rect tr = sprite.textureRect;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            GUI.DrawTextureWithTexCoords(r, tex, uv, alphaBlend: true);
        }
    }
}
#endif
