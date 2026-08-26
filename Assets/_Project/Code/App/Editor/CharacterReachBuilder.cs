#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art.Editor;      // MiniJson — the shared editor JSON reader

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The rig-6.6 REACH contract, imported once into an asset</b> — the consumer side of
    /// <c>Reach_sidecar.json</c>, which ships beside the three reach sheets for all ten presets.
    ///
    /// <para><b>Why edit time.</b> Both halves of the source are committed and change only when the
    /// character is re-baked, so there is nothing to decide at run time and nothing to parse there
    /// either — the same reasoning as <see cref="CharacterOffDeckMountsBuilder"/>, which this is
    /// modelled on line for line. Runtime never sees JSON (ADR 0021 §4). Non-destructive: the asset
    /// refreshes in place and keeps its guid, so nothing pointing at it breaks.</para>
    ///
    /// <para><b>What this reads, and what it deliberately does not.</b> Geometry and the two timing
    /// points that a hand-over turns on. It does NOT write the sheets, the frame count or the
    /// playback rate anywhere those already live — those are field initialisers on
    /// <see cref="CharacterVisualDef"/>'s clip blocks, where every clip family before this one keeps
    /// them. <see cref="CharacterReachDef.Frames"/> and
    /// <see cref="CharacterReachDef.MillisecondsPerFrame"/> are carried here as the SIDECAR's own
    /// statement of them, precisely so <c>CharacterReachTests</c> can hold the two against each other
    /// — a re-export that quietly retimed the set-down should go red, not drift.</para>
    ///
    /// <para><b>⚠️ The sidecar's <c>presets</c> is a LIST, not a dictionary.</b> The off-deck sidecar
    /// keys its presets by name; this one is an array of objects each carrying its own <c>key</c>.
    /// Reading it the other way finds nothing and imports an empty def, which every consumer then
    /// treats as "no measured rest" and silently degrades past. Hence the explicit refusal below.</para>
    ///
    /// <para><b>Everything degrades per element</b>, the house rule for importers: a missing sidecar
    /// leaves the asset untouched and says so loudly; a preset whose block is malformed is skipped
    /// with its own warning and the other nine still import.</para>
    /// </summary>
    public static class CharacterReachBuilder
    {
        const string MenuPath = "Hidden Harbours/Art/Import (after a new drop)/Build Reach Contract";
        const string OutputFolder = "Assets/_Project/Data/Characters";
        const string AssetName = "CharacterReach";
        const string ArtIso = "Assets/_Project/Art/Characters/Iso";

        /// <summary>The sidecar the character rig exports beside the reach sheets.</summary>
        public const string SidecarPath = ArtIso + "/Reach_sidecar.json";

        /// <summary>Where the built asset lands.</summary>
        public const string ReachPath = OutputFolder + "/" + AssetName + ".asset";

        /// <summary>The def id, append-only.</summary>
        public const string ReachId = "reach.character_iso";

        /// <summary>
        /// The sidecar's preset key → the <see cref="CharacterVisualDef"/> id it describes. The keys
        /// are already the rig's own lower-case build names, so this agrees with
        /// <see cref="CharacterVisualLibraryBuilder.CastVisualId"/> for all ten.
        ///
        /// <para>⚠️ It agrees for the ID only. A preset's sheet STEM is display capitalisation
        /// (<c>deckboss</c> → <c>DeckBoss</c>) and is not this.</para>
        /// </summary>
        public static string VisualIdForPresetKey(string presetKey) =>
            string.IsNullOrEmpty(presetKey)
                ? null
                : $"visual.{presetKey.ToLowerInvariant()}_iso";

        /// <summary>The sidecar's three rest names, in <see cref="CharacterClip"/> terms. The names are
        /// the RIG's own <c>REACH_LIFT</c> keys and are what the sheet files are suffixed with.</summary>
        public static readonly (string key, CharacterClip clip)[] Rests =
        {
            ("ground", CharacterClip.ReachGround),
            ("stowV", CharacterClip.ReachStowV),
            ("stowH", CharacterClip.ReachStowH),
        };

        [MenuItem(MenuPath, priority = 233)]
        public static void Build()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(SidecarPath);
            if (text == null)
            {
                Debug.LogWarning($"[CharacterReachBuilder] No sidecar at '{SidecarPath}' — nothing " +
                                 "imported and the existing asset is untouched. Copy " +
                                 "Reach_sidecar.json in with the sheets and re-run.");
                return;
            }

            var def = AssetDatabase.LoadAssetAtPath<CharacterReachDef>(ReachPath);
            bool created = def == null;
            if (created) def = ScriptableObject.CreateInstance<CharacterReachDef>();

            if (!TryParse(text.text, def, out string error))
            {
                Debug.LogError($"[CharacterReachBuilder] '{SidecarPath}' did not parse: {error}. " +
                               "The existing asset is untouched.");
                return;
            }

            EnsureFolder(OutputFolder);
            if (created) AssetDatabase.CreateAsset(def, ReachPath);
            else EditorUtility.SetDirty(def);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int clamped = 0;
            foreach (var row in def.Rows)
                if (row.Ground.Clamped || row.StowV.Clamped || row.StowH.Clamped) clamped++;

            Debug.Log($"[CharacterReachBuilder] {(created ? "Created" : "Refreshed")} '{ReachPath}' " +
                      $"from rig '{def.Rig}': {def.Rows.Length} preset rest set(s), {clamped} of them " +
                      $"with a clamped reach; {def.Frames} f at {def.MillisecondsPerFrame:0.#} ms " +
                      $"({def.FramesPerSecond:0.##} fps), tool home at {def.ArriveAt:0.##} and the " +
                      $"hand opens at {def.ReleaseAt:0.##} ({def.GrippedFrames} frames still gripped); " +
                      $"grip rise {def.GripRiseM:0.###} m; racks at {def.StowVLiftM:0.###} / " +
                      $"{def.StowHLiftM:0.###} m. Commit it.");
        }

        /// <summary>
        /// Parse the sidecar into <paramref name="into"/>. Pure apart from the def it fills — no asset
        /// I/O — so EditMode can drive it with a string, including the malformed strings that must come
        /// back <c>false</c> rather than half-filling.
        ///
        /// <para>Returns <c>false</c> only for a sidecar that is not usable AT ALL (unparseable, no
        /// preset list, no reach clip block, or not one preset row survived). A single bad preset row
        /// is a per-element skip with its own warning, because nine good rest sets are worth more than
        /// a refusal.</para>
        /// </summary>
        public static bool TryParse(string json, CharacterReachDef into, out string error)
        {
            error = null;

            if (into == null) { error = "no def to fill"; return false; }
            if (string.IsNullOrWhiteSpace(json)) { error = "empty sidecar"; return false; }

            object root;
            try { root = MiniJson.Parse(json); }
            catch (Exception e) { error = $"not JSON ({e.Message})"; return false; }

            if (root is not Dictionary<string, object>) { error = "top level is not an object"; return false; }

            var reach = MiniJson.Dict(MiniJson.Dict(root, "clips"), "reach");
            if (reach == null)
            {
                error = "no 'clips.reach' block — this is not a Reach_sidecar";
                return false;
            }

            var presets = MiniJson.List(root, "presets");
            if (presets == null || presets.Count == 0)
            {
                error = "no 'presets' list — this is not a Reach_sidecar (the off-deck sidecar keys " +
                        "its presets by name; this one is an array of objects carrying their own 'key')";
                return false;
            }

            var lifts = MiniJson.Dict(root, "reach_lift_m");
            var rows = new List<CharacterReachRow>();

            foreach (object entry in presets)
            {
                var node = entry as Dictionary<string, object>;
                string key = MiniJson.String(node, "key");
                var rests = MiniJson.Dict(node, "rests");
                if (string.IsNullOrEmpty(key) || rests == null)
                {
                    Debug.LogWarning("[CharacterReachBuilder] a preset entry has no 'key' or no " +
                                     "'rests' block — skipped. The other presets still import.");
                    continue;
                }

                var row = new CharacterReachRow { VisualId = VisualIdForPresetKey(key) };
                bool complete = true;
                for (int i = 0; i < Rests.Length; i++)
                {
                    var block = MiniJson.Dict(rests, Rests[i].key);
                    if (block == null) { complete = false; break; }
                    var rest = ReadRest(block);
                    switch (Rests[i].clip)
                    {
                        case CharacterClip.ReachStowV: row.StowV = rest; break;
                        case CharacterClip.ReachStowH: row.StowH = rest; break;
                        default: row.Ground = rest; break;
                    }
                }

                if (!complete)
                {
                    Debug.LogWarning($"[CharacterReachBuilder] preset '{key}' is missing one of its " +
                                     "three rest blocks — skipped whole rather than half-filled. The " +
                                     "other presets still import.");
                    continue;
                }

                rows.Add(row);
            }

            if (rows.Count == 0) { error = "every preset row was malformed"; return false; }

            // The clip's own numbers, read into LOCALS so the last check below can still refuse without
            // having half-written the def. Defaults are the shipped sidecar's values, so a sidecar that
            // omits a field keeps the shipped number rather than releasing the hand at frame zero.
            int frames = Mathf.Max(1, MiniJson.Int(reach, "frames", 6));
            float ms = MiniJson.Float(reach, "ms_per_frame", 100f);
            float releaseAt = Mathf.Clamp01(MiniJson.Float(reach, "release_at", 0.72f));
            float arriveAt = Mathf.Clamp01(MiniJson.Float(reach, "arrive_at", 0.62f));

            // The tool ARRIVES before the hand OPENS. Not a style rule — releasing at or after the seam
            // is what made the old rod rests read as teleports, and a sidecar that inverts the two would
            // import CLEANLY as a def telling every consumer to drop the tool before it lands. That is
            // the well-formed-and-wrong case, which only a parse can catch.
            if (arriveAt >= releaseAt)
            {
                error = $"arrive_at ({arriveAt}) is not before release_at ({releaseAt}) — the tool " +
                        "must be home BEFORE the hand opens";
                return false;
            }

            // Stable order, so a re-import of unchanged art is a byte-identical asset rather than a diff
            // that depends on the JSON reader's ordering.
            rows.Sort((a, b) => string.CompareOrdinal(a.VisualId, b.VisualId));

            into.Id = ReachId;
            into.Rig = MiniJson.String(MiniJson.Dict(root, "rig"), "revision", "");
            into.Rows = rows.ToArray();

            into.Frames = frames;
            into.MillisecondsPerFrame = ms;
            into.ReleaseAt = releaseAt;
            into.ArriveAt = arriveAt;
            into.GripRiseM = MiniJson.Float(root, "grip_rise_m", 0.095f);

            into.GroundLiftM = MiniJson.Float(lifts, "ground", 0f);
            into.StowVLiftM = MiniJson.Float(lifts, "stowV", 0.95f);
            into.StowHLiftM = MiniJson.Float(lifts, "stowH", 1.05f);

            return true;
        }

        static CharacterReachRest ReadRest(Dictionary<string, object> block) => new CharacterReachRest
        {
            LiftM = MiniJson.Float(block, "lift_m"),
            RequestedM = MiniJson.Float(block, "requested_m"),
            Clamped = MiniJson.Bool(block, "lift_clamped_to_reach"),
        };

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }
}
#endif
