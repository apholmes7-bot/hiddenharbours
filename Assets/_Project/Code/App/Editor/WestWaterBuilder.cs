#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// One-click <b>THE WEST WATER</b> — the bay's first open-water scene. Menu: Hidden Harbours ▸ Build
    /// West Water Scene. Re-runnable (idempotent on the assets).
    ///
    /// <para><b>⭐ STEP 1 OF THE BAY</b> (world-map-plan §6). The easiest case first: the sheltered run
    /// between St Peters and Nine Mile Creek, north of the tidal bar — "the first sail. Sheltered,
    /// forgiving, the dory's water" (§2.2). It is built before the mid-bay and the east water precisely
    /// so the water-scene template gets proved on the case with no hazard, no fishery and no landfall in
    /// it, and so the owner can play a whole home arc before more water is built.</para>
    ///
    /// <para><b>This builder is deliberately thin, and that is the deliverable.</b> Everything it does is
    /// <see cref="WaterSceneTemplate"/>'s; everything it knows is <see cref="WestWaterPlan"/>'s. The
    /// mid-bay and the east water should each be a file this length — a plan and a call — rather than a
    /// third and fourth copy of a region builder. If building one of them needs something this file
    /// still hand-rolls, that thing belongs in the template.</para>
    ///
    /// <para><b>What it does NOT wire, and why.</b> No dev bootstrap: Nine Mile Creek and St Peters bake
    /// one so the owner can press Play and walk, but there is nothing to walk on out here — this region
    /// is entered by SAILING into it from either neighbour, with the persistent core, which is also the
    /// only way it is ever entered in the game. No painted seabed and no paint-tool seam: open water
    /// carries height data and no paint (world-map-plan §2.2). No hazard and no spawn table: both belong
    /// to the mid-bay (§2.2, §6 step 4) and building them here would be a later phase smuggled into this
    /// one (rule 8).</para>
    ///
    /// <para><b>The other halves of the two doors are the NEIGHBOURS'.</b> A region builder may not reach
    /// into another region's scene, so the passages that send a boat INTO this region live in
    /// <see cref="StPetersBuilder"/> and <see cref="NineMileCreekBuilder"/> — each guarded on this
    /// region's def existing, exactly as Nine Mile Creek's half of the tidal crossing is guarded on
    /// St Peters'. Re-run all three builders and the round trip is whole.</para>
    /// </summary>
    public static class WestWaterBuilder
    {
        static string ScenePath => WaterSceneTemplate.ScenePathFor(WestWaterPlan.SceneName);

        const string StPetersRegionAsset      = WaterSceneTemplate.DataRegions + "/StPeters.asset";
        const string NineMileCreekRegionAsset = WaterSceneTemplate.DataRegions + "/NineMileCreek.asset";

        [MenuItem("Hidden Harbours/Build West Water Scene")]
        public static void Build()
        {
            // ADR 0019 §1 guard: a from-zero build WIPES anything hand-authored in WestWater.unity.
            // First-ever build (no file) proceeds silently; a re-run over a committed scene must ask.
            if (!RegionBuildGuard.ConfirmOverwrite("The West Water", ScenePath))
                return;

            WaterSceneTemplate.EnsureFolders();

            var plan = WestWaterPlan.Plan;

            // --- DATA: this region's def, with its facts applied whether or not the asset is new -------
            var westWater = WaterSceneTemplate.LoadOrCreateRegion(plan);

            var config = WaterSceneTemplate.LoadOrCreate<GameConfig>(
                WaterSceneTemplate.DataConfig + "/GameConfig.asset");

            // The two neighbours, by stable id. GUARDED rather than created: a region def minted here
            // from guesses would be a second, wrong copy of a shipped region — Nine Mile Creek's builder
            // takes the same care with St Peters' def for the same reason. A missing neighbour costs its
            // door and says so; it never costs the build.
            var stPeters      = AssetDatabase.LoadAssetAtPath<RegionDef>(StPetersRegionAsset);
            var nineMileCreek = AssetDatabase.LoadAssetAtPath<RegionDef>(NineMileCreekRegionAsset);
            if (stPeters == null)
                Debug.LogWarning($"[WestWaterBuilder] No St Peters RegionDef at {StPetersRegionAsset} — the " +
                                 "east door has no passage, so the run dead-ends at the region edge. " +
                                 "Re-run after building St Peters.");
            if (nineMileCreek == null)
                Debug.LogWarning($"[WestWaterBuilder] No Nine Mile Creek RegionDef at {NineMileCreekRegionAsset} — " +
                                 "the west door has no passage, so the run dead-ends at the region edge. " +
                                 "Re-run after building Nine Mile Creek.");

            // --- SCENE ---------------------------------------------------------------------------------
            var scene = WaterSceneTemplate.NewWaterScene();

            // Standalone review opens on the Nine Mile Creek end — where the sail begins.
            WaterSceneTemplate.AddReviewCamera(plan, WestWaterPlan.FromNineMileCreekArrivalPos);

            // --- THE BED (before the sea: the terrain's OnEnable must register before the surface bakes) -
            WaterSceneTemplate.AddTerrain(plan);

            // --- THE SEA (the layered sim-driven shader over the whole rectangle) ------------------------
            var fallbackSquare = WaterSceneTemplate.MakeSquareSprite(WaterSceneTemplate.ArtSprites + "/Square.png");
            WaterSceneTemplate.AddSea(plan, config, fallbackSquare);

            // ⚠ Re-load from disk before wiring refs: an intervening import can invalidate the in-memory
            // instances above (the gotcha every region builder guards against).
            westWater     = AssetDatabase.LoadAssetAtPath<RegionDef>(
                                WaterSceneTemplate.RegionAssetPathFor(WestWaterPlan.RegionAssetName));
            stPeters      = AssetDatabase.LoadAssetAtPath<RegionDef>(StPetersRegionAsset);
            nineMileCreek = AssetDatabase.LoadAssetAtPath<RegionDef>(NineMileCreekRegionAsset);

            // …and re-apply the region's facts to the reloaded asset. LoadOrCreate's initialiser fires
            // only on CREATE, so an already-shipped def would keep saying whatever it said the day it was
            // written — with the builder reporting success (#462).
            WaterSceneTemplate.ApplyRegionFacts(westWater, plan);

            // --- TRAVEL: the loader and the two doors ----------------------------------------------------
            var loader = WaterSceneTemplate.AddSceneLoader(WestWaterPlan.SceneName,
                                                           westWater, stPeters, nineMileCreek);

            // Both outbound passages are KEYLESS, and that is the right answer rather than a fallback:
            // each neighbour has exactly one way in by water — St Peters' slip, Nine Mile Creek's berth —
            // and its own default arrival IS that berth. You sail home and you tie up where you tie up.
            // (The neighbours' passages INTO here do name a key, because this region has two doors.)
            if (stPeters != null)
                WaterSceneTemplate.AddPassage("PassageToStPeters", WestWaterPlan.ToStPetersPassagePos,
                                              plan.EdgeBandSize, stPeters, loader);
            if (nineMileCreek != null)
                WaterSceneTemplate.AddPassage("PassageToNineMileCreek", WestWaterPlan.ToNineMileCreekPassagePos,
                                              plan.EdgeBandSize, nineMileCreek, loader);

            // --- THE ANCHOR: where a boat lands, per door, plus the extent the camera clamps to ----------
            WaterSceneTemplate.AddAnchor(
                plan,
                WestWaterPlan.DefaultArrivalPos,
                (WestWaterPlan.FromStPetersArrivalKey, WestWaterPlan.FromStPetersArrivalPos),
                (WestWaterPlan.FromNineMileCreekArrivalKey, WestWaterPlan.FromNineMileCreekArrivalPos));

            // --- REGION DISPLAY NAMES (the world registrar → Core) ---------------------------------------
            // So the UI's crossing card can name where you have arrived without referencing World.
            var namesGo = new GameObject("RegionDisplayNames");
            var registrar = namesGo.AddComponent<RegionDisplayNameRegistrar>();
            var known = new List<Object> { westWater };
            if (stPeters != null) known.Add(stPeters);
            if (nineMileCreek != null) known.Add(nineMileCreek);
            WaterSceneTemplate.SetRefArray(registrar, "_regions", known.ToArray());

            // --- SAVE & REGISTER --------------------------------------------------------------------------
            WaterSceneTemplate.SaveAndRegister(scene, ScenePath);

            float crossSeconds = WestWaterPlan.RegionWorldSize.x / 3.0f;   // the rowed dory's terminal speed
            Debug.Log(
                $"[WestWaterBuilder] Built {WestWaterPlan.SceneName}.unity — ⭐ THE WEST WATER, the bay's " +
                $"first open-water scene and the first sail. {WestWaterPlan.RegionWorldSize.x:0} × " +
                $"{WestWaterPlan.RegionWorldSize.y:0} m, which is {crossSeconds / 60f:0}:{crossSeconds % 60f:00} " +
                "edge to edge in the rowed dory — inside scene-sizing §1.3's 3–5 min inshore band, and a " +
                "shade longer than the 610 m walk over the bar, so the boat is the freedom and the tide " +
                "stays the drama. NO PAINTED SEABED and FULL HEIGHT DATA (the owner's 2026-08-07 ruling): " +
                $"an analytic bed running from {WestWaterPlan.BayFloorElevation:0.0} m at the Nine Mile " +
                $"Creek end up onto the island's {WestWaterPlan.LeeFloorElevation:0.0} m lee shelf, with " +
                "the tidal bar's own shoulder as the southern boundary — and every one of those three " +
                "numbers is the NEIGHBOUR'S constant, reached for rather than re-typed, so no seam can " +
                $"step. TIDE: mean {WestWaterPlan.TideMean}, amplitude {WestWaterPlan.TideAmplitude} m, " +
                $"phase {WestWaterPlan.TidePhaseHours} h — St Peters' and Nine Mile Creek's verbatim, " +
                "because a region that shares terrain with its neighbours may not have a tide of its own. " +
                "TWO DOORS, each a band spanning the full height of the region: out here the edge IS the " +
                $"door. NAME OWED: the def ships as '{WestWaterPlan.PlaceholderDisplayName}' under the " +
                $"stable id {WestWaterPlan.RegionId}.");

            EditorUtility.DisplayDialog("Hidden Harbours",
                "The West Water built — the bay's first open-water scene.\n\n" +
                $"• {WestWaterPlan.RegionWorldSize.x:0} × {WestWaterPlan.RegionWorldSize.y:0} m — about " +
                "3 min 45 s across in the rowed dory\n" +
                "• St Peters is EAST, Nine Mile Creek is WEST, the bar is the southern edge\n" +
                "• No painted seabed, but real depth everywhere — the sounder reads a true bottom\n" +
                "• Sheltered by design: nothing out here to hit\n\n" +
                "TO SAIL IT:\n" +
                "1. Re-run 'Build St Peters Scene' and 'Build Nine Mile Creek Scene' — each gains its " +
                "passage OUT to this region (a builder may not reach into another region's scene)\n" +
                "2. Open StPeters.unity, press Play, and take the dory west round the island\n\n" +
                "STILL YOURS: this region needs a NAME (your PEI-variant style). It ships as " +
                $"'{WestWaterPlan.PlaceholderDisplayName}' — the id underneath never changes.",
                "Fair winds");
        }
    }
}
#endif
