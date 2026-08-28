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
    /// two are inverse over the tide. St Peters runs a BIG tide amplitude (~2.2 m) vs the cove's gentle one,
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
        const string DataBait    = "Assets/_Project/Data/Resources/Catalog/Bait";     // the trap bait (herring/fish-scrap/mackerel)
        const string DataTools   = "Assets/_Project/Data/Tools";    // the rod + clam shovel you pick up and carry
        const string DataNpcs    = "Assets/_Project/Data/NPCs";     // the opening cast (Aunt Ginny, Ned's letter)
        const string DataGear     = "Assets/_Project/Data/Resources/Catalog/Gear";      // the rod on the store's counter (§7.5)
        const string DataLicenses = "Assets/_Project/Data/Resources/Catalog/Licenses";  // the clam licence the store vends (§7.5)
        const string DataSupplies = "Assets/_Project/Data/Resources/Catalog/Supplies";  // the ice the store restocks (§7.3/§7.5)
        const string DataInstruments = "Assets/_Project/Data/Resources/Catalog/Instruments"; // helm instruments the counter fits (ADR 0025 S2)
        const string ArtSprites  = "Assets/_Project/Art/Sprites";
        // Opening-cast art (greybox; final St Peters storekeeper etc. are on the owner's draw-list).
        const string ArtGinny         = "Assets/_Project/Art/Characters/Ginny.png";   // Aunt Ginny standee
        // ⚠ ORPHANED BY #557, kept only so the paths are findable. The screen-space panel and the
        // nameplate were deleted when the bubble became anchored art with no portrait; the speech
        // bubble's art now comes from the baked kit via DialogueBubbleArt.Dress. Nothing reads these.
        const string ArtDialoguePanel = "Assets/_Project/Art/UI/DialoguePanel.png";   // unused since #557
        const string ArtNamePlate     = "Assets/_Project/Art/UI/NamePlate.png";       // unused since #557
        const string ArtSea      = "Assets/_Project/Art/Tilesets/Water/SeaTile.png";
        const string ArtWaterMat = "Assets/_Project/Art/Materials/Water.mat";   // the layered SIM-driven water shader (ADR 0010)
        const string ArtTerrainSplatMat = "Assets/_Project/Art/Materials/TerrainSplat.mat"; // the splat-shaded ground (ADR 0028)
        const string DataTerrain        = "Assets/_Project/Data/Terrain";
        // (The WaterOverlay + ADR 0017 weather-preset paths that stood here are gone with the local water
        // wiring — they live once now, in WaterSceneTemplate, which is what loads them for both land
        // regions. The BASE/calm anchor is still deliberately left UNWIRED there, so the calm sea is the
        // owner's own LIVE Water.mat rather than a frozen preset copy of it.)
        // (The single Grass.png / Sand.png fills are gone with the greybox ground patches — the island's
        // ground is painted from the shoreline-ISO v8 kit now; see StPetersShorePainter.)
        // ⭐ ArtCottage / ArtCottageNight are GONE with the greybox standee they drew (2026-08-16). Ginny's
        // cottage is a village-kit build on her own plot now, drawn from the kit's baked per-facing
        // sheets. Cottage.png and CottageNight.png are still on disk and still the only lit-window pair
        // the project owns — which is exactly the art ask the move left open: the kit bakes no night
        // sheet, so the promoted cottage keeps its window GLOW but loses the lit PANES.
        const string ArtClamHole   = "Assets/_Project/Art/Sprites/ClamHole.png";    // the still dig-spot sprite
        const string ArtClamSquirt = "Assets/_Project/Art/Sprites/ClamSquirt.png";  // 4-frame "two squirts" tell
        // The carried tools' greybox pictures — already committed, 32 px, sliced, pivot centred. These are
        // what a tool looks like lying on the sand and hanging at the hip until the directional kits bake.
        const string ArtToolShovel = "Assets/_Project/Art/Sprites/Gear/Shovel.png";
        const string ArtToolRod    = "Assets/_Project/Art/Sprites/Gear/Rod.png";
        const string Scenes      = "Assets/_Project/Scenes";
        const string SceneName   = "StPeters";
        const string ScenePath   = Scenes + "/" + SceneName + ".unity";

        /// <summary>
        /// ⭐ THE DEV PICKER'S CYCLE — file names under <see cref="DataBoats"/>, in the order F walks them.
        ///
        /// <para><b>This list is the roster, and it is PUBLIC so the tests read the same one the builder
        /// builds from.</b> It used to be an inline array, and the in-memory mirror in
        /// <c>DevBoatPickerTests</c> drifted from it silently — the mirror still said seven rungs long after
        /// the real cycle had reached fifteen, so the count it asserted was folklore. Reading this array is
        /// the only way for a test to make a claim about the cycle's SHAPE that cannot rot: add a rung here
        /// and the guard grows with it.</para>
        ///
        /// <para>The order is deliberate and is NOT one rule. Rungs 1–11 are a SPEED ramp — the dory at
        /// 1.7 m/s up to the Zodiac Hurricane at 5.9, because at this end of the fleet what a press of F
        /// should buy you is "faster than the last one". From the Cape Islander on it is a SIZE ladder —
        /// 12.9 m to the tanker's 110 — because up there the boats differ by an order of magnitude in
        /// displacement and speed stops being the interesting axis (the tanker is the SLOWEST powered hull
        /// afloat). The two sport fishers are placed by length inside the upper ladder even though they
        /// out-run their neighbours, so the size story stays readable.</para>
        ///
        /// <para><c>Punt</c> is builder-generated and NOT committed, so on a clean clone she loads null and
        /// the roster is one shorter. That is why the builder filters nulls and warns rather than throwing,
        /// and why every test that counts rungs counts what actually LOADED.</para>
        /// </summary>
        public static readonly string[] DevPickerRosterFiles =
        {
            // --- the speed ramp: the M1 slice, then the planing craft ---
            "Dory",
            // ...and the SAME dory with the used kicker on her transom (D8), next to her rowed self on
            // purpose: it is the M1 slice's closing rung, and the only way to feel what the outboard
            // actually buys is to press F once and still be in the same boat.
            "DoryOutboard",
            "FishingSkiff",
            "Punt",
            "PuntUpgraded",
            "ConsoleSkiff",
            "SportSkiff",
            // The reshaped sport skiff, immediately after her v1 sister — which is BOTH things at once:
            // she is the v1 hull re-lofted (7.0 m still, but a chined deep-V — beam 2.30 → 2.54, sole
            // 0.28 → 0.46), so back-to-back with SportSkiff is the only way to feel the reshape; and at
            // 5.19 m/s she keeps the ramp monotonic on the way up to the twin's 5.63.
            "SportSkiffMk2",
            "SportSkiffTwin",
            // The two coast-guard RIBs close the ramp as the fastest things afloat (5.70 / 5.89 m/s —
            // deliberately under BowSprayGrading's 6 m/s ceiling, or they would outrun their own spray
            // art). They are SHORTER than the skiffs they follow: this is the clearest case of the ramp
            // being ordered by speed, because a planing RIB slotted at her 6.7/7.3 m length would break it.
            "ZodiacFrc",
            "ZodiacHurricane",
            // --- the size ladder: 12.9 m to 110 m ---
            "CapeIslander",
            "LobsterBoat",
            // THE LOBSTER FAMILY, THREE RUNGS FOR EIGHTEEN HULLS. All 18 variants are authored as Defs,
            // but SIZE is the only axis that changes how one sails: style (open/hardtop) moves no hull
            // metric at all, and region moves beam by ±2% — ±0.01 of liveliness (fleet-flotation.md §3
            // measured both). So the picker carries one rung per size and the other 15 exist for moorage
            // and for M2's fleet roster. Ordered small→large inside the family, immediately after the
            // lobster boat they are variants OF — and the standard rung is HER OWN metrics under
            // different art, which is what makes the other two mean something.
            "LobsterInshoreOpenNorthumberland",
            "LobsterStandardHardtopNorthumberland",
            "LobsterOffshoreOpenNorthumberland",
            // The two sport fishers take their places by LENGTH: 16.2 m puts the convertible between the
            // lobster boat and the dragger, 27.4 m puts the skybridge between the dragger and the
            // trawler. Both are the fast outliers of their neighbourhood (5.39 / 4.98 against the
            // dragger's 3.48) — the point of a battlewagon, and felt at once cycling into one off a workboat.
            "SportFisherConvertible",
            "SideDragger",
            "SportFisherSkybridge",
            "SternTrawler",
            "SternTrawlerMk2",
            "CoastalPacket",
            "Tanker",
        };

        // --- St Peters region tunables (authored data; mirrored onto the RegionDef + TidalTerrain + Env) ---
        // The opening's defining feature is a BIG tide vs the cove's gentle 1.6 m — the bar must visibly bare
        // and flood. Mean 0 (chart datum centred), amplitude 2.2 m → water swings ≈ -2.2 .. +2.2 m.
        //
        // ⭐ 2.2, NOT 3.5 — the owner's TIDE-PACING ruling (2026-08-01): "the tide falls too fast", fixed
        // with BOTH offered levers at once. This is the amplitude lever; the other is the day length
        // (GameConfig.DefaultSecondsPerDay 1200 → 1800). The peak rate of the water level is
        //     amplitude × 2π / TidalPeriodHours / SecondsPerHour,
        // so the two levers multiply: 3.5→2.2 is ×0.63 and 50→75 s/in-game-hour is ×0.67, together
        // ~3.5 cm/s → ~1.5 cm/s of real-time water movement (≈2.4× slower). It is still a BIG tide next
        // to the cove's 1.6 m, and every elevation whose meaning is a TIDE FRACTION scales with it
        // (SandbarCrestElevation below, StPetersShoreMap.BarSpineFloorElevation) so no in-game-hour
        // window the owner tuned moves. Elevations whose meaning is HULL DRAUGHT or WADING DEPTH
        // deliberately do NOT scale — see the note on ChannelBedElevation / BerthBedElevation below.
        public const float TideMean = 0f;
        public const float TideAmplitude = 2.2f;
        public const float TidePhaseHours = 1f;

        // --- REGION EXTENT (authored here, PUBLISHED on the RegionDef, read by everyone else) ---------
        // ⭐ The region's rectangle used to be the literal `new Vector2(160f, 120f)` written out in four
        // places in this builder and two more in TerrainPaintTool — so the sea plane, the shader's height
        // bake, the displaced mesh and the painted seabed each carried their own copy of the same
        // decision, and resizing the region meant finding all six (scene-sizing §6.2). They now all read
        // ONE authored number, mirrored onto RegionDef.WorldSizeMeters the same way the tide fields are.
        //
        // ⭐ 760 × 520 m — the RULED size (scene-sizing §5.1/§7.1, owner 2026-07-23), sized by
        // TIME-TO-CROSS rather than by feel. It replaces a 160 × 120 m greybox whose whole island was
        // a 44 m disc — smaller than the sandbar it is gated by. ⚠ The 2026-07-30 island re-ruling
        // (240 × 140 m, ~1/3 the old landmass) changed the ISLAND ONLY: this rectangle is unchanged,
        // and the difference is open water — which is the ruling's whole point.
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
        // ⚠ An ELLIPSE, not a disc: ~240 × 140 m of landmass is the RULED scale — RE-RULED 2026-07-30
        // from the earlier 450 × 260 (the owner: the island felt too large; he wants MORE OPEN WATER
        // around it). The linear span roughly halves, so the AREA lands at ~29% of what it was —
        // between the 1/3 and 1/4 he asked for — while the surrounding sea/region rectangle stays
        // exactly 760 × 520 m. TidalTerrain's Y radius still earns its keep: an island is longer than
        // it is wide, and 240 × 140 is not a disc at any radius.
        public const float IslandRadius            = 120f;   // semi-axis along X → 240 m end to end
        public const float IslandRadiusY           = 70f;    // semi-axis along Y → 140 m across
        // The beach band, in METRES of shore — not a fraction of the island. 20 m carries the plateau
        // down to the reef shelf below at about 1:3, still reading as the brief's steep red-sandstone
        // coast, and it keeps the shore-band ladder legible at the smaller scale: against the
        // StPetersShoreMap floors (grass 4.2 / marram 1.6 / sand −0.4 / ripple −1.7) the smoothstep
        // profile gives ~6 m of grass lip, ~5.5 m of marram, ~4.5 m of sand and then the flats —
        // measured off the analytic profile, not eyeballed (elev at radius+6 = 4.49, +10 = 2.50,
        // +14 = 0.51, +16 = −0.27). The old 30 m band on the old island gave the same rings ~1.5×
        // wider; shrinking the beach with the island keeps beach:island proportion instead of letting
        // the falloff eat a third of the new radius.
        public const float IslandFalloff          = 20f;
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
        // ⚠ Width is NOT in the ruling — it is what the scene has room for. On the old 450 m island
        // the budget was exact (310 m from centre to the east edge, less 225 island + 30 beach +
        // 30 drop-off = 25 m of apron); the 2026-07-30 shrink frees ~100 m of sea room, but the
        // shelf keeps its 25 m: the ring is a DEPTH gate, not a decoration, and widening it would
        // only stretch the crossing at the same depth while eating the open water the shrink was
        // ruled to create. The DEPTH numbers are the ruled ones and are untouched.
        public const float ReefShelfWidth          = 25f;

        /// <summary>
        /// How steeply this island shelves, for the displaced sea's shore-fade band (ADR 0023). Sizes the
        /// band that keeps the displaced mesh from TEARING where the ground climbs out from under it.
        ///
        /// <para><b>A MEASUREMENT of this coast, carried unchanged.</b> The plateau falls to the reef shelf
        /// over <see cref="IslandFalloff"/> metres — 7 m over 20, a mean of 0.35 — and a smoothstep's
        /// gradient peaks at 1.5× its mean, so the island's steepest metre is 0.525. It has always been
        /// carried here at 0.5 (the <c>DisplacedWaterSurface</c> field default this builder relied on before
        /// the water wiring moved into <see cref="WaterSceneTemplate"/>), and it stays at 0.5: naming a
        /// number is not licence to move it, and this region's look is not the Nine Mile Creek water PR's to
        /// change. Overestimating only widens the band, so the 5% under-read is the safe direction anyway.</para>
        ///
        /// <para>A MAINLAND region derives its own instead — see
        /// <c>MainlandTidalTerrain.MaxShoreGradient()</c> — because a coast plan that stands cliffs off the
        /// bay floor shelves an order of magnitude harder than this island does, and 0.5 there is the
        /// direction of the error that tears.</para>
        /// </summary>
        public const float ShoreGradient           = 0.5f;

        // The berth: §5.1a's "dock approach / berth bed ≈ −1.0 m", which clears the 0.6 m tier whenever
        // the water is above −0.4 m and DRIES near spring low — so the BEACH SLIP keeps its own gentle
        // tide gate, and wading out to it means reading the tide.
        //
        // ⚠ §5.1a's other half — "even coming home under power means reading the tide" — was OVERRULED
        // for this dock on 2026-08-19 (see the dredged approach below). The slip's own numbers are
        // unchanged; what changed is that the slip is no longer the only water here.
        public const float BerthBedElevation       = -1.0f;
        // ⚠ Also not in the ruling: a slip wide enough for the skiff tier to enter without threading a
        // needle, narrow enough to read as one door in the ring rather than a gap in it.
        public const float BerthHalfWidth          = 8f;
        // ⚠ The seaward end must reach PAST the shelf's outer edge into the drop-off, or the slip is a
        // dead-end pocket inside the reef that nothing can enter — which is exactly what the first
        // version was, and what the ring test caught. The shelf ends 165 m from the island centre
        // (120 island + 20 beach + 25 shelf), so the mouth sits at 170 — measured −1.89 m, genuinely
        // in the drop-off. The shoreward end is AT the land edge (IslandRadius), so the slip reaches
        // the beach; the carve's own falloff ramps it up onto the shore from there.
        public static readonly Vector2 BerthFrom    = new Vector2(240f, 0f);   // 170 m out — past the reef
        public static readonly Vector2 BerthTo      = new Vector2(190f, 0f);   // 120 m out — the shoreline

        // ================================================================================================
        //  ⭐ THE DREDGED APPROACH — "the east dock always has water" (OWNER RULING, 2026-08-19)
        // ================================================================================================
        // The owner ruled that St Peters' east dock holds water at EVERY state of tide, spring low
        // included, because it is the game's front door: a new game pilots you in aboard a cape islander
        // and puts you ashore on these planks, and an arrival that grounds on its own doorstep at dead
        // low tide is not an opening. It is stated here as GEOMETRY — a dredged channel and a berth
        // pocket — rather than as a special case in the sim, so the water the player reads is the water
        // the hull floats in (ADR 0014: paint = sail).
        //
        // ⚠ WHAT THIS DOES **NOT** CHANGE. The reef ring still bares (ReefShelfInner/Outer −1.0/−1.5),
        // the flats still bare, the bar guts still bare, and the BEACH SLIP above still dries near
        // spring low. The §5.1a draught ladder is untouched: the cut is 16 m wide on the SAME line as
        // the slip, so the island still has exactly ONE door, and a hull too deep for the ring is still
        // a hull that cannot come home. What changed is that the one door is now dredged.
        //
        // --- THE ARITHMETIC (re-derived by StPetersEastBerthTests, never trusted to this comment) -----
        //   spring low water                = TideMean − TideAmplitude       = 0 − 2.2   = −2.20 m
        //   the arrival hull                = boat.cape_islander, DraughtMeters          =  1.40 m
        //   under-keel clearance            = BerthUnderKeelClearance                    =  0.40 m
        //   REQUIRED bed                    = −2.20 − (1.40 + 0.40)                      = −4.00 m
        //   authored bed                    = ApproachBedElevation                       = −4.00 m
        //
        // ⭐ THE CLEARANCE IS NOT INVENTED. 0.40 m is exactly what this region's own deep-harbour floor
        // (DeepHarbourElevation −4.0 m) gives that hull at that tide: −2.20 − (−4.00) − 1.40 = 0.40. So
        // the channel offers the water the APPROACH offers and not a centimetre more — the dredge digs
        // the slip down to the harbour floor and stops. Naming it as a requirement rather than writing
        // "dig to the floor" is what makes the whole thing re-derive if the tide, the floor or the
        // arrival hull ever moves: the test fails WITH the number to use.

        /// <summary>
        /// Water under the arrival hull's keel at the LOWEST water, in metres — the margin the dredged
        /// approach is cut to hold. See the arithmetic above for why it is 0.40 and not a taste.
        /// </summary>
        public const float BerthUnderKeelClearance  = 0.4f;

        /// <summary>
        /// The hull the opening is sailed in on — the def id, so the sequencer that drives the
        /// arrival and the dredge that has to float her cannot come to disagree about which boat
        /// this channel was cut for.
        /// </summary>
        public const string ArrivalHullId = "boat.cape_islander";

        /// <summary>
        /// Her draught, in metres. ⚠ A MIRROR of <c>CapeIslander.asset</c>'s <c>DraughtMeters</c> and
        /// not a second opinion: a <c>const</c> cannot load an asset, and the seabed arithmetic below
        /// has to be a compile-time expression. StPetersEastBerthTests holds the two equal, so moving
        /// the def and forgetting this one fails loudly instead of quietly leaving her aground.
        /// </summary>
        public const float ArrivalHullDraughtMetres = 1.4f;

        /// <summary>Seaward end of the dredged approach — 185 m out, ON the −4 m contour, so the cut
        /// lands flush on the harbour floor instead of ending in a sill nothing can cross. (The beach
        /// slip's own mouth at <see cref="BerthFrom"/> measures −1.89 m, which is why it was a door
        /// that shut twice a month.)</summary>
        public static readonly Vector2 ApproachFrom = new Vector2(255f, 0f);

        /// <summary>
        /// Shoreward end of the dredged approach — the head of the berth pocket.
        ///
        /// <para>⚠ 206 is MEASURED against two things, not chosen. (a) The arrival hull is 12.9 m long
        /// and lies at <see cref="DoryMooredPos"/> (215, 0), so her bow reaches 208.6 — the pocket's
        /// flat bottom runs 4 m past this end (the cut is a capsule, not a segment), so it holds her
        /// whole length with room over. (b) The far side: the cut's shoulder dies out 8 m west of here,
        /// at x = 198, which is 15 m clear of <see cref="StPetersWharf.RootCellX"/> — so the pier root's
        /// MEASURED +5.35 m deck elevation, and the dry ground the pier launches from, do not move by a
        /// millimetre. Both are asserted, not trusted.</para>
        /// </summary>
        public static readonly Vector2 ApproachTo   = new Vector2(206f, 0f);

        /// <summary>Half-width of the dredged cut — the SAME 8 m as the slip it shares a line with, so
        /// the reef still reads as having exactly one door through it.</summary>
        public const float ApproachHalfWidth        = 8f;

        /// <summary>
        /// Half-width of the approach's FLAT dredged bottom — the navigable width the channel keeps at
        /// the lowest water, 8 m of it against the cape islander's 4.80 m beam.
        ///
        /// <para>⚠ This is the constant a pure falloff curve cannot express. Carving to −4 m with the
        /// old single-slope trough gives the design depth at ONE point and only 3.7 m of water wide
        /// enough to swim her at spring low — narrower than she is. The flat states the width; the
        /// shoulders beyond it still close in as the tide drops (8.00 m at spring low → 11.5 at neap
        /// low → 13.4 at mean → the whole shelf floods above that), which is the owner's "shrinks in
        /// width at low tide but stays navigable" exactly.</para>
        /// </summary>
        public const float ApproachThalwegHalfWidth = 4f;

        /// <summary>Bed of the dredged approach — see the arithmetic above. Equal to
        /// <see cref="DeepHarbourElevation"/> by construction, not by coincidence — and written as
        /// the arithmetic rather than as its answer, so it cannot be right today and stale tomorrow.
        /// (In float this folds to exactly −4.0; the test says so rather than trusting it.)</summary>
        public const float ApproachBedElevation =
            TideMean - TideAmplitude - (ArrivalHullDraughtMetres + BerthUnderKeelClearance);
        // West end of the island's land → west toward the passage. (From is ON the island so the bar
        // actually joins it — at (−45, 0) the elliptical distance is 115 against a 120 radius, so the
        // bar head stands on the plateau; To is short of the scene edge so the passage band has room.
        // ⭐ To STAYS at −350 through the 2026-07-30 shrink: the Nine Mile Creek walk passage lives at
        // that end of the region, so the bar GROWS from 200 m to ~305 m as the island's west shore
        // retreats east — more bar, more flats, more open water, which is the ruling's whole point.)
        public static readonly Vector2 SandbarFrom  = new Vector2(-45f, 0f);  // toward the island
        public static readonly Vector2 SandbarTo    = new Vector2(-350f, 0f); // toward Nine Mile Creek
        public const float SandbarHalfWidth        = 30f;
        // ⚠ THE CREST IS A TIDE FRACTION, NOT A HEIGHT. Everything the owner tuned about the crossing —
        // that the bar floods at EVERY tide, and how long it is dry at spring vs at neap — depends only on
        // the RATIO crest/amplitude, because the water is a sinusoid: the bar is dry while sin θ < ratio,
        // and that set of θ has no other input. So when the amplitude moves, this moves with it or every
        // window silently changes.
        //
        // ⭐ 0.88 = 0.4 × amplitude, holding the ratio the 2026-07-23 ruling (#280) settled at 1.4/3.5.
        // Under the 2026-08-01 amplitude change (3.5 → 2.2) that keeps BOTH properties exactly:
        //   · crest 0.88 < neap high water (NeapAmplitudeFraction 0.45 × 2.2 = 0.99) → the bar still
        //     floods at every point in the lunar month, so the tide gate never switches itself off;
        //   · the spring dry window keeps its IN-GAME duration to the second (7 h 50 m of game time —
        //     63.1% of the cycle, unchanged), and its REAL duration grows ×1.5 with the day length,
        //     6:31 → 9:48, along with everything else the day-length lever stretched.
        // The gradient survives too: neap is still the FORGIVING end (long dry bar, brief flood), spring
        // the TENSE one. See docs/design/scene-sizing-and-world-scale.md §5.2 and the neap-gap +
        // window-invariant tests in StPetersTerrainTests / TidePacingInvariantTests.
        public const float SandbarCrestElevation    = 0.88f;  // < neap high water (0.99) → floods at EVERY tide
        public const float ChannelAlong            = 0.62f;
        public const float ChannelHalfWidth        = 15f;
        public const float ChannelBedElevation      = -0.6f;  // a gut: boat-crossable high, narrows as tide falls

        // ⚠ THE DEPTH ELEVATIONS ABOVE DO NOT SCALE WITH THE TIDE, and that is deliberate (2026-08-01).
        // DeepHarbourElevation, ReefShelfInner/Outer, BerthBedElevation and ChannelBedElevation mean
        // HULL DRAUGHT and WADING DEPTH — a −1.0 m berth bed is "a 0.6 m draught clears it above −0.4 m",
        // a statement about boats, not about the tide. Scaling them with the amplitude would silently
        // re-gate the whole boat ladder. What DID change is their EXPOSURE, because spring low water rose
        // from −3.5 m to −2.2 m: every one of them now bares somewhat less, and two crossed a threshold —
        // at NEAP the swing is only ±0.99 m, so the reef apron (−1.0) and the berth bed (−1.0) no longer
        // dry at all during a neap week (they used to bare for ~28% of a neap cycle under ±1.575). They
        // still dry for ~35% of a SPRING cycle, so "the berth dries near spring low" — the §5.1a gate the
        // ruling actually cares about — is intact; what is gone is a neap-week drying nobody specified.
        // The full before/after table is in the PR body; the invariants are pinned by TidePacingInvariantTests.

        // =================================================================================================
        //  THE COAST PLAN — which stretch of shoreline stands up (owner-tunable; the builder pushes it)
        // =================================================================================================
        // Bearings are COMPASS degrees (N = 0, clockwise) in the island's ellipse-normalised frame, the
        // same frame TidalTerrain.IslandDistance and StPetersShoreMap.IsWeatherCoast measure in. Each
        // entry starts a sector that runs clockwise to the next, so the ring cannot be authored with a
        // gap or an overlap. To retune the coast, move these numbers and re-run the builder — nothing
        // else needs touching, and StPetersCoastTests re-derives every claim from this table.
        //
        // ⭐ WHY THE PLAN LOOKS LIKE THIS — the three constraints, in the order they bind:
        //
        // 1. THE ASPECT LAW. The cliff kit authors five outward facings (W·SW·S·SE·E) and no north,
        //    because a north-facing wall shows its shadowed back to a ¾ top-down camera. With the kit's
        //    22.5° snap a cliff may stand only where its seaward NORMAL bears 67.5°…292.5°. On this
        //    120 × 70 m ellipse that is bearings 76.4°…283.6° — MEASURED 55.6% of the shoreline, not the
        //    62% the compass suggests, because a wide island's outward normal swings toward the short
        //    axis faster than its radial bearing does. The whole north coast is therefore beach and dune
        //    by law, which is also exactly what the region docs already asked for (§5.1: "beaches,
        //    sandbar, clam flats → the intertidal west/north").
        //
        // 2. THE TWO CROSSINGS. The sandbar leaves the WEST end (bearing 270) and bares to ±17.7 m of its
        //    centre-line at spring low; the berth is the ONE door in the reef and lands at the EAST end
        //    (bearing 90) with a ±8 m channel. Both must be walkable/floatable shore, so both are beach
        //    inside the legal arc — 5.78% and 2.65% of the shoreline respectively. They are, fortunately,
        //    at the two CHEAPEST points on the ellipse: a degree near the short axis buys 70 m/rad of
        //    coast where a degree near due south buys 120.
        //
        // 3. NO CLIFF RUN SHORTER THAN ONE FACE TILE (12 m of surface). A shorter run cannot show the
        //    kit's periodic wall even once, so it reads as a texture accident rather than as rock. That
        //    rule alone costs 1.45%: the 9 m of legal arc between 76.4° and the harbour cove is too short
        //    to stand up, so the north coast's beach simply wraps on into the harbour.
        //
        // WHAT THAT ADDS UP TO — and it is a correction to the handoff, which asked for ~50%:
        //    55.56% legal − 5.78% bar landing − 2.65% berth cove              = 47.13% CEILING
        //    − 4.73% access trails (§3.4 requires them) − 1.45% min-run
        //    − 0.74% landing margin (see the 252° note below)                 = 40.20% AUTHORED
        // 50% is not expressible on this island: it exceeds even the ceiling, which is reachable only by
        // a coast with no way down to the shore anywhere. Every point of the shortfall is named above and
        // re-derived by StPetersCoastTests.TheCliffShareIsAboutHalfTheCoast_AndSitsUnderTheAspectLawsOwnCeiling
        // rather than written down as a literal.
        public static readonly CoastSector[] CoastSectors =
        {
            // The sheltered north — the intertidal coast §5.1 asks for. Illegal for cliff by the aspect
            // law, so nothing here is a choice; it is the law wearing the docs' clothes.
            new CoastSector(  0.00f, CoastClass.Beach),
            new CoastSector( 30.00f, CoastClass.Dune),

            // The harbour cove. Wraps out of the north coast and holds the berth (bearing 90).
            new CoastSector( 76.42f, CoastClass.Beach),

            // The east weather face — deep-shore, so the dory can lie close alongside under the rock.
            new CoastSector( 96.60f, CoastClass.DeepShoreCliff),

            // The east trail: a gully down to the foreshore, and the way onto the ledge run below.
            new CoastSector(128.00f, CoastClass.Access),
            new CoastSector(136.00f, CoastClass.LedgeCliff),

            // Due south — the tallest, plainest wall, with its foot below the lowest water.
            new CoastSector(172.00f, CoastClass.Cliff),

            // The west trail, and the second ledge run it opens.
            new CoastSector(205.00f, CoastClass.Access),
            new CoastSector(213.00f, CoastClass.LedgeCliff),

            // The west face falls straight to the harbour floor, then the ground opens out for the bar.
            new CoastSector(240.00f, CoastClass.DeepShoreCliff),

            // The bar landing — the crossing to Nine Mile Creek leaves here (bearing 270). It starts at
            // 252° and not at the 255.4° where the bar's BARED width strictly ends, because the bar's
            // south edge lands within a whisker of that bearing and a landing with no margin is a
            // landing that a later nudge silently walls off. 252° covers the crossing out to 21.6 m
            // either side of its centre-line against the 17.7 m it actually bares.
            new CoastSector(252.00f, CoastClass.Beach),
            new CoastSector(283.58f, CoastClass.Dune),
        };

        /// <summary>Degrees of bearing the sector joins are feathered over. Butted hard, a cliff meeting
        /// a beach is a 6.5 m step ACROSS the shore — a wall running out to sea. Every sector must be at
        /// least twice this wide, which <c>StPetersCoastTests</c> asserts against the table above.</summary>
        public const float CoastBlendDegrees = 3f;

        /// <summary>How far (m of elliptical distance) a cliff takes to fall its full height. This is
        /// the wall: the beach next door spends 20 m on a seventh of the same drop.</summary>
        public const float CliffPlungeWidth = 3f;

        /// <summary>A plain cliff's foot, as a fraction of the tide's amplitude BELOW the lowest spring
        /// water: −3.19 m today. Below the water at every tide, so no part of a plain face ever bares,
        /// but shoal enough that lying alongside one is not the same offer the deep-shore faces make.
        ///
        /// <para>⚠ Not merely "below the lowest water" — below it by more than a WADE. At 0.25 the foot
        /// sat at −2.75 m and spring low water is −2.2 m, which put 0.55 m of water over it against a
        /// 0.5 m wade depth: a 5 cm margin between "a wall" and "a wall you can paddle round the bottom
        /// of at dead low". The margin is now 0.5 m, and the test that measures it says so.</para></summary>
        public const float CliffToeTideFraction = 0.45f;

        /// <summary>The low-tide ledge's bench, as a fraction of the amplitude ABOVE the lowest spring
        /// water: −1.65 m today.
        ///
        /// <para>⚠ A TIDE FRACTION, NEVER A METRE — the 2026-08-01 pacing ruling. When the amplitude fell
        /// 3.5 → 2.2 m every absolute elevation in this file had to be re-audited by hand and one of them
        /// (the paint floor) had actually broken. A fraction moves with the water instead.</para>
        ///
        /// <para>⚠ AND IT IS A SPRING-TIDE BENCH. At 0.25 the bench bares for ~23% of a SPRING cycle —
        /// about 1.4 h either side of dead low — and never during a neap week at all, because neap low
        /// water (−0.99 m) does not reach it. That is a deliberate P1/P5 statement (the sea has moods;
        /// the tide lends you the ledge on the big tides only) but it IS a design call, and the owner
        /// should rule on it: raising this above 0.55 would make the ledges bare every day and lose the
        /// distinction.</para></summary>
        public const float LedgeBenchTideFraction = 0.25f;

        /// <summary>Width (m) of the ledge bench — a shelf the tide lends you, not a beach.</summary>
        public const float LedgeBenchWidth = 4f;

        // --- CLAM-HOLE scatter (deterministic; single source of truth shared with the EditMode test) ------
        // Holes scatter over the bar's footprint on a jittered grid, kept only where the authored ground is
        // INTERTIDAL — between the lowest and highest water of the swing (mean ∓ amplitude), so a hole bares
        // as the tide falls and floods as it rises. ClamScatterStep = grid spacing; ClamScatterJitter = the
        // max deterministic offset (hashed off the cell, no RNG) that breaks the grid look. The band is the
        // tide swing inset by a small margin so a hole isn't perpetually at the very waterline edge.
        // ⚠ Scaled WITH the bar. At 6 m over the original 56 × 34 m greybox footprint this was ~54
        // candidate cells; left alone over today's ~345 × 100 m footprint (the bar grew again with the
        // 2026-07-30 island shrink — the west shore retreated east while the passage end stayed put)
        // it would be ~960, i.e. ~960 GameObjects with sprites and colliders in one scene (rule 7).
        // 14 m keeps the kept field at a measured ~85 and still reads as scattered rather than
        // gridded once the jitter is applied.
        public const float ClamScatterStep   = 14f;    // one candidate hole per ~14×14 m cell over the bar
        public const float ClamScatterJitter = 4.5f;   // ± world units of stable hash jitter per cell
        public const float ClamBandMargin     = 0.4f;   // inset (m) from the extreme water levels (kindness band)
        // The bar footprint to scatter over (a margin around the bar so the flats either side are covered).
        public const float ClamScatterMargin = 20f;

        // Player START spawn — on the island's WEST half, so the bar head is in sight and the opening's
        // first walk reads as leaving home. (Moved with the 2026-07-30 shrink: the whole village
        // cluster translated +105 in X — exactly the distance the bar head moved (−150 → −45) — so
        // every internal spacing AND every distance-to-the-bar margin is preserved to the metre.)
        public static readonly Vector3 StartSpawnPos = new Vector3(5f, 0f, 0f);
        // Where the walk path reaches Nine Mile Creek — a forgiving band at the WEST end of the bar,
        // past its far tip and short of the scene edge.
        public static readonly Vector3 ToNineMileCreekPassagePos = new Vector3(-356f, 0f, 0f);

        // --- ⭐ THE SEA DOOR: out west into THE WEST WATER (world-map-plan §6 step 1) -------------------
        // The island's second way off, and its first BY BOAT. The bar at y = 0 is the WALK; this is the
        // sail — 150 m north of the bar's centre-line, which puts it in open water on three counts at
        // once: 150 m clear of the bar's 30 m half-width (so the band is never on the crossing, and never
        // on ground that bares), 80 m clear of the island's northern reef apron, and squarely on the
        // deep-harbour floor the west water borrows for its own lee shelf. A seam in featureless water
        // clear of hazards, which is the rule the bay is being built to (world-map-plan §4.4).
        //
        // ⚠ NOT a full-height band like the west water's own doors. This is a LAND region: its west edge
        // carries the crossing, and a band spanning the region would swallow the walk passage below it.
        // 120 m tall leaves a 60 m gap between the two, which is more than a hull's width of daylight at
        // the one place both could plausibly fire.
        public static readonly Vector3 ToWestWaterPassagePos = new Vector3(-356f, 150f, 0f);
        public static readonly Vector2 WestWaterPassageBandSize = new Vector2(6f, 120f);

        // --- THE VILLAGE (the authoring pass StartSpawnPos above was waiting for) ---------------------
        // ⭐ Every one of these used to sit within a few metres of (-40, 0) — the centre of the 44 m
        // greybox disc the island WAS before #328 rescaled it. #345 moved the village onto the (then
        // 450 × 260 m) landmass; the 2026-07-30 shrink to 240 × 140 moved the west shore ~105 m east,
        // so the whole cluster TRANSLATES +105 in X — the same distance the bar head moved — which
        // preserves every internal spacing, every clearance the village tests derive from the
        // building contract, and every sightline margin to the bar, all to the metre.
        //
        // The village stands where the docs put it: on the island's WEST half beside the start spawn —
        // "Quiet, close, the whole world the size of a low-tide walk" (§6.0) — tight enough to read as one
        // small place, and within sight of the bar head the opening walks out across. Every position is on
        // the plateau (+6 m, dry at every tide), which StPetersVillageTests asserts against the authored
        // terrain rather than trusting these numbers.
        /// <summary>
        /// <b>THE VILLAGE HEARTH — the anchor, not a building.</b> This was <c>CottagePos</c>, and Ginny's
        /// cottage stood on it until the owner moved her into the woods on 2026-08-16
        /// (<see cref="StPetersGinnyPlot"/>). <b>The VALUE has not moved and must not</b>: five separate
        /// systems read this point meaning "the middle of the village", not "where the aunt lives" —
        /// <see cref="VillageGreen"/> is its midpoint with the spawn, <see cref="StPetersWoods.IsPlantable"/>
        /// centres the village's 44 m no-plant clearing on it, <see cref="StPetersVillage.RequiredClearingRadius"/>
        /// measures building reach from it, <see cref="StPetersShops"/> measures back to it, and the grass
        /// and decor keepouts ring it. Moving it with the cottage would have swung every villager's door
        /// facing east and planted the woods through the schoolhouse. So the cottage left and the anchor
        /// stayed, renamed for what it always meant.
        /// </summary>
        public static readonly Vector3 VillageHearthPos = new Vector3(0f, 14f, 0f);

        /// <summary>
        /// Ginny's mark IN THE VILLAGE — where she stands through the working day, and
        /// <b>the spot the opening's first conversation happens on</b>. It is unmoved, and that is
        /// deliberate: a new game starts at hour 6 (<c>GameClock._startHour</c>) with the player waking
        /// beside her, so her HOME could move to the woods but her DAY could not follow it without
        /// rewriting the opening — which is the owner's call, not this file's. Her routine walks her in
        /// from the plot before six to keep this true; see <see cref="StPetersRoutines"/>.
        /// </summary>
        public static readonly Vector3 GinnyPos      = new Vector3(4f, 11f, 0f);        // her village mark

        /// <summary>
        /// Ned's letter — the opening's other piece, and the one thing that stayed behind when the cottage
        /// went. It reads as lying on the ground where the hearth used to be, a few strides from where you
        /// wake; it is the PLAYER's letter at the PLAYER's spawn, so it belongs to the opening and not to
        /// Ginny. The vacated site gets nothing else: an empty lot is what an empty lot looks like.
        /// </summary>
        public static readonly Vector3 NedsLetterPos = new Vector3(-2f, 10.5f, 0f);     // where the hearth was
        // ⭐ The wet bucket belongs AT THE WATER, and on this island that means the HEAD OF THE FLATS — the
        // last dry ground before the sandbar's cobble spine runs out west. The ground here is +4.49 m
        // (measured off the analytic profile — the same beach-band point the old island had it at), about
        // a metre clear of spring high water, so the barrel is never swimming; a few metres west and the
        // bar itself floods twice a day. It is exactly where you come off the flats with a bucket of clams.
        // ⚠ MOVED 3 m seaward (−56 → −59) with the 2026-08-01 amplitude ruling. The barrel stands at the
        // HEAD OF THE FLATS — the last dry ground, right at the water. That is a position relative to the
        // waterline, not a coordinate: with spring high water down from 3.5 m to 2.2 m the old spot
        // (ground 4.49 m) ended up 2.3 m above the highest tide, i.e. up the beach, and
        // StPetersVillageTests caught it. At −59 the ground is 3.02 m — 0.8 m clear of spring high, which
        // is the same "dry, but only just" it had before (it was 0.99 m clear).
        public static readonly Vector3 WetBucketPos  = new Vector3(-59f, 0f, 0f);

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
        public static readonly Vector3 SchoolPos         = new Vector3(-12f,  33f, 0f);
        public static readonly Vector3 GeneralStorePos   = new Vector3(  4f,  31f, 0f);
        public static readonly Vector3 WhiteFarmhousePos = new Vector3( 21f,  26f, 0f);
        public static readonly Vector3 RedSaltboxPos     = new Vector3( 25f,   8f, 0f);
        public static readonly Vector3 SageCottagePos    = new Vector3( 10f, -20f, 0f);

        // Where the village LOOKS. Every door is turned toward this point rather than given a hard-coded
        // facing index, so the village faces itself — and so a re-bake with four facings instead of eight
        // re-derives the nearest cell instead of silently pointing five doors at a wall (the kit's own
        // warning: nothing in placement may assume a facing count). It is the ground between the hearth
        // and the spawn: the midpoint of the two, which is where the life is.
        //
        // ⚠ It is the midpoint of the HEARTH, not of Ginny's cottage. Those were the same point until
        // 2026-08-16; they are not any more, and the green stayed with the village. Reading
        // StPetersGinnyPlot.CottagePos here would drag the green — and every door on the island turned
        // toward it — 85 m east into the woods.
        public static Vector2 VillageGreen =>
            (new Vector2(VillageHearthPos.x, VillageHearthPos.y) +
             new Vector2(StartSpawnPos.x, StartSpawnPos.y)) * 0.5f;

        /// <summary>
        /// How far out past the storekeeper's own doorstep the general store's COUNTER stands, in metres —
        /// one stride nearer the lane than she is, so you meet the counter and then her behind it.
        ///
        /// <para>It has to stay well inside <c>StallGate.DefaultRange</c> (4 m, the reach every stall
        /// interaction uses) or the shop and the shopkeeper come apart into two things to walk up to —
        /// the same margin <c>NineMileCreekPeople.ByHisTruckMetres</c> keeps between the buyer and his
        /// till, for the same reason.</para>
        /// </summary>
        public const float StoreCounterStrideMetres = 1.5f;

        /// <summary>Whose counter the general store's is — the seller id every vendor on it carries, and
        /// the id a catalog row in Marguerite's dialogue opens her book against. Stock is CONTENT now
        /// (CatalogListing): the listings name this seller, so adding to her shelf is one asset and never
        /// a scene edit.</summary>
        public const string StoreSellerId = "seller.leblancs";

        /// <summary>
        /// Where the general store's counter stands: DERIVED from the store's own site, out its door
        /// toward <see cref="VillageGreen"/> and a stride past the dooryard its keeper stands in. Never
        /// typed in — move the store and its counter, its keeper and its door all move together (#345's
        /// standing lesson: a copied coordinate is a coordinate that will stop agreeing).
        /// </summary>
        public static Vector3 GeneralStoreCounterPos
        {
            get
            {
                Vector2 door = StPetersInhabitants.Dooryard(GeneralStorePos);
                Vector2 outward = VillageGreen - door;
                Vector2 spot = outward.sqrMagnitude < 1e-6f
                    ? door
                    : door + outward.normalized * StoreCounterStrideMetres;
                return new Vector3(spot.x, spot.y, 0f);
            }
        }

        /// <summary>
        /// Dresses the general store's counter in the REAL baked fixture, turned so its service face
        /// meets the customer walking up from <see cref="VillageGreen"/>.
        ///
        /// <para><b>The art only. Nothing here moves the counter.</b> Its position is
        /// <see cref="GeneralStoreCounterPos"/> and the five vendors, the market, the buyer and the sell
        /// point are all components on that one GameObject — so the stand-point every one of them is
        /// reached at is the transform this method is handed, and this method never touches it. The
        /// fixture's pivot is its GROUND CENTRE, which is why no offset is needed and why adding one
        /// would sink it: under the ¾ camera the near half of the footprint projects below that point.</para>
        ///
        /// <para><b>The facing is derived, never typed.</b> <c>ShopFixtureCatalog.FacingToward</c> reads
        /// the bake's own per-facing service-face anchors through <c>BuildingFacing</c> — the same call
        /// that aims every shop door and every village house, and the reason there is one piece of sign
        /// reasoning in the repo instead of two. A counter that faced the wrong way would read as an art
        /// bug, and the arithmetic version of this got a schoolhouse 92° wrong before #495 measured it.</para>
        ///
        /// <para>Falls back to the tinted placeholder, loudly, when the fixture has not been baked or
        /// sliced — a checkout that has not run the bake still gets a village it can trade in.</para>
        /// </summary>
        static void PlaceStoreCounterArt(GameObject counterGo, Sprite fallbackSprite)
        {
            var fixture = HiddenHarbours.Art.Editor.ShopFixtureCatalog.Find("counter", "generalStore");
            if (fixture.IsValid)
            {
                int facing = HiddenHarbours.Art.Editor.ShopFixtureCatalog.FacingToward(
                    fixture, counterGo.transform.position, VillageGreen);
                Sprite baked = HiddenHarbours.Art.Editor.ShopFixtureCatalog.LoadFacing(fixture, facing);
                if (baked != null)
                {
                    // Configure pins unit scale, identity rotation, a white tint (this REPLACES a tinted
                    // placeholder — a leftover tint would multiply through the baked art) and YSortSprite.
                    HiddenHarbours.Art.Editor.ShopFixtureCatalog.Configure(counterGo, fixture, baked);
                    return;
                }

                Debug.LogWarning(
                    $"[StPetersBuilder] the store counter's facing {facing} has no sprite — its sheet is " +
                    $"missing or unsliced ({fixture.SheetPath}). Standing the placeholder instead. " +
                    "⚠️ A sheet can be Multiple-mode and still not be sliced this kit's way: run " +
                    "Hidden Harbours ▸ Art ▸ Bake Shop Fixtures, then ▸ Slice Shop Fixture Sheets.");
            }
            else
            {
                Debug.LogWarning(
                    "[StPetersBuilder] no baked counter in the shop-fixture contract, so the store is " +
                    "trading over a tinted rectangle. Run Hidden Harbours ▸ Art ▸ Bake Shop Fixtures.");
            }

            var sr = counterGo.GetComponent<SpriteRenderer>();
            if (sr == null) sr = counterGo.AddComponent<SpriteRenderer>();
            sr.sprite = fallbackSprite;
            sr.color = new Color(0.55f, 0.42f, 0.28f);   // a shop counter in oiled wood, until art lands
            sr.sortingOrder = HiddenHarbours.Art.Editor.ShopFixtureCatalog.SortingOrder;
            counterGo.transform.localScale = new Vector3(1.6f, 0.9f, 1f);
            if (counterGo.GetComponent<YSortSprite>() == null)
                counterGo.AddComponent<YSortSprite>();   // walk-up prop: layers by world Y (see the freezer)
        }

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
        // StPetersLayoutTests rather than asserted in a comment.
        //
        // ⭐ SHE LIES NORTH OF THE FAIRWAY, CLEAR OF THE COME-ALONGSIDE TRACK (owner eyeball,
        // 2026-08-27: "the test dory is in the way of the arrival boat"). She sat on the channel's own
        // centre-line at (215, 0) — 5.29 m from the line the skipper runs in on, against 2.40 m of
        // arriving cape islander and 0.85 m of dory. That is not a berth beside a fairway; it is a berth
        // IN one. Moving her north puts the whole marked width between her and the pier: 8.41 m,
        // measured over the real terrain, with nothing else in the region moved.
        //
        // ⭐⭐ AND THE §5.1a ARITHMETIC THIS COMMENT USED TO CARRY WAS ALREADY STALE. It read: the mooring
        // sits 5 m past the beach toe onto the reef shelf, measured bed −1.05 m, so a 0.30 m dory floats
        // 56.9% of the cycle and dries near spring low. None of that has been true since the 2026-08-19
        // dredge (#582) — <see cref="ApproachFrom"/> → <see cref="ApproachTo"/> cut the channel through
        // this exact water, and the bed under her now measures <see cref="ApproachBedElevation"/>
        // (−4.00 m): 1.80 m under a 0.30 m dory at the lowest water of the biggest spring tide, every
        // hour of every month. The ruling that replaced it is the one StPetersLayoutTests states in its
        // own NAME — TheMooringLiesInTheDredgedPocket_AndFloatsHerAtEveryTide — and the tide gate that
        // used to be hers now lives on the BEACH slip at <see cref="BerthTo"/>, which still bares by
        // 1.20 m. So the quantity to preserve across this move is not a drying fraction. It is that she
        // stays IN the pocket, and she does: her bed is −4.00 m before and after.
        //
        // ⚠ WHICH IS WHY THE NEW BERTH IS DERIVED RATHER THAN NUDGED. North of the centre-line the
        // dredged flat ends at <see cref="ApproachThalwegHalfWidth"/> and the shoulder climbs away fast
        // (−2.52 m by y = 6). Her berth is that edge less her own half-beam, so her outboard RAIL is
        // what lies on it and every part of her floats over dredged ground. One metre further out and
        // the worst bed under her is −3.65 m; at y = 4.25 it is −3.45 m and climbing. The x does not
        // move: she stays off the pier HEAD, which StPetersVillageTests holds in as many words ("a
        // mooring under the planks is a mooring you cannot see").
        //
        // ⚠ PROVISIONAL, and the owner said so — "another small dock for now nearby". This is the
        // nearest water that is dredged, off the head, and clear of the track. It is a BERTH, not a
        // second dock: there is no other pier on this island to build her one against yet.
        public const float DockZoneRadius = 3.5f;                                  // ControlSwitcher's default _zoneRadius

        /// <summary>
        /// The starting dory's half-beam, in metres. ⚠ A MIRROR of <c>DoryIsoHullMesh.asset</c>'s
        /// <c>WatertightHalfBeamMeters</c>, for exactly the reason <see cref="ArrivalHullHalfBeamMetres"/>
        /// is one: a <c>const</c> cannot load an asset and her berth has to be a compile-time expression.
        /// StPetersAlongsideBerthTests holds the two equal, so re-lofting her hull and forgetting this
        /// fails loudly instead of quietly hanging her rail over the shoulder.
        /// </summary>
        public const float DoryHalfBeamMetres = 0.85f;

        /// <summary>Where along the channel she lies. Unchanged by the 2026-08-27 move: east of
        /// <see cref="StPetersWharf.HeadCellX"/>, so she is off the pier head and not under its planks.
        /// (Its own derivation — beach ends 140 m from the island centre → 70 + 145 — is history now that
        /// the dredge owns this water, but the number it produced is still the right one.)</summary>
        public const float DoryMooredX = 215f;

        /// <summary>
        /// How far NORTH of the fairway's centre-line she lies: the dredged flat's own north edge, less
        /// her own half-beam. Her rail sits on the edge; her whole hull floats over the cut. Derived, so
        /// a re-dredge that narrows the flat brings her in with it rather than leaving her on a shoulder.
        /// </summary>
        public const float DoryMooredY = ApproachThalwegHalfWidth - DoryHalfBeamMetres;

        /// <summary>The starting dory's berth — north of the fairway, off the pier head, in the cut.</summary>
        public static readonly Vector3 DoryMooredPos = new Vector3(DoryMooredX, DoryMooredY, 0f);

        // ================================================================================================
        //  ⭐ SHE LIES ALONGSIDE (owner playtest, 2026-08-22)
        // ================================================================================================
        // The owner watched the opening and said what the geometry had been saying all along: the boat
        // finishes with her BOW ON THE PLANKS. It was not a pilot bug. The berth was a point on the
        // pier's own centre-line one metre off its head, so a 12.9 m hull lying on the channel's axis
        // reached x = 208.6 — which is 5.4 m INSIDE the deck (x 183 → 214). Bow-on was AUTHORED; the
        // pilot ran it faithfully.
        //
        // ⚠ WHAT MADE THIS MORE THAN MOVING A POINT. Alongside means her whole beam lies off the pier's
        // FACE: 4.8 m of hull starting 0.4 m out from y = −3. The dredged approach's flat bottom is
        // ±4 m about y = 0 and the pier is ±3 m — so there is exactly ONE metre of dredged water beside
        // each face, and the ground past it BARES at spring low (measured −1.94 m at y = −7 against
        // −2.20 m of water). Every alongside berth on this pier, on either face, was aground. The fix
        // is therefore geometry as well as a coordinate — see the berth pocket below.
        //
        // ⭐ NOTHING BELOW IS TYPED. The face comes from the deck's own footprint, the offset from the
        // hull's own half-beam, the station from the ladder fitting. Re-site the pier, re-measure the
        // hull or move the ladder and the berth follows — which is exactly what the old literals could
        // not do, and why they went stale without anything noticing.

        /// <summary>
        /// How far her side lies off the wharf face, in metres — the fendering gap. The pier hangs tyre
        /// fenders on this edge, and this is one of them squashed: close enough to step across, far
        /// enough that she is rubbing rope and rubber rather than timber.
        /// </summary>
        public const float AlongsideFenderGapMetres = 0.4f;

        /// <summary>
        /// Room either side of her inside the berth pocket's FLAT bottom, in metres. The channel's own
        /// margin, for the channel's own reason: she is steered in, not threaded through, and a hull
        /// whose beam ends exactly at the flat's edge touches on one side as soon as the tide falls.
        /// </summary>
        public const float BerthPocketMarginMetres = 1.0f;

        /// <summary>
        /// Her length, in metres. ⚠ A MIRROR of <c>CapeIslander.asset</c>'s <c>LengthMeters</c>, for the
        /// same reason <see cref="ArrivalHullDraughtMetres"/> is one: a <c>const</c> cannot load an
        /// asset and the berth arithmetic has to be a compile-time expression. StPetersEastBerthTests
        /// holds the two equal, so moving the def and forgetting this one fails loudly.
        /// </summary>
        public const float ArrivalHullLengthMetres = 12.9f;

        /// <summary>
        /// Her half-beam, in metres. ⚠ A MIRROR of <c>CapeIslanderIsoHullMesh.asset</c>'s
        /// <c>WatertightHalfBeamMeters</c> — the same sidecar the channel's width was sized against, so
        /// the door she comes through and the berth she lies in are measured off one hull.
        /// </summary>
        public const float ArrivalHullHalfBeamMetres = 2.4f;

        /// <summary>Where her keel lies: off the wharf's mooring face by her own half-beam plus the
        /// fendering gap. This is the y a hull lying ALONGSIDE this pier sits on.</summary>
        public static float AlongsideBerthY =>
            StPetersWharf.MooringFaceY - AlongsideFenderGapMetres - ArrivalHullHalfBeamMetres;

        /// <summary>Where along the face she lies: <b>abreast of the ladder</b> — the one fitting on
        /// this pier whose whole job is getting a person between a boat and the planks.</summary>
        public static float AlongsideBerthX => StPetersWharf.LadderPosition().x;

        /// <summary>Her inboard gunwale — the rail you step from. Derived, so the step ashore is
        /// measured off the HULL rather than off the pier's centre-line (which is what left the old
        /// disembark sitting on a line she no longer lies on).</summary>
        public static float AlongsideGunwaleY => AlongsideBerthY + ArrivalHullHalfBeamMetres;

        /// <summary>The cove's step-ashore pattern, in metres — how far in from her rail you land.</summary>
        public const float StepAshoreMetres = 1.5f;

        /// <summary>How far seaward of the berth an arriving boat is parked. Well inside
        /// <see cref="DockZoneRadius"/>, which the ControlSwitcher's pure-distance disembark test needs —
        /// asserted by StPetersLayoutTests rather than trusted to this note.</summary>
        public const float ArrivalStandoffMetres = 2f;

        /// <summary>The berth — she lies ALONGSIDE the pier's mooring face, abreast of the ladder.</summary>
        public static Vector3 DockZonePos  => new Vector3(AlongsideBerthX, AlongsideBerthY, 0f);

        /// <summary>Step ashore: on the planks, abreast of where she lies, the cove's 1.5 m in from her
        /// rail. Inside the deck footprint by construction — which is what makes it planks, not water.</summary>
        public static Vector3 DisembarkPos => new Vector3(AlongsideBerthX,
                                                          AlongsideGunwaleY + StepAshoreMetres, 0f);

        /// <summary>Sail home: park just seaward of the berth, along the pier's own axis, in range.</summary>
        public static Vector3 ArrivalPos   => new Vector3(
            AlongsideBerthX - StPetersWharf.AxisInward().x * ArrivalStandoffMetres,
            AlongsideBerthY, 0f);

        // --- THE BERTH POCKET: the water she lies IN --------------------------------------------------
        // ⚠ A SEPARATE CUT, and deliberately a SHORT one. Widening the dredged approach until its flat
        // bottom reached past the pier's face would also have floated her — and would have widened the
        // DOOR. The channel's narrowest section is what gates the fleet (StPetersEastBerthTests walks it
        // and reports who gets in at which tide); taking the flat from ±4 m to the ±8.2 m an alongside
        // hull needs would let the stern trawler and the coastal packet up to this wharf at spring low.
        // That is a RULING, not a side effect of an arrival fix. A pocket is LOCAL: it holds her where
        // she lies and leaves the pinch — which is out along the channel, not here — where it was.
        //
        // The centre-line IS her own footprint at the berth, bow to stern, so the flat can never come
        // out shorter than the hull it was cut for.

        /// <summary>Bow end of the pocket's centre-line — her own bow at the berth.</summary>
        public static Vector2 BerthPocketFrom => new Vector2(
            AlongsideBerthX - ArrivalHullLengthMetres * 0.5f, AlongsideBerthY);

        /// <summary>Stern end of the pocket's centre-line — her own stern at the berth.</summary>
        public static Vector2 BerthPocketTo => new Vector2(
            AlongsideBerthX + ArrivalHullLengthMetres * 0.5f, AlongsideBerthY);

        /// <summary>The pocket's FLAT bottom: her whole beam, plus the room she is steered in with.</summary>
        public static float BerthPocketThalwegHalfWidth =>
            ArrivalHullHalfBeamMetres + BerthPocketMarginMetres;

        /// <summary>
        /// Shoulder width beyond the flat, in metres — how long the cut takes to climb back to the
        /// ground around it.
        ///
        /// <para>⚠ BOUNDED FROM ABOVE by something real rather than chosen for looks. The dredge test
        /// pins a ring of probes that must NOT move — the pier root's dry ground, and the shoal a metre
        /// off each deck lip — and the nearest of them (the south lip, at the deck's centre) lies
        /// 6.79 m from this pocket's nearest end. The cut's outer toe has to stay inside that, or the
        /// berth starts digging out the ground the pier stands on. StPetersAlongsideBerthTests asserts
        /// the clearance instead of trusting this paragraph.</para>
        /// </summary>
        public const float BerthPocketShoulderMetres = 1.6f;

        /// <summary>Half-width of the whole cut — the flat plus its shoulder.</summary>
        public static float BerthPocketHalfWidth =>
            BerthPocketThalwegHalfWidth + BerthPocketShoulderMetres;

        /// <summary>The pocket is dredged to the SAME bed as the channel that feeds it — a berth
        /// shallower than its own approach is a berth you can reach and cannot lie in.</summary>
        public static float BerthPocketBedElevation => ApproachBedElevation;

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
            // characters (the player Y-sorts in the decor band, floored at SortingBands.DecorFloor = 2 and
            // climbing to DecorCeiling — #110, re-based by ADR 0032) — so holes and the player draw on the
            // bared flat IN FRONT of the water. (Before this change a 2 m TidalFlatVisual colour grid sat at
            // -5 and masked BOTH the sand and the shader; retiring that grid is what lets the smooth shader
            // show, and the Sea takes the vacated -5 slot. The always-dry island stays visible because the
            // shader clips over it: its 6 m elevation is above every water level, so depth<0 → transparent.)
            wsr.sortingOrder = -5;
            if (seaTile != null) wsr.sprite = seaTile;
            else                 { wsr.sprite = waterSprite; wsr.color = new Color(0.15f, 0.27f, 0.34f); }
            // ⭐ ONE QUAD over the region, never a tiling of it — 760 × 520 m at a 2 m tile is 98,800 tiles
            // (395,200 vertices), which Unity's tiling generator refused and logged on every build. The
            // tile contributed nothing to draw: the water shader samples no _MainTex and reads only world
            // XY. The whole argument, and the sprite-derived scale that also fixes the half-size
            // no-material backdrop, live in WaterSceneTemplate.ConfigureSeaPlane.
            WaterSceneTemplate.ConfigureSeaPlane(wsr, RegionWorldSize);

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
                water.AddComponent<HiddenHarbours.Art.WaterSurface>();

                // ⭐ ONE WIRING, SHARED WITH NINE MILE CREEK (WaterSceneTemplate.ConfigureLandRegionWater).
                // This block used to be the ONLY copy of it, and that is exactly how Nine Mile Creek came to
                // be sailing on the pre-displacement flat sea while this region sailed on the displaced one:
                // there was no shared home for the wiring, so the second region simply did not get half of
                // it. Everything the four hand-rolled calls did is done there, in this order and for these
                // reasons:
                //
                //  · the seabed height bake over the region rect at 192² (ADR 0012 §A step 1 — a ~0.83 m
                //    texel, so the wet edge follows a fine grid instead of ~1.5 m steps on the bar crest),
                //    with the elevation range bracketing the field from the harbour floor to the island top;
                //  · the owner's GameConfig, whose DisplacedWater block carries the salience knobs both
                //    surfaces re-read every tick (arc step 3 — tuning is an asset edit, never code);
                //  · the ADR 0017 weather palette with base = null ON PURPOSE, so the calm sea is the LIVE
                //    Water.mat the owner hand-tunes rather than a frozen preset copy of it;
                //  · the ADR 0023 displaced surface over the SAME rect — the game's water since 2026-08-05.
                WaterSceneTemplate.ConfigureLandRegionWater(
                    water, RegionWorldCenter, RegionWorldSize,
                    WaterHeightBakeResolution, DeepHarbourElevation, IslandElevation,
                    ShoreGradient);
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
            // ⚠️ EVERY "TAILS THE CYCLE / position N" CLAIM BELOW IS HISTORICAL. Each paragraph records
            // why a hull was ADDED and where she landed AT THE TIME she arrived, and each of the Cape, the
            // lobster boat and the side dragger really did tail the cycle on the day she was written about.
            // None of them does now. The live order is DevPickerRosterFiles at the top of this class and
            // nothing else — read it there, and treat the positions below as the reasoning that produced it
            // rather than as a description of it.
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
            //
            // THE 2026-08-15 RULING TOOK THE CYCLE FROM 15 RUNGS TO 23. #541 and #543 baked 23 more
            // hulls end to end — mesh, deck and visual — but gave none of them a BoatHullDef, so
            // none could be sailed. All 23 now have one. Eight reach the picker: the five #543
            // families (two RIBs, the reshaped sport skiff, two sport fishers) and three of the 18
            // lobster variants, one per size. Same scope bar as every rung above — PICKER ONLY, no
            // ShipwrightOffer, no economy (rule 8) — and the same authoring rule: their numbers live
            // in committed assets an art re-bake cannot reach (rule 2).
            //
            // ⚠️ ALL EIGHT ARE MESH-ONLY, like the dragger above: no sheet, so the V key correctly
            // reports "this hull has only one look", and no fallback Sprite either. Their gameplay
            // DraughtMeters is NOT the HullMeshDef.RestingDraftMeters the art side committed — the
            // two are different fields with different owners, related across the whole shipped fleet
            // by the ≈0.38 visual-to-gameplay ratio water-rendering.md names. Deriving them that way
            // reproduces the committed lobster boat to the unit, which is why the anchor row of the
            // variant family is asserted against her in LobsterVariantLadderTests.
            var pickerRoster = DevPickerRosterFiles
                .Select(f => AssetDatabase.LoadAssetAtPath<BoatHullDef>($"{DataBoats}/{f}.asset"))
                .Where(h => h != null)
                .ToArray();

            // DERIVED, not typed: this used to be a hand-maintained `const int ExpectedPickerRungs`, which
            // is a second copy of a number the array above already knows and so a thing that goes stale the
            // first time someone adds a rung and forgets. The only hull that may legitimately be missing is
            // the uncommitted Punt, so any shortfall is worth a warning.
            int expectedPickerRungs = DevPickerRosterFiles.Length;
            if (pickerRoster.Length < expectedPickerRungs)
                Debug.LogWarning($"[StPetersBuilder] The dev boat picker got {pickerRoster.Length}/" +
                                 $"{expectedPickerRungs} hulls — " +
                                 "some Data/Boats assets are missing, so those boats won't be in the cycle. " +
                                 "Run Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Build Boat " +
                                 "Visual Defs and the cove builder " +
                                 "(which authors the hull assets), then re-run this builder.");

            // The opening cast as DATA (CLAUDE.md rule 2): Aunt Ginny (teaches the buy-and-repair loop) and
            // Ned's letter (the remembered presence — no inherited dory). Each NpcDef carries its name, its
            // DialogueDef, the verb, and the completion flag, so the builder only PLACES them — the words
            // live in assets the owner can edit (Data/NPCs + Data/NPCs/Dialogue), never hard-coded here.
            var ginnyNpc = AssetDatabase.LoadAssetAtPath<NpcDef>(DataNpcs + "/AuntGinny.asset");
            var nedNpc   = AssetDatabase.LoadAssetAtPath<NpcDef>(DataNpcs + "/NedLetter.asset");

            // The general store's STOCK, as data (#356 authored every one of these; nobody had stood them
            // on a counter yet — plan-to-m1 §7.5's "what is left is placement"). Four Defs, four vendors,
            // one counter below. Null-tolerant: an unimported Def leaves that row off the screen with a
            // warning, rather than a vendor component that silently sells nothing.
            var rodOffer    = AssetDatabase.LoadAssetAtPath<GearOffer>(DataGear + "/Rod.asset");
            var clamLicence = AssetDatabase.LoadAssetAtPath<LicenseDef>(DataLicenses + "/ClamLicense.asset");
            var iceSupply   = AssetDatabase.LoadAssetAtPath<SupplyDef>(DataSupplies + "/Ice.asset");
            var capelinBait = AssetDatabase.LoadAssetAtPath<BaitDef>(DataBait + "/Capelin.asset");
            // The chandlery half of the same counter (ADR 0025 S2): the depth sounder, fitted to the boat
            // you came in on. Same null-tolerance — an unimported Def leaves the row off with a warning.
            var depthSounder = AssetDatabase.LoadAssetAtPath<InstrumentOffer>(
                DataInstruments + "/DepthSounderOffer.asset");

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
                TideMean         = TideMean,        // St Peters' BIG tide (±2.2 m) so the bar bares + floods
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

            // ⭐ GINNY'S COTTAGE IS NOT BUILT HERE ANY MORE. It stood on this line as "IslandCottage" —
            // a greybox SpriteRenderer standee with a day/night sprite swap, a window glow and a chimney
            // plume — until the owner moved her into the eastern woods on 2026-08-16 and ruled the new
            // cottage a REAL building you can walk into. It is now a village-kit build with a baked
            // interior, placed by StPetersGinnyPlot.Place along with her sheds, her freezer and her
            // garden. The greybox path is gone rather than moved: a standee cannot be entered, and the
            // ruling was that this one is.
            //
            // What did NOT survive the promotion, stated so it is not mistaken for a regression: the
            // CottageDayNight lit-window SPRITE SWAP. It swapped Cottage.png ⇄ CottageNight.png, and a
            // kit building draws baked per-facing sheets with no lit twin — so the lit PANES are an
            // upstream art ask (a night sheet, or a lit-window axis on the house rig). The window GLOW
            // and the chimney SMOKE moved across unchanged, and they are what actually make the cottage
            // read warm from across a clearing at night.

            // --- THE COLD CHAIN, ashore (M1 §7.3 — gameplay-systems) --------------------------------
            // The WET-BUCKET spot at the water's edge (Live: free, shellfish only). A proximity
            // interactable on the stall pattern (StallReach resolves the player itself — no wiring);
            // greybox colour marker until art lands. Ice for the boat rides the DeckIceBox on the hold
            // (runtime-spawned) — nothing to place here.
            //
            // ⚠ GINNY'S FREEZER (Frozen: time ashore, free but only at home) IS NO LONGER IN THE
            // VILLAGE. The owner ruled on 2026-08-16 that it follows her: it is her freezer at her
            // house. StPetersGinnyPlot places it. That is a live cold-chain change — freezing a catch
            // was three metres from the spawn and is now an 85 m walk inland — and nothing in the
            // economy was retuned to match.
            var wetGo = new GameObject("WetBucketSpot");
            wetGo.transform.position = WetBucketPos;   // the head of the flats — the last dry ground
            var wetSr = wetGo.AddComponent<SpriteRenderer>();
            wetSr.sprite = waterSprite;
            wetSr.color = new Color(0.25f, 0.45f, 0.60f);   // a seawater barrel, reads wet
            wetSr.sortingOrder = 3;   // pre-Play default only; the YSortSprite below OWNS the order
            wetGo.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            wetGo.AddComponent<WetBucketPoint>();
            wetGo.AddComponent<YSortSprite>();   // walk-up prop: layers by world Y (see the freezer above)

            // ⭐ THE COTTAGE'S NIGHT PIECES MOVED WITH IT. The lit-window swap (CottageDayNight), the
            // PreconfiguredLight window glow and the ChimneySmoke plume all hung off the greybox standee
            // that used to stand at the hearth. StPetersGinnyPlot now attaches the glow and the smoke to
            // the kit cottage in the woods; the sprite SWAP could not follow (a kit building has no
            // lit-window twin sheet) and is restated as an art ask. See the note at the cottage's old
            // build site above.

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

            // --- THE CLIFF KIT: BAKE ON MISSING ---------------------------------------------------------
            // The kit ships as the rig, not as sheets (PR #427) — the PNGs are gitignored and regenerate
            // in seconds. The coordinator made the consequence binding: a fresh clone must SELF-HEAL, so
            // the menu item is a dev convenience and this is the requirement. Warn-and-continue rather
            // than throw: a bake that fails should leave the region built and untextured with an error
            // saying why, not take the whole scene down with it (rule 10, leave a working build).
            try
            {
                HiddenHarbours.Tools.RigBaking.CliffBakeMenu.EnsureBaked();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[StPetersBuilder] the cliff kit could not bake — the coast will build " +
                               $"but its faces will render untextured. Run " +
                               $"'Hidden Harbours ▸ Dev ▸ Bake Cliff Face Kit' and check the rig. {e.Message}");
            }

            // --- THE CLIFF WALLS: THE COAST STANDS UP ---------------------------------------------------
            // Face quads generated from the plan above and sampled off the SAME TidalTerrain the walk gate
            // reads, so the wall and the walkability cannot disagree. Runs to the terrain component that
            // was just configured, never to this file's constants, which is what makes a coast retune move
            // the geometry with it. Visual only (rule 5) — walkability is the terrain's slope, not this.
            StPetersCliffWalls.Build(terrain);

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
                // ⚠⚠ CREATED INACTIVE, CONFIGURED, AND ONLY THEN ACTIVATED — and that order is
                // load-bearing. TerrainSplatSurface is [ExecuteAlways], so AddComponent fires OnEnable
                // IMMEDIATELY and builds the quad's mesh from the component's serialized DEFAULT extent
                // (160 × 120 m); Configure() afterwards updates the fields but EnsureBuilt() early-returns
                // once the mesh exists. This builder used the naive order and survived ONLY because it
                // saves the scene — the next load re-ran OnEnable with the correct serialized extent — an
                // undocumented save/reload dependency that broke the moment anything looked at the freshly
                // built scene without reloading it. See NineMileCreekBuilder.BuildSplatGround (where the
                // trap was found) and StPetersSplatGroundTests, which pins this order.
                var splatGo = new GameObject("TerrainSplat");
                splatGo.SetActive(false);
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

                // The terrain material kit (ADR 0028 PR 2): pack the detail arrays (derived, GUID-
                // stable) and wire them plus any painted splat maps. Both no-op safely when the kit
                // or the paint is absent — the shader falls back to flat band colours / bands-only.
                HiddenHarbours.Art.Editor.TerrainTexArrayBuilder.Build();
                splat.ConfigureDetail(
                    AssetDatabase.LoadAssetAtPath<Texture2DArray>(
                        HiddenHarbours.Art.Editor.TerrainTexArrayBuilder.Array256Path),
                    AssetDatabase.LoadAssetAtPath<Texture2DArray>(
                        HiddenHarbours.Art.Editor.TerrainTexArrayBuilder.Array512Path));
                // Paths from TerrainSplatAssets, not literals here: the map count grew from three
                // to four with kit v2 and to five with kit v3's reef beds, and a second spelling of
                // the same filenames is exactly the duplicate that wires null in silence once the
                // two copies drift.
                var splatMaps = new Texture2D[TerrainSplatBrush.TextureCount];
                for (int i = 0; i < splatMaps.Length; i++)
                    splatMaps[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainSplatAssets.PathOf(i));
                splat.ConfigureSplat(splatMaps[0], splatMaps[1], splatMaps[2], splatMaps[3], splatMaps[4]);

                // Everything is on the component; NOW let OnEnable build the quad — at the region's real
                // extent, with every uniform already in place. See the SetActive(false) note above.
                splatGo.SetActive(true);
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

            // THE CLAM-HOLE BEACON (gameplay-systems). ⚠️ This no longer owns the E key — the dig moved onto
            // the one interact verb when tools became things you HOLD, because two independent readers of E
            // (dig, and "put the shovel down") would be arbitrated by Update order and nothing else. What it
            // still does is publish the on-foot player position for the per-hole skittish-clam escape timer.
            // Sits on the persistent Player so it carries across region hops (no per-region re-wire).
            var digger = core.PlayerGo.AddComponent<ClamDigger>();
            SetRef(digger, "_player", core.PlayerGo.transform);

            // --- THE TOOLS, STANDING IN THE WORLD (the owner's ruling, 2026-08-13) ------------------------
            // "Currently the player always has a rod available to use — this should be a carried item like
            // any item", and "they should need a shovel to dig clams". So the rod and the shovel are OBJECTS
            // on the ground at the start spawn: you pick one up with the same interact press that lifts a
            // fuel can, you carry ONE at a time, and you put it back to swap. The dig and the cast now ask
            // what is in your hands rather than what is on your gear list.
            //
            // WHERE: at the start spawn, a pace either side, so the very first thing in view on a new game
            // is the two tools the opening is about. They are REGION content (they belong to St Peters, not
            // to the persistent player) — carry one to Nine Mile Creek and it comes with you in your hands;
            // set it down there and it stays there, which is the honest physical answer and is exactly what
            // CarryHands' place path now does correctly across a hop.
            //
            // ART: the committed 32 px gear sprites. The DIRECTIONAL kits (shovelIsoRig.js is in
            // docs/art/rigs/ but not yet in RigCatalog; a carried-rod stance likewise) are art-pipeline's and
            // land later WITHOUT touching this builder — CarriableTool already reports BakedFacings 0 and
            // takes a facing it currently ignores.
            var toolsRoot = new GameObject("Tools");
            MakeTool(toolsRoot.transform, DataTools + "/Shovel.asset", ArtToolShovel,
                     new Vector2(StartSpawnPos.x - 1.1f, StartSpawnPos.y - 0.6f), "tool.st_peters.shovel");
            MakeTool(toolsRoot.transform, DataTools + "/Rod.asset", ArtToolRod,
                     new Vector2(StartSpawnPos.x + 1.1f, StartSpawnPos.y - 0.6f), "tool.st_peters.rod");

            // THE PAIL, beside them (the two-hand ruling). A carriable bucket that IS its own hold and
            // draws its own fill: pick it up in one hand, keep the rod in the other, and a landed fish
            // goes in with the same press. It stands a pace SOUTH of the two tools rather than in line
            // with them, so the three are a group you walk into rather than a row you clip through.
            // Everything about it — the hold, the verbs, the fill bridge, the eight baked facings — is
            // DevBucketBuilder's; only the spot is ours.
            DevBucketBuilder.Place(toolsRoot.transform,
                                   new Vector2(StartSpawnPos.x, StartSpawnPos.y - 1.35f),
                                   "container.st_peters.bucket");

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
            // pier: a 31 x 6 m timber finger running out along the dredged slip from the last dry ground to
            // just short of the mooring, so the ratified disembark at (213, 0) lands ON the planks. Drawn
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

            // ⭐ THE NAV MARKS — the buoyed line onto the ONE door in the reef, and the gap in the bar
            // you cross to Nine Mile Creek. Places and does NOT decide: NavMarkPlan derives every
            // position, colour, size and facing against THIS terrain — the same ElevationAt the reef
            // gate itself reads — so the marks can never disagree with the water they stand in. IALA
            // Region B, handedness derived from each channel's authored seaward end.
            //
            // ⚠ It will REFUSE marks every build: the reef apron and the slip both bare at spring low,
            // so nothing floats inside the ring. That is the finding, not a gap — see StPetersNavMarks'
            // class note and the perch/withy art ask it states.
            StPetersNavMarks.Place(terrain);

            // --- THE VILLAGE, at last (§5.1's three houses + the school + the general store) --------------
            // The last item on the owner's dressing list. #345 relocated the village's PROPS but deferred
            // the buildings themselves because the building kit had no consumer; #352 built one, so the
            // school, the store and the three clapboard houses can finally stand where the docs put them.
            // AUTHORED sites, one facing derived per building so every door turns toward the green — see
            // StPetersVillage for why the arc is the only shape the site allows.
            //
            // ⭐ THE DOORS OPEN NOW. Any building the interior kit has baked a room for gets that room
            // standing under it, walled and furnished, and a BuildingInterior that swaps shell for room
            // when the player crosses the threshold — seamless, no scene load, no camera cut (the
            // owner's 2026-07-30 ruling). The player transform is what makes it a door rather than a
            // diorama, which is why it is handed in here rather than searched for at runtime.
            int villageBuildings = StPetersVillage.Place(terrain, core.PlayerGo.transform);

            // --- THE SHOPS (the owner's 2026-08-11 rulings) -----------------------------------------------
            // The general store stops being a house with a bay window and becomes a shop, at the SAME
            // authored site — so the storekeeper's dooryard and the counter below still derive from one
            // coordinate and nothing had to be copied. The post office is new. Both get a walkable ground
            // floor: EVERY building gets an interior is canon now, not a per-building choice.
            int shopBuildings = StPetersShops.Place(terrain, core.PlayerGo.transform);

            // --- AUNT GINNY'S PLOT (the owner's 2026-08-16 ruling) ----------------------------------------
            // She moved out of the village onto land that is hers, 85 m east in the woods: a cottage you
            // can walk into, three derelict sheds, her garden and her freezer. Placed here because it
            // borrows the same building kit and the same interior kit the village and the shops just
            // used, with the same player transform for the door.
            //
            // ⚠ ORDER DOES NOT MATTER against the woods planter above, and must not start to. The trees
            // were already scattered by the time this line runs, and they miss her plot because
            // StPetersWoods.IsPlantable asks StPetersGinnyPlot.IsInsideClearing — a pure function of
            // static constants, answerable before anything is placed. If a future clearing ever needs to
            // know what actually got BUILT, it has to move above the planter, not hope.
            //
            // `waterSprite` is the greybox quad the sheds are drawn with until a shed rig exists — the
            // same marker the wet-bucket barrel uses.
            int ginnyPlotBuildings =
                StPetersGinnyPlot.Place(terrain, waterSprite, core.PlayerGo.transform);

            // --- THE CANNERY (2026-08-19) -----------------------------------------------------------------
            // The fish plant that shut when the business went to the mainland — the island's biggest
            // building, standing derelict beside the pier it used to load from. SCENERY: it carries no
            // interaction and no def, and the restart arc the owner named is logged in backlog/ as M3
            // work for the economy-sim lane (CLAUDE.md rule 8).
            //
            // ⚠ Same order note as Ginny's plot above: the trees were scattered before this line runs and
            // miss the cannery because StPetersWoodlandZones declares its clearing from static constants,
            // answerable before anything is placed.
            StPetersCannery.Place(null, terrain, waterSprite);

            // --- THE DOORYARDS (the lawn ruling's third derivation) ---------------------------------------
            // #604 authored one yard POLYGON per property and made the grass gates read it — the mow line
            // and the wild-grass suppression. This is the third thing that polygon says: the FENCE LINE.
            // One authored fact, three derivations, so a lawn painted to one edge and a fence standing on
            // another is a shape this data cannot take.
            //
            // ⚠ IT RUNS AFTER THE BUILDINGS AND THAT ORDER IS NOT LOAD-BEARING — the yards derive from
            // StPetersYards, a pure function of authored constants, and none of them asks what actually
            // got built. It is here because a yard is read against the house it belongs to, and putting
            // the two together in this file is what makes that legible.
            //
            // ⚠ THE PIECES ARE AIMED FROM THE BAKE'S MEASURED FACING TABLE, never from the kit's own
            // sidecar, whose facing list is mirrored against its art (IMPORT.md §6). YardDressing refuses
            // to place anything at all if that measured table is missing.
            var dooryards = YardDressing.Place(StPetersYards.Yards, parent: null, regionLabel: "St Peters");
            Debug.Log($"[StPetersBuilder] the dooryards: {dooryards}");

            // --- THE STORE'S COUNTER (§7.5's other half: the placement) -----------------------------------
            // #356 built the island general store's ECONOMY — the StPetersStore market channel that pays a
            // worse price level than the creek, the clam licence, the counted bait/ice stock — and said so
            // itself: "what is left is placement, which is the world-content lane's". This is that. The
            // store BUILDING went up in #353 and the storekeeper stood at its door in #354; until now you
            // could walk up to both and there was no counter to trade over.
            //
            // ⭐ IT GOES IN THE BUILDER, NOT IN THE SCENE. A counter dragged into StPeters.unity by hand is
            // undone by the next build — the whole scene is authored from this file, every run. Same rule
            // as the freezer, the wet bucket and the five islanders above.
            //
            // ONE GameObject, FOUR VENDORS: BuyCatalog enumerates every vendor component on a stall, so the
            // rod, a lot of capelin, a bag of ice and the clam licence come up on ONE buy screen with no new
            // UI (IslandGeneralStoreTests pins that shape). Plus the SELL side — a Market on the
            // StPetersStore channel behind a FishBuyer — so the first bucket of clams can be sold here, for
            // deliberately less than Nine Mile Creek pays, which is how "where you sell matters" gets taught
            // in the first hour instead of explained in a tooltip.
            var storeCounter = new GameObject("GeneralStoreCounter");
            storeCounter.transform.position = GeneralStoreCounterPos;
            PlaceStoreCounterArt(storeCounter, waterSprite);

            // The wallet is the persistent services root's (St Peters IS the start scene, so it is in this
            // scene rather than behind a proxy — the cove's pattern, not the creek's).
            var storeWallet = core.ServicesRoot;

            var rodShop = storeCounter.AddComponent<GearShop>();
            SetRef(rodShop, "_offer", rodOffer);
            SetRef(rodShop, "_walletProvider", storeWallet);
            SetString(rodShop, "_sellerId", StoreSellerId);

            var baitShop = storeCounter.AddComponent<BaitShop>();
            SetRef(baitShop, "_bait", capelinBait);
            SetRef(baitShop, "_walletProvider", storeWallet);
            SetString(baitShop, "_sellerId", StoreSellerId);

            var iceShop = storeCounter.AddComponent<SupplyShop>();
            SetRef(iceShop, "_supply", iceSupply);
            SetRef(iceShop, "_walletProvider", storeWallet);
            SetString(iceShop, "_sellerId", StoreSellerId);

            // The DEPTH SOUNDER — the first purchasable helm instrument (ADR 0025 S2). It bolts into the
            // dash of the boat the player arrived in, so it sells here beside the rod rather than needing
            // its own chandlery: one counter, one screen (the §7.5 general-store pattern).
            var sounderShop = storeCounter.AddComponent<InstrumentShop>();
            SetRef(sounderShop, "_offer", depthSounder);
            SetRef(sounderShop, "_walletProvider", storeWallet);
            SetString(sounderShop, "_sellerId", StoreSellerId);

            // The clam licence — the FIRST licence of the game, and the one Ginny fronts the fee for. The
            // player still buys it themselves at this counter (§7.5: the transaction stays theirs).
            var clamVendor = storeCounter.AddComponent<LicenseVendor>();
            SetRef(clamVendor, "_license", clamLicence);
            SetRef(clamVendor, "_walletProvider", storeWallet);
            SetString(clamVendor, "_sellerId", StoreSellerId);

            // ⚠️ SAY WHICH MARKET IT IS. Market defaults to MarketId.Cove, and a counter left on the default
            // quietly quotes the home cove's price level — the exact bug the creek shipped with for a year
            // (#364's note, and MarketPriceGapTests' last case). The island store is the WORSE outlet or it
            // is nothing.
            var storeMarket = storeCounter.AddComponent<Market>();
            SetRef(storeMarket, "_config", config);
            SetEnum(storeMarket, "_marketId", MarketId.StPetersStore);

            var storeBuyer = storeCounter.AddComponent<FishBuyer>();
            SetRef(storeBuyer, "_market", storeMarket);

            var storeSell = storeCounter.AddComponent<WharfSellPoint>();
            SetRef(storeSell, "_buyer", storeBuyer);
            // The hold is the hand-carried ClamBucket on the on-foot player — you sell what is in your pail
            // over this counter, and the boat never comes into it (the store is inland, up the lane).
            SetRef(storeSell, "_holdProvider", core.Bucket != null ? core.Bucket.gameObject : core.PlayerGo);
            SetRef(storeSell, "_walletProvider", storeWallet);

            // The walk-up drivers, on the established stall pattern (StallReach finds the on-foot player
            // itself — no wiring): P browses and buys, B sells. Both refuse unless you are ON FOOT and
            // within StallGate.DefaultRange of this counter, so neither fires from across the green. These
            // are the placeholder keys the whole coast already uses; ui-ux owns the real Interact mapping
            // (§7.6). Adding DevBuyInput here rather than leaving it to BuyPointInstaller's runtime scan
            // makes the counter self-describing in the saved scene; the installer's own check is
            // idempotent, so it simply finds one already there.
            storeCounter.AddComponent<DevSellInput>();   // RequireComponent(WharfSellPoint) — present above
            storeCounter.AddComponent<DevBuyInput>();

            if (rodOffer == null || clamLicence == null || iceSupply == null || capelinBait == null
                || depthSounder == null)
                Debug.LogWarning(
                    "[StPetersBuilder] The general store's counter is missing stock — rod=" +
                    $"{(rodOffer != null)}, clam licence={(clamLicence != null)}, ice={(iceSupply != null)}, " +
                    $"capelin={(capelinBait != null)}, depth sounder={(depthSounder != null)}. Those rows " +
                    "won't appear on the buy screen. Re-run after the Data assets import rather than " +
                    "shipping a counter with an empty shelf.");
            else
                Debug.Log(
                    $"[StPetersBuilder] The general store has a COUNTER at {GeneralStoreCounterPos} — the " +
                    "rod, capelin by the lot, ice and the CLAM LICENCE on one buy screen (P), and a " +
                    "StPetersStore-channel buyer that takes your bucket over the same counter (B) for " +
                    "deliberately less than Nine Mile Creek pays.");

            // --- THE OPENING CAST + ONBOARDING (world-content; the buy-and-repair beat, canon §5.8) ------
            // Aunt Ginny + Ned's LETTER, anchored up on the island near the cottage (no routines — that's
            // M2), the self-built dialogue panel, the proximity INTERACT driver, and the light one-line
            // onboarding nudge that walks the NEW loop: dig clams → cross the bar → Nine Mile Creek (cod licence +
            // rod) → buy + REPAIR the damaged dory → sail home. The dory is EARNED, never inherited.
            //
            // Everything sits UP BY THE COTTAGE in the village on the island's WEST half — and the dock is
            // ~215 m away on the EAST end (§5.1a), so the shared E key could not fire both "talk" and
            // "board" even if it wanted to (it is context-aware by proximity regardless). Belt-and-
            // braces, the open dialogue raises the Core InteractionGate the ControlSwitcher honours. Content
            // is DATA — the Interactables carry NpcDef refs, not strings; the words live in Data/NPCs.
            // Art is greybox (Ginny standee + portraits + panel), null-safe if a sprite isn't imported.

            // The speech bubble (builds its own canvas in Awake, so it needs no prefab).
            //
            // ⚠ The two lines that used to be here set "_panelSprite" and "_nameplateSprite" — fields
            // #557 DELETED when the screen-space panel and the portrait went away (the character on
            // screen is the portrait now). SetRef no-ops silently on a property that does not exist,
            // so they had been wiring nothing for some time while still reading as if they were.
            //
            // This is the real thing: the baked bubble kit, wired through one lookup that tolerates
            // absence. Nothing baked → every piece stays null → the presenter draws its tinted-rect
            // greybox exactly as before.
            var dialogueGo = new GameObject("DialogueUI");
            var presenter = dialogueGo.AddComponent<DialoguePresenter>();
            DialogueBubbleArt.Dress(presenter);
            // The wares book rides the same GameObject as the bubble: it is opened BY a conversation
            // (CatalogViewRequested) and closed back TO one (CatalogClosed), and it sorts below the
            // bubble so the speaker is never hidden behind the book she is holding out.
            dialogueGo.AddComponent<HiddenHarbours.Economy.CatalogBookPresenter>();

            // Aunt Ginny — by the cottage on the island plateau. Teaches the buy-and-repair loop; finishing
            // her conversation sets met_ginny (which gates the first onboarding nudge + her warmer re-greet).
            var ginnyGo = MakeNpc("AuntGinny", GinnyPos, LoadSpriteAny(ArtGinny),
                                  waterSprite, new Color(0.78f, 0.55f, 0.62f),
                                  npc: ginnyNpc);
            var ginnyIt = ginnyGo.AddComponent<Interactable>();
            ConfigureInteractableNpc(ginnyIt, ginnyNpc);

            // ⭐ AND SHE FRONTS THE LICENCE FEE — on her, because it is her beat (§7.5). CatchLicensePolicy
            // fails CLOSED, so the clam licence gates the only income day one has: "buy the clam licence
            // with clam money" is a soft-lock, and the ruled fix is a character, not a mechanic. #356 built
            // the mechanism and left it unplaced; this stands it up. It grants ONCE per game, flag-guarded
            // through the Core save service (no schema field of its own), and re-granting is a no-op — so
            // this component landing on a scene that reloads costs nothing.
            //
            // Her WORDS about it are not here and must not be: the grant persists the flag
            // 'ginny_fronted_clam_fee', GinnyOpening.asset gates its ConditionalLines on that same authored
            // key, and the two never reference each other's code (rule 4). The economy writes no prose; the
            // dialogue names no component.
            var frontedFee = ginnyGo.AddComponent<FrontedFeeGrant>();
            SetRef(frontedFee, "_config", config);

            // "Ned's Letter" on the cottage step — the remembered presence (no inherited dory; the boat is
            // bought + mended). Read it to set read_logbook. A small book-sized marker if the art's absent.
            var letterGo = MakeNpc("NedsLetter", NedsLetterPos, null,
                                   waterSprite, new Color(0.62f, 0.47f, 0.30f));
            letterGo.transform.localScale = new Vector3(0.6f, 0.8f, 1f);
            var letterIt = letterGo.AddComponent<Interactable>();
            ConfigureInteractableNpc(letterIt, nedNpc);

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

            // --- AND NOW THEY WALK (design/npcs-and-routines.md §2; P3, A Living Working Coast) -----------
            // ⭐ THE ISLANDERS STOP BEING ANCHORED. StPetersInhabitants placed five people on fixed spots
            // and said so in its own remarks — "routines are M2 — nobody here walks anywhere". This is M2:
            // the island's named places (dooryards, the counter's customer side, the rooms, the flats, the
            // wharf head), the LANES between them — two of which are literally the dirt paths
            // StPetersStarterSplat already paints — and one VillagerRoutine per person with a day authored
            // under Data/Routines.
            //
            // ⚠️ IT MUST RUN AFTER the buildings, the shops, the store counter, the islanders AND the
            // WorldInteractor above: it reads the placed BuildingInterior components to find the real drawn
            // doors, and it finds each villager by the Interactable their NpcDef named. Run it earlier and
            // the stations resolve to nothing and everybody quietly stands still.
            //
            // Where anybody IS at a given hour stays a pure function of (worldSeed, gameTime) — recomputed,
            // never saved (rule 5), so this adds nothing to the save and a region loaded at 14:20 puts
            // every villager mid-stride on the right leg.
            int villagersLiving = StPetersRoutines.Place(terrain);

            // Light onboarding: one nudge per beat of the new earned-dory loop, then it bows out and persists
            // 'onboarded' (on the dory being REPAIRED) so the opening never re-triggers on reload.
            new GameObject("Onboarding").AddComponent<OnboardingDirector>();

            // --- ⭐ THE ARRIVAL (owner ruling, 2026-08-19) ------------------------------------------
            // Onboarding's step ZERO, and the reason the east berth was dredged: a new game does not
            // begin on this beach, it arrives at it. A skipper runs the player in down the buoyed
            // fairway, ties up at the wharf and points them up the path to Ginny — which is exactly
            // where the director above picks the thread up ("meet Aunt Ginny"). Placed at the SCENE
            // root and emphatically NOT in the dev core, which is a grave: everything parented there
            // dies on the travel path, and this must survive a player who arrives by sea.
            //
            // It PLACES and does not draw — the hull, her paint and the man on her deck are all
            // skinned at runtime, for the reason MooredBoat spells out (a builder that instantiated
            // the hull would bake the sprite fallback into the committed scene).
            StPetersArrivalOpening.Place(parent: null);

            // …and the World side of its one line, beside the dialogue view it drives.
            new GameObject("ArrivalLine").AddComponent<ArrivalLineSpeaker>();

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

            // ⭐ THE SEA DOOR — out west into THE WEST WATER (world-map-plan §6 step 1), the island's
            // first way off that is not the tide's to grant. Sail north round the island and west, and
            // the bay's open-water scene takes over; the same trip on foot is the bar 150 m south of
            // here, and only at low water.
            //
            // ⚠ THIS HALF IS ST PETERS' TO WIRE, and the other half is not. WestWaterBuilder authors the
            // passage that brings a boat BACK — a region builder may not reach into another region's
            // scene — so the two builders each own their own side, exactly as the crossing's two halves
            // already do. Guarded on the west water's def existing: until it is built the island simply
            // has no sea door, which is what it has today.
            var westWater = AssetDatabase.LoadAssetAtPath<RegionDef>(
                                DataRegions + "/" + WestWaterPlan.RegionAssetName + ".asset");
            if (westWater != null)
            {
                var seaDoorGo = new GameObject("PassageToWestWater");
                seaDoorGo.transform.position = ToWestWaterPassagePos;
                var seaTrigger = seaDoorGo.AddComponent<BoxCollider2D>();
                seaTrigger.isTrigger = true;
                seaTrigger.size = WestWaterPassageBandSize;
                var seaDoor = seaDoorGo.AddComponent<RegionPassage>();
                // NAMED: the west water has two doors of equal standing, so it must be told which one
                // this boat came in by (#456) or she lands at the far end of the run she just started.
                seaDoor.Configure(westWater, loader, WestWaterPlan.FromStPetersArrivalKey);
                SetRefArray(loader, "_regions", new Object[] { stPeters, nineMileCreek, westWater });
                SetRefArray(registrar, "_regions", new Object[] { stPeters, nineMileCreek, westWater });
            }
            else
            {
                Debug.LogWarning("[StPetersBuilder] No West Water RegionDef at " + DataRegions + "/" +
                                 WestWaterPlan.RegionAssetName + ".asset — the island has no sea door, so " +
                                 "the only way off is the tidal bar. Re-run after building the west water.");
            }

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
                      $"of {villageBuildings} houses + {shopBuildings} shop(s) — the school, the houses, the " +
                      $"general store and the post office — standing round the green beside the start spawn, " +
                      $"with {villagersLiving} villager(s) keeping a day in it. Aunt Ginny is not among the " +
                      $"village any more: her {ginnyPlotBuildings} building(s) stand on her own plot " +
                      $"{Vector2.Distance(StPetersGinnyPlot.CottagePos, VillageHearthPos):0.0} m east, in " +
                      "the woods.");
            Debug.Log($"[StPetersBuilder] THE COAST IS PAINTED: {coast.GroundTiles:N0} shoreline-ISO ground " +
                      $"tiles + {coast.FringeTiles:N0} fringe overlays + {coast.Rocks} rocks on the reef " +
                      $"({coast.MaterialSummary()}). The island paints at its re-ruled 240 x 140 m " +
                      "(2026-07-30) inside the unchanged 760 x 520 m sea.");
            Debug.Log("[StPetersBuilder] Built StPeters.unity — the OPENING + START region (greybox), now " +
                      "with the PERSISTENT CORE so it's playable: press Play and you control the on-foot " +
                      "fisher at the START spawn (WASD), the camera follows at the on-foot framing, and the " +
                      "clock/tide run (GameServices online → the tide advances + the bar bares/floods). The " +
                      "moored hand-rowed Dory floats off the south coast (board at the slip once she's " +
                      "yours). Island = high (always exposed); the SANDBAR bridges it to Nine Mile Creek as a " +
                      "tide-gated path: the crest (0.88 m) bares as the BIG tide (±2.2 m) falls, while a " +
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
                "as the big tide (±2.2 m) falls and covers them with depth-graded water as it floods (the " +
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
        /// [Range(16,256)]; pass 256 if the near-flat bar crest still facets at 192.
        ///
        /// <para><b>A FORWARDER now, not a second implementation.</b> The body moved to
        /// <see cref="WaterSceneTemplate.ConfigureWaterSurface"/> so the two land builders cannot drift
        /// apart again; this entry point stays because the shoreline-render tests are written against it.
        /// The elevation range it forwards — <see cref="DeepHarbourElevation"/> to
        /// <see cref="IslandElevation"/> — is exactly the component's own −4…6 default this always relied
        /// on, now said out loud instead of inherited.</para></summary>
        public static void ConfigureWaterSurface(HiddenHarbours.Art.WaterSurface surface,
                                                 Vector2 worldCenter, Vector2 worldSize, int resolution)
            => WaterSceneTemplate.ConfigureWaterSurface(surface, worldCenter, worldSize, resolution,
                                                        DeepHarbourElevation, IslandElevation);

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
        /// the calm look to a fixed preset copy.
        ///
        /// <para><b>A FORWARDER now</b>, for the same reason as <see cref="ConfigureWaterSurface"/> — one
        /// implementation, in <see cref="WaterSceneTemplate"/>, so the land builders cannot drift.</para></summary>
        public static void ConfigureWeatherPalette(HiddenHarbours.Art.WaterSurface surface,
                                                   Material baseMood, Material calmMood,
                                                   Material stormMood, Material fogMood)
            => WaterSceneTemplate.ConfigureWeatherPalette(surface, baseMood, calmMood, stormMood, fogMood);

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
            SetF(so, "_approachHalfWidth", ApproachHalfWidth);
            SetV2(so, "_approachFrom", ApproachFrom);
            SetV2(so, "_approachTo", ApproachTo);
            SetF(so, "_approachThalwegHalfWidth", ApproachThalwegHalfWidth);
            SetF(so, "_approachBedElevation", ApproachBedElevation);
            SetV2(so, "_berthPocketFrom", BerthPocketFrom);
            SetV2(so, "_berthPocketTo", BerthPocketTo);
            SetF(so, "_berthPocketHalfWidth", BerthPocketHalfWidth);
            SetF(so, "_berthPocketThalwegHalfWidth", BerthPocketThalwegHalfWidth);
            SetF(so, "_berthPocketBedElevation", BerthPocketBedElevation);
            SetF(so, "_islandFalloff", IslandFalloff);
            SetF(so, "_islandElevation", IslandElevation);
            SetV2(so, "_sandbarFrom", SandbarFrom);
            SetV2(so, "_sandbarTo", SandbarTo);
            SetF(so, "_sandbarHalfWidth", SandbarHalfWidth);
            SetF(so, "_sandbarCrestElevation", SandbarCrestElevation);
            SetF(so, "_channelAlong", ChannelAlong);
            SetF(so, "_channelHalfWidth", ChannelHalfWidth);
            SetF(so, "_channelBedElevation", ChannelBedElevation);

            // The coast plan, and the cliff tunables it switches on.
            SetF(so, "_coastBlendDegrees", CoastBlendDegrees);
            SetF(so, "_cliffPlungeWidth", CliffPlungeWidth);
            SetF(so, "_cliffToeTideFraction", CliffToeTideFraction);
            SetF(so, "_ledgeBenchTideFraction", LedgeBenchTideFraction);
            SetF(so, "_ledgeBenchWidth", LedgeBenchWidth);

            // ⚠ The tide REFERENCE the fraction elevations are measured against — the region's own
            // authored tide, pushed from the same constants the environment gets, so a retune moves the
            // ledges with the water instead of stranding them. Not a tide the sim reads: the sim still
            // recomputes water level from (worldSeed, gameTime). StPetersCoastTests holds the two equal.
            SetF(so, "_tideMean", TideMean);
            SetF(so, "_tideAmplitude", TideAmplitude);

            SetCoastSectors(so, "_coastSectors", CoastSectors);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Write the sector ring onto a serialized array field. Element-wise rather than by
        /// assigning the property, so the plan persists through the builder's SerializedObject
        /// convention like every other authored number on the component.</summary>
        static void SetCoastSectors(SerializedObject so, string field, CoastSector[] sectors)
        {
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.arraySize = sectors.Length;
            for (int i = 0; i < sectors.Length; i++)
            {
                var element = prop.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("FromBearing").floatValue = sectors[i].FromBearing;
                // intValue, not enumValueIndex: it writes the enum's VALUE rather than its position in
                // the inspector popup. The two agree only while CoastClass stays contiguous from zero,
                // and a member inserted mid-enum would silently reclassify the whole coast.
                element.FindPropertyRelative("Class").intValue = (int)sectors[i].Class;
            }
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

        /// <summary>
        /// Persist an ENUM field by NAME, not by ordinal — the same helper (and the same reasoning)
        /// NineMileCreekBuilder carries: <c>SerializedProperty.enumValueIndex</c> indexes
        /// <c>enumNames</c> rather than the enum's value, so writing <c>(int)someEnum</c> is only right
        /// while the enum happens to be contiguous from zero. LOUD on a miss, because a market left on
        /// its default keeps working at another outlet's prices (MarketPriceGapTests' closing case).
        /// </summary>
        static void SetEnum<TEnum>(Component c, string field, TEnum value) where TEnum : System.Enum
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null || p.propertyType != SerializedPropertyType.Enum)
            {
                Debug.LogWarning($"[StPetersBuilder] no enum field '{field}' on {c.GetType().Name}.");
                return;
            }
            int i = System.Array.IndexOf(p.enumNames, value.ToString());
            if (i < 0)
            {
                Debug.LogWarning($"[StPetersBuilder] '{value}' is not a member of '{field}' on " +
                                 $"{c.GetType().Name} — left at its default.");
                return;
            }
            p.enumValueIndex = i;
            so.ApplyModifiedPropertiesWithoutUndo();
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

        static void SetV2(SerializedObject so, string field, Vector2 value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.vector2Value = value;
            else Debug.LogWarning($"[StPetersBuilder] no Vector2 field '{field}'.");
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
        /// <summary>
        /// A placed person's body. With an <paramref name="npc"/> that carries a baked build this goes
        /// through <see cref="NpcBodyDresser"/> — the same ladder the island's and the creek's casts
        /// use, so Ginny is dressed by the same code as the neighbours rather than by a second copy of
        /// the rule. Without one it is the old two-step: the conventional sprite, else the marker.
        /// </summary>
        static GameObject MakeNpc(string name, Vector3 pos, Sprite sprite, Sprite fallback,
                                  Color fallbackColor, NpcDef npc = null,
                                  float headingDegrees = 180f)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 9;   // pre-Play default only; the YSortSprite below OWNS the order

            // Layer with the player by world Y like every other thing you can walk past. Added BEFORE the
            // dress branches so both the baked-body and the flat-sprite path get it. Without this they held
            // a FIXED order 9 — which read correctly only while the player's own Y-sort resolved (the old
            // band covered −7.5…+2.0 m, and the village stands at Y +8…+33), so up in the village the
            // player drew BEHIND every NPC, even face to face. Added STATIC; StPetersRoutines turns it
            // DYNAMIC for whoever it gives a day to keep (Ginny among them, whose day stays in her own
            // dooryard).
            go.AddComponent<YSortSprite>();

            if (npc != null && npc.HasBakedBody)
            {
                NpcBodyDresser.Dress(go, sr, npc, artStem: null, greyboxSquare: fallback,
                                     greyboxTint: fallbackColor, headingDegrees: headingDegrees);
                return go;
            }

            if (sprite != null) { sr.sprite = sprite; go.transform.localScale = Vector3.one; }
            else { sr.sprite = fallback; sr.color = fallbackColor; go.transform.localScale = new Vector3(1f, 2f, 1f); }
            return go;
        }

        // Wire an Interactable to an NpcDef (the data-driven path), via the builder's persist-the-refs
        // SerializedObject convention so the ref survives into the saved scene. There is no portrait to
        // wire: the bubble anchors at the speaker and they are their own portrait (owner ruling 2026-07-30).
        static void ConfigureInteractableNpc(Interactable it, NpcDef npc)
        {
            var so = new SerializedObject(it);
            var npcProp = so.FindProperty("_npc");
            if (npcProp != null) npcProp.objectReferenceValue = npc;
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
            // characters. A small base-Y term sorts holes among themselves (lower on screen = higher
            // Y-negated = draws in front) — clamped to -4..4 so the term stays a hole-vs-hole tiebreak.
            //
            // ⚠ The clamp's top (4) is INSIDE the decor band, whose floor is 2 — so "can't pop in front of
            // the player" was never what the clamp guaranteed. What actually keeps a hole under the player
            // is that the player's own Y-sort resolves: on the bar they sort near SortingBands.DecorBase
            // (~1202), far above any hole. Before ADR 0032 that was only true south of Y = +2, and north of
            // it the player saturated to order 2 and holes at 3-4 drew OVER their feet. Re-basing the band
            // fixed that; the clamp is left alone because it is only ever a tiebreak between flat holes.
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

            // THE HOLE IS AN INTERACT CANDIDATE NOW (the tools-in-hand ruling): it registers itself and the
            // one verb picks the nearest qualifying one, instead of ClamDigger reading E and scanning by
            // hand. Two things it needs from the builder:
            //
            //  • a UNIQUE, READABLE id — the resolver's LAST tie-break, so two holes sharing one would make
            //    its order non-total and let registration order decide between them. Derived from the
            //    hole's own scattered position, which the scatter guarantees is one per grid cell and
            //    which a rebuild reproduces exactly (the scatter is a stable hash, not RNG).
            //  • the TOOL id the dig gates on. Passed explicitly rather than left to the field default so
            //    the gate is visible HERE, at the place that decides what the opening's flats require.
            dig.ConfigureInteract($"fixture.clam_hole.{pos.x:0.00}_{pos.y:0.00}", ToolDef.ShovelId);

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

        /// <summary>
        /// Stand one carriable tool on the ground — the rod or the clam shovel (the tools-in-hand ruling).
        ///
        /// <para>Null-safe in the greybox way the rest of this builder is: a missing Def logs and builds
        /// NOTHING, because a tool object with no Def is not carriable and would be an invisible,
        /// un-liftable prop sitting in the sand pretending to be the shovel. A missing SPRITE is survivable
        /// by comparison — the object still works, it is just not drawn — so that only warns.</para>
        /// </summary>
        static void MakeTool(Transform parent, string defPath, string spritePath, Vector2 pos, string id)
        {
            var tool = AssetDatabase.LoadAssetAtPath<ToolDef>(defPath);
            if (tool == null)
            {
                Debug.LogWarning($"[StPetersBuilder] No ToolDef at '{defPath}' — no tool placed. The dig " +
                                 "and the cast both gate on a tool being IN HAND, so without this the " +
                                 "opening has no way to dig or fish.");
                return;
            }

            var go = new GameObject(tool.name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSpriteAny(spritePath);
            if (sr.sprite == null)
                Debug.LogWarning($"[StPetersBuilder] No sprite at '{spritePath}' — {tool.Id} is invisible " +
                                 "but still liftable.");

            // Lying on the ground among the decor: Y-sorted like every other walk-up prop (ADR 0032), so it
            // draws behind the fisher standing north of it and in front of one standing south. While CARRIED
            // this component stands itself down and CarryHands states the order instead — one Y-sort policy
            // at a time, never two fighting over the renderer.
            go.AddComponent<YSortSprite>();

            var carriable = go.AddComponent<CarriableTool>();
            carriable.Configure(tool, sr, id);
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
