using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// The owner-facing entry point for the cliff kit: one menu item, ~30 s, and the wall textures
    /// exist.
    ///
    /// <para><b>⭐ THE CLIFF PNGs ARE DELIBERATELY NOT IN THE REPOSITORY.</b> Every sibling kit commits
    /// its sheets; this one measured 16.0 MB for even the narrow St Peters subset (and ~90 MB for the
    /// full parametric set), all of it regenerable in seconds and all of it stale the moment a rig
    /// coefficient moves — so the rig is committed and the pixels are baked locally.</para>
    ///
    /// <para><b>⭐ …WHICH IS WHY THIS MENU IS A CONVENIENCE AND NEVER A REQUIREMENT.</b> The coordinator
    /// made that binding when the rig-not-sheets trade was accepted (PR #427): <i>"a fresh clone
    /// self-heals — a checkout that renders pink until someone finds a menu item is not shipped."</i> So
    /// the region builder calls <see cref="EnsureBaked"/> and bakes on missing, and this menu exists for
    /// re-baking after a rig edit. Both go through <see cref="Bake(string, bool)"/>, so there is exactly
    /// one bake path and the automatic one cannot drift from the one a human has been running.</para>
    ///
    /// <para><b>⭐ TWO LOOKS, AND THE SWITCH IS PULLED AT BAKE TIME</b> (owner, 09-18: "bake switch").
    /// <see cref="KitPxItem"/> re-bakes every rock this checkout carries in the px kit's skin, writes the
    /// palette LUT and turns the wall material's <see cref="CliffCatalog.PxKeyword"/> ON;
    /// <see cref="KitV10Item"/> bakes them back and turns it OFF. The pixels and the keyword move
    /// together because the colour slot holds either v10 colour or a px index, and the shader branch
    /// must match what is on disk. There is no runtime toggle, and the material ships in the v10
    /// look.</para>
    /// </summary>
    public static class CliffBakeMenu
    {
        /// <summary>The whole kit, back in the v10 look.</summary>
        public const string KitV10Item = "Hidden Harbours/Dev/Bake Cliff Kit — v10";

        /// <summary>The whole kit, in the px look.</summary>
        public const string KitPxItem = "Hidden Harbours/Dev/Bake Cliff Kit — px";

        [MenuItem(KitV10Item, priority = 144)]
        public static void BakeKitV10() => BakeKit(px: false);

        [MenuItem(KitPxItem, priority = 145)]
        public static void BakeKitPx() => BakeKit(px: true);

        [MenuItem("Hidden Harbours/Dev/Bake Cliff Face Kit (sandstone, 5 aspects × 3 batters)",
                  priority = 146)]
        public static void BakeDefault() => Bake(CliffBaker.DefaultRock);

        [MenuItem("Hidden Harbours/Dev/Bake Cliff Face Kit — till", priority = 147)]
        public static void BakeTill() => Bake("till");

        [MenuItem("Hidden Harbours/Dev/Bake Cliff Face Kit — basalt", priority = 148)]
        public static void BakeBasalt() => Bake("basalt");

        static readonly int PaletteId = Shader.PropertyToID("_Palette");
        static readonly int PxKeyId = Shader.PropertyToID("_PxKey");

        /// <summary>
        /// True when the shipped wall material is in the px look. Its keyword is the record of which skin
        /// the last whole-kit bake wrote, so every single-rock bake and every sentinel follows it.
        /// </summary>
        public static bool IsPxLook
        {
            get
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(CliffCatalog.MaterialPath);
                return mat != null && mat.IsKeywordEnabled(CliffCatalog.PxKeyword);
            }
        }

        /// <summary>
        /// The one asset whose presence means "this checkout has been baked" — the sandstone south wall's
        /// colour slot, at the base batter and the base wear step. Chosen rather than a folder
        /// existence check because an EMPTY <c>Faces/</c> folder is exactly what a half-finished bake
        /// leaves behind, and because this is the first face the default bake writes.
        /// </summary>
        public static string SentinelFacePath => SentinelFacePathFor(CliffBaker.DefaultRock);

        /// <summary>The same sentinel, for any rock — because a wall is built from more than one and a
        /// checkout carrying only the first is exactly as unbuildable as one carrying none.</summary>
        public static string SentinelFacePathFor(string rock) => SentinelFacePathFor(rock, IsPxLook);

        /// <summary>The sentinel in a named look: the colour slot is <c>_unlit</c> in v10 and
        /// <c>_index</c> in px — the channel the wall shader cannot render without.</summary>
        public static string SentinelFacePathFor(string rock, bool px) =>
            CliffBaker.FaceAssetPath(CliffCatalog.BakeRoot, rock, "S", 0,
                                     px ? CliffCatalog.IndexChannel : CliffCatalog.UnlitChannel);

        /// <summary>
        /// True when this checkout carries a baked face for <b>every rock the wall is built from</b>.
        ///
        /// <para><b>⚠ Every rock, not just the first — and that matters for an EXISTING checkout, not
        /// only a fresh one.</b> A face is now stratified: sandstone below, till above. A machine that
        /// was baked before the strata landed has sandstone and no till, and a sentinel that asked only
        /// about sandstone would call it baked, skip the bake, and leave every wall to fall back to bare
        /// rock — the owner's stratified-cliff direction quietly not applied, with nothing on screen
        /// saying so.</para>
        /// </summary>
        public static bool IsBaked
        {
            get
            {
                foreach (string rock in CliffBaker.DefaultRocks)
                    if (AssetDatabase.LoadAssetAtPath<Texture2D>(SentinelFacePathFor(rock)) == null)
                        return false;
                return true;
            }
        }

        /// <summary>
        /// <b>Bake the kit if — and only if — this checkout has none.</b> The self-heal the fresh-clone
        /// condition rests on: the region builder calls it, so cloning and building gives you a textured
        /// coast without anyone having to know a menu item exists. Returns true if it actually baked.
        ///
        /// <para>Deliberately does NOT re-bake when the sheets are present. A bake is ~30 s and the
        /// builder is re-run constantly; and re-baking on every build would silently overwrite a rock the
        /// owner had chosen from the two alternate menu items above.</para>
        ///
        /// <para><b>⚠ In the px look it refuses, baked or not.</b> Both region builders wire each wall's
        /// colour texture by its v10 name (<c>LoadFace(…, "_unlit")</c>), and a px bake has moved that
        /// file to <c>_index</c> — so a build now would stand the coast up with no colour on any wall. A
        /// builder change would re-run the exporter, and this switch ships without one; until the band's
        /// field is renamed, the way to rebuild is v10, rebuild, px — and the message says so.</para>
        /// </summary>
        public static bool EnsureBaked(string rock = null)
        {
            if (IsPxLook)
                throw new InvalidOperationException(
                    "[cliff-bake] the cliff kit is in the px look, and a region builder cannot wire it yet: " +
                    "it loads each wall's colour texture by its v10 name (_unlit), which the px bake moved " +
                    $"to _index. Press {Said(KitV10Item)}, rebuild the region, then press {Said(KitPxItem)}.");

            if (IsBaked) return false;

            // Named rock: the caller wants that one and only that one.
            if (rock != null)
            {
                Debug.Log($"[cliff-bake] baking {rock} into {CliffCatalog.BakeRoot} on request.");
                Bake(rock, px: false);
                return true;
            }

            // ⭐ Bake only the rocks that are actually MISSING. A checkout baked before the strata
            // landed has sandstone already, and re-baking it would spend ~30 s overwriting good pixels
            // just to reach the till it genuinely lacks.
            Debug.Log($"[cliff-bake] {CliffCatalog.BakeRoot} is missing a rock the wall needs — baking " +
                      "now (the kit ships as the rig; the PNGs are gitignored and regenerate in seconds).");
            foreach (string needed in CliffBaker.DefaultRocks)
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(SentinelFacePathFor(needed, false)) == null)
                    Bake(needed, px: false);
            return true;
        }

        /// <summary>
        /// The rocks a whole-kit bake re-bakes: the two the coast is built from, plus any other rock
        /// this checkout already carries in either skin (basalt, once someone has baked it from its own
        /// item) — so a switch never leaves one rock behind in the other look.
        /// </summary>
        public static string[] RocksToBake() =>
            CliffBaker.DefaultRocks
                .Concat(CliffCatalog.Rocks.Where(r => CliffBaker.OnDisk(SentinelFacePathFor(r, false)) ||
                                                      CliffBaker.OnDisk(SentinelFacePathFor(r, true))))
                .Distinct()
                .ToArray();

        /// <summary>
        /// The switch: every rock in <see cref="RocksToBake"/> re-baked in one look, then the material
        /// moved to it. Every rock's slots are checked before anything renders, so a refusal leaves the
        /// kit exactly as it was.
        /// </summary>
        public static void BakeKit(bool px)
        {
            string[] rocks = RocksToBake();
            string item = px ? KitPxItem : KitV10Item;

            CliffBaker.AssertSlotsUnambiguous(CliffCatalog.BakeRoot, rocks);

            // The LUT FIRST: nothing samples it until the keyword is on, so writing it can never break
            // the look on screen, and the material needs it imported before it can hold it.
            bool wroteLut = false;
            if (px)
            {
                wroteLut = CliffBaker.WritePaletteLut();
                AssetDatabase.Refresh();
                CliffBaker.ApplyImportSettings(CliffCatalog.PxPalettePath, CliffAssetKind.Palette, "");
            }

            try
            {
                foreach (string rock in rocks) Bake(rock, px);
            }
            catch
            {
                Debug.LogError(
                    $"[cliff-bake] the {(px ? "px" : "v10")} bake stopped part-way, so some rocks are in the " +
                    $"new skin while the material is still in the old look. Press {Said(item)} again.");
                throw;
            }

            bool moved = SetLook(px);
            Debug.Log(
                $"[cliff-bake] the cliff kit is in the {(px ? "px" : "v10")} look: " +
                $"{string.Join(", ", rocks)} re-baked; the wall material " +
                $"{(moved ? "switched and saved" : "was already in this look")}" +
                (px ? $"; the palette LUT at {CliffCatalog.PxPalettePath} " +
                      $"{(wroteLut ? "written" : "unchanged")}." : ".") +
                (moved ? $" Commit {CliffCatalog.MaterialPath}" +
                         (px ? $" with {CliffCatalog.PxPalettePath}, its .meta, and " +
                               $"{CliffCatalog.PxPaletteFolder}.meta (the folder's own, new on the first " +
                               "px bake)." : ".") : ""));
        }

        /// <summary>
        /// Moves the shipped wall material to a look: px turns the keyword on and hands it the LUT and
        /// the bake key; v10 turns the keyword off and lets go of the LUT. Saves only this asset, and
        /// only when something changed. Returns whether it did.
        /// </summary>
        public static bool SetLook(bool px)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CliffCatalog.MaterialPath);
            if (mat == null)
                throw new InvalidOperationException(
                    $"[cliff-bake] no wall material at {CliffCatalog.MaterialPath} — the look lives on it.");

            bool changed = false;
            if (px)
            {
                var lut = AssetDatabase.LoadAssetAtPath<Texture2D>(CliffCatalog.PxPalettePath);
                if (lut == null)
                    throw new InvalidOperationException(
                        $"[cliff-bake] the px palette LUT is not imported at {CliffCatalog.PxPalettePath}; " +
                        "the material was left in the v10 look.");
                if (!mat.IsKeywordEnabled(CliffCatalog.PxKeyword))
                { mat.EnableKeyword(CliffCatalog.PxKeyword); changed = true; }
                if (mat.GetTexture(PaletteId) != lut) { mat.SetTexture(PaletteId, lut); changed = true; }
                Vector4 key = CliffCatalog.PxBakeKey;
                if (mat.GetVector(PxKeyId) != key) { mat.SetVector(PxKeyId, key); changed = true; }
            }
            else
            {
                if (mat.IsKeywordEnabled(CliffCatalog.PxKeyword))
                { mat.DisableKeyword(CliffCatalog.PxKeyword); changed = true; }
                if (mat.GetTexture(PaletteId) != null) { mat.SetTexture(PaletteId, null); changed = true; }
            }

            if (!changed) return false;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            return true;
        }

        /// <summary>Bake one rock in the look the material is in (see <see cref="IsPxLook"/>).</summary>
        public static void Bake(string rock) => Bake(rock, IsPxLook);

        /// <summary>Bake one rock's full St Peters subset in one skin, import it, and apply the kit's
        /// import contract. The single bake path — the menu items, <see cref="BakeKit"/> and
        /// <see cref="EnsureBaked"/> all land here. It never touches the material.</summary>
        public static void Bake(string rock, bool px)
        {
            string skin = px ? "px" : "v10";
            CliffBakeResult result;
            try
            {
                result = CliffBaker.BakeAll(
                    rock: rock, px: px,
                    progress: (what, t) => EditorUtility.DisplayProgressBar(
                        $"Baking the cliff kit ({rock}, {skin})", what, t));
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[cliff-bake] {rock} ({skin}): {e.Message}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // Import BEFORE the settings pass — an asset Unity has not seen yet has no importer to
            // configure, which is the quiet way a kit ends up point-filtered-by-default and sRGB-on.
            AssetDatabase.Refresh();

            // The channel rides on each asset from the bake (never re-derived from its name), because the
            // px _index must import linear and a suffix list that forgets it imports it as colour.
            int retuned = 0;
            foreach (CliffAssetBake a in result.Assets)
                if (CliffBaker.ApplyImportSettings(a.AssetPath, a.Kind, a.Channel)) retuned++;

            Debug.Log(
                $"[cliff-bake] {rock} ({skin}) via {result.EngineName}: " +
                $"{result.FaceCount} face sets ({result.FaceCount * CliffCatalog.LiveChannels.Length} " +
                $"channel PNGs), {result.StripCount} brow/toe decals, {result.LedgeCount} ledge sheets, " +
                $"{result.ProfileCount} profiles — {result.TotalPngBytes / 1048576.0:F1} MB total, " +
                $"{retuned} import settings applied. " +
                $"Render {result.RenderMilliseconds / 1000.0:F1} s of " +
                $"{result.TotalMilliseconds / 1000.0:F1} s.\n" +
                $"Written under {CliffCatalog.BakeRoot} (gitignored — the rig is the committed source).");
        }

        /// <summary>A menu path the way a person reads it off the menu bar.</summary>
        static string Said(string menuPath) => menuPath.Replace("/", " ▸ ");
    }
}
