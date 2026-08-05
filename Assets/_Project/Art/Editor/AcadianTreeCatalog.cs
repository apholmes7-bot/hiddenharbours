#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The PLACEMENT half of the Acadian tree kit — "what trees can I put in a scene, which sprite
    /// is which, and what does one of them look like once it is standing there?" The sibling of
    /// <see cref="FlowerCatalog"/>, and the one place <see cref="AcadianTreePrefabBuilder"/>, the
    /// <see cref="TreePaintTool"/> and the tests go through, so they cannot drift from each other.
    ///
    /// <para><b>It owns no numbers.</b> Cell, pivot, flare pad, trunk anchor and true height all come
    /// from <see cref="TreeKitCatalog"/> reading the bake's own <c>Trees.json</c>. This type only
    /// answers questions about SPRITES and SCENE OBJECTS. Splitting it that way keeps the bake lane's
    /// file (which the remaining 330 sheets will grow) separate from the placement lane's.</para>
    ///
    /// <para><b>Three things about a tree that are not true of any other decor in this repo:</b></para>
    /// <list type="bullet">
    ///   <item><b>It does not pivot bottom-centre.</b> The near-root flare projects BELOW the trunk
    ///   foot under the 40° camera, so each cell carries <c>nearFlarePad</c> rows under the pivot
    ///   (8–13 px = 0.25–0.41 m on the pass-2 rig; 13–23 px = 0.41–0.72 m on pass 1, which drew a
    ///   broad skirt where pass 2 draws three splayed buttresses). The pivot is already baked into
    ///   the sliced sub-sprite, so nothing
    ///   here re-derives it — but anything that "corrects" a tree to <c>{0.5, 0}</c> sinks it into
    ///   the ground by that much, and it reads as an art bug.</item>
    ///   <item><b>Its trunk anchor is per species.</b> See <see cref="TreeTrunkAnchor"/>.</item>
    ///   <item><b>Its drawn height is NOT its height in metres.</b> The rig bakes the tree already
    ///   foreshortened by the camera (<c>heightScale = sin 40° = 0.766</c>), so a 5.7 m Red Spruce
    ///   draws 4.63 m tall at PPU 32 and <c>localScale</c> stays 1. Scaling a tree up to its
    ///   <c>metres</c> would make it ~31% too tall and break the pixel grid. (The drawn height runs
    ///   a little ABOVE a flat height×0.766 — 0.998–1.081 of it across the ten — because the cell's
    ///   top row is set by the crown's silhouette, not by the leader, and the crown projects toward
    ///   the camera.)</item>
    /// </list>
    /// </summary>
    public static class AcadianTreeCatalog
    {
        /// <summary>Where the built prefabs land. A subfolder of the decor <c>Trees/</c> so the 43
        /// old hand-drawn <c>TreeNN</c> prefabs (a different, un-retired set) stay separate and
        /// neither builder can overwrite the other's output.</summary>
        public const string PrefabRoot = "Assets/_Project/Prefabs/Decor/Trees/Acadian";

        /// <summary>The shared canopy-sway material. ONE material for all ten species — the
        /// per-species part is <c>_TrunkAnchor</c>, driven per renderer by
        /// <see cref="TreeTrunkAnchor"/>.</summary>
        public const string MaterialPath = "Assets/_Project/Art/Materials/Tree.mat";

        /// <summary>Pre-YSort sorting order. <see cref="YSortSprite"/> OWNS the final value (it
        /// recomputes from world Y on enable); this is only what a tree shows before that runs.
        /// Matches <c>DecorPrefabBuilder</c>'s tree default so the two sets read alike.</summary>
        public const int SortingOrder = 5;

        /// <summary>The shared canopy material, or null if Unity has not imported the TreeWind
        /// shader + material yet (a tool should say so rather than place static trees silently).</summary>
        public static Material LoadMaterial() => AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        // =================================================================================
        // what is placeable
        // =================================================================================

        /// <summary>
        /// One placeable tree: a contract entry plus the season being placed. The kit is keyed
        /// species × stage × season and today bakes mature/summer only, so this is one per species —
        /// but it is modelled as the full key so a later bake of the other stages and seasons simply
        /// appears in the tool instead of needing new code.
        /// </summary>
        public readonly struct Placement
        {
            public readonly TreeKitCatalog.Entry Entry;
            public readonly string Season;

            public Placement(TreeKitCatalog.Entry entry, string season)
            { Entry = entry; Season = season; }

            public string Species => Entry.species;

            /// <summary>The ALBEDO sheet's stem — also the prefab name and the sprite-name prefix.</summary>
            public string Stem =>
                TreeKitCatalog.StemFor(Entry.species, Entry.stage, Season, TreeKitCatalog.Channel.Albedo);

            public string SheetPath =>
                TreeKitCatalog.SheetPath(Entry.species, Entry.stage, Season, TreeKitCatalog.Channel.Albedo);

            /// <summary>The MASK sheet (R key · G rim · B depth · A coverage). Bound as a TEXTURE, not
            /// placed as a sprite: <c>HiddenHarboursTreeWind</c> samples it at the albedo's uv through
            /// the albedo's mesh, because the rig's 1 px keyline ring exists here but not in the
            /// normal, and giving either its own sprite loses the ring.</summary>
            public string MaskSheetPath =>
                TreeKitCatalog.SheetPath(Entry.species, Entry.stage, Season, TreeKitCatalog.Channel.Mask);

            /// <summary>The view-space NORMAL sheet. Same rule as the mask.</summary>
            public string NormalSheetPath =>
                TreeKitCatalog.SheetPath(Entry.species, Entry.stage, Season, TreeKitCatalog.Channel.Normal);

            /// <summary>For a tool dropdown and the scene hierarchy: "Red Spruce — 5.7 m". The
            /// height is the number the owner is being asked to judge, so it is in the label.</summary>
            public string Label => $"{Entry.name} — {Entry.metres:0.#} m";

            public bool IsValid => Entry != null;
        }

        /// <summary>
        /// Every placeable tree the committed contract claims, in the RIG'S OWN ORDER (conifers then
        /// hardwoods — spire, pine, cedar, larch, oval, round), not alphabetical: that ordering is
        /// what makes a lineup readable as a comparison of forms. Returns empty (never throws) when
        /// the contract is missing, so a tool can say so rather than fall over.
        /// </summary>
        public static List<Placement> Scan()
        {
            var list = new List<Placement>();
            TreeKitCatalog.Contract contract = TreeKitCatalog.Load();
            if (contract?.trees == null) return list;

            foreach (var entry in contract.trees)
            {
                if (entry?.seasons == null) continue;
                foreach (string season in entry.seasons)
                    list.Add(new Placement(entry, season));
            }
            return list;
        }

        // =================================================================================
        // sprites
        // =================================================================================

        /// <summary>
        /// How many drawn VARIANTS a species has on its sheet — <c>sheetW / cellW</c>, read from the
        /// contract rather than assumed to be 4. Variants are different individual trees of the same
        /// species (not poses and not sway frames), which is exactly what a stand wants mixed.
        /// </summary>
        public static int VariantCount(TreeKitCatalog.Entry entry) =>
            entry == null || entry.cellW <= 0 ? 0 : Mathf.Max(1, entry.sheetW / entry.cellW);

        /// <summary>
        /// One variant's sprite, matched BY NAME (never by <c>LoadAllAssetsAtPath</c>'s array order,
        /// which Unity does not promise). The sheets import spriteMode MULTIPLE, so a plain
        /// <c>LoadAssetAtPath&lt;Sprite&gt;</c> returns null for them.
        ///
        /// <para>Accepts both of <c>TreeSheetSlicer</c>'s names: <c>&lt;stem&gt;_v&lt;n&gt;</c> for
        /// today's one-sway-row sheets and <c>&lt;stem&gt;_v&lt;n&gt;_f0</c> for a future bake that
        /// adds rows — so adding sway rows does not silently empty this tool.</para>
        ///
        /// <para>Returns null (no throw) when the sheet is missing or unsliced, so a partial art
        /// state still places what it can.</para>
        /// </summary>
        public static Sprite LoadVariant(Placement placement, int variant)
        {
            if (!placement.IsValid || variant < 0) return null;
            string stem = placement.Stem;
            string exact = $"{stem}_v{variant}";
            string firstFrame = $"{exact}_f0";

            Sprite fallback = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(placement.SheetPath))
            {
                if (!(o is Sprite s)) continue;
                if (string.Equals(s.name, exact, StringComparison.Ordinal)) return s;
                if (string.Equals(s.name, firstFrame, StringComparison.Ordinal)) fallback = s;
            }
            return fallback;
        }

        /// <summary>
        /// This species' baked light sheets as TEXTURES — <c>(mask, normal)</c>, either of which may be
        /// null if that sheet is missing. Loaded with <see cref="AssetDatabase.LoadAssetAtPath{T}"/> at
        /// <c>Texture2D</c>, which works even though these import <c>spriteMode: Multiple</c>: the
        /// MULTIPLE trap bites <c>LoadAssetAtPath&lt;Sprite&gt;</c> (the sprites are sub-assets), not the
        /// main texture object, and the main texture is exactly what the shader wants — it samples
        /// these at the albedo's uv rather than as sprites of their own.
        /// </summary>
        public static (Texture2D mask, Texture2D normal) LoadLightSheets(Placement placement)
        {
            if (!placement.IsValid) return (null, null);
            return (AssetDatabase.LoadAssetAtPath<Texture2D>(placement.MaskSheetPath),
                    AssetDatabase.LoadAssetAtPath<Texture2D>(placement.NormalSheetPath));
        }

        /// <summary>Every variant of a species, in order, with any that failed to load dropped.</summary>
        public static List<Sprite> LoadAllVariants(Placement placement)
        {
            var sprites = new List<Sprite>();
            for (int v = 0; v < VariantCount(placement.Entry); v++)
            {
                var s = LoadVariant(placement, v);
                if (s != null) sprites.Add(s);
            }
            return sprites;
        }

        // =================================================================================
        // measurements the owner is judging (all derived, none stored)
        // =================================================================================

        /// <summary>How tall the tree DRAWS above its trunk foot, in metres at PPU 32
        /// (<c>pivotY / ppu</c>) — what the owner sees, as opposed to <c>Entry.metres</c>, which is
        /// how tall the tree IS before the camera foreshortens it.</summary>
        public static float DrawnHeightMetres(TreeKitCatalog.Entry entry, int ppu) =>
            entry == null || ppu <= 0 ? 0f : (float)entry.pivotY / ppu;

        /// <summary>The full drawn width of a cell in metres — used to space a lineup so crowns do
        /// not overlap.</summary>
        public static float DrawnWidthMetres(TreeKitCatalog.Entry entry, int ppu) =>
            entry == null || ppu <= 0 ? 0f : (float)entry.cellW / ppu;

        /// <summary>How far below the trunk foot the near-root flare hangs, in metres — the exact
        /// distance a bottom-centre pivot would sink this species into the ground.</summary>
        public static float FlareDropMetres(TreeKitCatalog.Entry entry, int ppu) =>
            entry == null || ppu <= 0 ? 0f : (float)entry.nearFlarePad / ppu;

        // =================================================================================
        // the canonical scene object
        // =================================================================================

        /// <summary>
        /// Turn <paramref name="go"/> into the canonical Acadian tree: a
        /// <see cref="SpriteRenderer"/> on the shared wind material, a <see cref="YSortSprite"/> so
        /// it layers around the player by world Y, a <see cref="TreeTrunkAnchor"/> carrying THIS
        /// species' planted fraction, a <see cref="HiddenHarbours.Art.ReflectiveObject"/> and a
        /// <see cref="HiddenHarbours.Art.SpriteShadow"/>. Nothing else — no Animator (the sway is the
        /// shader reading the shared sim wind; an Animator would be deaf to it) and no rescaling (the
        /// bake is already at honest metric size).
        ///
        /// <para>Both the prefab builder and the paint tool go through here, so a tree the owner
        /// paints and a tree they drag in are the same object. Returns the renderer for callers that
        /// want to tint or flip it.</para>
        /// </summary>
        public static SpriteRenderer Configure(GameObject go, Placement placement, Sprite sprite,
                                               Material material, int sortingOrder = SortingOrder)
        {
            if (!placement.IsValid)
                throw new ArgumentException(
                    "Configure was handed a placement with no contract entry, so there is no trunk " +
                    "anchor to give it. Anchoring at a plausible default is exactly the reading " +
                    "this component replaces — fix the caller.", nameof(placement));

            // ⚠️ `GetComponent<T>() ?? AddComponent<T>()` LOOKS right and is wrong: `??` tests the
            // real C# reference, so it never reaches UnityEngine.Object's overloaded `==` and a
            // "fake null" component satisfies it. AddComponent is then skipped and the first field
            // write throws MissingComponentException. Always compare with `== null`.
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();

            sr.sprite = sprite;
            if (material != null) sr.sharedMaterial = material;
            sr.sortingOrder = sortingOrder;

            // Honest metric size. The rig baked the foreshortening in; scaling to Entry.metres here
            // is the tempting wrong move (see the class doc).
            go.transform.localScale = Vector3.one;

            if (go.GetComponent<YSortSprite>() == null) go.AddComponent<YSortSprite>();

            var anchor = go.GetComponent<TreeTrunkAnchor>();
            if (anchor == null) anchor = go.AddComponent<TreeTrunkAnchor>();
            anchor.Anchor = placement.Entry.trunkAnchor;

            // This species' baked lighting channels, on the SAME property block as the anchor. They
            // are inert until the owner raises Tree.mat's _LightResponse off 0 — binding them costs
            // nothing and means turning the response on is one slider, not a re-place of the forest.
            var (mask, normal) = LoadLightSheets(placement);
            anchor.SetLightSheets(mask, normal);

            // (ADR 0027 #8) A tree standing at the water's edge REFLECTS in it. Added HERE and not
            // in the prefab builder because this method is the ONE place an Acadian tree is
            // configured — the Tree Paint Tool comes through it too, so a painted treeline reflects
            // without the owner touching a component.
            //
            // No pivot override: the rig's pivot is the TRUNK FOOT, which already IS the
            // ground-contact point the mirror axis wants (ADR 0026). And the component's own
            // distance gate is what keeps an inland forest out of the reflective set — every tree may
            // carry this, only the ones standing near the water join the pass.
            if (go.GetComponent<HiddenHarbours.Art.ReflectiveObject>() == null)
                go.AddComponent<HiddenHarbours.Art.ReflectiveObject>();

            // (Owner's ruling 2026-08-05) A TREE CASTS. Added here for the same reason as the
            // reflection above — this is the ONE place an Acadian tree is configured, so the woods the
            // builder plants, the trees the owner paints and the prefabs all throw the same shadow, and
            // a rebuild cannot undo it. The rig's pivot IS the trunk foot (ADR 0026), which is exactly
            // the ground-contact point the shadow anchors at, so no foot offset and no per-species
            // tuning: the shear is proportional to the sprite's own height, so a 6.9 m white pine
            // rakes further than a 4.8 m cedar for free.
            //
            // ⚠️ The whole population casts, unlike the shore's height rule — because there is no
            // short tree. The kit's smallest mature cell is 4.8 m, every one of them has the silhouette
            // the component was written for, and the count is the woods' own (a few hundred), not the
            // grass layer's thousands. LitDecorCasterBudgetTests pins that against the planter's grid.
            if (go.GetComponent<HiddenHarbours.Art.SpriteShadow>() == null)
                go.AddComponent<HiddenHarbours.Art.SpriteShadow>();

            return sr;
        }

        /// <summary>
        /// A one-line report of what is placeable, for a tool's info box: how many species, and the
        /// spread of trunk anchors against the single value the shared material ships — which is the
        /// whole reason the anchor is per renderer.
        /// </summary>
        public static string Summary(IReadOnlyList<Placement> placements)
        {
            if (placements == null || placements.Count == 0) return "No trees baked.";
            float lo = placements.Min(p => p.Entry.trunkAnchor);
            float hi = placements.Max(p => p.Entry.trunkAnchor);
            return $"{placements.Count} baked tree(s); trunk anchors {lo:F4}–{hi:F4} " +
                   "(each tree carries its own — the shared material's single value would " +
                   "over-anchor most of them).";
        }
    }
}
#endif
