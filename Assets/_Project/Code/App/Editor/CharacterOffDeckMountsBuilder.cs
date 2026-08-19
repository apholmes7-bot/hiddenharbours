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
    /// <b>The rig-6.5 OFF-DECK mount contract, imported once into an asset</b> — the consumer side of
    /// <c>OffDeck_mounts.json</c>, which ships beside the swim / tread / sleep / drive sheets.
    ///
    /// <para><b>Why edit time.</b> Both halves of the source are committed and change only when the
    /// character is re-baked, so there is nothing to decide at run time and nothing to parse there
    /// either — the same reasoning as <see cref="CarryAnchorTableBuilder"/> and
    /// <see cref="CharacterVisualLibraryBuilder"/>. Runtime never sees JSON (ADR 0021 §4).
    /// Non-destructive: the asset refreshes in place and keeps its guid, so nothing pointing at it
    /// breaks.</para>
    ///
    /// <para><b>What this reads, and what it deliberately does not.</b> Geometry only — the per-preset
    /// water lines and the global bed and driving mounts. The sidecar's <c>anims</c> block (frame
    /// counts and milliseconds) is NOT written anywhere by this builder: those numbers live as field
    /// initialisers on <see cref="CharacterVisualDef"/>'s clip blocks, which is where every clip family
    /// before this one keeps them. They are still PARSED here, and handed back by
    /// <see cref="TryParse"/>, so that <c>CharacterOffDeckMountsTests</c> can assert the two agree — a
    /// re-export that quietly retimed an animation should go red, not drift.</para>
    ///
    /// <para><b>Everything degrades per element</b>, the house rule for importers: a missing sidecar
    /// leaves the asset untouched and says so loudly; a preset whose block is malformed is skipped with
    /// its own warning and the other nine still import. Silent half-wiring is what cost the owner a
    /// playtest.</para>
    /// </summary>
    public static class CharacterOffDeckMountsBuilder
    {
        const string MenuPath = "Hidden Harbours/Art/Import (after a new drop)/Build Off-Deck Mounts";
        const string OutputFolder = "Assets/_Project/Data/Characters";
        const string AssetName = "CharacterOffDeckMounts";
        const string ArtIso = "Assets/_Project/Art/Characters/Iso";

        /// <summary>The sidecar the character rig exports beside the off-deck sheets.</summary>
        public const string SidecarPath = ArtIso + "/OffDeck_mounts.json";

        /// <summary>Where the built asset lands.</summary>
        public const string MountsPath = OutputFolder + "/" + AssetName + ".asset";

        /// <summary>The def id, append-only.</summary>
        public const string MountsId = "offdeckmounts.character_iso";

        /// <summary>
        /// The sidecar's preset key → the <see cref="CharacterVisualDef"/> id it describes.
        ///
        /// <para>Lower-casing the key IS the rule — <c>Fisher</c> → <c>visual.fisher_iso</c>,
        /// <c>Deckboss</c> → <c>visual.deckboss_iso</c> — and it agrees with
        /// <see cref="CharacterVisualLibraryBuilder.CastVisualId"/> for all ten. ⚠️ It agrees for the
        /// ID only. The sidecar's <c>Deckboss</c> is NOT this preset's sheet STEM, which is
        /// <c>DeckBoss</c> with a capital B — the drop ships its PNGs spelled the sidecar's way and
        /// they are renamed on import, because the builder looks its sheets up by stem and an
        /// unrenamed file would simply never be found. An absent sheet is silent by design (the
        /// all-or-nothing gate drops the clip whole), so this is exactly the kind of mismatch that
        /// ships looking fine.</para>
        /// </summary>
        public static string VisualIdForPresetKey(string presetKey) =>
            string.IsNullOrEmpty(presetKey)
                ? null
                : $"visual.{presetKey.ToLowerInvariant()}_iso";

        /// <summary>One anim's timing as the sidecar states it: frames, and MILLISECONDS per frame.</summary>
        public readonly struct AnimTiming
        {
            public readonly int Frames;
            public readonly float MillisecondsPerFrame;

            public AnimTiming(int frames, float millisecondsPerFrame)
            {
                Frames = frames;
                MillisecondsPerFrame = millisecondsPerFrame;
            }

            /// <summary>The rate a <see cref="CharacterClipSheets"/> stores, so the two are comparable.</summary>
            public float FramesPerSecond =>
                MillisecondsPerFrame > 0f ? 1000f / MillisecondsPerFrame : 0f;
        }

        /// <summary>The sidecar's four anim names, in <see cref="CharacterClip"/> terms.</summary>
        public static readonly (string key, CharacterClip clip)[] Anims =
        {
            ("swim", CharacterClip.Swim),
            ("tread", CharacterClip.Tread),
            ("sleep", CharacterClip.Sleep),
            ("drive", CharacterClip.Drive),
        };

        [MenuItem(MenuPath, priority = 232)]
        public static void Build()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(SidecarPath);
            if (text == null)
            {
                Debug.LogWarning($"[CharacterOffDeckMountsBuilder] No sidecar at '{SidecarPath}' — " +
                                 "nothing imported and the existing asset is untouched. Copy " +
                                 "OffDeck_mounts.json in with the sheets and re-run.");
                return;
            }

            var def = AssetDatabase.LoadAssetAtPath<CharacterOffDeckMountsDef>(MountsPath);
            bool created = def == null;
            if (created) def = ScriptableObject.CreateInstance<CharacterOffDeckMountsDef>();

            if (!TryParse(text.text, def, out var anims, out string error))
            {
                Debug.LogError($"[CharacterOffDeckMountsBuilder] '{SidecarPath}' did not parse: {error}. " +
                               "The existing asset is untouched.");
                return;
            }

            EnsureFolder(OutputFolder);
            if (created) AssetDatabase.CreateAsset(def, MountsPath);
            else EditorUtility.SetDirty(def);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var timings = new List<string>();
            foreach (var (key, clip) in Anims)
                if (anims.TryGetValue(clip, out var t))
                    timings.Add($"{key} {t.Frames}f/{t.MillisecondsPerFrame:0.#}ms");

            Debug.Log($"[CharacterOffDeckMountsBuilder] {(created ? "Created" : "Refreshed")} " +
                      $"'{MountsPath}' from rig '{def.Rig}': {def.Rows.Length} preset water line(s); " +
                      $"bed {def.SleepBedZ:0.###} m; seat {def.DriveSeatZ:0.###} m, wheel " +
                      $"{def.DriveWheelZ:0.###}/{def.DriveWheelY:0.###} m; anims {string.Join(", ", timings)}. " +
                      "Commit it.");
        }

        /// <summary>
        /// Parse the sidecar into <paramref name="into"/>, and hand back the anim timings it declares.
        /// Pure apart from the def it fills — no asset I/O — so EditMode can drive it with a string,
        /// including the malformed strings that must come back <c>false</c> rather than half-filling.
        ///
        /// <para>Returns <c>false</c> only for a sidecar that is not usable AT ALL (unparseable, or no
        /// presets in it). A single bad preset row is a per-element skip with its own warning, because
        /// nine good water lines are worth more than a refusal.</para>
        /// </summary>
        public static bool TryParse(string json, CharacterOffDeckMountsDef into,
                                    out Dictionary<CharacterClip, AnimTiming> anims, out string error)
        {
            anims = new Dictionary<CharacterClip, AnimTiming>();
            error = null;

            if (into == null) { error = "no def to fill"; return false; }
            if (string.IsNullOrWhiteSpace(json)) { error = "empty sidecar"; return false; }

            object root;
            try { root = MiniJson.Parse(json); }
            catch (Exception e) { error = $"not JSON ({e.Message})"; return false; }

            if (root is not Dictionary<string, object>) { error = "top level is not an object"; return false; }

            var presets = MiniJson.Dict(root, "presets");
            if (presets == null || presets.Count == 0)
            {
                error = "no 'presets' block — this is not an OffDeck_mounts sidecar";
                return false;
            }

            // The anim timings. Not written to any asset (see the class remarks) — parsed so a test can
            // hold them against CharacterVisualDef's clip initialisers.
            var animsNode = MiniJson.Dict(root, "anims");
            foreach (var (key, clip) in Anims)
            {
                var node = MiniJson.Dict(animsNode, key);
                if (node == null) continue;
                anims[clip] = new AnimTiming(MiniJson.Int(node, "frames"), MiniJson.Float(node, "ms"));
            }

            var rows = new List<CharacterOffDeckRow>();
            foreach (var kv in presets)
            {
                var node = kv.Value as Dictionary<string, object>;
                var swim = MiniJson.Dict(node, "swim");
                var tread = MiniJson.Dict(node, "tread");
                if (swim == null || tread == null)
                {
                    Debug.LogWarning($"[CharacterOffDeckMountsBuilder] preset '{kv.Key}' is missing its " +
                                     "swim or tread block — skipped. The other presets still import.");
                    continue;
                }

                rows.Add(new CharacterOffDeckRow
                {
                    VisualId = VisualIdForPresetKey(kv.Key),
                    Swim = new CharacterWaterLine
                    {
                        WaterZ = MiniJson.Float(swim, "waterZ"),
                        WaterRowLane = MiniJson.Int(swim, "waterRowLane"),
                    },
                    Tread = new CharacterWaterLine
                    {
                        WaterZ = MiniJson.Float(tread, "waterZ"),
                        WaterRowLane = MiniJson.Int(tread, "waterRowLane"),
                    },
                });
            }

            if (rows.Count == 0) { error = "every preset row was malformed"; return false; }

            // Stable order, so a re-import of unchanged art is a byte-identical asset rather than a diff
            // that depends on the JSON reader's dictionary ordering.
            rows.Sort((a, b) => string.CompareOrdinal(a.VisualId, b.VisualId));

            into.Id = MountsId;
            into.Rig = MiniJson.String(root, "rig", "");
            into.Rows = rows.ToArray();

            // The GLOBAL mounts. Defaults here are the shipped sidecar's own values, so a sidecar that
            // omits a block keeps the shipped number rather than snapping a bed to the floor.
            var sleep = MiniJson.Dict(root, "sleep");
            into.SleepBedZ = MiniJson.Float(sleep, "bedZ", 0.30f);

            var drive = MiniJson.Dict(root, "drive");
            into.DriveSeatZ = MiniJson.Float(drive, "seatZ", 0.40f);
            into.DriveWheelZ = MiniJson.Float(drive, "wheelZ", 0.655f);
            into.DriveWheelY = MiniJson.Float(drive, "wheelY", 0.315f);

            return true;
        }

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
