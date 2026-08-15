#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;   // PixelPerfectCamera
using UnityEngine.SceneManagement;       // Scene
using HiddenHarbours.Art.Editor;         // ArtCameraSetup — the VS-23 locked pixel-perfect convention
using HiddenHarbours.Core;               // GameConfig, SortingBands
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// Everything an OPEN-WATER region scene is, as one parameterised builder — the
    /// <c>world-map-plan.md</c> §4.5 item ("the three bay scenes are ~90 % identical … one parameterised
    /// builder, three small plans"), extracted while building the first of the three.
    ///
    /// <para><b>Why this exists.</b> <see cref="StPetersBuilder"/> and <see cref="NineMileCreekBuilder"/>
    /// each hand-roll the same six things — the empty scene and its review camera, the sea plane with the
    /// layered <c>WaterSurface</c> over it, the height source registered for the region, the passage
    /// bands, the arrival anchor, and the "re-apply the region's facts to the shipped asset" step. They
    /// are LAND regions and their copies have diverged for real reasons (a painted island vs an authored
    /// mainland), so this does not rewrite them. It generalises <b>their conventions</b> so the west
    /// water, the mid-bay and the east water get one copy between them instead of three more.</para>
    ///
    /// <para><b>What a water scene is NOT.</b> No painted seabed — open water is deep enough that the
    /// bottom is never visible, so the paint pipeline (which exists for coasts the owner paints) is not
    /// wired here at all. It still carries <b>height data everywhere</b>, because the depth sounder and
    /// the fish finder read real depth: the one-height-map law, owner's ruling 2026-08-07
    /// (world-map-plan §2.2). The height source is therefore ANALYTIC —
    /// <see cref="RectTidalTerrain"/>, whose deep floor plus max-composing plateaus is exactly the shape
    /// an offshore bed needs and whose composition is already pure and headlessly tested.</para>
    ///
    /// <para><b>Seams belong to the plan, not to this file.</b> A water scene touches its neighbours, and
    /// the elevation it exposes where they meet has to be the neighbour's own number rather than a
    /// re-typed copy of it (the crossing's lesson: two halves of one bar that disagree by arithmetic).
    /// This helper never invents an elevation — it pushes whatever the <see cref="WaterScenePlan"/> hands
    /// it, and the plan is where a region reaches for its neighbours' constants.</para>
    ///
    /// <para><b>Rule 4.</b> Authoring code necessarily touches the concrete components it is authoring —
    /// that is what the two region builders already do. What goes through Core is the RUNTIME wiring this
    /// leaves behind: the terrain publishes itself into <c>GameServices.TidalTerrain</c> on enable, the
    /// passage publishes its arrival key into <c>GameServices.PendingArrivalKey</c>, and the anchor
    /// publishes the region bounds — no feature module ever reaches for another's classes at run time.</para>
    /// </summary>
    public static class WaterSceneTemplate
    {
        public const string DataConfig  = "Assets/_Project/Data/Config";
        public const string DataRegions = "Assets/_Project/Data/Regions";
        public const string ArtSprites  = "Assets/_Project/Art/Sprites";
        public const string Scenes      = "Assets/_Project/Scenes";

        const string ArtSea      = "Assets/_Project/Art/Tilesets/Water/SeaTile.png";
        const string ArtWaterMat = "Assets/_Project/Art/Materials/Water.mat";   // the layered SIM-driven shader (ADR 0010)

        // Weather-driven water palette anchor presets (ADR 0017) — the two land builders' wiring, verbatim:
        // base = null ON PURPOSE so the live Water.mat is the calm baseline (never a frozen preset copy).
        const string ArtWaterPresets   = "Assets/_Project/Art/Materials/WaterPresets";
        const string ArtWaterCalmMood  = ArtWaterPresets + "/Water_GlassyCalm.mat";
        const string ArtWaterStormMood = ArtWaterPresets + "/Water_StormGrey.mat";
        const string ArtWaterFogMood   = ArtWaterPresets + "/Water_FoggySmother.mat";

        /// <summary>The displaced surface's in-scene face (ADR 0023 phase 2).</summary>
        public const string ArtWaterOverlayMat = "Assets/_Project/Art/Materials/WaterOverlay.mat";

        /// <summary>The one shared <see cref="GameConfig"/> every region's water reads its owner-tuned
        /// knobs from.</summary>
        public const string GameConfigAsset = DataConfig + "/GameConfig.asset";

        /// <summary>The scene asset path for a water region's scene name.</summary>
        public static string ScenePathFor(string sceneName) => $"{Scenes}/{sceneName}.unity";

        /// <summary>The region-def asset path a water region's plan is committed at.</summary>
        public static string RegionAssetPathFor(string assetName) => $"{DataRegions}/{assetName}.asset";

        // =============================================================================================
        //  DATA — the region's def, and the #462 lesson about how to apply it
        // =============================================================================================

        /// <summary>
        /// Load (or first-create) the region's committed <see cref="RegionDef"/> and apply the plan's
        /// facts to it <b>unconditionally</b>.
        ///
        /// <para>⚠ <b>This is the #462 lesson and it is the whole reason this is one call.</b> A
        /// <c>LoadOrCreate</c> initialiser runs only when the asset is ABSENT, which is right for
        /// owner-tuned fields (prices are his) and wrong for the geography: once a region has shipped, a
        /// changed extent or tide would be left saying the old thing forever, on the owner's machine and
        /// on anyone's, with the builder reporting success. St Peters shipped a 160 × 120 m extent while
        /// its builder authored 760 × 520 for exactly this reason. So the facts are re-applied on every
        /// run, and there is no code path that authors a water region's extent any other way.</para>
        /// </summary>
        public static RegionDef LoadOrCreateRegion(WaterScenePlan plan)
        {
            string path = RegionAssetPathFor(plan.RegionAssetName);
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>(path);
            if (region == null)
            {
                region = ScriptableObject.CreateInstance<RegionDef>();
                AssetDatabase.CreateAsset(region, path);
                AssetDatabase.SaveAssets();
                region = AssetDatabase.LoadAssetAtPath<RegionDef>(path);
            }
            ApplyRegionFacts(region, plan);
            return region;
        }

        /// <summary>
        /// Everything a water region's def asserts about itself, applied to an EXISTING asset as well as
        /// a new one. See <see cref="LoadOrCreateRegion"/> for why that is not optional.
        ///
        /// <para><see cref="RegionDef.SeabedPixelsPerMetre"/> is authored even though an open-water scene
        /// paints no seabed: it is the region's height-data resolution, the number
        /// <see cref="WaterScenePlan.HeightBakeResolution"/> derives the shader's bake grid from, and the
        /// figure the paint tool would honour if the owner ever did reach for it here.</para>
        /// </summary>
        public static void ApplyRegionFacts(RegionDef region, WaterScenePlan plan)
        {
            if (region == null || plan == null) return;

            region.Id = plan.RegionId;
            region.DisplayName = plan.DisplayName;
            region.SceneName = plan.SceneName;
            region.Description = plan.Description;
            region.UnlockFlag = plan.UnlockFlag ?? "";

            region.WorldCenter = plan.WorldCenter;
            region.WorldSizeMeters = plan.WorldSize;
            region.SeabedPixelsPerMetre = plan.SeabedPixelsPerMetre;

            // Open water is never a harbour: nothing here is dredged and nothing berths.
            region.IsDeepHarbour = false;
            region.HarbourDepthMeters = plan.NominalDepthMetres;

            region.TideMeanLevel = plan.TideMean;
            region.TideAmplitude = plan.TideAmplitude;
            region.TidePhaseHours = plan.TidePhaseHours;

            region.SpawnFishIds = plan.SpawnFishIds ?? new string[0];

            EditorUtility.SetDirty(region);
        }

        // =============================================================================================
        //  SCENE — the scaffold and the review camera
        // =============================================================================================

        /// <summary>
        /// Clear to an empty scene for this water region. Guarded by <see cref="RegionBuildGuard"/>
        /// (ADR 0019 §1) at the CALLER, which must abort before reaching here if the owner declines.
        /// </summary>
        public static Scene NewWaterScene() =>
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        /// <summary>
        /// The standalone-review camera, framed for the region's gating boat rather than for a walker —
        /// a water scene is never opened on foot. Mirrors the two land builders' locked pixel-perfect
        /// setup (ArtCameraSetup + the matching reference resolution) so every region reads at one scale.
        ///
        /// <para>SCOPE, and it is the land builders' too: the scene carries its own camera + listener so
        /// it can be opened and reviewed standalone. The real player arrives with the persistent core,
        /// which brings its own.</para>
        /// </summary>
        public static Camera AddReviewCamera(WaterScenePlan plan, Vector3 lookAt)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(plan.CameraWorldHeightMetres);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = plan.BackdropColor;
            camGo.transform.position = new Vector3(lookAt.x, lookAt.y, -10f);
            camGo.AddComponent<AudioListener>();
            ArtCameraSetup.ConfigurePixelPerfect(camGo);
            var ppc = camGo.GetComponent<PixelPerfectCamera>();
            if (ppc != null)
            {
                CameraFollow.ReferenceResolutionForWorldHeight(plan.CameraWorldHeightMetres,
                                                               out int refW, out int refH);
                ppc.refResolutionX = refW;
                ppc.refResolutionY = refH;
                EditorUtility.SetDirty(ppc);
            }
            return cam;
        }

        // =============================================================================================
        //  HEIGHT — the analytic bed, everywhere, with no paint over it
        // =============================================================================================

        /// <summary>
        /// The region's height source: a <see cref="RectTidalTerrain"/> carrying the plan's deep floor and
        /// its shoals. It registers itself into <c>GameServices.TidalTerrain</c> on enable, so the depth
        /// sounder, the boat's grounding gate and the water shader's bake all read the SAME number (P1).
        ///
        /// <para>⚠ Created BEFORE the sea plane, deliberately: on a region toggle-on the terrain's
        /// <c>OnEnable</c> must register before the <c>WaterSurface</c>'s bakes, and scene roots activate
        /// in creation order. The land builders both order it this way and both say so.</para>
        /// </summary>
        public static RectTidalTerrain AddTerrain(WaterScenePlan plan)
        {
            var go = new GameObject("TidalTerrain");
            var terrain = go.AddComponent<RectTidalTerrain>();
            plan.ConfigureTerrain(terrain);
            return terrain;
        }

        // =============================================================================================
        //  SEA — the plane, the layered shader, the weather palette
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>THE SEA PLANE IS ONE QUAD, NOT A GRID OF TILES.</b> Size <paramref name="sr"/>'s already-
        /// assigned sprite to cover <paramref name="worldSize"/> by SCALING it, and never by tiling it.
        ///
        /// <para><b>What this replaced, and why it had to go.</b> Every sea plane used to be a
        /// <c>SpriteDrawMode.Tiled</c> renderer sized to the region rect. <c>SeaTile.png</c> is 64 px at 32
        /// px/unit — a 2 × 2 m tile — so Nine Mile Creek's 760 × 560 m rect asked for 380 × 280 = 106,400
        /// tiles, and Unity's tiling generator refuses past a 16-bit index buffer and logged, on every
        /// build of the region:
        /// <code>
        ///   Cannot generate 9 slice most likely because the size is too big.
        ///   Requires 425600 vertices and 638400 indices
        /// </code>
        /// 106,400 × 4 verts = 425,600 and × 6 indices = 638,400 — the logged numbers exactly, which is
        /// what identifies the tiling as the thing counting. St Peters (760 × 520 → 395,200) and Greywick
        /// (the same rect through <see cref="AddSea"/>) were over the same limit; only the 80 × 50 m
        /// greybox cove was under it.</para>
        ///
        /// <para><b>The tile was never drawn, at any size.</b> <c>HiddenHarbours/Water</c> declares no
        /// <c>_MainTex</c> and <c>Water.mat</c> has no slot for one, so the sprite's TEXTURE is never
        /// sampled; the shader writes <c>OUT.uv = IN.uv</c> and no fragment stage ever reads it. Every
        /// layer is driven by <c>worldXY</c>, taken from <c>GetVertexPositionInputs(IN.positionOS).positionWS</c>
        /// — and a SpriteRenderer submits its mesh ALREADY IN WORLD SPACE, so a Simple sprite scaled to
        /// 760 × 560 hands the fragment stage the same four world corners a Tiled sprite sized 760 × 560
        /// would. Same <c>worldXY</c>, same pixels, 4 vertices instead of 425,600 attempted. The plane's
        /// real job is to BE that quad and to carry Water.mat, the sorting slot and the property block
        /// <c>WaterSurface</c> pushes — and, with the displaced sea on (the default since 2026-08-05),
        /// <c>DisplacedWaterSurface</c> disables this renderer outright and reads only those three things
        /// off it.</para>
        ///
        /// <para><b>⚠ The scale is DERIVED from the sprite, never assumed to be 1 m.</b> The hand-rolled
        /// no-material fallback in all four builders scaled <c>Square.png</c> by <c>(worldSize.x,
        /// worldSize.y, 1)</c> — but Square is 16 px at 32 px/unit, i.e. 0.5 m, so the greybox backdrop
        /// covered a QUARTER of the region's area. Deriving the factor from <c>sprite.bounds.size</c> (what
        /// the renderer actually draws at scale 1) is what makes one call correct for both sprites.</para>
        ///
        /// <para>A scaled sea transform is an anticipated shape, not a new one:
        /// <c>DisplacedWaterSurface</c> already inverse-scales its mesh root and overlay off
        /// <c>transform.lossyScale</c>, and the old fallback branch already scaled this way.</para>
        /// </summary>
        public static void ConfigureSeaPlane(SpriteRenderer sr, Vector2 worldSize)
        {
            // ⚠️ An explicit `== null`, never `??`: `??` bypasses UnityEngine.Object's equality overload.
            if (sr == null || sr.sprite == null) return;

            sr.drawMode = SpriteDrawMode.Simple;

            Vector3 atUnitScale = sr.sprite.bounds.size;   // metres this sprite draws before any scaling
            sr.transform.localScale = new Vector3(
                atUnitScale.x > 1e-4f ? worldSize.x / atUnitScale.x : 1f,
                atUnitScale.y > 1e-4f ? worldSize.y / atUnitScale.y : 1f,
                1f);
        }

        /// <summary>
        /// The sea plane over the whole region rectangle, carrying the layered SIM-driven water shader
        /// (ADR 0010) with its seabed height bake pointed at the plan's extent, plus the weather-driven
        /// palette (ADR 0017) wired exactly as both land builders wire it.
        ///
        /// <para>Null-tolerant on art: a checkout without Water.mat leaves a plain tiled backdrop and
        /// warns, rather than failing the build (the greybox rule).</para>
        /// </summary>
        public static GameObject AddSea(WaterScenePlan plan, GameConfig config, Sprite fallbackSquare)
        {
            var seaTile = LoadSpriteAny(ArtSea);
            var water = new GameObject("Sea");
            water.transform.position = new Vector3(plan.WorldCenter.x, plan.WorldCenter.y, 0f);
            var wsr = water.AddComponent<SpriteRenderer>();
            // Named off the partition rather than typed, so a re-base of the bands moves it (ADR 0032).
            wsr.sortingOrder = SortingBands.Sea;
            if (seaTile != null)
            {
                wsr.sprite = seaTile;
            }
            else
            {
                wsr.sprite = fallbackSquare;
                wsr.color = plan.BackdropColor;
            }
            // One quad over the region rect, whichever sprite is carrying it — see ConfigureSeaPlane for
            // why this is never tiled (Greywick's 760 × 520 rect was over the tiling limit too).
            ConfigureSeaPlane(wsr, plan.WorldSize);

            var waterMat = AssetDatabase.LoadAssetAtPath<Material>(ArtWaterMat);
            if (waterMat == null)
            {
                Debug.LogWarning($"[WaterSceneTemplate] Water.mat not found at {ArtWaterMat} — {plan.SceneName}'s " +
                                 "Sea is a plain backdrop. Re-run after the material imports for the tide-driven water.");
                return water;
            }

            wsr.sharedMaterial = waterMat;
            var surface = water.AddComponent<HiddenHarbours.Art.WaterSurface>();
            ConfigureWaterSurface(surface, plan.WorldCenter, plan.WorldSize,
                                  plan.HeightBakeResolution, plan.HeightMin, plan.HeightMax);
            // (ADR 0023 arc step 3) The owner's GameConfig salience knobs — one asset, every region.
            SetRef(surface, "_config", config);
            // (ADR 0017) base = null ON PURPOSE: the live Water.mat is the calm baseline. A null anchor no-ops.
            ConfigureWeatherPalette(
                surface,
                /*baseMood*/ null,
                AssetDatabase.LoadAssetAtPath<Material>(ArtWaterCalmMood),
                AssetDatabase.LoadAssetAtPath<Material>(ArtWaterStormMood),
                AssetDatabase.LoadAssetAtPath<Material>(ArtWaterFogMood));
            return water;
        }

        /// <summary>The world rectangle the seabed height map bakes over, the bake grid, and the
        /// elevation range the baked R channel maps across — which must BRACKET the whole field or the
        /// bake clips (the component's −4…6 default would flatten an offshore bed entirely). Persisted
        /// via SerializedObject, the builders' persist-the-refs convention.</summary>
        public static void ConfigureWaterSurface(HiddenHarbours.Art.WaterSurface surface,
                                                 Vector2 worldCenter, Vector2 worldSize, int resolution,
                                                 float heightMin, float heightMax)
        {
            var so = new SerializedObject(surface);
            SetV2(so, "_heightWorldCenter", worldCenter);
            SetV2(so, "_heightWorldSize", worldSize);
            SetInt(so, "_heightResolution", Mathf.Clamp(resolution, 16, 256));
            SetF(so, "_heightMin", heightMin);
            SetF(so, "_heightMax", heightMax);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Enable the weather-driven water palette (ADR 0017) and assign the anchor mood
        /// presets. A null base = the live Water.mat is the calm baseline; a null anchor no-ops safely.</summary>
        public static void ConfigureWeatherPalette(HiddenHarbours.Art.WaterSurface surface,
                                                   Material baseMood, Material calmMood,
                                                   Material stormMood, Material fogMood)
        {
            var so = new SerializedObject(surface);
            var enabled = so.FindProperty("_weatherPaletteEnabled");
            if (enabled != null) enabled.boolValue = true;
            SetObj(so, "_baseMoodMaterial", baseMood);
            SetObj(so, "_calmMoodMaterial", calmMood);
            SetObj(so, "_stormMoodMaterial", stormMood);
            SetObj(so, "_fogMoodMaterial", fogMood);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =============================================================================================
        //  THE LAND REGIONS' SEA — one wiring, called by both land builders
        // =============================================================================================

        /// <summary>
        /// Everything a LAND region's sea plane carries, applied to a Sea <paramref name="water"/> object the
        /// caller already built: the height-bake rect and elevation range, the owner's
        /// <see cref="GameConfig"/>, the weather palette anchors, and the <b>displaced surface</b>.
        ///
        /// <para><b>Why this is one call and not four.</b> The displaced sea has been the game's water since
        /// 2026-08-05 (<c>GameConfig.DisplacedWater.DefaultOn</c>), but the wiring that gives a region one
        /// existed in exactly ONE builder — so St Peters got the displaced sea and Nine Mile Creek got the
        /// pre-displacement flat face, and the owner sailed from one into the other. A second hand-rolled
        /// copy would have drifted the same way; this is the copy there is.</para>
        ///
        /// <para><b>The <see cref="GameConfig"/> is loaded HERE, from disk, rather than taken as a
        /// parameter.</b> Both land builders passed an instance they had loaded earlier in their run and both
        /// banked scenes carry <c>_config: {fileID: 0}</c> for the WaterSurface — the reference did not
        /// survive, which is the very hazard those builders' own "reload assets from disk before wiring
        /// refs" comments warn about (an intervening import invalidates an in-memory instance, and
        /// <c>SerializedProperty.objectReferenceValue</c> takes an invalid one as null). Loading it at the
        /// moment of writing removes the ordering question rather than restating it, and
        /// <see cref="SetRefChecked"/> makes a failure say so instead of leaving the owner's salience knobs
        /// silently unwired.</para>
        ///
        /// <para>Null-tolerant throughout: a checkout without <c>WaterOverlay.mat</c> warns and leaves the
        /// flat face, rather than failing the build (the greybox rule).</para>
        /// </summary>
        /// <param name="maxShoreGradient">How steeply THIS region's coast shelves — see
        /// <c>DisplacedWaterSurface.Configure</c>. Derive it from the region's authored profile.</param>
        public static void ConfigureLandRegionWater(GameObject water,
                                                    Vector2 worldCenter, Vector2 worldSize,
                                                    int bakeResolution, float heightMin, float heightMax,
                                                    float maxShoreGradient)
        {
            if (water == null) return;
            var surface = water.GetComponent<HiddenHarbours.Art.WaterSurface>();
            if (surface == null)
            {
                Debug.LogWarning("[WaterSceneTemplate] ConfigureLandRegionWater called on an object with no " +
                                 "WaterSurface — the caller must apply Water.mat and add the surface first.");
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigAsset);

            // ⭐ FIRST, and on the renderer the caller HANDS US rather than only at the call sites: the sea
            // plane is one quad over the region rect. Both land builders size their own plane through the
            // same call before getting here, so for them this is a re-assert — but a fixture or a future
            // builder that hand-rolls a Sea and reaches for the shared wiring gets the quad too, instead of
            // logging the 425,600-vertex tiling refusal that hand-rolled copies kept re-introducing. No-ops
            // on a MeshRenderer sea (the parity tests stand one up), which has no draw mode to correct.
            ConfigureSeaPlane(water.GetComponent<SpriteRenderer>(), worldSize);

            ConfigureWaterSurface(surface, worldCenter, worldSize, bakeResolution, heightMin, heightMax);
            // (ADR 0023 arc step 3) The owner's salience knobs — how loudly the big wave is marked.
            SetRefChecked(surface, "_config", config);
            // (ADR 0017) base = null ON PURPOSE: the live Water.mat is the calm baseline, never a frozen
            // preset copy, so the owner's Water.mat tuning always flows through the calm sea.
            ConfigureWeatherPalette(
                surface,
                /*baseMood*/ null,
                AssetDatabase.LoadAssetAtPath<Material>(ArtWaterCalmMood),
                AssetDatabase.LoadAssetAtPath<Material>(ArtWaterStormMood),
                AssetDatabase.LoadAssetAtPath<Material>(ArtWaterFogMood));

            AddDisplacedSurface(water, worldCenter, worldSize, config, maxShoreGradient);
        }

        /// <summary>
        /// (ADR 0023 phase 2) Hang the <c>DisplacedWaterSurface</c> beside the flat one on the same object,
        /// covering the SAME world rect as the sea plane and the height bake — the sea as a real
        /// vertex-displaced mesh, which is the look every region has drawn since the 2026-08-05 retirement
        /// of the flat face.
        ///
        /// <para>Idempotent: a re-run configures the surface that is already there rather than adding a
        /// second one (the component is <c>[DisallowMultipleComponent]</c>, so a blind Add would throw on a
        /// builder that ever stopped starting from an empty scene).</para>
        /// </summary>
        public static HiddenHarbours.Art.DisplacedWaterSurface AddDisplacedSurface(
            GameObject water, Vector2 worldCenter, Vector2 worldSize,
            GameConfig config, float maxShoreGradient)
        {
            if (water == null) return null;

            var overlay = AssetDatabase.LoadAssetAtPath<Material>(ArtWaterOverlayMat);
            if (overlay == null)
                Debug.LogWarning($"[WaterSceneTemplate] WaterOverlay.mat not found at {ArtWaterOverlayMat} — " +
                                 "the displaced sea will fall back to Shader.Find at runtime. Re-run after " +
                                 "the material imports.");

            // ⚠️ An explicit `== null`, never `??`: `??` bypasses UnityEngine.Object's equality overload, so
            // a component on an object being torn down would come back fake-null-but-not-C#-null and we
            // would configure a corpse instead of adding a live one.
            var displaced = water.GetComponent<HiddenHarbours.Art.DisplacedWaterSurface>();
            if (displaced == null) displaced = water.AddComponent<HiddenHarbours.Art.DisplacedWaterSurface>();
            displaced.Configure(worldCenter, worldSize, overlay, config, maxShoreGradient);
            EditorUtility.SetDirty(displaced);
            return displaced;
        }

        /// <summary>
        /// <see cref="SetRef"/> that READS THE REFERENCE BACK and says so when it did not take.
        ///
        /// <para>A silent no-op here is the worst kind: the builder reports success, the scene saves, and
        /// the field is simply null forever — which is exactly how both land regions' seas ended up with no
        /// <c>GameConfig</c> and nobody noticed. <c>SerializedProperty.objectReferenceValue</c> quietly
        /// stores null when handed a destroyed or unpersisted object, so "I assigned it" is not evidence
        /// that it is assigned.</para>
        /// </summary>
        public static void SetRefChecked(Component c, string field, Object value)
        {
            SetRef(c, field, value);
            if (value == null) return;   // a deliberate null is not a failure

            var read = new SerializedObject(c).FindProperty(field);
            if (read != null && read.objectReferenceValue == null)
                Debug.LogWarning($"[WaterSceneTemplate] '{field}' on {c.GetType().Name} did not take the " +
                                 $"reference it was handed ('{value.name}'). The instance is most likely " +
                                 "stale — re-load the asset from disk immediately before wiring it.", c);
        }

        // =============================================================================================
        //  TRAVEL — the loader, the passage bands, the anchor
        // =============================================================================================

        /// <summary>This region's scene-load path: the loader that performs the additive hop, told which
        /// regions it may reach and which scene it is standing in.</summary>
        public static RegionSceneLoader AddSceneLoader(string currentSceneName, params RegionDef[] regions)
        {
            var go = new GameObject("RegionSceneLoader");
            var loader = go.AddComponent<RegionSceneLoader>();
            SetRefArray(loader, "_regions", regions.Where(r => r != null).Cast<Object>().ToArray());
            SetString(loader, "_currentSceneName", currentSceneName);
            return loader;
        }

        /// <summary>
        /// One passage band: a trigger you sail across to leave this region, wired to a target region and
        /// this scene's loader, optionally naming WHICH WAY IN it lands at over there (#456).
        ///
        /// <para><b>A water region's door is its EDGE.</b> Nothing steers a boat in open water the way a
        /// bar or a channel steers one inshore, so a narrow band would be a door you can miss by sailing
        /// forty metres north of it — and a crossing that silently does not fire is the worst failure a
        /// seam has. The band is therefore as wide as the region is tall (see
        /// <see cref="WaterScenePlan.EdgeBandSize"/>); the re-fire guard on <c>RegionPassage</c> is what
        /// makes a generous band safe, so width costs nothing.</para>
        /// </summary>
        public static RegionPassage AddPassage(string name, Vector3 position, Vector2 bandSize,
                                               RegionDef target, RegionSceneLoader loader,
                                               string arrivalKey = "")
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = bandSize;
            var passage = go.AddComponent<RegionPassage>();
            passage.Configure(target, loader, arrivalKey);
            return passage;
        }

        /// <summary>
        /// The region's binding point for the persistent travel rig: where the boat parks on arrival, the
        /// region's other ways in, and the authored extent the camera's bounds clamp reads.
        ///
        /// <para><b>No dock and no disembark, and that is the point.</b> There is nothing to step ashore
        /// onto in open water. <c>ControlSwitcher.InDockZone()</c> is false against a null dock zone and
        /// <c>CanStepAshore()</c> then falls to <c>OnLand()</c>, which the region's own deep bed answers —
        /// so "you cannot get out of the boat at sea" is the terrain saying it, not a rule bolted on.</para>
        ///
        /// <para>Each named arrival gets its own child transform so the points survive in the saved scene
        /// and can be read back by name; the DisembarkPoint of each is left unset, which falls back
        /// independently to the region's own (#456) — a boat arrival has no landing.</para>
        /// </summary>
        public static RegionAnchor AddAnchor(WaterScenePlan plan, Vector3 defaultArrivalPos,
                                             params (string Key, Vector3 Position)[] arrivals)
        {
            var anchorGo = new GameObject(plan.SceneName + "RegionAnchor");
            // Configure BEFORE enable, the builders' convention (and what the #456 play test mirrors).
            anchorGo.SetActive(false);

            var defaultArrival = new GameObject(plan.SceneName + "Arrival");
            defaultArrival.transform.SetParent(anchorGo.transform, worldPositionStays: false);
            defaultArrival.transform.position = defaultArrivalPos;

            var anchor = anchorGo.AddComponent<RegionAnchor>();
            anchor.Configure(plan.RegionId, defaultArrival.transform,
                             dockZone: null, disembarkPoint: null);

            if (arrivals != null && arrivals.Length > 0)
            {
                var named = new NamedArrival[arrivals.Length];
                for (int i = 0; i < arrivals.Length; i++)
                {
                    var point = new GameObject("Arrival_" + arrivals[i].Key);
                    point.transform.SetParent(anchorGo.transform, worldPositionStays: false);
                    point.transform.position = arrivals[i].Position;
                    named[i] = new NamedArrival(arrivals[i].Key, point.transform, disembarkPoint: null);
                }
                anchor.ConfigureArrivals(named);
            }

            // The camera's bounds clamp reads this on arrival — the SAME authored pair the sea plane and
            // the height bake get, so nothing can sail off the region without the view knowing.
            anchor.ConfigureExtent(plan.WorldCenter, plan.WorldSize);
            anchorGo.SetActive(true);
            return anchor;
        }

        // =============================================================================================
        //  SAVE
        // =============================================================================================

        /// <summary>Write the scene and put it in Build Settings — a region whose scene is not registered
        /// cannot be travelled to, and <c>RegionSceneLoader.Travel</c> declines silently.</summary>
        public static void SaveAndRegister(Scene scene, string scenePath)
        {
            EditorSceneManager.SaveScene(scene, scenePath);
            RegisterScene(scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void RegisterScene(string path)
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(s => s.path == path)) return;
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // =============================================================================================
        //  shared helpers — the third copy of these is what this file exists to prevent
        // =============================================================================================

        public static T LoadOrCreate<T>(string path, System.Action<T> init = null) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            init?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        public static void SetRef(Component c, string field, Object value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
            { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning($"[WaterSceneTemplate] no object-reference field '{field}' on {c.GetType().Name}.");
        }

        public static void SetRefArray(Component c, string field, Object[] values)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[WaterSceneTemplate] no array field '{field}'."); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetString(Component c, string field, string value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p != null && p.propertyType == SerializedPropertyType.String)
            { p.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        static void SetF(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
            else Debug.LogWarning($"[WaterSceneTemplate] no float field '{field}'.");
        }

        static void SetInt(SerializedObject so, string field, int value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.intValue = value;
            else Debug.LogWarning($"[WaterSceneTemplate] no int field '{field}'.");
        }

        static void SetV2(SerializedObject so, string field, Vector2 value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.vector2Value = value;
            else Debug.LogWarning($"[WaterSceneTemplate] no Vector2 field '{field}'.");
        }

        static void SetObj(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference) p.objectReferenceValue = value;
            else Debug.LogWarning($"[WaterSceneTemplate] no object-reference field '{field}'.");
        }

        /// <summary>Imported art is sliced (spriteMode Multiple), so <c>LoadAssetAtPath&lt;Sprite&gt;</c>
        /// returns null — fall back to the first sub-sprite. Null if the art isn't imported.</summary>
        public static Sprite LoadSpriteAny(string path)
        {
            var direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (direct != null) return direct;
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                                .OrderBy(s => SpriteIndex(s.name)).FirstOrDefault();
        }

        static int SpriteIndex(string spriteName)
        {
            int u = spriteName.LastIndexOf('_');
            return (u >= 0 && int.TryParse(spriteName.Substring(u + 1), out int n)) ? n : 0;
        }

        /// <summary>The 16 px white square every builder falls back to when art is missing. Shared so the
        /// three water scenes cannot each mint their own copy at a different PPU.</summary>
        public static Sprite MakeSquareSprite(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            var tex = new Texture2D(16, 16);
            tex.SetPixels(Enumerable.Repeat(Color.white, 16 * 16).ToArray());
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spritePixelsPerUnit = 32f;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static void EnsureFolders()
        {
            foreach (var f in new[] { DataConfig, DataRegions, ArtSprites, Scenes })
            {
                if (AssetDatabase.IsValidFolder(f)) continue;
                var parent = System.IO.Path.GetDirectoryName(f).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(f));
            }
        }
    }
}
#endif
