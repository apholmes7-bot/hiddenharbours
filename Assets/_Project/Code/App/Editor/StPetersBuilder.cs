#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.Boats;               // BoatHullDef (the carried Dory/Punt hulls handed to the core)
using HiddenHarbours.Fishing;             // FishSpeciesDef + ClamDig/ClamHoleVisual + the trap-haul rig (Build 4)
using HiddenHarbours.Economy;             // BaitDef (the trap loop's bait content, Economy data)
using HiddenHarbours.Player;              // ClamBucket (the on-foot clam hold the dig fills)
using HiddenHarbours.App;                 // RegionAnchor — St Peters' travel bind point (the persistent rig rebinds here)
using HiddenHarbours.Art;                 // ChimneySmoke — the cosy hearth plume off the cottage chimney (ambient VFX)

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// One-click <b>St Peters Island</b> — the owner-ratified OPENING region as GREYBOX (no final art),
    /// built by its own builder so the cove (GreyboxBuilder) and Nine Mile Creek (NineMileCreekBuilder) scenes are
    /// untouched (scene per region, CLAUDE.md §3). Menu: Hidden Harbours ▸ Build St Peters Scene.
    /// Re-runnable (idempotent on the assets).
    ///
    /// <para><b>The opening (this builder authors the PLACES; gameplay-systems builds the ACTIONS).</b>
    /// You start on St Peters with a shovel + bucket → at LOW tide the sandbar/seabed bares → dig clams on
    /// the exposed flats → walk the emerged sandbar PATH to Nine Mile Creek (first trip, on foot) → at Nine Mile Creek
    /// sell clams + buy a cod licence + rod, save up, buy the DAMAGED dory + repair → sail home to the cove.
    /// This builder lays the island, the coast, the tide-gated sandbar (a walk path at low / boat channel at
    /// high), the clam-holes, and the START spawn. The dig/walk/gear actions are gameplay-systems'.</para>
    ///
    /// <para><b>The showcase is the TIDE.</b> A <see cref="TidalTerrain"/> authors the elevation zones so the
    /// sandbar is a ridge JUST BELOW high water (covered at high, exposing as the tide falls — widest flat at
    /// low) WITH a deeper CHANNEL cut through it (boat-crossable at higher tide, narrowing as it falls). The
    /// two are inverse over the tide. St Peters runs a BIG tide amplitude (~3.5 m) vs the cove's gentle one,
    /// so the bar visibly bares and floods (P1 at its purest, the kindest tide-gate — being cut off costs
    /// only time). The terrain registers itself into <see cref="GameServices.TidalTerrain"/> at runtime.</para>
    ///
    /// <para>St Peters is now the GAME'S START scene (#64 made it the entry point), so it stands up the
    /// shared PERSISTENT CORE via <see cref="PersistentCoreBuilder"/> — the same rig the cove builds: the
    /// services root (clock/environment/wallet + HUD), the controllable on-foot Player (spawned at
    /// <see cref="StartSpawnPos"/>), the moored hand-rowed Dory, the follow camera at the on-foot framing,
    /// the control switcher, and the travel rig (loader + coordinator) so the sandbar passage to Nine Mile Creek
    /// works. The extracted helper keeps the core IDENTICAL between the two start scenes (it can't diverge).
    /// This builder then authors only the REGION content around that core (island/coast/sandbar/clam-holes/
    /// passage/anchor). The dig/walk/gear ACTIONS remain gameplay-systems'.</para>
    ///
    /// <para>FLAG lead-architect (follow-ups, not this PR): (1) the cove (GreyboxBuilder) STILL authors its
    /// own copy of the core because it was the old start — now that St Peters is the start and the cove is a
    /// sail-to region, the cove must be DEMOTED to a plain region (drop its core authoring, give it a
    /// RegionAnchor like Nine Mile Creek) before the full sail-home chain is tested. (2) The long-term fix is the
    /// dedicated Bootstrap scene (VS-01) carrying the one core that additively loads the start region.</para>
    /// </summary>
    public static class StPetersBuilder
    {
        const string DataRegions = "Assets/_Project/Data/Regions";
        const string DataConfig  = "Assets/_Project/Data/Config";   // shared GameConfig (cove builder authors it)
        const string DataBoats   = "Assets/_Project/Data/Boats";    // the Dory + Punt hulls (the carried rig)
        const string DataFish    = "Assets/_Project/Data/Fish";     // the soft-shell clam (the flats' catch)
        const string DataTraps   = "Assets/_Project/Data/Traps";    // the lobster/crab pots (the trap-haul loop, Build 4)
        const string DataBait    = "Assets/_Project/Data/Bait";     // the trap bait (herring/fish-scrap/mackerel)
        const string DataNpcs    = "Assets/_Project/Data/NPCs";     // the opening cast (Aunt Ginny, Ned's letter)
        const string ArtSprites  = "Assets/_Project/Art/Sprites";
        // Opening-cast art (greybox; final St Peters storekeeper etc. are on the owner's draw-list).
        const string ArtGinny         = "Assets/_Project/Art/Characters/Ginny.png";   // Aunt Ginny standee
        const string ArtPortraitGinny = "Assets/_Project/Art/Portraits/Ginny.png";    // Ginny dialogue portrait
        const string ArtPortraitNed   = "Assets/_Project/Art/Portraits/Ned.png";      // Ned portrait on his letter
        const string ArtDialoguePanel = "Assets/_Project/Art/UI/DialoguePanel.png";   // dialogue panel art
        const string ArtNamePlate     = "Assets/_Project/Art/UI/NamePlate.png";       // nameplate art
        const string ArtSea      = "Assets/_Project/Art/Tilesets/Water/SeaTile.png";
        const string ArtWaterMat = "Assets/_Project/Art/Materials/Water.mat";   // the layered SIM-driven water shader (ADR 0010)
        const string ArtWaterOverlayMat = "Assets/_Project/Art/Materials/WaterOverlay.mat"; // the displaced surface's in-scene face (ADR 0023)
        const string ArtTerrainSplatMat = "Assets/_Project/Art/Materials/TerrainSplat.mat"; // the splat-shaded ground (ADR 0028)
        const string DataTerrain        = "Assets/_Project/Data/Terrain";
        // Weather-driven water palette anchor presets (ADR 0017) — the moods the deterministic weather blends
        // between on the Sea's WaterSurface (calm <-> storm by sea-state, pulled toward fog by low visibility).
        // The BASE / calm anchor is deliberately NOT a preset here: it is left UNWIRED so WaterSurface uses the
        // Sea's own LIVE Water.mat as the calm baseline (see ConfigureWeatherPalette below) — that way the
        // owner's hand-tuning of Water.mat always drives the calm sea, and weather-off / strength-0 is exactly
        // Water.mat (no pinned-preset-copy trap; ADR 0017).
        const string ArtWaterPresets   = "Assets/_Project/Art/Materials/WaterPresets";
        const string ArtWaterCalmMood  = ArtWaterPresets + "/Water_GlassyCalm.mat";    // CALM (low sea-state)
        const string ArtWaterStormMood = ArtWaterPresets + "/Water_StormGrey.mat";     // STORM (high sea-state)
        const string ArtWaterFogMood   = ArtWaterPresets + "/Water_FoggySmother.mat";  // FOG (low visibility)
        // (The single Grass.png / Sand.png fills are gone with the greybox ground patches — the island's
        // ground is painted from the shoreline-ISO v8 kit now; see StPetersShorePainter.)
        const string ArtCottage  = "Assets/_Project/Art/Sprites/Buildings/Cottage.png";
        const string ArtCottageNight = "Assets/_Project/Art/Sprites/Buildings/CottageNight.png";  // lit-window night swap
        const string ArtClamHole   = "Assets/_Project/Art/Sprites/ClamHole.png";    // the still dig-spot sprite
        const string ArtClamSquirt = "Assets/_Project/Art/Sprites/ClamSquirt.png";  // 4-frame "two squirts" tell
        const string Scenes      = "Assets/_Project/Scenes";
        const string SceneName   = "StPeters";
        const string ScenePath   = Scenes + "/" + SceneName + ".unity";

        // --- St Peters region tunables (authored data; mirrored onto the RegionDef + TidalTerrain + Env) ---
        // The opening's defining feature is a BIG tide vs the cove's gentle 1.6 m — the bar must visibly bare
        // and flood. Mean 0 (chart datum centred), amplitude 3.5 m → water swings ≈ -3.5 .. +3.5 m.
        public const float TideMean = 0f;
        public const float TideAmplitude = 3.5f;
        public const float TidePhaseHours = 1f;

        // --- REGION EXTENT (authored here, PUBLISHED on the RegionDef, read by everyone else) ---------
        // ⭐ The region's rectangle used to be the literal `new Vector2(160f, 120f)` written out in four
        // places in this builder and two more in TerrainPaintTool — so the sea plane, the shader's height
        // bake, the displaced mesh and the painted seabed each carried their own copy of the same
        // decision, and resizing the region meant finding all six (scene-sizing §6.2). They now all read
        // ONE authored number, mirrored onto RegionDef.WorldSizeMeters the same way the tide fields are.
        //
        // ⭐ 760 × 520 m — the RULED size (scene-sizing §5.1/§7.1, owner 2026-07-23), sized by
        // TIME-TO-CROSS rather than by feel: the island's ~450 × 260 m landmass is a ~2:30 walk end to
        // end and a ~1.1 km coastline. It replaces a 160 × 120 m greybox whose whole island was a 44 m
        // disc — smaller than the sandbar it is gated by.
        public static readonly Vector2 RegionWorldCenter = new Vector2(0f, 0f);
        public static readonly Vector2 RegionWorldSize   = new Vector2(760f, 520f);

        /// <summary>
        /// Painted-seabed and shader-height-bake resolution, in TEXELS PER METRE. The old code baked a
        /// SQUARE 192² over this non-square rect, which is 1.2 px/m across and 1.6 px/m up — different
        /// densities on the two axes, chosen by nobody. 2 px/m is the ruled inshore figure
        /// (scene-sizing §6.1): a 0.5 m texel, so the shader's wet edge follows a fine grid rather than
        /// reading as rectangular steps on the near-flat bar crest.
        /// </summary>
        public const float SeabedPixelsPerMetre = 2f;

        /// <summary>The height-bake grid the water shader uses — derived from the extent, never a
        /// literal. Square, because <c>WaterSurface</c> bakes a square texture.</summary>
        public static int WaterHeightBakeResolution =>
            Mathf.Clamp(
                Mathf.CeilToInt(Mathf.Max(RegionWorldSize.x, RegionWorldSize.y) * SeabedPixelsPerMetre),
                64, HiddenHarbours.World.RegionDef.MaxSeabedTexels);

        // --- TidalTerrain elevation zones (authored geometry; single source of truth shared with the
        // EditMode test). Island plateau is high (always exposed). The sandbar crest sits JUST BELOW high
        // water (covers at high, bares as the tide falls). The channel bed is between crest and deep floor
        // (a gut a boat crosses at higher tide). Deep harbour is the low floor everywhere else. ---
        public const float DeepHarbourElevation   = -4f;

        // ⭐ THE ISLAND SITS EAST OF CENTRE AND THE BAR LEAVES THE WEST END — this FLIPS the old greybox,
        // where the island sat at x = −40 and the bar ran east to +34 (scene-sizing §5.1). The dock is
        // then on the EAST end, the far side from the crossing, which is also right dramatically: you
        // walk out the west and you come home under power to the east (§5.1a, ruled).
        public static readonly Vector2 IslandCenter = new Vector2(70f, 0f);
        // ⚠ An ELLIPSE, not a disc: ~450 × 260 m of landmass is the RULED scale (§7.1), a ~1:5 linear
        // compression of the real island's 2.4 × 1.1 km. rx 225 + ry 130 gives exactly that, which is
        // why TidalTerrain gained a Y radius — a disc big enough to be 450 m long would be 450 m wide
        // and would not fit inside a 520 m scene.
        public const float IslandRadius            = 225f;   // semi-axis along X → 450 m end to end
        public const float IslandRadiusY           = 130f;   // semi-axis along Y → 260 m across
        // The beach band, in METRES of shore — not a fraction of the island. 30 m carries the plateau
        // down to the reef shelf below at about 1:4, which reads as the brief's steep red-sandstone coast.
        public const float IslandFalloff          = 30f;
        public const float IslandElevation         = 6f;     // dry at every tide

        // --- ⭐ THE REEF RING AND THE ONE DOCK (§5.1a, RULED by the owner 2026-07-23) ------------------
        // "Reefs make landing hard for all but shallow draft" is a GAMEPLAY RULE, not decoration — and it
        // costs no new systems, because draught is already real data on every hull and the painted seabed
        // already decides depth per tide. So the whole thing is authored terrain: a shallow apron rings
        // the island, and exactly ONE dredged slip cuts a door through it.
        //
        // The cut lands where the boat ladder does. Every skiff/punt-tier hull draws ≤ 0.6 m; the first
        // two WORKING hulls are 1.3–1.4 m; the dragger is 2.9 m. So the island you start on becomes the
        // island your big boat can never come home to — P2 and P5 in one piece of geography.
        public const float ReefShelfInnerElevation = -1.0f;   // against the beach — the shallow side
        public const float ReefShelfOuterElevation = -1.5f;   // where it drops away to the floor
        // ⚠ Width is NOT in the ruling — it is what the scene has room for, and the budget is exact.
        // The island sits 70 m east of centre, so from its centre to the east scene edge there is
        // 380 − 70 = 310 m; the island itself takes 225 and the beach 30, and the drop-off past the
        // shelf needs another 30, which leaves exactly 25 m of apron. A first attempt at 60 m ran the
        // ring 5 m off the east edge of the map AND left the berth channel stopping short of deep
        // water — a slip you could not actually enter. The DEPTH numbers are the ruled ones and are
        // untouched by this; the width only decides how far you cross at that depth.
        public const float ReefShelfWidth          = 25f;

        // The berth: §5.1a's "dock approach / berth bed ≈ −1.0 m", which clears the 0.6 m tier whenever
        // the water is above −0.4 m and DRIES near spring low — so the dock keeps its own gentle tide
        // gate, and even coming home under power means reading the tide.
        public const float BerthBedElevation       = -1.0f;
        // ⚠ Also not in the ruling: a slip wide enough for the skiff tier to enter without threading a
        // needle, narrow enough to read as one door in the ring rather than a gap in it.
        public const float BerthHalfWidth          = 8f;
        // ⚠ The seaward end must reach PAST the shelf's outer edge into the drop-off, or the slip is a
        // dead-end pocket inside the reef that nothing can enter — which is exactly what the first
        // version was, and what the ring test caught. The shelf ends 280 m from the island centre, so
        // the mouth sits at 285. The shoreward end is AT the land edge (IslandRadius), so the slip
        // reaches the beach; the carve's own falloff ramps it up onto the shore from there.
        public static readonly Vector2 BerthFrom    = new Vector2(355f, 0f);   // 285 m out — past the reef
        public static readonly Vector2 BerthTo      = new Vector2(295f, 0f);   // 225 m out — the shoreline
        // West end of the island's land → west toward the passage. (From is ON the island so the bar
        // actually joins it; To is short of the scene edge so the passage band has room.)
        public static readonly Vector2 SandbarFrom  = new Vector2(-150f, 0f); // toward the island
        public static readonly Vector2 SandbarTo    = new Vector2(-350f, 0f); // toward Nine Mile Creek
        public const float SandbarHalfWidth        = 30f;
        // ⚠ 1.4, NOT 1.6 — the crest must clear the NEAP amplitude, not just the spring one. At neap the
        // envelope drops the swing to NeapAmplitudeFraction (0.45) × 3.5 = 1.575 m, so a 1.6 m crest sits
        // ABOVE the highest water of a neap week and the bar simply never floods: the tide gate switches
        // itself off for part of every lunar month, silently, and the region's defining mechanic goes with
        // it. At 1.4 the gate exists at every point in the month AND gains a gradient — neap is FORGIVING
        // (long dry bar, brief flood), spring is TENSE. Ruled 2026-07-23 (#280); see
        // docs/design/scene-sizing-and-world-scale.md §5.2 and the neap-gap tests in StPetersTerrainTests.
        public const float SandbarCrestElevation    = 1.4f;   // < neap high water (1.575) → floods at EVERY tide
        public const float ChannelAlong            = 0.62f;
        public const float ChannelHalfWidth        = 15f;
        public const float ChannelBedElevation      = -0.6f;  // a gut: boat-crossable high, narrows as tide falls

        // --- CLAM-HOLE scatter (deterministic; single source of truth shared with the EditMode test) ------
        // Holes scatter over the bar's footprint on a jittered grid, kept only where the authored ground is
        // INTERTIDAL — between the lowest and highest water of the swing (mean ∓ amplitude), so a hole bares
        // as the tide falls and floods as it rises. ClamScatterStep = grid spacing; ClamScatterJitter = the
        // max deterministic offset (hashed off the cell, no RNG) that breaks the grid look. The band is the
        // tide swing inset by a small margin so a hole isn't perpetually at the very waterline edge.
        // ⚠ Scaled WITH the bar. At 6 m over the old 56 × 34 m footprint this was ~54 candidate cells;
        // left alone over the new 240 × 100 m footprint it would be ~670, i.e. ~670 GameObjects with
        // sprites and colliders in one scene (rule 7). 14 m keeps the field at a comparable ~130 and
        // still reads as scattered rather than gridded once the jitter is applied.
        public const float ClamScatterStep   = 14f;    // one candidate hole per ~14×14 m cell over the bar
        public const float ClamScatterJitter = 4.5f;   // ± world units of stable hash jitter per cell
        public const float ClamBandMargin     = 0.4f;   // inset (m) from the extreme water levels (kindness band)
        // The bar footprint to scatter over (a margin around the bar so the flats either side are covered).
        public const float ClamScatterMargin = 20f;

        // Player START spawn — on the island's WEST half, so the bar head is in sight and the opening's
        // first walk reads as leaving home. (Where the village actually stands is the authoring pass;
        // this is the same functional spawn the greybox had, moved onto the new landmass.)
        public static readonly Vector3 StartSpawnPos = new Vector3(-100f, 0f, 0f);
        // Where the walk path reaches Nine Mile Creek — a forgiving band at the WEST end of the bar,
        // past its far tip and short of the scene edge.
        public static readonly Vector3 ToNineMileCreekPassagePos = new Vector3(-356f, 0f, 0f);

        // --- THE VILLAGE (the authoring pass StartSpawnPos above was waiting for) ---------------------
        // ⭐ Every one of these used to sit within a few metres of (-40, 0) — the centre of the 44 m
        // greybox disc the island WAS before #328 moved it to (70, 0) with 450 x 260 m of landmass. The
        // rescale did not move them, so the cottage, Aunt Ginny, Ned's letter and the freezer ended up
        // huddled 60 m east of the player's own spawn in the middle of an empty island, and the WET-BUCKET
        // spot whose own comment reads "the sand rim, at the water" ended up ~100 m inland, on grass.
        //
        // The village now stands where the docs put it: on the island's WEST half beside the start spawn —
        // "Quiet, close, the whole world the size of a low-tide walk" (§6.0) — tight enough to read as one
        // small place, and within sight of the bar head the opening walks out across. Every position is on
        // the plateau (+6 m, dry at every tide), which StPetersVillageTests asserts against the authored
        // terrain rather than trusting these numbers.
        public static readonly Vector3 CottagePos    = new Vector3(-105f, 14f, 0f);     // Ginny's — the hearth
        public static readonly Vector3 GinnyPos      = new Vector3(-101f, 11f, 0f);     // out front, on the path
        public static readonly Vector3 NedsLetterPos = new Vector3(-107f, 10.5f, 0f);   // on the cottage step
        public static readonly Vector3 FreezerPos    = new Vector3(-102f, 12.5f, 0f);   // round the side
        // ⭐ The wet bucket belongs AT THE WATER, and on this island that means the HEAD OF THE FLATS — the
        // last dry ground before the sandbar's cobble spine runs out west. The ground here is +4.49 m, about
        // a metre clear of spring high water, so the barrel is never swimming; a few metres west and the bar
        // itself floods twice a day. It is exactly where you come off the flats with a bucket of clams.
        public static readonly Vector3 WetBucketPos  = new Vector3(-164f, 0f, 0f);

        // --- THE FIVE BUILDINGS (§5.1: "three clapboard houses, a one-room school, a general store") ----
        // ⭐ AUTHORED, NOT SCATTERED (ADR 0002: author identity, simulate variety). A village is the one
        // thing on this island that must not come out of a hash — its shape is its identity.
        //
        // They stand in an ARC around the hearth and the green, opening south-east, because that is the
        // only shape the site allows and it happens to be the shape a real one takes. Four things bind at
        // once and the arc is what is left: the cottage sits in the middle (nothing may crowd it), the
        // start spawn's own clearing is the green (§6.0's "you wake up IN the village"), the whole west
        // side is inside the sandbar's 40 m sightline — the bar is the region's ONE lesson and you must be
        // able to see it from the island — and each footprint needs its own diagonal of clearance because
        // the owner may re-bake with fewer facings and a quarter-turned building is deeper than a face-on
        // one. Solved against all four rather than eyeballed; StPetersVillageTests re-derives every margin
        // from the building contract's own footprints, so it fails with the number to use if a site moves.
        //
        // The lane runs east–west north of the cottage: SCHOOL at its west end (a schoolhouse stands a
        // little apart), the STORE at its centre where the path from the spawn meets it, the FARMHOUSE —
        // the biggest of them — closing the east end. Then the two smaller houses come round the green:
        // the SALTBOX on the east side and the SAGE COTTAGE on the south, the last house before the shore.
        public static readonly Vector3 SchoolPos         = new Vector3(-117f,  33f, 0f);
        public static readonly Vector3 GeneralStorePos   = new Vector3(-101f,  31f, 0f);
        public static readonly Vector3 WhiteFarmhousePos = new Vector3( -84f,  26f, 0f);
        public static readonly Vector3 RedSaltboxPos     = new Vector3( -80f,   8f, 0f);
        public static readonly Vector3 SageCottagePos    = new Vector3( -95f, -20f, 0f);

        // Where the village LOOKS. Every door is turned toward this point rather than given a hard-coded
        // facing index, so the village faces itself — and so a re-bake with four facings instead of eight
        // re-derives the nearest cell instead of silently pointing five doors at a wall (the kit's own
        // warning: nothing in placement may assume a facing count). It is the ground between the hearth
        // and the green: the midpoint of the cottage and the start spawn, which is where the life is.
        public static Vector2 VillageGreen =>
            (new Vector2(CottagePos.x, CottagePos.y) + new Vector2(StartSpawnPos.x, StartSpawnPos.y)) * 0.5f;

        // --- St Peters DOCK / mooring geometry (the persistent rig binds here; mirrors the cove pattern) ---
        // ⭐ THE DOCK IS ON THE EAST END, opposite the sandbar (§5.1a, ruled 2026-07-23): you walk out the
        // west and you come home under power to the east. The arrival point sits within DockZoneRadius of
        // the dock zone so the persistent ControlSwitcher's pure-distance disembark test registers the
        // moment you're home (the cove/Nine Mile Creek proven pattern — don't regress).
        //
        // ⚠ THE OLD MOORING WAS AGROUND, and only the rescale made it obvious. It sat at (−40, −26) with
        // the island at (−40, 0) r 22 falloff 10 — a comment saying "deep water" over ground the falloff
        // actually puts at +2.48 m, so a 0.30 m dory floated there only above +2.78 m, i.e. near high
        // water and nowhere else. These positions are now checked against the authored terrain by
        // StPetersTerrainTests rather than asserted in a comment.
        public const float DockZoneRadius = 3.5f;                                  // ControlSwitcher's default _zoneRadius
        public static readonly Vector3 DoryMooredPos  = new Vector3(330f, 0f, 0f); // east of the island, past the beach toe — genuinely afloat
        public static readonly Vector3 DockZonePos    = new Vector3(330f, 0f, 0f); // the slip head — dock here
        public static readonly Vector3 DisembarkPos   = new Vector3(328f, 0f, 0f); // step ashore up the slip (the cove's 1.5 m pattern)
        public static readonly Vector3 ArrivalPos     = new Vector3(332f, 0f, 0f); // sail home: park just off the slip, in range

        /// <summary>ADR 0028: the ground renders as the splat-shaded field and the painter skips the
        /// ground/fringe tile layers. Flip to false and rebuild for the tiled-coast A/B.</summary>
        public const bool UseSplatGround = true;

        [MenuItem("Hidden Harbours/Build St Peters Scene")]
        public static void Build()
        {
            // ADR 0019 §1 guard: this is a from-zero build (NewScene(EmptyScene) below) that WIPES anything the
            // owner has hand-authored in StPeters.unity — the exact failure that ate a boat spotlight elsewhere.
            // If the committed scene already exists on disk, make the owner confirm before we clear it; abort
            // (touch nothing) on cancel. First-ever build (no file) proceeds silently. Shared wording with every
            // region builder via RegionBuildGuard.
            if (!RegionBuildGuard.ConfirmOverwrite("St Peters Island", ScenePath))
                return;

            EnsureFolders();

            // --- DATA: the St Peters region + the regions it links to (by stable id) -------------------
            var stPeters = LoadOrCreate<RegionDef>(DataRegions + "/StPeters.asset", r =>
            {
                r.Id = "region.st_peters"; r.DisplayName = "St Peters Island"; r.SceneName = SceneName;
                r.IsDeepHarbour = false; r.HarbourDepthMeters = 2f;
                r.TideMeanLevel = TideMean; r.TideAmplitude = TideAmplitude; r.TidePhaseHours = TidePhaseHours;
                // ⭐ The region's rectangle, PUBLISHED as data so the sea plane, the painted seabed, the
                // paint tool and (later) the camera bounds all read one authored number instead of each
                // carrying a copy of `new Vector2(160f, 120f)` (scene-sizing §6.2).
                r.WorldCenter = RegionWorldCenter;
                r.WorldSizeMeters = RegionWorldSize;
                r.SeabedPixelsPerMetre = SeabedPixelsPerMetre;
                r.UnlockFlag = "";   // the opening — always reachable, no gate
                // The region's spawn table: the flats' hand-dug clam PLUS the trap-caught shellfish now that
                // the lobster/crab are region-tagged for St Peters (their FishSpeciesDef.RegionIds include
                // region.st_peters — the actual catch gate). This list is region metadata (validated to
                // resolve to real fish); the catch itself is filtered by the species' RegionIds.
                r.SpawnFishIds = new[] { "fish.soft_shell_clam", "fish.lobster", "fish.rock_crab" };
                r.Description = "The home island where the game begins: a tiny weathered coast cut off from " +
                                "the mainland except when the big tide bares the sandbar. Dig clams on the " +
                                "flats at low water, walk the bar to Nine Mile Creek, earn your way to the dory.";
            });

            // ⚠ RE-APPLY the extent and tide on EVERY run. LoadOrCreate's initialiser fires only when the
            // asset is CREATED, so an existing StPeters.asset never picked up a changed constant — which
            // is exactly how the committed def came to publish a 160 × 120 m extent while this builder
            // authored 760 × 520, with nothing to say so. These four are DERIVED design numbers (the
            // ruled scale and the tide profile), not owner-tuned fields, so refreshing them is the
            // non-destructive Refresh model (ADR 0019) doing its job rather than clobbering hand edits.
            // A test holds the def to the builder (StPetersLayoutTests).
            stPeters.WorldCenter = RegionWorldCenter;
            stPeters.WorldSizeMeters = RegionWorldSize;
            stPeters.SeabedPixelsPerMetre = SeabedPixelsPerMetre;
            stPeters.TideMeanLevel = TideMean;
            stPeters.TideAmplitude = TideAmplitude;
            stPeters.TidePhaseHours = TidePhaseHours;
            EditorUtility.SetDirty(stPeters);

            // Nine Mile Creek + cove regions referenced for the walk passage / loader (created here if absent;
            // GreyboxBuilder/NineMileCreekBuilder author the canonical versions under the same stable ids).
            var nineMileCreek = LoadOrCreate<RegionDef>(DataRegions + "/NineMileCreek.asset", r =>
            {
                r.Id = "region.nine_mile_creek"; r.DisplayName = "Nine Mile Creek"; r.SceneName = "NineMileCreek";
                r.IsDeepHarbour = true; r.HarbourDepthMeters = 6f;
                r.TideMeanLevel = 0f; r.TideAmplitude = 0.8f; r.TidePhaseHours = 2f;
                r.Description = "The market town: a deep, sheltered harbour where the coast's business gets done.";
            });

            // The persistent-core data refs (shared stable assets; the cove builder authors the canonical
            // versions — created here if absent so this builder is self-sufficient/re-runnable). LoadOrCreate
            // reloads the PERSISTED asset so they serialize into the scene rather than saving as "None".
            var config = LoadOrCreate<GameConfig>(DataConfig + "/GameConfig.asset");
            var dory   = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Dory.asset");
            var punt   = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Punt.asset");
            var clam   = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/SoftShellClam.asset");
            // The rod-catchable pool (dock/shore fishing — the owner's "fish from the St Peters shore"):
            // the persistent FishingController's species list rides the CORE across region hops, so it
            // carries every rod species; the per-region gate is each species' own RegionIds, filtered by
            // the travel-aware region at cast time (CatchResolver.Matches — data, not code).
            // (Named *Fish — the bait locals below already own the plain names, e.g. the mackerel BAIT.)
            var codFish      = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/AtlanticCod.asset");
            var haddockFish  = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/Haddock.asset");
            var mackerelFish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/Mackerel.asset");
            var pollockFish  = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/Pollock.asset");

            // --- SCENE ----------------------------------------------------------------------------------
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- SEA backdrop (the deep water surrounding the island + bar) -----------------------------
            var waterSprite = MakeSquareSprite(ArtSprites + "/Square.png");
            var seaTile = LoadSpriteAny(ArtSea);
            var water = new GameObject("Sea");
            var wsr = water.AddComponent<SpriteRenderer>();
            // The Sea plane carries the layered WaterSurface shader (below), whose clip(depth) makes the
            // plane TRANSPARENT over dry ground (depth<=0) and renders OPAQUE water over covered ground
            // (depth>0) — the smooth wet/dry reveal. For "the ground shows through when dry, water covers it
            // when wet" (design/water-rendering.md §5.1: depth<=0 → hands off to terrain) the water plane must
            // sit ABOVE the authored tidal-ground sprites it reveals/covers: the Sandbar (-9), the
            // BoatChannelMarker (-8), the island beach/grass (-8/-7) and the slip (-6). At -5 it sits just
            // above that ground stack and still below the clam holes (clamped -4..4) and the on-foot
            // characters (the player Y-sorts within +2..+40, #110) — so holes and the player draw on the
            // bared flat IN FRONT of the water. (Before this change a 2 m TidalFlatVisual colour grid sat at
            // -5 and masked BOTH the sand and the shader; retiring that grid is what lets the smooth shader
            // show, and the Sea takes the vacated -5 slot. The always-dry island stays visible because the
            // shader clips over it: its 6 m elevation is above every water level, so depth<0 → transparent.)
            wsr.sortingOrder = -5;
            if (seaTile != null)
            {
                wsr.sprite = seaTile;
                wsr.drawMode = SpriteDrawMode.Tiled;
                wsr.size = RegionWorldSize;
                water.transform.localScale = Vector3.one;
            }
            else
            {
                wsr.sprite = waterSprite; wsr.color = new Color(0.15f, 0.27f, 0.34f);
                water.transform.localScale = new Vector3(RegionWorldSize.x, RegionWorldSize.y, 1f);
            }

            // --- LAYERED SIM-DRIVEN WATER SHADER (ADR 0010 / design/water-rendering.md) ------------------
            // FLAG lead-architect (this PR, the FREE touch): apply the HiddenHarbours/Water material to the
            // Sea plane and hang the runtime WaterSurface on it so the layered shader (depth gradient / surface
            // distortion / foam fringe / specular / caustics) MOVES off the live deterministic sim. WaterSurface
            // feeds the EnvironmentSample + WaterLevelAt into the shader each throttled tick (current→flow,
            // wind→roughness, level→shoreline, sea→chop) and BAKES the TidalTerrain seabed into a height texture,
            // so the visible waterline/depth match the SAME number the walkability/boat-cross gate reads. The
            // material's colours/speeds/thresholds are all Inspector tunables (no magic numbers) — the owner
            // art-directs the LOOK on the Water.mat after re-running this builder and viewing it in Play.
            // Visual-only: drives no sim, saves nothing (CLAUDE.md rule 5). If the material isn't present (first
            // checkout before import) the Sea stays the plain tiled/flat backdrop — the wiring no-ops safely.
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>(ArtWaterMat);
            if (waterMat != null)
            {
                wsr.sharedMaterial = waterMat;
                var surface = water.AddComponent<HiddenHarbours.Art.WaterSurface>();
                // Bake the seabed height map at 192² (ADR 0012 §A step 1): over the 160×120 m plane that's a
                // ~0.83 m texel (half the old 96²/~1.67 m), so the shader's depth/foam shoreline follows a
                // FINE grid — the wet edge stops reading as ~1.5 m rectangular steps on the near-flat bar
                // crest. The bake is a one-time R8 texture on enable (trivial CPU/VRAM, rule 7). 256 is
                // available if the crest still facets, but 192 is the ADR's recommended start.
                ConfigureWaterSurface(surface, RegionWorldCenter, RegionWorldSize,
                                      WaterHeightBakeResolution);
                // (ADR 0023 arc step 3) The owner's GameConfig: WaterSurface pushes its DisplacedWater
                // salience knobs (cap salience / envelope threshold / band strength) each tick — the
                // one asset where the owner tunes how loudly the big wave is marked, no code (rule 6).
                SetRef(surface, "_config", config);

                // (ADR 0017) WEATHER-DRIVEN PALETTE: enable the weather mood on the Sea + assign the storm/fog/
                // calm anchor preset moods, so this scene's sea EASES through the preset library as the
                // deterministic weather shifts (calm <-> storm by sea-state, pulled toward fog by low visibility —
                // P1 "the sea has moods"). The blend rides the per-renderer MPB alongside the physics props
                // (disjoint key sets, no double-drive) and NEVER mutates Water.mat (rule 5). Opt-in elsewhere —
                // only this builder turns it on. If a preset is missing (first checkout before import) the anchor
                // is left null and WaterSurface falls back to the base/own material — the wiring no-ops safely.
                //
                // BASE anchor = null (UNWIRED) ON PURPOSE: WaterSurface then uses the Sea's own LIVE Water.mat as
                // the calm baseline the storm/fog moods blend RELATIVE TO. So weather-off / strength-0 reads as
                // exactly Water.mat, and the owner's hand-tuning of Water.mat always flows through the calm sea —
                // not a frozen preset COPY (the latent trap the ADR 0017 review caught). Pinning a base preset
                // here would pin the calm look to that copy.
                ConfigureWeatherPalette(
                    surface,
                    /*baseMood (null = use the live Water.mat as the calm baseline)*/ null,
                    AssetDatabase.LoadAssetAtPath<Material>(ArtWaterCalmMood),
                    AssetDatabase.LoadAssetAtPath<Material>(ArtWaterStormMood),
                    AssetDatabase.LoadAssetAtPath<Material>(ArtWaterFogMood));

                // (ADR 0023 phase 2) THE DISPLACED SURFACE A/B: hang the DisplacedWaterSurface
                // beside WaterSurface, covering the SAME 160×120 m rect. Defaults OFF — the flat
                // water renders exactly as today until the owner presses the dev key (O) in Play,
                // which flips the sea to the vertically displaced mesh (same sim, same material,
                // same waterline — the readability verdict instrument). Exaggeration + the shore-band
                // coefficient are OWNER DATA (arc step 3): the wired GameConfig's DisplacedWater
                // block is the live source, re-read every tick; grid density + the per-coast shore
                // gradient stay on the component (scene data, not world policy).
                var displaced = water.AddComponent<HiddenHarbours.Art.DisplacedWaterSurface>();
                displaced.Configure(RegionWorldCenter, RegionWorldSize,
                    AssetDatabase.LoadAssetAtPath<Material>(ArtWaterOverlayMat), config);
            }
            else
            {
                Debug.LogWarning("[StPetersBuilder] Water.mat not found at " + ArtWaterMat + " — Sea left as the " +
                                 "plain backdrop. Re-run after the material imports to get the layered water shader.");
            }

            // Reload data assets from disk before wiring refs (an intervening import can invalidate the
            // in-memory instances — the gotcha the cove/Nine Mile Creek builders guard against).
            stPeters = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/StPeters.asset");
            nineMileCreek = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/NineMileCreek.asset");
            config   = AssetDatabase.LoadAssetAtPath<GameConfig>(DataConfig + "/GameConfig.asset");
            dory     = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Dory.asset");
            punt     = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Punt.asset");
            clam     = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/SoftShellClam.asset");
            codFish      = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/AtlanticCod.asset");
            haddockFish  = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/Haddock.asset");
            mackerelFish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/Mackerel.asset");
            pollockFish  = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/Pollock.asset");

            // THE PILOTABLE FLEET (the owner's ask): every boat he can put himself in from the helm, in
            // cycle order — the iso dory he starts in, the 8-direction fishing boat, the punt on each of her
            // two engines, and the two 7 m skiffs with the twin-outboard sport as its own entry. Roughly the
            // speed ladder, so walking F walks UP it. St Peters is the only builder that spawns the player's
            // boat, so it is the only one that hands over a roster (Nine Mile Creek spawns none).
            //
            // Nulls are FILTERED, not tolerated: a missing asset would otherwise be a dead rung he cycles
            // into. The picker is NOT the fleet: being on it sells nothing.
            //
            // The punts are the one nuance. boat.punt is NOT a dev-only hull — she is a REAL purchasable M1
            // boat (PuntOffer, ₲1800) and lives in OwnedFleet's registry; she rides the picker so her new iso
            // skin can be felt against the others, not because the picker owns her. Her upgraded engine
            // (boat.punt_upgraded) is a picker rung ONLY: deliberately no ShipwrightOffer, because a
            // purchasable engine upgrade is real economy work nobody has asked for (rule 8).
            //
            // THE CAPE ISLANDER TAILS THE CYCLE, and she is deliberately OUT of speed order: at 4.20 m/s
            // she is slower than both sport skiffs, but she is ~13 m and 6000 kg against their 7 m and
            // ~1000, so she is a SIZE step rather than a speed step and the cycle ends on the biggest boat
            // rather than burying her mid-list. Picker rung only, on the upgraded punt's precedent — a
            // purchasable 13 m workboat is the M2 fleet roster and its economy (rule 8).
            //
            // THE LOBSTER BOAT TAILS THE CYCLE, one rung past the Cape Islander — position 9, the last press
            // of F before it wraps back to the dory. She belongs there on the same SIZE-step logic the Cape
            // was placed on rather than on speed: she measures a shade quicker than the Cape but is heavier
            // (6800 kg vs 6000) and reads bigger in the hand, so the cycle still ends on the biggest boat.
            // She is also the smoothest TURNER on the roster at 32 facings against everyone else's 8, which
            // is worth feeling immediately after the Cape rather than several rungs away from her.
            // Picker rung only, on the Cape Islander's and the upgraded punt's precedent (rule 8).
            //
            // THE SIDE DRAGGER NOW TAILS THE CYCLE — position 10, the last press of F before it wraps
            // back to the dory (ADR 0022 phase 5). She takes the tail on exactly the SIZE-step logic
            // the Cape and the lobster were placed on, and she is not a marginal step: 25 m and
            // 90 tonnes against the lobster's 12 m and 6.8, the first offshore hull in the game. She
            // is also the second REAL-TIME MESH hull and the one that motivated ADR 0022 at all —
            // her sheet set would have been 433.1 MiB; her mesh is 143.9 KB. Feeling her immediately
            // after the lobster is the comparison worth having, since the lobster is the only other
            // hull drawn the same way.
            //
            // ⚠️ She is MESH-ONLY: no sheet, so no sprite half and no V-key A/B (the toggle correctly
            // reports "this hull has only one look"). She also has no fallback Sprite — if the mesh
            // path is ever unavailable at runtime she draws nothing rather than draws wrongly.
            //
            // THE UPPER FLEET TAILS HER — positions 11–14 (ADR 0022 phase 6, owner's call 2026-07-23:
            // "import + sailable dev rungs"). Four hulls that had never been in Unity at all, because
            // a sheet set was never possible for them: the coastal packet's would have been 1.8 GiB
            // and the tanker could not be baked at the fleet's own 32 px/m without a 3,500 px cell.
            // They continue the same SIZE ladder the Cape, the lobster and the dragger were placed on
            // — 38 m, 38 m, 60 m, 110 m — so walking F walks UP it and ends on the biggest thing that
            // floats. The Mk2 sits immediately after the Mk1 deliberately: same 38 m envelope, more
            // deck gear, and they are only worth telling apart back to back.
            //
            // ⚠️ THEIR NUMBERS ARE PLACEHOLDERS AND SAY SO IN THE ASSETS. Mass, thrust, drag and hold
            // are scaled off the side dragger by hull-form law (displacement ~L³, drag ~L², authority
            // falling with size) so they sail plausibly rather than correctly. They are picker rungs
            // ONLY — no ShipwrightOffer, no economy, not purchasable — on exactly the precedent the
            // Cape Islander and the upgraded punt set, because a purchasable 110 m gas carrier is the
            // M2/M3 fleet roster and its economy (rule 8). Tuning them is M2's job and re-running any
            // art bake can never touch them: they live in their own committed assets (rule 2).
            var pickerRoster = new[]
            {
                dory,
                // ...and the SAME dory with the used kicker on her transom (D8), next to her rowed
                // self on purpose: it is the M1 slice's closing rung, and the only way to feel what
                // the outboard actually buys is to press F once and still be in the same boat.
                AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/DoryOutboard.asset"),
                AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/FishingSkiff.asset"),
                punt,
                AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/PuntUpgraded.asset"),
                AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/ConsoleSkiff.asset"),
                AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/SportSkiff.asset"),
                AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/SportSkiffTwin.asset"),
                AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/CapeIslander.asset"),
                AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/LobsterBoat.asset"),
                AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/SideDragger.asset"),
                AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/SternTrawler.asset"),
                AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/SternTrawlerMk2.asset"),
                AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/CoastalPacket.asset"),
                AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Tanker.asset"),
            }.Where(h => h != null).ToArray();

            const int ExpectedPickerRungs = 15;
            if (pickerRoster.Length < ExpectedPickerRungs)
                Debug.LogWarning($"[StPetersBuilder] The dev boat picker got {pickerRoster.Length}/" +
                                 $"{ExpectedPickerRungs} hulls — " +
                                 "some Data/Boats assets are missing, so those boats won't be in the cycle. " +
                                 "Run Hidden Harbours ▸ Art ▸ Build Boat Visual Defs and the cove builder " +
                                 "(which authors the hull assets), then re-run this builder.");

            // The opening cast as DATA (CLAUDE.md rule 2): Aunt Ginny (teaches the buy-and-repair loop) and
            // Ned's letter (the remembered presence — no inherited dory). Each NpcDef carries its name, its
            // DialogueDef, the verb, and the completion flag, so the builder only PLACES them — the words
            // live in assets the owner can edit (Data/NPCs + Data/NPCs/Dialogue), never hard-coded here.
            var ginnyNpc = AssetDatabase.LoadAssetAtPath<NpcDef>(DataNpcs + "/AuntGinny.asset");
            var nedNpc   = AssetDatabase.LoadAssetAtPath<NpcDef>(DataNpcs + "/NedLetter.asset");

            // --- PERSISTENT CORE (THE FIX) --------------------------------------------------------------
            // St Peters is the START scene, so it stands up the SAME persistent rig the cove builds — a
            // controllable on-foot Player at the START spawn, the moored hand-rowed Dory, the follow camera
            // at the on-foot framing, the services (clock/environment/wallet → the tide actually advances),
            // the control switcher, and the travel rig. Extracted to PersistentCoreBuilder so the two start
            // scenes can't diverge. The walk is TIDE-GATED here (St Peters' falling-tide wading edge, P1).
            var core = PersistentCoreBuilder.Build(new PersistentCoreBuilder.Params
            {
                Config           = config,
                StartDory        = dory,
                DoryOutboardHull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/DoryOutboard.asset"),
                PuntHull         = punt,
                DevPickerRoster  = pickerRoster,   // F at the helm cycles the hull in place (dev affordance)
                // The clam (the flats' dig) + the rod-catchable trio: the persistent controller carries
                // ALL rod species; each cast filters by the species' RegionIds against the travel-aware
                // current region, so the same pool serves St Peters' shore, the cove and Nine Mile Creek.
                RegionFish       = new[] { clam, codFish, haddockFish, mackerelFish, pollockFish }
                                       .Where(f => f != null).ToArray(),
                Square           = waterSprite,
                CameraBackground = new Color(0.07f, 0.14f, 0.18f),   // St Peters' cool dawn water
                PlayerStartPos   = StartSpawnPos,
                BoatMooredPos    = DoryMooredPos,
                TideGatedWalk    = true,
                CurrentSceneName = SceneName,
                TideMean         = TideMean,        // St Peters' BIG tide (±3.5 m) so the bar bares + floods
                TideAmplitude    = TideAmplitude,
                TidePhaseHours   = TidePhaseHours,
            });

            // --- ISLAND GROUND: PAINTED now, by StPetersShorePainter further down -----------------------
            // ⭐ RETIRED HERE: two tiled greybox patches — a 26 m sand rim under a 20 m grass disc, both at
            // (-40, 0) — used to stand in for the island's ground. They were authored for the 160 × 120 m
            // greybox, whose whole island WAS a 44 m disc at that centre; the rescale to 760 × 520 (#328)
            // moved the island to (70, 0) with 450 × 260 m of landmass and did not move them. So the island
            // had a 26 m patch of ground adrift inside it and NO ground at all over the other ~91,000 m²:
            // the water shader clips itself transparent over dry land, so walking inland was walking on the
            // camera's background colour. The shoreline-ISO v8 kit now paints the whole landmass and its
            // intertidal — grass / marram / sand / shingle / ripple / shelf, chosen by elevation crossed with
            // which way the coast faces (scene-sizing §5.1) — so the patches have nothing left to stand in
            // for. The paint runs after the TidalTerrain is authored below, because the terrain IS the map.

            // The cottage / the hard where the uncle's dory waits (greybox marker — the actual damaged-dory
            // OFFER lives at the Nine Mile Creek Shipwright this round; the slip here is set dressing for the opening).
            var cottageGo = new GameObject("IslandCottage");
            cottageGo.transform.position = CottagePos;
            var cottageSr = cottageGo.AddComponent<SpriteRenderer>();
            cottageSr.sortingOrder = 2;
            var cottageSprite = LoadSpriteAny(ArtCottage);
            if (cottageSprite != null) { cottageSr.sprite = cottageSprite; cottageGo.transform.localScale = Vector3.one; }
            else { cottageSr.sprite = waterSprite; cottageSr.color = new Color(0.70f, 0.50f, 0.40f); cottageGo.transform.localScale = new Vector3(6f, 6f, 1f); }

            // --- THE COLD CHAIN, ashore (M1 §7.3 — gameplay-systems) --------------------------------
            // Ginny's FREEZER by the cottage (Frozen: time ashore, free but only at home) and the
            // WET-BUCKET spot at the water's edge (Live: free, shellfish only). Both are proximity
            // interactables on the stall pattern (StallReach resolves the player itself — no wiring);
            // greybox colour markers until art lands. Ice for the boat rides the DeckIceBox on the
            // hold (runtime-spawned) — nothing to place here.
            var freezerGo = new GameObject("GinnyFreezer");
            freezerGo.transform.position = FreezerPos;
            var freezerSr = freezerGo.AddComponent<SpriteRenderer>();
            freezerSr.sprite = waterSprite;
            freezerSr.color = new Color(0.85f, 0.92f, 0.95f);   // chest-freezer white, reads icy
            freezerSr.sortingOrder = 3;
            freezerGo.transform.localScale = new Vector3(1.2f, 1.0f, 1f);
            freezerGo.AddComponent<GinnyFreezer>();

            var wetGo = new GameObject("WetBucketSpot");
            wetGo.transform.position = WetBucketPos;   // the head of the flats — the last dry ground
            var wetSr = wetGo.AddComponent<SpriteRenderer>();
            wetSr.sprite = waterSprite;
            wetSr.color = new Color(0.25f, 0.45f, 0.60f);   // a seawater barrel, reads wet
            wetSr.sortingOrder = 3;
            wetGo.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            wetGo.AddComponent<WetBucketPoint>();

            // NIGHT WINDOWS — the cottage's lit-window SPRITE SWAP (CottageDayNight): swap to the lit-window
            // night sprite after dusk (through the Core clock only). Complements the window GLOW below: the swap
            // shows lit panes, the glow makes the cottage actually CAST warm light onto the ground. Both are
            // automatic + self-driven; if the night sprite isn't imported yet the swap simply no-ops (the glow
            // still reads as light spilling from the windows).
            var cottageNightSprite = LoadSpriteAny(ArtCottageNight);
            if (cottageSprite != null && cottageNightSprite != null)
            {
                var dayNight = cottageGo.AddComponent<CottageDayNight>();
                SetRef(dayNight, "_renderer", cottageSr);
                SetRef(dayNight, "_daySprite", cottageSprite);
                SetRef(dayNight, "_nightSprite", cottageNightSprite);
            }

            // PRECONFIGURED WINDOW GLOW — the cottage carries its OWN night light (the owner's lighting principle,
            // ADR 0016): a soft warm RADIAL pool that self-installs + NIGHT-GATES automatically (on at dusk, off
            // by day, reading the published _DayNightTint in-shader — no owner wiring, no clock read here). So the
            // cottage doesn't just SWAP to lit panes, it CASTS warm light spilling from the windows. Sits a touch
            // below the sprite centre (the preset's origin offset pools the light at the sill/ground). Same
            // attach-and-forget pattern any future lit object reuses.
            var cottageGlowGo = new GameObject("WindowGlow");
            cottageGlowGo.transform.SetParent(cottageGo.transform, worldPositionStays: false);
            cottageGlowGo.AddComponent<PreconfiguredLight>();   // defaults to LightPresets.Kind.WindowGlow

            // Cosy HEARTH SMOKE off the cottage chimney — the one positioned living-coast ambient effect (P3).
            // A thin pooled plume that rises and bends DOWNWIND on the SAME shared wind the grass/water/mist
            // read; it dims with the day/night cycle (the hearth burns on at night). Sits at the roofline,
            // slightly to one side where a flue would be (offset is in WORLD metres; the cottage isn't scaled
            // when the real sprite loads, so a fixed offset reads at the roof either way). The mist/gulls/motes
            // self-install; this is the only one the builder places. (ChimneySmoke is visual-only, rule 5.)
            var chimneyGo = new GameObject("ChimneySmoke");
            chimneyGo.transform.SetParent(cottageGo.transform, worldPositionStays: false);
            chimneyGo.transform.localPosition = new Vector3(0.6f, 1.6f, 0f);   // roofline, flue side
            chimneyGo.AddComponent<ChimneySmoke>();

            // --- SANDBAR + CHANNEL: PAINTED now too -----------------------------------------------------
            // ⭐ RETIRED HERE: a flat sand strip down the bar's centre-line and a teal rectangle marking
            // where the channel cuts it. Both were greybox stand-ins from before the coast had real ground,
            // and both now LIE about the terrain they sit on: the strip drew the bar as a uniform rectangle
            // when the authored ridge falls away smoothly to the deep floor at its half-width, and the teal
            // patch drew the channel as a hard-edged box over a smoothly carved gut. The painter reads the
            // same TidalTerrain the walk gate reads, so the bar now comes out as the docs describe it — "a
            // cobble-and-sand bar" (§6.0), a shingle spine you can see to walk with sand and then rippled
            // flats either side where the clams are — and the channel reads as the ripple gut it is,
            // narrowing as the tide falls, with no rectangle anywhere.

            // --- TIDAL TERRAIN (THE SHOWCASE — authored elevation zones) --------------------------------
            // The world's height map for the region: island high (always exposed), sandbar crest just below
            // high water (bares as the tide falls), a deeper channel cut through (boat-crossable high), deep
            // harbour elsewhere. Registers itself into GameServices.TidalTerrain on enable so gameplay's
            // on-foot walkability + the future water shader read it through Core. Values mirror the public
            // constants above (single source of truth shared with the EditMode zone test).
            var terrainGo = new GameObject("TidalTerrain");
            var terrain = terrainGo.AddComponent<TidalTerrain>();
            ConfigureTidalTerrain(terrain);

            // --- SPLAT GROUND (ADR 0028) ----------------------------------------------------------------
            // The ground as a FIELD, not a grid: one full-region quad carrying the TerrainSplat shader,
            // fed the painted height map, replaces the ground/fringe tile layers (the flag below makes the
            // painter skip stamping them — flip it and rebuild for the tiled A/B). Every design number is
            // PUSHED from its owner — the band ladder + meander from StPetersShoreMap (the CPU classifier
            // stays the single source of truth), the bar/island geometry from this builder's constants —
            // so the shader re-declares nothing (rule 6). Sorts at -21: below the retained contact/rock
            // layers (-18) and far below the Sea plane (-5), so the ADR 0012 tide reveal is untouched.
            if (UseSplatGround)
            {
                var splatGo = new GameObject("TerrainSplat");
                var splat = splatGo.AddComponent<HiddenHarbours.Art.TerrainSplatSurface>();
                splat.Configure(RegionWorldCenter, RegionWorldSize,
                    AssetDatabase.LoadAssetAtPath<Material>(ArtTerrainSplatMat),
                    HiddenHarbours.Art.TerrainSplatSurface.DefaultSortingOrder);

                // The owner's painted seabed if it exists (the paint tool's asset), else a fresh bake of
                // the analytic terrain — NEVER overwrite an existing paint (ADR 0019: refresh, don't clobber).
                var heightMap = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(
                    DataTerrain + "/" + TerrainPaintTool.StPetersSeabedName + ".asset");
                if (heightMap == null)
                    heightMap = TerrainPaintTool.BakeStPetersSeabed(
                        RegionWorldCenter, RegionWorldSize, stPeters.SeabedTexels);
                if (heightMap != null && heightMap.HeightTexture != null)
                    splat.ConfigureHeightMap(heightMap.HeightTexture,
                        heightMap.MinElevation, heightMap.MaxElevation);
                else
                    Debug.LogWarning("[StPetersBuilder] no painted height map for the splat ground — " +
                                     "the quad clips itself empty (below the paint floor everywhere).");

                splat.ConfigureBands(
                    StPetersShoreMap.PaintFloorElevation, StPetersShoreMap.RippleFloorElevation,
                    StPetersShoreMap.SandFloorElevation, StPetersShoreMap.MarramFloorElevation,
                    StPetersShoreMap.GrassFloorElevation, StPetersShoreMap.ShingleFloorElevation,
                    StPetersShoreMap.BandWiggleMetres, StPetersShoreMap.BandWiggleScale,
                    StPetersShoreMap.BandDetailMetres, StPetersShoreMap.BandDetailScale);
                splat.ConfigureWeatherSector(IslandCenter, IslandRadius / IslandRadiusY,
                    StPetersShoreMap.WeatherCoastFacing, StPetersShoreMap.SectorFeather);
                splat.ConfigureSandbar(SandbarFrom, SandbarTo, SandbarHalfWidth,
                    StPetersShoreMap.BarSpineHalfWidth, StPetersShoreMap.BarSpineFloorElevation);
            }

            // --- THE PAINTED COAST (shoreline-ISO v8) ---------------------------------------------------
            // Now that the height map exists, paint the ground it implies: the whole landmass and its
            // intertidal, in the kit's six materials, plus the ragged fringe where one laps onto another and
            // the reef's rock tells on the shelf. Everything is a pure function of THIS terrain
            // (StPetersShoreMap), so the picture and the walk gate can never disagree, and a rebuild
            // reproduces the coast to the tile (rule 5).
            //
            // The layers sort at -20..-18 — BELOW the Sea plane at -5 — which is the whole tide reveal: the
            // WaterSurface shader clips itself transparent wherever the ground is above the live water level,
            // so the painting shows through on dry ground and depth-graded water covers it as the tide floods
            // (ADR 0010/0012). The kit bakes ZERO water for exactly that reason.
            var coast = StPetersShorePainter.Paint(terrain, RegionWorldCenter, RegionWorldSize,
                                                   splatGround: UseSplatGround);

            // --- THE WOODS AND THE MEADOW (§5.1: "the reverting interior") -----------------------------
            // Stands of Acadian trees with meadow between them and wildflowers through both, all chosen by
            // HABITAT off the same terrain: exposure (how much salt and wind this ground takes, and which
            // way its coast faces) and wetness (hollows hold water). Black spruce and fir take the blasted
            // fringe, red spruce and cedar the middle, the hardwoods only the sheltered core. The village,
            // the spawn, the crossing's approach and the dock are left clear — a village stands IN a
            // clearing, and you must be able to see the bar from the island.
            //
            // ⚠ NINE species: tamarack is held back by ruling and absent from Trees.json. The planter only
            // ever plants what AcadianTreeCatalog.Scan() returns, so it cannot be reached by accident.
            var woods = StPetersWoodsPlanter.Plant(terrain);

            // --- TIDE-REVEAL: now owned by the smooth WaterSurface shader (ADR 0012) --------------------
            // The falling-tide reveal (the bar VISIBLY baring/covering) is rendered by the layered WaterSurface
            // shader on the Sea plane above — it reads the SAME deterministic tide + TidalTerrain the
            // walkability/boat-cross gate reads (depth = WaterLevelAt(t) − elevation), clips to expose the
            // authored sand ground where dry and renders a depth-graded, foam-fringed water where covered.
            // That is the SMOOTH wavy shore. An older purely-visual 2 m colour-cell grid (TidalFlatVisual)
            // used to be stamped here on top of the shader; it double-drew the bar as big flat blue/teal
            // squares and HID the smooth shader (the owner-reported blockiness). ADR 0012 "converge the live
            // shoreline on the shader path" retired it — the shader alone now owns the reveal. The P1
            // gameplay (clam-baring + the crossing gate) is unchanged: it reads ITidalTerrain/WaterLevelAt
            // directly (see ScatterClamHoles + ClamDig + the StPeters/Clam EditMode tests), never this visual.

            // --- OPTIONAL DEV FAST-TIDE (greybox aid; OFF by default) -----------------------------------
            // The real-time tide is slow by design (~minutes per high→low), so in a short playtest the
            // reveal is gradual. This dev component lets a tester TICK 'Enabled' on it in the Inspector
            // during Play to fast-forward the clock and watch the WHOLE swing in seconds — it never changes
            // the shipping default (it only multiplies the live TimeScale while ticked, and is OFF by
            // default). The in-editor Tide Scrubber remains the precise way to jump the tide.
            var devTideGo = new GameObject("DevFastTide");
            devTideGo.AddComponent<HiddenHarbours.Environment.DevFastTide>();

            // --- CLAM-HOLES (VISIBLE + diggable; scattered wherever the tide bares ground) ---------------
            // The owner's expectation: clam holes appear ANYWHERE the falling tide bares the flats, and are
            // diggable. So instead of a fixed handful of points, we DETERMINISTICALLY scatter holes over the
            // bar's whole footprint and keep only the cells whose authored ground is INTERTIDAL — ground that
            // actually reveals as the big tide falls (between the lowest and highest water of the swing). That
            // skips always-dry island and always-deep harbour, so holes land exactly on the bared flats.
            //
            // Each hole gets: a SpriteRenderer (ClamHole.png, base-Y sorted so it sits on the flat), a World
            // ClamSpot marker (names the yield by id), a ClamDig (the hole's gate-and-yield — wired to the
            // species + the player's ClamBucket, gating reach/exposure/shovel/room), and a ClamHoleVisual that
            // shows the hole ONLY while its ground is exposed and runs the ClamSquirt tell off
            // ClamDig.ShowingSquirt. The position + the exposure are a pure function of (position, tide) — no
            // RNG drift (CLAUDE.md rule 5); the scatter jitter is a stable hash of the grid cell, so a rebuild
            // reproduces the same field. The holes DON'T listen for input themselves — a single ClamDigger on
            // the player (below) owns the Interact key and digs only the nearest in-range hole, one clam per
            // press (the fix for "E dug every exposed hole at once + filled the bucket in two presses").
            var clamRoot = new GameObject("ClamHoles");
            var holeSprite = LoadSpriteAny(ArtClamHole);
            var squirtFrames = LoadSheetFrames(ArtClamSquirt);
            foreach (var p in ScatterClamHoles(terrain))
                MakeClamHole(clamRoot.transform, p, clam, "fish.soft_shell_clam",
                             core.Bucket, holeSprite, squirtFrames, waterSprite);

            // THE DIGGER (gameplay-systems): one Interact owner on the player. On E it digs the nearest hole
            // that's BOTH in shovel reach AND exposed — so a press is exactly one clam from the hole you're
            // standing on, and nothing when you're not at a bared hole. Sits on the persistent Player so it
            // carries across region hops with the player (no per-region re-wire needed).
            var digger = core.PlayerGo.AddComponent<ClamDigger>();
            SetRef(digger, "_player", core.PlayerGo.transform);

            // --- THE TRAP-HAUL LOOP (gameplay-systems, Build 4 — the playable manual loop) ---------------
            // Set → soak → lay alongside → HAUL WITH THE SWELL → collect → sell. The trap runtime
            // (PlacedTrapService) owns determinism + save; the DevTrapInput drops a baited pot through the
            // REAL depth gate (only in deep-enough water, consuming one bait); the TrapHaulController runs the
            // diegetic haul-with-the-swell (lay the boat alongside a buoy, H to start, then HOLD the pull
            // key/click WITH the swell — take line as she lifts, ease as she falls; calm is a quick steady
            // wind-in, a big sea is a fight where holding through the drop strains and slips the rope).
            // On surface it lands Build 3's already-deterministic catch into the boat's hold (sellable at
            // Nine Mile Creek like any fish). The buoy is the self-installing TrapBuoyPresenter (Build 1/3). All the
            // trap/service objects live under one plain root so they never touch authored content.
            //
            // WIRED ON THE BOAT: the DevTrapInput + TrapHaulController sit on the persistent Dory
            // (core.DoryGo), so the drop point / rail / hold are the boat you sail — you set + haul FROM the
            // boat (a boat action, canon §6.3). Content is DATA: the LobsterTrap/CrabTrap Defs + the
            // Herring/FishScrap/Mackerel BaitDefs (Build 2), loaded by path (null-safe if not imported).
            var lobsterTrap = AssetDatabase.LoadAssetAtPath<TrapDef>(DataTraps + "/LobsterTrap.asset");
            var crabTrap    = AssetDatabase.LoadAssetAtPath<TrapDef>(DataTraps + "/CrabTrap.asset");
            var herring     = AssetDatabase.LoadAssetAtPath<BaitDef>(DataBait + "/Herring.asset");
            var fishScrap   = AssetDatabase.LoadAssetAtPath<BaitDef>(DataBait + "/FishScrap.asset");
            var mackerel    = AssetDatabase.LoadAssetAtPath<BaitDef>(DataBait + "/Mackerel.asset");

            if (lobsterTrap != null && herring != null && core.DoryGo != null)
            {
                // The trap runtime service (owns place/haul/save; restores traps off the load edge). Its
                // registry is every trap/bait KIND that can be restored by id (the OwnedFleet pattern).
                var trapServiceGo = new GameObject("PlacedTrapService");
                var trapService = trapServiceGo.AddComponent<PlacedTrapService>();
                var trapRegistry = crabTrap != null ? new[] { lobsterTrap, crabTrap } : new[] { lobsterTrap };
                var baitRegistry = new[] { herring, fishScrap, mackerel }.Where(b => b != null).ToArray();
                trapService.Configure(trapRegistry, baitRegistry, trapServiceGo.transform);

                // GREYBOX starting bait so the loop is playable NOW (real acquisition is a later economy
                // offer). Seeds a handful of herring once per game into the save's BaitStock.
                var startBaitGo = new GameObject("StartingBait");
                startBaitGo.AddComponent<StartingBait>();

                // THE POT STARTER KIT (pots are OWNED now — bought at the Nine Mile Creek shipwright): grants
                // GameConfig.StarterPotKit once per game, flag-guarded — a new game starts with a couple
                // of pots, and a pre-update save gets the same kit on first load, so nobody is stranded
                // potless mid-loop. Counts live on the config asset (owner-tunable, rule 6).
                var startPotsGo = new GameObject("StartingPots");
                var startingPots = startPotsGo.AddComponent<StartingPots>();
                SetRef(startingPots, "_config", config);

                // DevTrapInput on the BOAT: T sets a baited lobster pot at the boat (depth-gated), G tops up
                // dev bait. Placement region = St Peters (the save/scene region); the keys live only while
                // the player stands ON DECK (Build 5) — never at the helm, never on foot (a deck action).
                var devTrap = core.DoryGo.AddComponent<DevTrapInput>();
                devTrap.Configure(trapService, core.DoryGo.transform, lobsterTrap, herring, "region.st_peters");

                // The HAUL-WITH-THE-SWELL controller on the BOAT: rail = the boat, hold = the boat's ShipHold. The
                // CATCH region is region.st_peters — the region this scene's pots are PLACED in (DevTrapInput
                // tags them region.st_peters), so a St-Peters-set pot draws St Peters' LOCAL lobster/crab, not
                // Coddle Cove's. This is now correct because the lobster + crab FishSpeciesDefs are region-
                // tagged for St Peters (their RegionIds include region.st_peters — the actual catch gate the
                // CatchResolver filters on). Determinism is unaffected — the region only filters the pool
                // (rule 5). Was region.coddle_cove as a stopgap before the species were tagged for St Peters.
                var haul = core.DoryGo.AddComponent<TrapHaulController>();
                haul.Configure(trapService, core.DoryGo.transform, core.Hold, "region.st_peters");
                SetRef(haul, "_holdProvider", core.DoryGo);
                // The rope's PLAYER-SIDE anchor: draw the haul rope from the deck-walking fisher's HANDS
                // (the persistent Player + the tunable hand offset), not the boat's centre — so the line
                // meets the haul sprite. Visual only; reach/drift still measure from the rail (the boat).
                SetRef(haul, "_ropeAnchor", core.PlayerGo.transform);
            }
            else
            {
                Debug.LogWarning("[StPetersBuilder] Trap/bait content missing (LobsterTrap/Herring) — the " +
                                 "trap-haul loop was not wired. Re-run after Data/Traps + Data/Bait import.");
            }

            // --- THE ONE DOCK (§5.1a, ruled) — dressed from the wharf tile kit -------------------------
            // ⭐ RETIRED HERE: a 2.5 x 1.2 m brown rectangle named "DorySlipMarker" that stood in for the
            // dock, sitting on a shore the island no longer has. St Peters is ruled to have ONE dock, on the
            // east end opposite the sandbar, "modest, but can take powerboats" — so it gets a real modest
            // pier: a 39 x 6 m timber finger running out along the dredged slip from the last dry ground to
            // just short of the mooring, so the ratified disembark at (328, 0) lands ON the planks. Drawn
            // strictly back to front, because a wharf cell is 32 x 56 px whose bottom 24 rows of vertical
            // face overhang the cell below (StPetersWharf carries the whole contract).
            //
            // ⭐ The deck is FLOOR now, not dressing. TidalWalkability used to answer purely from (elevation
            // vs water level), so the sim saw 4.5 m of open water over the dredged -1.0 m slip at the
            // RATIFIED disembark point for most of every tide: you sailed home to your own island and swam
            // across your own pier. The pier registers a StandablePlatform (Core's standable-structure seam),
            // and its deck height is MEASURED off `terrain` at the pier root rather than picked — so a
            // terrain edit can never leave the planks floating. The seabed is untouched: the slip stays
            // dredged (boats need the depth) and only the ON-FOOT reads consult the deck.
            StPetersWharf.Place(terrain);

            // --- THE VILLAGE, at last (§5.1's three houses + the school + the general store) --------------
            // The last item on the owner's dressing list. #345 relocated the village's PROPS but deferred
            // the buildings themselves because the building kit had no consumer; #352 built one, so the
            // school, the store and the three clapboard houses can finally stand where the docs put them.
            // AUTHORED sites, one facing derived per building so every door turns toward the green — see
            // StPetersVillage for why the arc is the only shape the site allows.
            int villageBuildings = StPetersVillage.Place(terrain);

            // --- THE OPENING CAST + ONBOARDING (world-content; the buy-and-repair beat, canon §5.8) ------
            // Aunt Ginny + Ned's LETTER, anchored up on the island near the cottage (no routines — that's
            // M2), the self-built dialogue panel, the proximity INTERACT driver, and the light one-line
            // onboarding nudge that walks the NEW loop: dig clams → cross the bar → Nine Mile Creek (cod licence +
            // rod) → buy + REPAIR the damaged dory → sail home. The dory is EARNED, never inherited.
            //
            // Everything sits UP BY THE COTTAGE in the village on the island's WEST half — and the dock is
            // now 430 m away on the EAST end (§5.1a), so the shared E key could not fire both "talk" and
            // "board" even if it wanted to (it is context-aware by proximity regardless). Belt-and-
            // braces, the open dialogue raises the Core InteractionGate the ControlSwitcher honours. Content
            // is DATA — the Interactables carry NpcDef refs, not strings; the words live in Data/NPCs.
            // Art is greybox (Ginny standee + portraits + panel), null-safe if a sprite isn't imported.

            // Dialogue panel (builds its own canvas in Awake; needs only the panel + nameplate art).
            var dialogueGo = new GameObject("DialogueUI");
            var presenter = dialogueGo.AddComponent<DialoguePresenter>();
            SetRef(presenter, "_panelSprite", LoadSpriteAny(ArtDialoguePanel));
            SetRef(presenter, "_nameplateSprite", LoadSpriteAny(ArtNamePlate));

            // Aunt Ginny — by the cottage on the island plateau. Teaches the buy-and-repair loop; finishing
            // her conversation sets met_ginny (which gates the first onboarding nudge + her warmer re-greet).
            var ginnyGo = MakeNpc("AuntGinny", GinnyPos, LoadSpriteAny(ArtGinny),
                                  waterSprite, new Color(0.78f, 0.55f, 0.62f));
            var ginnyIt = ginnyGo.AddComponent<Interactable>();
            ConfigureInteractableNpc(ginnyIt, ginnyNpc, LoadSpriteAny(ArtPortraitGinny));

            // "Ned's Letter" on the cottage step — the remembered presence (no inherited dory; the boat is
            // bought + mended). Read it to set read_logbook. A small book-sized marker if the art's absent.
            var letterGo = MakeNpc("NedsLetter", NedsLetterPos, null,
                                   waterSprite, new Color(0.62f, 0.47f, 0.30f));
            letterGo.transform.localScale = new Vector3(0.6f, 0.8f, 1f);
            var letterIt = letterGo.AddComponent<Interactable>();
            ConfigureInteractableNpc(letterIt, nedNpc, LoadSpriteAny(ArtPortraitNed));

            // --- THE ISLAND'S OWN PEOPLE (M1 §7.1) -------------------------------------------------------
            // Ginny and the letter are the OPENING; these five are the PLACE. §7.1 asks for "4–6 named
            // inhabitants with a line of dialogue with an opinion and a fixed spot", and its exit condition
            // is that you can walk the island in two minutes, meet everyone, and know what each building is
            // for — so the storekeeper stands at the store and the teacher at the school. #353 built the
            // doors; this is who answers them. Anchored, NOT scheduled: routines are M2.
            var islanders = StPetersInhabitants.Place(terrain, waterSprite);

            // The proximity INTERACT driver: shows "E: …" near an interactable and runs its conversation.
            var interactorGo = new GameObject("WorldInteractor");
            var interactor = interactorGo.AddComponent<WorldInteractor>();
            SetRef(interactor, "_player", core.PlayerGo.transform);
            SetRef(interactor, "_presenter", presenter);
            SetRefArray(interactor, "_interactables",
                        new Object[] { ginnyIt, letterIt }.Concat(islanders).ToArray());

            // Light onboarding: one nudge per beat of the new earned-dory loop, then it bows out and persists
            // 'onboarded' (on the dory being REPAIRED) so the opening never re-triggers on reload.
            new GameObject("Onboarding").AddComponent<OnboardingDirector>();

            // --- REGION DISPLAY NAME (the world registrar → Core) ---------------------------------------
            // Register "St Peters Island" / "Nine Mile Creek" so the UI crossing-card resolves them by scene
            // name or id without referencing World (the RegionDisplayNames seam, #59/#54).
            var namesGo = new GameObject("RegionDisplayNames");
            var registrar = namesGo.AddComponent<RegionDisplayNameRegistrar>();
            SetRefArray(registrar, "_regions", new Object[] { stPeters, nineMileCreek });

            // --- REGION SCENE-LOAD PATH + the WALK PASSAGE to Nine Mile Creek ----------------------------------
            // The persistent travel rig (loader + coordinator) was built with the core; here we fill the
            // loader's region list and wire this region's passage to it. Consistent with the TidalTerrain:
            // the bar is a WALK path at low water (the first trip is on foot). The passage band sits at the
            // Nine Mile Creek (east) end of the bar; triggering is forgiving (greybox) — gameplay gates the actual
            // crossing on the bar being EXPOSED (TidalExposure) and on the player being on foot.
            var loader = core.Loader;
            SetRefArray(loader, "_regions", new Object[] { stPeters, nineMileCreek });

            var passageGo = new GameObject("PassageToNineMileCreek");
            passageGo.transform.position = ToNineMileCreekPassagePos;
            var trigger = passageGo.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(3f, SandbarHalfWidth * 2f);   // a band across the bar's Nine Mile Creek end
            var passage = passageGo.AddComponent<RegionPassage>();
            SetRef(passage, "_target", nineMileCreek);
            SetRef(passage, "_loader", loader);

            // --- ST PETERS DOCK + ARRIVAL ANCHOR (the persistent rig binds here on the sail home) --------
            // St Peters' own board/dock geometry, mirroring the cove/Nine Mile Creek pattern. The persistent
            // ControlSwitcher starts pointed at this slip so you can board the moored Dory once she's yours;
            // the RegionAnchor lets the RegionTravelCoordinator re-bind the rig here when you sail back from
            // the cove. The arrival sits within DockZoneRadius of the dock zone (the proven disembark
            // geometry — don't regress #52). Disembark steps onto the island's south shore.
            var dockZone = new GameObject("StPetersDockZone");
            dockZone.transform.position = DockZonePos;
            var disembark = new GameObject("StPetersDisembark");
            disembark.transform.position = DisembarkPos;
            var arrival = new GameObject("StPetersArrival");
            arrival.transform.position = ArrivalPos;

            // Point the persistent switcher's dock at this slip so boarding works in the start region.
            core.Switcher.SetDock(dockZone.transform, disembark.transform);

            var anchor = new GameObject("StPetersRegionAnchor").AddComponent<RegionAnchor>();
            anchor.Configure("region.st_peters", arrival.transform, dockZone.transform, disembark.transform);
            // The camera's bounds clamp reads this on arrival (scene-sizing §6 item 4) — the SAME
            // authored pair the sea, the backdrop, the height bake and the displaced mesh get above.
            // At 760 × 520 m an unclamped camera sails clean off the painted map.
            anchor.ConfigureExtent(RegionWorldCenter, RegionWorldSize);

            // --- SAVE & REGISTER ------------------------------------------------------------------------
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StPetersBuilder] THE ISLAND IS DRESSED: {woods.Trees} trees + {woods.Shrubs} shrubs + " +
                      $"{woods.Flowers} wildflowers by habitat, the one dock at the east berth, and a village " +
                      $"of {villageBuildings} buildings — the school, the store and the houses — standing " +
                      "round the green beside the start spawn.");
            Debug.Log($"[StPetersBuilder] THE COAST IS PAINTED: {coast.GroundTiles:N0} shoreline-ISO ground " +
                      $"tiles + {coast.FringeTiles:N0} fringe overlays + {coast.Rocks} rocks on the reef " +
                      $"({coast.MaterialSummary()}). The island has ground everywhere for the first time " +
                      "since it was scaled to 450 x 260 m.");
            Debug.Log("[StPetersBuilder] Built StPeters.unity — the OPENING + START region (greybox), now " +
                      "with the PERSISTENT CORE so it's playable: press Play and you control the on-foot " +
                      "fisher at the START spawn (WASD), the camera follows at the on-foot framing, and the " +
                      "clock/tide run (GameServices online → the tide advances + the bar bares/floods). The " +
                      "moored hand-rowed Dory floats off the south coast (board at the slip once she's " +
                      "yours). Island = high (always exposed); the SANDBAR bridges it to Nine Mile Creek as a " +
                      "tide-gated path: the crest (1.6 m) bares as the BIG tide (±3.5 m) falls, while a " +
                      "deeper CHANNEL (-0.6 m) stays boat-crossable at higher tide. The layered WaterSurface " +
                      "shader VISIBLY reveals the bar/flats from the live water level (smooth depth-graded " +
                      "water that clips to bare the sand as the tide falls, foam hugging the moving edge) — " +
                      "the smooth shoreline, no blocky grid overlay (ADR 0012). Up by the " +
                      "cottage, AUNT GINNY (E to talk) teaches the buy-and-repair loop and NED'S LETTER (E " +
                      "to read) frames it — the dory is EARNED, not inherited; a one-line onboarding nudge " +
                      "walks the loop (clams → cross the bar → Nine Mile Creek licence+rod → buy+repair the dory → " +
                      "sail home). Clam-holes on " +
                      "the flats (fish.soft_shell_clam, gated by exposure). The walk passage at the Nine Mile Creek " +
                      "end leads on. NOTE: the tide is SLOW by design (~minutes per high→low); to see the " +
                      "full swing fast, tick 'Enabled' on the DevFastTide object in Play (OFF by default) or " +
                      "use the in-editor Tide Scrubber. StPeters is now BUILD-INDEX-0 (the start scene). " +
                      "RE-RUN this builder + NineMileCreekBuilder, then open StPeters.unity and press Play.");
            EditorUtility.DisplayDialog("Hidden Harbours",
                "St Peters Island built — now a PLAYABLE START scene (greybox).\n\nPress Play:\n• You control " +
                "the on-foot fisher (WASD / arrows) at the start spawn; the camera follows.\n• The clock + " +
                "tide RUN — the smooth WaterSurface shader bares the sandbar/flats (the sand shows through) " +
                "as the big tide (±3.5 m) falls and covers them with depth-graded water as it floods (the " +
                "tide-reveal is the point).\n• The tide is SLOW by design (~a few " +
                "minutes per high→low). To watch the full swing FAST: tick 'Enabled' on the DevFastTide " +
                "object in the Hierarchy while in Play (OFF by default), or use Tools ▸ Tide Scrubber.\n• " +
                "The hand-rowed Dory is moored off the south coast (board at the slip once she's yours).\n• " +
                "Walk the bared bar east to reach Nine Mile Creek.\n\nThe dig/walk/gear ACTIONS are " +
                "gameplay-systems'. StPeters is now build-index-0 (the start scene). RE-RUN 'Build St Peters " +
                "Scene' AND 'Build Nine Mile Creek Scene', then open StPeters.unity and press Play.", "Fair winds");
        }

        // ---- shared config (single source of truth with the EditMode test) -------------------------

        /// <summary>Configure the runtime <see cref="HiddenHarbours.Art.WaterSurface"/> so the layered shader's
        /// baked seabed height map covers the visible water (the Sea plane bounds) at a chosen bake resolution.
        /// The sim → uniform tuning (current/wind thresholds, refresh) lives on the component defaults and the
        /// LOOK on the Water material (no magic numbers — CLAUDE.md rule 6); here we set the world rectangle the
        /// height map bakes over (so the depth gradient + foam band line up with the St Peters TidalTerrain) and
        /// the bake <paramref name="resolution"/>. A FINER bake (ADR 0012 §A: 96 → 192) shrinks the texel from
        /// ~1.67 m to ~0.83 m over the 160×120 m plane, so the shader's wet edge follows a fine grid instead of
        /// reading as ~1.5 m rectangular steps — the smoothed shoreline. Clamped to the component's
        /// [Range(16,256)]; pass 256 if the near-flat bar crest still facets at 192.</summary>
        public static void ConfigureWaterSurface(HiddenHarbours.Art.WaterSurface surface,
                                                 Vector2 worldCenter, Vector2 worldSize, int resolution)
        {
            var so = new SerializedObject(surface);
            SetV2(so, "_heightWorldCenter", worldCenter);
            SetV2(so, "_heightWorldSize", worldSize);
            SetInt(so, "_heightResolution", Mathf.Clamp(resolution, 16, 256));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Enable the weather-driven water palette (ADR 0017) on the Sea's
        /// <see cref="HiddenHarbours.Art.WaterSurface"/> and assign the anchor mood presets, persisting the
        /// refs via SerializedObject so they survive in the saved scene (the builder's persist-the-refs
        /// convention, same as <see cref="ConfigureWaterSurface"/>). The blend rides the per-renderer
        /// MaterialPropertyBlock and never mutates Water.mat (CLAUDE.md rule 5); the mapping/strength tunables
        /// live on the component defaults (no magic numbers — rule 6), the owner dials them in the Inspector.
        /// A null anchor is left null and WaterSurface falls back to the base / its own material, so a missing
        /// preset no-ops safely. St Peters passes <paramref name="baseMood"/> = null ON PURPOSE so the BASE/calm
        /// anchor resolves to the Sea's own LIVE Water.mat (the owner's Water.mat tuning then always drives the
        /// calm sea; weather-off / strength-0 = exactly Water.mat — ADR 0017). Pass an explicit base only to PIN
        /// the calm look to a fixed preset copy.</summary>
        public static void ConfigureWeatherPalette(HiddenHarbours.Art.WaterSurface surface,
                                                   Material baseMood, Material calmMood,
                                                   Material stormMood, Material fogMood)
        {
            var so = new SerializedObject(surface);
            SetBoolProp(so, "_weatherPaletteEnabled", true);
            SetObjProp(so, "_baseMoodMaterial", baseMood);
            SetObjProp(so, "_calmMoodMaterial", calmMood);
            SetObjProp(so, "_stormMoodMaterial", stormMood);
            SetObjProp(so, "_fogMoodMaterial", fogMood);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Apply the authored elevation zones onto a <see cref="TidalTerrain"/> via SerializedObject
        /// (the builder's persist-the-refs convention). One place so the values live on the component, never
        /// duplicated — and an EditMode test asserts the same constants drive the showcase.</summary>
        public static void ConfigureTidalTerrain(TidalTerrain terrain)
        {
            var so = new SerializedObject(terrain);
            SetF(so, "_deepHarbourElevation", DeepHarbourElevation);
            SetV2(so, "_islandCenter", IslandCenter);
            SetF(so, "_islandRadius", IslandRadius);
            SetF(so, "_islandRadiusY", IslandRadiusY);
            SetF(so, "_reefShelfWidth", ReefShelfWidth);
            SetF(so, "_reefShelfInnerElevation", ReefShelfInnerElevation);
            SetF(so, "_reefShelfOuterElevation", ReefShelfOuterElevation);
            SetF(so, "_berthHalfWidth", BerthHalfWidth);
            SetV2(so, "_berthFrom", BerthFrom);
            SetV2(so, "_berthTo", BerthTo);
            SetF(so, "_berthBedElevation", BerthBedElevation);
            SetF(so, "_islandFalloff", IslandFalloff);
            SetF(so, "_islandElevation", IslandElevation);
            SetV2(so, "_sandbarFrom", SandbarFrom);
            SetV2(so, "_sandbarTo", SandbarTo);
            SetF(so, "_sandbarHalfWidth", SandbarHalfWidth);
            SetF(so, "_sandbarCrestElevation", SandbarCrestElevation);
            SetF(so, "_channelAlong", ChannelAlong);
            SetF(so, "_channelHalfWidth", ChannelHalfWidth);
            SetF(so, "_channelBedElevation", ChannelBedElevation);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- helpers (self-contained; mirror NineMileCreekBuilder's) ------------------------------------

        static T LoadOrCreate<T>(string path, System.Action<T> init = null) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            if (init != null) init(asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        static void SetRef(Component c, string field, Object value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
            { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        static void SetString(Component c, string field, string value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p != null && p.propertyType == SerializedPropertyType.String)
            { p.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        static void SetRefArray(Component c, string field, Object[] values)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[StPetersBuilder] no array field '{field}'."); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetF(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
            else Debug.LogWarning($"[StPetersBuilder] no float field '{field}'.");
        }

        static void SetInt(SerializedObject so, string field, int value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.intValue = value;
            else Debug.LogWarning($"[StPetersBuilder] no int field '{field}'.");
        }

        static void SetV2(SerializedObject so, string field, Vector2 value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.vector2Value = value;
            else Debug.LogWarning($"[StPetersBuilder] no Vector2 field '{field}'.");
        }

        static void SetBoolProp(SerializedObject so, string field, bool value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.boolValue = value;
            else Debug.LogWarning($"[StPetersBuilder] no bool field '{field}'.");
        }

        static void SetObjProp(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference) p.objectReferenceValue = value;
            else Debug.LogWarning($"[StPetersBuilder] no object-reference field '{field}'.");
        }

        // Imported art is sliced (spriteMode Multiple, one sub-sprite), so LoadAssetAtPath<Sprite> returns
        // null — fall back to the first sub-sprite. Null if the art isn't imported.
        static Sprite LoadSpriteAny(string path)
        {
            var direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (direct != null) return direct;
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                                 .OrderBy(s => SpriteIndex(s.name)).FirstOrDefault();
        }

        // All sub-sprites of a sliced sheet (spriteMode Multiple), in frame order — e.g. ClamSquirt_0.._3.
        static Sprite[] LoadSheetFrames(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                            .OrderBy(s => SpriteIndex(s.name)).ToArray();

        static int SpriteIndex(string spriteName)
        {
            int u = spriteName.LastIndexOf('_');
            return (u >= 0 && int.TryParse(spriteName.Substring(u + 1), out int n)) ? n : 0;
        }

        // A standing world NPC / marker: a SpriteRenderer above the ground (just under the player, which
        // draws at +10), with a tinted-square fallback so the greybox still builds before the art imports.
        static GameObject MakeNpc(string name, Vector3 pos, Sprite sprite, Sprite fallback, Color fallbackColor)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 9;
            if (sprite != null) { sr.sprite = sprite; go.transform.localScale = Vector3.one; }
            else { sr.sprite = fallback; sr.color = fallbackColor; go.transform.localScale = new Vector3(1f, 2f, 1f); }
            return go;
        }

        // Wire an Interactable to an NpcDef (the data-driven path) + an optional portrait, via the builder's
        // persist-the-refs SerializedObject convention so the refs survive into the saved scene.
        static void ConfigureInteractableNpc(Interactable it, NpcDef npc, Sprite portrait)
        {
            var so = new SerializedObject(it);
            var npcProp = so.FindProperty("_npc");
            if (npcProp != null) npcProp.objectReferenceValue = npc;
            var portraitProp = so.FindProperty("_portrait");
            if (portraitProp != null) portraitProp.objectReferenceValue = portrait;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (npc == null)
                Debug.LogWarning("[StPetersBuilder] NpcDef missing for an Interactable — re-run after the " +
                                 "Data/NPCs assets import (the opening NPC will show no dialogue otherwise).");
        }

        // (MakeTiledGround retired with the greybox ground patches it drew — StPetersShorePainter now lays
        // the island's ground as painted shoreline-ISO tiles. Nothing else in this builder tiled a sprite.)

        /// <summary>
        /// DETERMINISTIC clam-hole field: a hash-jittered grid over the bar footprint, keeping only the
        /// cells whose authored ground (<paramref name="terrain"/>) is INTERTIDAL — between the lowest and
        /// highest water of the St Peters swing (mean ∓ amplitude), inset by <see cref="ClamBandMargin"/>.
        /// That puts holes exactly where the falling tide bares ground (skipping always-dry island and
        /// always-deep harbour), so wherever a flat reveals there are holes to find. Pure: position is a
        /// function of the cell index (a stable hash, no <c>System.Random</c>) and the kept/dropped decision
        /// is a function of (position, tide band) — a rebuild reproduces the same field (CLAUDE.md rule 5).
        /// Public + static so the EditMode test can assert the same field the scene is built from.
        /// </summary>
        public static System.Collections.Generic.List<Vector2> ScatterClamHoles(ITidalTerrain terrain)
        {
            var holes = new System.Collections.Generic.List<Vector2>();
            if (terrain == null) return holes;

            // The intertidal band: ground that bares AND floods over the swing. Mean ∓ amplitude is the
            // extreme water; inset by the kindness margin so a hole isn't perpetually at the very edge.
            float lowWater  = TideMean - TideAmplitude + ClamBandMargin;
            float highWater = TideMean + TideAmplitude - ClamBandMargin;

            // Bounding box around the bar (From→To) plus a margin so the flats either side are covered.
            float minX = Mathf.Min(SandbarFrom.x, SandbarTo.x) - ClamScatterMargin;
            float maxX = Mathf.Max(SandbarFrom.x, SandbarTo.x) + ClamScatterMargin;
            float minY = Mathf.Min(SandbarFrom.y, SandbarTo.y) - SandbarHalfWidth - ClamScatterMargin;
            float maxY = Mathf.Max(SandbarFrom.y, SandbarTo.y) + SandbarHalfWidth + ClamScatterMargin;

            int nx = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / ClamScatterStep));
            int ny = Mathf.Max(1, Mathf.CeilToInt((maxY - minY) / ClamScatterStep));
            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                // Cell centre, then a stable hash-jitter (deterministic, no RNG) so the field isn't a grid.
                float cx = minX + (ix + 0.5f) * ClamScatterStep;
                float cy = minY + (iy + 0.5f) * ClamScatterStep;
                cx += (Hash01(ix, iy, 1) * 2f - 1f) * ClamScatterJitter;
                cy += (Hash01(ix, iy, 2) * 2f - 1f) * ClamScatterJitter;

                var pos = new Vector2(cx, cy);
                float ground = terrain.ElevationAt(pos);
                if (ground > lowWater && ground < highWater)   // intertidal → bares as the tide falls
                    holes.Add(pos);
            }
            return holes;
        }

        // A stable 0..1 hash of two integer cell coords + a salt — deterministic jitter without System.Random
        // (so the scatter is a pure function of the cell; a rebuild reproduces the same field — rule 5).
        static float Hash01(int x, int y, int salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)y) * 16777619u;
                h = (h ^ (uint)salt) * 16777619u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (h & 0xFFFFFF) / (float)0x1000000;   // 24-bit mantissa → [0,1)
            }
        }

        /// <summary>
        /// Build one VISIBLE, diggable clam hole: a SpriteRenderer (ClamHole.png, base-Y sorted so it sits on
        /// the flat) used as the visual's BASE (two-holes) renderer, the World <see cref="ClamSpot"/> marker,
        /// the <see cref="ClamDig"/> action wired to the clam species + the player's <see cref="ClamBucket"/>,
        /// and the <see cref="ClamHoleVisual"/> that shows the holes while exposed, layers the squirt tell on a
        /// SEPARATE overlay renderer it creates (the squirt is added on top, never a sprite-swap), runs the
        /// skittish-clam proximity escape, and vanishes the hole once it's been dug (a hole yields once).
        /// </summary>
        static void MakeClamHole(Transform parent, Vector2 pos, FishSpeciesDef clam, string yieldFishId,
                                 ClamBucket bucket, Sprite holeSprite, Sprite[] squirtFrames, Sprite fallback)
        {
            var go = new GameObject("ClamHole");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            // Sit ON the bared flat: above the Sandbar ground (-9) AND the water shader plane (Sea, -5) so a
            // hole on dry ground draws in front of the (clipped-transparent) water, and below the on-foot
            // characters (the player Y-sorts within +2..+40, #110). A small base-Y term sorts holes among
            // themselves (lower on screen = higher Y-negated = draws in front) without ever crossing the
            // character band — clamped to -4..4 so a far-south hole can't pop in front of the player.
            sr.sortingOrder = Mathf.Clamp(-Mathf.RoundToInt(pos.y), -4, 4);
            if (holeSprite != null) { sr.sprite = holeSprite; go.transform.localScale = Vector3.one; }
            else { sr.sprite = fallback; sr.color = new Color(0.30f, 0.24f, 0.16f, 0.65f); go.transform.localScale = new Vector3(0.5f, 0.5f, 1f); }

            // World marker: names the yield by id (no Fishing reference from World).
            var spot = go.AddComponent<ClamSpot>();
            SetString(spot, "_yieldFishId", yieldFishId);

            // The DIG action (gameplay-systems): species + the player's bucket; gates exposure/shovel/room.
            // The exposure test reads this object's transform (the hole position) via TidalTerrain.
            var dig = go.AddComponent<ClamDig>();
            SetRef(dig, "_clamSpecies", clam);
            SetRef(dig, "_bucketProvider", bucket != null ? bucket.gameObject : null);
            SetRef(dig, "_spot", go.transform);

            // The LOOK: the two-holes sprite stays on the BASE renderer while exposed; the squirt plays as an
            // OVERLAY on a separate child renderer the visual creates on top (added, never a sprite-swap), so
            // the holes are always visible when exposed. Driven off the SAME exposure read the dig gates on, so
            // picture and gate agree. The visual also runs the skittish-clam escape and vanishes a dug hole.
            var visual = go.AddComponent<ClamHoleVisual>();
            SetRef(visual, "_dig", dig);
            SetRef(visual, "_holeSprite", holeSprite);
            if (squirtFrames != null && squirtFrames.Length > 0)
                SetRefArray(visual, "_squirtFrames", squirtFrames.Cast<Object>().ToArray());
        }

        static Sprite MakeSquareSprite(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            var tex = new Texture2D(16, 16);
            var px = Enumerable.Repeat(Color.white, 16 * 16).ToArray();
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
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

        // St Peters is the GAME'S START scene, so it must be build-index-0 (the scene that boots on Play in
        // a player build). Insert it at the front (moving an existing entry to index 0 if a prior run added
        // it at the end), so the persistent core boots here. Idempotent on re-runs.
        static void RegisterScene(string path)
        {
            var list = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void EnsureFolders()
        {
            foreach (var f in new[] { DataRegions, DataConfig, DataBoats, DataFish, ArtSprites, Scenes })
            {
                if (AssetDatabase.IsValidFolder(f)) continue;
                var parent = Path.GetDirectoryName(f).Replace('\\', '/');
                var leaf = Path.GetFileName(f);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }
}
#endif
