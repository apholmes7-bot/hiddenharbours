#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.World;
using HiddenHarbours.Environment;        // GameClock/EnvironmentService (the dev-bootstrap core)
using HiddenHarbours.Player;             // PlayerWalkController/ClamBucket/PlayerWallet/DevToast (dev core)
using HiddenHarbours.Fishing;            // FishingController/DevFishingInput (dev core)
using HiddenHarbours.UI;                 // HudController (dev core — mirrors the persistent core's HUD)
using HiddenHarbours.Art;                // YSortSprite — buildings layer with the player by world Y
using HiddenHarbours.Art.Editor;        // VS-23 locked Pixel-Perfect camera convention
using UnityEngine.Rendering.Universal;   // PixelPerfectCamera

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// One-click <b>Nine Mile Creek</b> — the MAINLAND. Menu: Hidden Harbours ▸ Build Nine Mile Creek
    /// Scene. Re-runnable (idempotent on the assets).
    ///
    /// <para><b>⭐ PHASE A-2 OF THE RECREATION.</b> The region shipped as a 120 × 120 m harbour ISLAND
    /// with a rectangular quay poking east into a dredged −6 m basin — a stand-in for Port Greywick,
    /// authored before the 2026-07-25 ruling split the two. The owner's reference photographs are of a
    /// MAINLAND wharf on a low red spit, fields behind it, a barachois pond, and St Peters lying offshore
    /// to the south-east. That is a different landform, not a bigger one.</para>
    ///
    /// <para><b>Phase A-1 (#453) wrote the new geography down and proved it</b> —
    /// <see cref="NineMileCreekMainland"/> plus <c>MainlandTidalTerrain</c> / <c>MainlandCoast</c>, with
    /// thirty EditMode assertions, as data and pure maths with no builder on top. <b>This is the wiring:</b>
    /// the terrain the scene carries, every creekside site moved onto the new coast, the region's own
    /// def, and the two passages — the return to Coddle Cove and, new, the tidal crossing to St Peters.
    /// Everything geographic here is a window onto the plan; this builder authors no coastline of its
    /// own.</para>
    ///
    /// <para>The wharf lives in <see cref="NineMileCreekWharf"/>: two walls registered as standable floor
    /// at their MEASURED deck height, the mooring fittings and the <c>ShoreCleat</c>s derived from the
    /// same table, and the breakwater's collision line. It no longer DRAWS the quay — the walls are
    /// terrain fills and the drawn ISO quay is Phase B's (owner's ruling, 2026-08-07).</para>
    ///
    /// SCOPE / TODO: this scene currently carries its own Main Camera + AudioListener so it can be
    /// opened and reviewed standalone. When the additive Cove↔Nine Mile Creek transition is fully wired (player
    /// persistence + unloading the origin region — a bootstrap/GreyboxBuilder change, out of scope here),
    /// region scenes should drop their camera/listener in favour of a persistent bootstrap. The wharf's
    /// hold/wallet providers (the player's boat + wallet) live in the origin scene, so they're left
    /// unwired here with the same TODO. The matching Cove→Nine Mile Creek passage belongs in the cove scene.
    /// </summary>
    public static class NineMileCreekBuilder
    {
        const string DataConfig  = "Assets/_Project/Data/Config";
        const string DataShip    = "Assets/_Project/Data/Shipwright";
        const string DataRegions = "Assets/_Project/Data/Regions";
        const string DataLicenses= "Assets/_Project/Data/Licenses";   // St Peters opening: the cod licence
        const string DataGear    = "Assets/_Project/Data/Gear";        // St Peters opening: the rod
        const string ArtSprites  = "Assets/_Project/Art/Sprites";
        const string ArtSea      = "Assets/_Project/Art/Tilesets/Water/SeaTile.png";
        const string ArtWaterMat = "Assets/_Project/Art/Materials/Water.mat";   // the layered SIM-driven water shader (ADR 0010)
        // (The ADR 0017 weather-preset paths that stood here are gone with the local water wiring — they
        // live once now, in WaterSceneTemplate, which is what loads them for both land regions.)
        // (ArtGrass and ArtSand are RETIRED — the 1 m grass tile and the sand strip that the greybox
        // ground was tiled from. Nothing reads them now: the ground is painted (ADR 0028) and its
        // stand-in is a flat scaled quad, because tiling a 1 m sprite over 760 × 560 m asks for 425,600
        // tiles and gets a refusal. See MakeGreyboxGround. The PNGs stay on disk — Tilesets/Tiles/Grass
        // .asset, Sand.asset and the HiddenHarboursTerrain palette are built on them.)
        const string ArtTerrainSplatMat = "Assets/_Project/Art/Materials/TerrainSplat.mat"; // the splat-shaded ground (ADR 0028)
        const string DataTerrain        = "Assets/_Project/Data/Terrain";
        // ⚠ ORPHANED BY #557, kept only so the paths are findable. The screen-space panel and the
        // nameplate were deleted when the bubble became anchored art with no portrait; the speech
        // bubble's art now comes from the baked kit via DialogueBubbleArt.Dress. Nothing reads these.
        const string ArtDialoguePanel = "Assets/_Project/Art/UI/DialoguePanel.png";   // unused since #557
        const string ArtNamePlate     = "Assets/_Project/Art/UI/NamePlate.png";       // unused since #557
        const string ArtShipwright = "Assets/_Project/Art/Sprites/Buildings/ShipwrightShed.png";
        const string ArtFishStall  = "Assets/_Project/Art/Sprites/Buildings/FishBuyerStall.png";
        const string ArtHouseRed   = "Assets/_Project/Art/Sprites/Buildings/NineMileCreekHouseRed.png";
        const string ArtHouseTeal  = "Assets/_Project/Art/Sprites/Buildings/NineMileCreekHouseTeal.png";
        const string Scenes      = "Assets/_Project/Scenes";
        const string SceneName   = "NineMileCreek";
        const string ScenePath   = Scenes + "/" + SceneName + ".unity";

        // Dev-bootstrap core art/data (mirrors PersistentCoreBuilder's player + gauge wiring).
        const string DataFish        = "Assets/_Project/Data/Fish";
        const string ArtFisher       = "Assets/_Project/Art/Characters/FisherSheet.png";
        const string IsoFisherVisual = "Assets/_Project/Data/Characters/FisherIso.asset";
        // (The fight's UI art — TensionGauge / LineHook / FishOnSilhouette — is no longer wired anywhere:
        // the rod fight has no HUD. Owner's ruling 2026-07-23.)

        // =================================================================================================
        //  ⭐ THE GEOGRAPHY IS THE PLAN'S. This block FORWARDS, it does not author.
        // =================================================================================================
        // Phase A-1 (#453) wrote Nine Mile Creek's mainland down in ONE place — NineMileCreekMainland —
        // and proved it with thirty EditMode assertions before a line of builder code was written on top.
        // A-2's job is to make the scene say what that plan says, so every constant this builder used to
        // author is now a window onto it. Two copies of a coastline is the failure mode this region has
        // already been through once (#345), and a builder with a comment and a test with a literal is the
        // shape it takes.
        //
        // They stay PUBLIC and keep their names because the dressing layers and the tests read them
        // (NineMileCreekWharf, NineMileCreekDory, NineMileCreekFlavour, NineMileCreekPeople, and six test
        // files) — re-pointing the window moves all of them at once, which is the whole point.
        //
        // ⚠ EXPRESSION-BODIED, not `static readonly`, on purpose: a `static readonly` initialised from
        // another type's `static readonly` binds at type-init time, and this file already has a static
        // dependency on StPetersBuilder through the plan's CrossingTotalMetres. A property cannot be
        // caught by an initialisation order.
        //
        // WHAT CHANGED, IN ONE LINE: the region was a 120 × 120 m harbour ISLAND whose wharf pointed east
        // into a dredged -6 m basin. It is now 760 × 560 m of MAINLAND — water east, fields west, a
        // squared-U wharf on a made spit at the creek's mouth, and the tidal bar to St Peters coming
        // ashore 260 m south of it.

        /// <summary>The persistent ControlSwitcher's dock radius — the boat must PARK inside it or the
        /// player lands out of range and cannot disembark (owner-playtest gap #52; keep it).</summary>
        public const float DockZoneRadius = NineMileCreekMainland.DockZoneRadius;

        /// <summary>Where the greybox ground plane sits in the stack: below the sea, so the water shader's
        /// wet-dry clip is what decides whether you see ground or sea at a given tide. Named off the
        /// partition rather than typed, so a re-base of the bands moves it (ADR 0032).
        ///
        /// <para>⚠ <b>RE-BASED from <c>Sea - 2</c> (−7) to the rung it always claimed to be on.</b> The
        /// greybox ground STANDS IN FOR THE PAINTED GROUND, which draws at
        /// <see cref="HiddenHarbours.Art.TerrainSplatSurface.DefaultSortingOrder"/> (−21) — so everything
        /// laid ON the ground belongs above it. At −7 it was above the road kit's two layers (−17 / −16),
        /// above a submerged shore plant (−8), and eleven rungs clear of the band it replaces: an opaque
        /// green rectangle over Wharf Road and the bar road. It never actually drew that over them only
        /// because the tiling it was built from was refused by the engine (see
        /// <see cref="MakeGreyboxGround"/>) — so fixing the draw without fixing this would have TURNED ON
        /// a defect the builder's own comment already flagged and left.</para>
        ///
        /// <para><see cref="SortingBands.PaintedSeabedMax"/> is the tightest value that satisfies both
        /// standing rules — under the sea, above the painted seabed (<c>NineMileCreekWharfTests</c>
        /// asserts each) — and it is the first rung under the roads. "Above the painted seabed, which is
        /// the rung below it" is what that test's own message has always said this number meant; now it
        /// says it exactly rather than approximately.</para></summary>
        public const int GroundSortingOrder = SortingBands.PaintedSeabedMax;

        /// <summary>
        /// A passage trigger's band, in metres — wide ACROSS the way you are travelling and deep enough
        /// that you cannot slip through it at speed between two physics steps.
        ///
        /// <para>Both of this region's passages use it and both are crossed heading roughly east/west, so
        /// the band is a tall north–south gate. Forgiving on purpose (P5): the re-fire guard on
        /// <c>RegionPassage</c> is what makes a wide band safe, so width costs nothing and narrowness
        /// costs a crossing that silently does not fire.</para>
        /// </summary>
        public static readonly Vector2 PassageBandSize = new Vector2(6f, 40f);

        /// <summary>Where the boat parks arriving under power — in the basin, off the north wall's face.</summary>
        public static Vector3 ArrivalPos   => NineMileCreekMainland.ArrivalPos;
        /// <summary>The dock zone: against the north wall's south face, beside the unloading apron.</summary>
        public static Vector3 DockZonePos  => NineMileCreekMainland.DockZonePos;
        /// <summary>Step ashore onto the north wall's deck.</summary>
        public static Vector3 DisembarkPos => NineMileCreekMainland.DisembarkPos;
        /// <summary>Return passage to Coddle Cove — the cove lies EAST (canon), so you sail east out of
        /// the bay, well clear of the crossing's flats to the south.</summary>
        public static Vector3 ToCovePassagePos => NineMileCreekMainland.ToCovePassagePos;

        /// <summary>The walk-out band at the mainland's bar tip: cross it heading ESE and the crossing
        /// hands over to St Peters, mid-bar.</summary>
        public static Vector3 ToStPetersPassagePos => NineMileCreekMainland.ToStPetersPassagePos;

        /// <summary>⭐ The SEA door: hold east past the breakwater head and the west water takes over —
        /// the wharf's way out that does not wait on the tide (world-map-plan §6 step 1). The bar is the
        /// same trip on foot, 158 m south of this band and only at low water.</summary>
        public static Vector3 ToWestWaterPassagePos => NineMileCreekMainland.ToWestWaterPassagePos;

        /// <summary>Where the player STANDS arriving from St Peters ON FOOT, mid-crossing — the other end
        /// of the same bar. Consumed at last, through the per-passage arrival seam (#456): this is the
        /// region's named <see cref="BarArrivalKey"/> arrival.</summary>
        public static Vector3 WalkArrivalPos => NineMileCreekMainland.WalkArrivalPos;

        /// <summary>
        /// The name the St Peters side's passage asks for when the player walks in over the bar (#456).
        ///
        /// <para>⭐ THIS IS WHY PR 1 EXISTED. Nine Mile Creek is the first region with two doors, and they
        /// are <see cref="WalkArrivalPos"/> and <see cref="DisembarkPos"/> — 400 m apart. Without a named
        /// arrival, a fisher who has just walked 610 m of wet cobble is teleported onto the wharf deck the
        /// moment they step ashore.</para>
        ///
        /// <para>⚠ The MATCHING half is a passage in the St Peters scene pointing here with this key, and
        /// it is <b>not</b> wired by this builder — a region builder may not reach into another region's
        /// scene. It is StPetersBuilder's, and until it lands the walk-in lands at the wharf exactly as it
        /// did before, which is the fallback the seam was built to give.</para>
        /// </summary>
        public const string BarArrivalKey = "bar";

        // --- CREEKSIDE SITES (named, because other things are derived FROM them) ---------------------
        // Same windows-onto-the-plan rule as the block above: the people, the dory's sightline, the
        // flavour houses and the tests all ask where a building IS, and none of them may hold a second
        // copy of the number (the island's #345 lesson).
        //
        // ⚠ THE SITES DID NOT MOVE A FEW METRES — THEY MOVED TO A DIFFERENT LANDFORM. The old row at
        // x ∈ [−12, −6] was the 24 m-wide town strip of a 120 m island. The mainland separates what that
        // strip conflated: the WORKING sites are on the made spit at the creek's mouth (x ≈ 60…150,
        // y ≈ 96…140) and the TOWN is inland on the through-road, 230 m west of the shore — which is how
        // a rural PEI community actually sits, and is the whole reason the walk between them exists.

        /// <summary>
        /// The fish buyer's till, at his tailgate on the spit by the parking (the plan's §7 site).
        ///
        /// <para>⚠ HE IS NO LONGER WITHIN A STALL'S REACH OF THE PLANKS, and that is the geography rather
        /// than a regression. On a 120 m island the whole region fitted inside the on-foot frame, so "you
        /// arrive and you SELL" and "the till is 4 m from the deck" were the same sentence. On an 84 m
        /// working wharf they are not: the buyer stands where a buyer stands, on the apron among the
        /// trucks, and you walk up the quay past the sheds to him. What must stay true — and what the test
        /// now measures instead — is that selling happens ON THE WORKING SPIT and is a fraction of the
        /// walk to town, never a trip up the hill.</para>
        /// </summary>
        public static Vector3 FishBuyerPos => NineMileCreekMainland.FishBuyerPos;

        /// <summary>
        /// The shed that sells the Punt and the pots — the plan's reserved boat-shed lot in town.
        ///
        /// <para>⚠ NAMED NEUTRALLY ON PURPOSE, and this is an OPEN RULING, not a decision this slice
        /// makes. The 2026-07-25 ruling says there is no shipwright in this region; the shipped scene has
        /// one selling the Punt and the pots, and the economy data hangs off it. A-1 reserved a lot under
        /// a neutral name so nothing breaks. WHERE the shipwright's yard really lives is the
        /// coordinator's question — flagged, not settled (plan §10).</para>
        /// </summary>
        public static Vector3 ShipwrightShedPos => NineMileCreekMainland.BoatShedPos;

        /// <summary>The hard the damaged dory is bought off — and where the used-outboard man stands. On
        /// the spit beside the derelict herself, because the boat you are shown and the boat you are sold
        /// have to be the same boat.</summary>
        public static Vector3 DoryYardPos => NineMileCreekMainland.DoryYardPos;

        /// <summary>Hector's barrel — the used-outboard till. DERIVED from where he actually stands
        /// (<see cref="NineMileCreekPeople.OutboardStallMetres"/> out from the yard toward the water), so
        /// the man and the counter cannot come apart: a player who can talk to him but not buy from him
        /// has met a decoration.</summary>
        public static Vector3 HectorsBarrelPos
        {
            get
            {
                Vector2 p = NineMileCreekPeople.Toward(DoryYardPos, NineMileCreekWharf.DeckFootprint().center,
                                                       NineMileCreekPeople.OutboardStallMetres);
                return new Vector3(p.x, p.y, 0f);
            }
        }

        /// <summary>The harbourmaster's office: the cod licence. In town, on the through-road.</summary>
        public static Vector3 HarbourmasterPos => NineMileCreekMainland.HarbourmasterPos;

        /// <summary>The chandlery: the rod. In town.</summary>
        public static Vector3 ChandleryPos => NineMileCreekMainland.ChandleryPos;

        /// <summary>Flavour, north — one of the plan's nine town lots. The village kit's houses measure
        /// 6.6 × 8.1 m and 7.0 × 8.7 m in its own contract, and the plan reserves 6 m of radius per lot,
        /// so the pair clear each other by construction rather than by a typed-in gap; the tests re-derive
        /// both numbers from the contract rather than trusting this comment.</summary>
        public static Vector3 FlavourHouseRedPos => NineMileCreekMainland.HouseNorthPos;

        /// <summary>Flavour, south — strung along the same through-road, 136 m away, so the two read as a
        /// community spread along a road rather than a matched pair.</summary>
        public static Vector3 FlavourHouseTealPos => NineMileCreekMainland.HouseSouthPos;

        // --- THE REGION'S TIDE — ⚠ THIS IS A CHANGE, AND IT IS FORCED --------------------------------
        // The region shipped mean 0, amplitude 0.8 m, phase 2 h: the "gentle market harbour so business is
        // never stranded" profile, authored when Nine Mile Creek was standing in for Port Greywick.
        //
        // It cannot survive the recreation, for a reason that is geometry rather than taste: the tidal bar
        // to St Peters is ONE bar SPANNING THE REGION SEAM, and its exposure is a function of (crest,
        // amplitude, phase). Two tides either side of the seam means the crossing is dry on one side and
        // flooded on the other at the same instant — the region's whole lesson, broken by arithmetic.
        // So the mainland takes St Peters' tide verbatim (plan §2; endorsed in the A-2 handoff).
        public const float TideMean = NineMileCreekMainland.TideMean;
        public const float TideAmplitude = NineMileCreekMainland.TideAmplitude;
        public const float TidePhaseHours = NineMileCreekMainland.TidePhaseHours;

        /// <summary>
        /// The highest water that can actually reach this region.
        ///
        /// <para>Still folded through <see cref="RegionValidation.WidestSwing"/> even though the two
        /// swings are now IDENTICAL — deliberately. Nothing re-points the tide per region yet, so the
        /// START scene's profile is what really runs here; the fold is what makes that fact survive the
        /// day somebody re-tunes one of the two, and a hard-coded 2.2 would not.</para>
        /// </summary>
        public static float SpringHighWater =>
            RegionValidation.WidestSwing(
                RegionValidation.SwingOf(TideMean, TideAmplitude),
                RegionValidation.SwingOf(StPetersBuilder.TideMean, StPetersBuilder.TideAmplitude)).High;

        /// <summary>
        /// The ground a creekside building reserves, as a RADIUS. These are still greybox 5 × 5 m squares
        /// (the village kit dresses them in a later pass), so this is that square's half-diagonal — a
        /// circle for the same reason the island's village reserves one: it is the same claim at every
        /// facing, and a facing is the first thing that changes.
        /// </summary>
        public static readonly float CreeksideBuildingRadius = Mathf.Sqrt(2f) * 2.5f;

        /// <summary>
        /// Every WORKING building site — the ones still placed as loose sprites with
        /// <see cref="CreeksideBuildingRadius"/> for a footprint, for anything that has to ask "is one of
        /// these in the way?" (the dory's arrival sightline, for one).
        ///
        /// <para>The two flavour houses are deliberately NOT here: they come from the village kit now, so
        /// their footprints are published numbers rather than a stand-in radius, and anything asking
        /// about them should ask the contract. <see cref="NineMileCreekFlavour"/> is where they live.</para>
        /// </summary>
        public static IReadOnlyList<Vector3> CreeksideBuildingSites => new[]
        {
            FishBuyerPos, ShipwrightShedPos, DoryYardPos, HarbourmasterPos, ChandleryPos,
        };

        /// <summary>
        /// Which market the creek's buyer IS. It has to be said out loud: <see cref="Market"/> defaults to
        /// <see cref="MarketId.Cove"/>, and a Market left at its default here quietly reads the HOME
        /// COVE's demand and price level — so the island store's "deliberately worse prices" (§7.5) would
        /// be measured against the wrong outlet and the whole reason to cross would evaporate with
        /// nothing failing.
        /// </summary>
        public const MarketId CreekMarket = MarketId.NineMileCreek;

        // --- THE WATER MODEL: a MAINLAND coast, not a dredged basin (ADR 0012 rec. 4 / ADR 0014) --------
        // Same converged model as before and as St Peters — ONE authored height field registered into
        // GameServices.TidalTerrain, with the WaterSurface shader baking it, so the visible waterline and
        // the walkability / boat-grounding gate read the same number (P1). What changed is the field:
        //
        //   WAS  RectTidalTerrain — two axis-aligned plateaus (a 24 × 40 m town strip and an 8 × 6 m
        //        wharf) over a flat -6 m DREDGED floor, inside a 120 × 120 m plane.
        //   NOW  MainlandTidalTerrain — an open coast RUN with a coast plan (beach · dune · ledge · gully
        //        · cliff · deep shore), the tidal bar to St Peters, two ponds carved behind the shore and
        //        the harbour shoal / spit / walls / breakwater filled on top, inside 760 × 560 m.
        //
        // ⚠ THE SHORELINE FENCE IS GONE, and its job is done properly now. The region used to carry a
        // hand-traced EdgeCollider2D at x = -4 that dipped around the wharf, because a rectangular quay on
        // a flat -6 m floor gave the boat nothing to ground on. A mainland does not need one: the hull is
        // stopped by DEPTH against the authored terrain (BoatController's shallows drag over
        // BoatCrossing.DepthAt), which is exactly how St Peters — a painted region with no fence at all —
        // already works. One coastline, and it is the one the water draws.
        //
        // ⚠ AND THE DEEP-HARBOUR CANON IS RETIRED WITH IT. Nine Mile Creek is no longer "the deep dredged
        // harbour": the ruled ladder is three HARBOURS (St Peters ~0.6 m, here ~1.6 m, Port Greywick 6 m
        // dredged), and this is the lobster-boat berth. RegionDef goes IsDeepHarbour false /
        // HarbourDepthMeters 1.6 below.

        /// <summary>The open bay floor — nothing grounds out there.</summary>
        public const float NineMileCreekDeepElevation = NineMileCreekMainland.BayFloorElevation;
        /// <summary>The fields inland of the shore: dry at every tide.</summary>
        public const float NineMileCreekLandElevation = NineMileCreekMainland.LandElevation;
        /// <summary>⭐ THE RULED GATE — the harbour shoal the wharf stands out onto, and the one number
        /// every hull here is measured against (plan §3/§6).</summary>
        public const float NineMileCreekBasinElevation = NineMileCreekMainland.BasinBedElevation;

        /// <summary>The region's world rectangle — sized by TIME TO CROSS (plan §1). The long axis carries
        /// the crossing; the short axis is the landing → wharf → town walk. 760 m is St Peters' own width
        /// deliberately: the same water 610 m away gets the same scale.</summary>
        public static Vector2 NineMileCreekSeaCenter => NineMileCreekMainland.RegionWorldCenter;
        /// <inheritdoc cref="NineMileCreekSeaCenter"/>
        public static Vector2 NineMileCreekSeaSize   => NineMileCreekMainland.RegionWorldSize;

        /// <summary>Height-bake resolution for the water shader — DERIVED from the extent and the ruled
        /// 2 px/m inshore figure, never a literal, and clamped to what a region may ask for.</summary>
        public static int NineMileCreekHeightResolution => NineMileCreekMainland.WaterHeightBakeResolution;
        /// <summary>The elevation range the baked R channel maps across. It must BRACKET the whole field
        /// or the bake clips: the bay floor at the bottom, the fields at the top.</summary>
        public const float NineMileCreekHeightMin = NineMileCreekMainland.BayFloorElevation;
        /// <inheritdoc cref="NineMileCreekHeightMin"/>
        public const float NineMileCreekHeightMax = NineMileCreekMainland.LandElevation;

        [MenuItem("Hidden Harbours/Build Nine Mile Creek Scene")]
        public static void Build()
        {
            // ADR 0019 §1 guard: this is a from-zero build (NewScene(EmptyScene) below) that WIPES anything the
            // owner has hand-authored in NineMileCreek.unity — the exact failure that ate a boat spotlight elsewhere.
            // If the committed scene already exists on disk, make the owner confirm before we clear it; abort
            // (touch nothing) on cancel. First-ever build (no file) proceeds silently. Shared wording with every
            // region builder via RegionBuildGuard.
            if (!RegionBuildGuard.ConfirmOverwrite("Nine Mile Creek", ScenePath))
                return;

            EnsureFolders();

            // --- DATA: regions + the boat offer (reused by stable id) -----------------------
            var nineMileCreek = LoadOrCreate<RegionDef>(DataRegions + "/NineMileCreek.asset", r =>
            {
                r.Id = "region.nine_mile_creek"; r.DisplayName = "Nine Mile Creek"; r.SceneName = SceneName;
                ApplyMainlandRegionFacts(r);
            });
            var cove = LoadOrCreate<RegionDef>(DataRegions + "/CoddleCove.asset", r =>
            {
                r.Id = "region.coddle_cove"; r.DisplayName = "Coddle Cove"; r.SceneName = "Greybox";
                r.IsDeepHarbour = false; r.HarbourDepthMeters = 2f;
                r.TideMeanLevel = 0f; r.TideAmplitude = 1.6f; r.TidePhaseHours = 0f;
                r.Description = "Your home harbour — the sheltered greybox cove.";
            });

            var config = LoadOrCreate<GameConfig>(DataConfig + "/GameConfig.asset");
            // Reuse the Punt offer by id (boat.punt) — created by the cove builder; create if absent.
            var puntOffer = LoadOrCreate<ShipwrightOffer>(DataShip + "/PuntOffer.asset", o =>
            {
                o.BoatId = "boat.punt"; o.DisplayName = "The Punt"; o.Price = 1800;
            });

            // --- St Peters opening vendors (economy data from #60; authored there, PLACED here) ----------
            // The cod licence, the rod, and the DAMAGED dory offer. Created here if absent so the builder is
            // self-sufficient (re-runnable), but economy-sim owns the canonical assets under the same paths.
            var codLicense = LoadOrCreate<LicenseDef>(DataLicenses + "/CodLicense.asset", l =>
            {
                l.Id = "license.cod"; l.DisplayName = "Cod Fishing License"; l.Price = 120;
                l.PermittedSpeciesIds = new[] { "fish.atlantic_cod" };
                l.Flavor = "Nine Mile Creek's harbourmaster signs you off to take cod on rod and line.";
            });
            var rodOffer = LoadOrCreate<GearOffer>(DataGear + "/Rod.asset", g =>
            {
                g.Id = "gear.rod"; g.DisplayName = "Fishing Rod"; g.Price = 60;
                g.Flavor = "A proper rod and reel - the step up from a hand-line.";
            });
            var damagedDoryOffer = LoadOrCreate<ShipwrightOffer>(DataShip + "/DamagedDoryOffer.asset", o =>
            {
                o.BoatId = "boat.dory"; o.DisplayName = "The Old Dory (needs work)";
                o.Price = 400; o.StartsDamaged = true; o.RepairCost = 300;
            });

            // THE USED OUTBOARD — the M1 ladder's CLOSING rung (m1-progression-pacing.md §2, target day
            // 13–15), and the thing that was missing: boat.dory_outboard has existed as a hull since #366
            // but nothing sold it, so the player could never legitimately reach it. This is that offer, and
            // it is the SAME mechanism the dory herself uses — a ShipwrightOffer pointed at a stable boat
            // id. D8's answer is a hull VARIANT, so buying it swaps the active hull to the dory-with-a-
            // kicker through the existing BoatPurchased path: no new system, no save-schema change (v7
            // stands — OwnedBoats/ActiveHullId already carry it).
            //
            // ⚠ NOT damaged: unlike the hull, an outboard is bought working, so there is no second repair
            // beat here. That also matters mechanically — Shipwright.TryBuy marks a non-damaged buy
            // repaired on grant, which is what keeps ControlSwitcher's boarding gate from locking the
            // player out of the boat they just upgraded.
            //
            // PRICE IS THE OWNER'S TUNABLE (rule 6) and ₲900 is a proposal, not a measurement: it sits
            // ABOVE the whole dory (₲400 hull + ₲300 repair = ₲700, the day-6–9 big save-up) so the
            // closing rung still costs a real climb, and at exactly HALF the Punt (₲1800) so hanging a
            // kicker on the boat you already own is plainly the cheaper rung than buying a bigger boat
            // (P2 dory-to-dynasty, P4 earn it then automate it). Committed canonical asset (economy-sim
            // owns Data/Shipwright); created here only if absent so the builder stays self-sufficient.
            var doryOutboardOffer = LoadOrCreate<ShipwrightOffer>(DataShip + "/DoryOutboardOffer.asset", o =>
            {
                o.BoatId = "boat.dory_outboard"; o.DisplayName = "Used Outboard (fitted to your dory)";
                o.Price = 900; o.StartsDamaged = false; o.RepairCost = 0;
            });

            // Pot offers (pots are BOUGHT, not conjured — the trap loop's P2 money wheel): counted,
            // repeatable stock sold at the shipwright shed. Committed canonical assets (economy-sim);
            // created here only if absent so the builder stays self-sufficient. Prices are the offer
            // assets' balance call: a full lobster pot sorts to ≈ ₲70 (#194), so a pot pays for itself
            // in about two good hauls.
            var lobsterPotOffer = LoadOrCreate<PotOffer>(DataShip + "/LobsterPotOffer.asset", o =>
            {
                o.Id = "offer.lobster_pot"; o.TrapDefId = "trap.lobster"; o.DisplayName = "Lobster Pot";
                o.Price = 120;
                o.Flavor = "A slatted timber pot. Bait her with herring, set her deep, come back to the buoy.";
            });
            var crabPotOffer = LoadOrCreate<PotOffer>(DataShip + "/CrabPotOffer.asset", o =>
            {
                o.Id = "offer.crab_pot"; o.TrapDefId = "trap.crab"; o.DisplayName = "Crab Pot";
                o.Price = 60;
                o.Flavor = "A low, wide pot for the mud crab grounds. Takes fish scrap for bait.";
            });

            // --- SCENE ----------------------------------------------------------------------
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera (standalone-viewable; see the class TODO about additive cleanup). Mirrors the
            // cove's locked pixel-perfect, on-foot landscape framing so Nine Mile Creek reads at the same scale.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(CameraFollow.OnFootWorldHeightMeters);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.10f, 0.15f); // deep-harbour dusk
            // Standalone review opens on the WHARF, because that is the thing worth looking at and the
            // region is now 760 m wide — a camera at the origin would open on empty bay. Derived from the
            // quay rather than typed, so it follows the wharf if the wharf ever moves.
            camGo.transform.position = new Vector3(DisembarkPos.x, DisembarkPos.y, -10f);
            camGo.AddComponent<AudioListener>();
            ArtCameraSetup.ConfigurePixelPerfect(camGo);
            var ppc = camGo.GetComponent<PixelPerfectCamera>();
            if (ppc != null)
            {
                CameraFollow.ReferenceResolutionForWorldHeight(CameraFollow.OnFootWorldHeightMeters, out int refW, out int refH);
                ppc.refResolutionX = refW;
                ppc.refResolutionY = refH;
                EditorUtility.SetDirty(ppc);
            }

            // --- TIDAL TERRAIN (the converged one-height source; ADR 0012 rec. 4) -----------
            // Nine Mile Creek's analytic seabed: town land + wharf plateaus over the dredged -6 m floor. It
            // registers into GameServices.TidalTerrain at runtime so the walkability, boat grounding AND
            // the water shader below read the SAME height (P1). Created BEFORE the Sea so on a region
            // toggle-on the terrain's OnEnable registers before the WaterSurface's OnEnable bakes (scene
            // roots activate in order). Hand-painting later replaces this via the Terrain Paint Tool's
            // Adopt step (ADR 0014) — the same adoption seam St Peters has.
            var terrainGo = new GameObject("TidalTerrain");
            var terrain = terrainGo.AddComponent<MainlandTidalTerrain>();
            ConfigureNineMileCreekTerrain(terrain);

            // --- DEEP HARBOUR WATER (the layered SIM-DRIVEN water shader — the St Peters model) ---
            // The harbour plane now carries Water.mat + a WaterSurface baking the terrain above, so the
            // depth gradient / foam / wet-dry clip follow the live deterministic tide against the quay.
            // The old static look (a tinted flat tile + a drifting-marker scatter) is RETIRED — the shader
            // surface moves for real. Sorting -5 (the island's number): ABOVE the floodable ground strips
            // (Quay -7 / QuayEdge -6, whose always-dry parts show through the shader's clip) and BELOW
            // everything that STANDS OVER the water — the wharf deck's band (-4..1, see
            // NineMileCreekWharf), the buildings (2) and the player (10). The Sea used to sit at -4
            // because the wharf was a floodable ground strip beneath it; now it is a structure on piles,
            // so the water goes under it.
            var waterSprite = MakeSquareSprite(ArtSprites + "/Square.png");
            var seaTile = LoadSpriteAny(ArtSea);
            var water = new GameObject("Sea");
            water.transform.position = new Vector3(NineMileCreekSeaCenter.x, NineMileCreekSeaCenter.y, 0f);
            var wsr = water.AddComponent<SpriteRenderer>();
            wsr.sortingOrder = -5;
            if (seaTile != null) wsr.sprite = seaTile;
            else                 { wsr.sprite = waterSprite; wsr.color = new Color(0.12f, 0.22f, 0.30f); } // deep harbour
            // ⭐ ONE QUAD over the bay, never a tiling of it. This used to be a Tiled draw sized to the
            // region, which at a 2 m tile asked for 106,400 tiles — over Unity's limit, refused, and logged
            // on every build of the region. The tile was never drawn anyway: the water shader samples no
            // _MainTex and reads only world XY. WaterSceneTemplate.ConfigureSeaPlane carries the whole
            // argument, and derives the scale from the sprite so the no-material fallback covers the bay
            // instead of a quarter of it.
            WaterSceneTemplate.ConfigureSeaPlane(wsr, NineMileCreekSeaSize);
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>(ArtWaterMat);
            if (waterMat != null)
            {
                wsr.sharedMaterial = waterMat;
                water.AddComponent<HiddenHarbours.Art.WaterSurface>();
                // ⭐ ONE WIRING, SHARED WITH ST PETERS (WaterSceneTemplate.ConfigureLandRegionWater): the
                // height-bake rect and range, the owner's GameConfig, the ADR 0017 weather palette, and —
                // the part Nine Mile Creek did not have — the ADR 0023 DISPLACED SURFACE. The displaced sea
                // has been the game's water since 2026-08-05, and until now the wiring for it lived only in
                // StPetersBuilder: so the owner sailed out of a displaced sea and into this flat one. The
                // two seas are the same renderer stack from here on, because there is only one copy of the
                // code that builds them.
                //
                // The shore gradient is MEASURED off the coast plan this region actually authors, not
                // inherited from St Peters' soft island — see MainlandTidalTerrain.MaxShoreGradient. Nine
                // Mile Creek stands a deep-shore cliff off the bay floor at the spit's root, and sizing the
                // fade band for a beach there is the direction of the error that tears.
                WaterSceneTemplate.ConfigureLandRegionWater(
                    water, NineMileCreekSeaCenter, NineMileCreekSeaSize,
                    NineMileCreekHeightResolution, NineMileCreekHeightMin, NineMileCreekHeightMax,
                    terrain.MaxShoreGradient());
            }
            else
            {
                Debug.LogWarning("[NineMileCreekBuilder] Water.mat not found at " + ArtWaterMat + " — the harbour " +
                                 "Sea is a plain backdrop. Re-run after the material imports for the tide-driven water.");
            }

            // Reload assets from disk before wiring refs (an intervening import can invalidate the
            // in-memory instances created above — same gotcha the cove builder guards against).
            config    = AssetDatabase.LoadAssetAtPath<GameConfig>(DataConfig + "/GameConfig.asset");
            puntOffer = AssetDatabase.LoadAssetAtPath<ShipwrightOffer>(DataShip + "/PuntOffer.asset");
            nineMileCreek  = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/NineMileCreek.asset");
            cove      = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/CoddleCove.asset");
            codLicense       = AssetDatabase.LoadAssetAtPath<LicenseDef>(DataLicenses + "/CodLicense.asset");
            rodOffer         = AssetDatabase.LoadAssetAtPath<GearOffer>(DataGear + "/Rod.asset");
            damagedDoryOffer = AssetDatabase.LoadAssetAtPath<ShipwrightOffer>(DataShip + "/DamagedDoryOffer.asset");
            doryOutboardOffer = AssetDatabase.LoadAssetAtPath<ShipwrightOffer>(DataShip + "/DoryOutboardOffer.asset");
            lobsterPotOffer  = AssetDatabase.LoadAssetAtPath<PotOffer>(DataShip + "/LobsterPotOffer.asset");
            crabPotOffer     = AssetDatabase.LoadAssetAtPath<PotOffer>(DataShip + "/CrabPotOffer.asset");

            // ⚠ RE-APPLY the region's facts to the EXISTING asset, unconditionally.
            //
            // LoadOrCreate runs its initialiser ONLY when the asset is absent, which is right for the
            // offers (the owner's prices are his) and WRONG for the geography. Nine Mile Creek's def has
            // shipped since VS-22, so every fact the recreation changes — the extent, the tide, the depth
            // ladder, the "deep dredged harbour" description — would have been left saying the old thing
            // forever, on the owner's machine and on anyone's, with the builder reporting success. The
            // committed asset in this PR already carries these values; this is what keeps a re-run from
            // being the only way to get them, and a hand-edit from being the only way to keep them.
            ApplyMainlandRegionFacts(nineMileCreek);

            // --- QUAY (the land the town sits on, along the WEST) ---------------------------
            // Nine Mile Creek lies WEST of the cove, so you arrive from the EAST and the town is to the WEST; the
            // public wharf is a peninsula reaching EAST into the deep harbour (open water is to the east).
            // ⭐ The DRAWN ground is now the AUTHORED ground: the grass covers exactly the terrain's land
            // zone (x ∈ [-28,-4], y ∈ [-20,20]) instead of a smaller rectangle guessed beside it. It used
            // to stop at x = -15, which was fine while everything stood in the middle — and stopped being
            // fine the moment the flavour houses moved out to the empty western land, where they would
            // have been standing on walkable terrain with the sea plane drawn under them. One rule, one
            // shape: the same convergence the water model already follows.
            // ⭐ ONE ground plane over the WHOLE region, under the sea, and the shader's wet-dry clip
            // decides which of the two you see at any tide. The old pair — a 24 × 40 m grass rectangle and
            // a 3 m sand strip beside it — described the town strip of a 120 m island; laid on a 760 m
            // mainland they would be a patch of lawn in the middle of a bay.
            //
            // ⭐ NO LONGER GREYBOX. The paragraph above described a flat green rectangle standing in until
            // the ground was painted; this is the painted ground (ADR 0028), the same splat-shaded surface
            // St Peters has. The greybox fallback below it stays for the one case the splat cannot cover —
            // see BuildSplatGround's return.
            //
            // ⚠ It is a SCALED QUAD, not a tiling. It used to be a Tiled draw of the 1 m Grass tile sized
            // to the region, which over 760 × 560 m asked for 425,600 tiles and was refused by the engine
            // — see MakeGreyboxGround for the whole argument.
            if (!BuildSplatGround(nineMileCreek))
                MakeGreyboxGround("Ground", waterSprite, NineMileCreekSeaCenter, NineMileCreekSeaSize,
                                  GroundSortingOrder, new Color(0.40f, 0.46f, 0.40f));

            // --- THE ROADS, ON THE GROUND THAT WAS JUST LAID -------------------------------------------
            // ⭐ THE ROUTES HAVE BEEN PUBLISHED SINCE PHASE A-1 AND NOTHING DREW THEM. NineMileCreekMainland
            // §9 carries the bar road, Wharf Road, the through-road and the gully path as derived data —
            // the pole line already walks Wharf Road and the lot tests already measure against its
            // corridor — but the ground under all of it was bare. This lays the v3 road kit along those
            // same published routes: dirt on the bar road, gravel on the two named roads, foot tread down
            // the gully, concrete on the winch apron and gravel under the buyers' trucks. Re-site a road
            // in the plan and the tiles follow it; nothing here authors a coordinate.
            //
            // ⚠ AFTER the splat ground and BEFORE the quay, which is the order the sorting band wants:
            // the painted ground is at -21, the road's two layers at -17/-16, the sea at -5. A lane that
            // floods is covered by depth-graded water, exactly as painted ground is.
            //
            // ⭐ The greybox fallback above no longer covers these. It sat at GroundSortingOrder = Sea - 2
            // (-7), eleven rungs above the road layers, and the note here used to excuse that on the
            // grounds that the path "only fires when the splat material or the region extent is missing".
            // That was an argument for the defect being rare, not for it being acceptable — and the same
            // pass that made the fallback actually draw would have made it actually cover the roads. The
            // constant is now SortingBands.PaintedSeabedMax (-18), the first rung under the road kit.
            NineMileCreekRoadPainter.Paint(terrain);

            // --- THE WORKING QUAY (the wharf tile kit, replacing the flat WharfDeck.png rectangle) ----
            // The public wharf reaching EAST out into the deep harbour (head = the east tip, x=4) is now
            // built from the BAKED kit: 48 'quay' cells drawn back to front with one sorting order per
            // row, the kit's own fittings on the mooring edge, a 'crib' breakwater arm sheltering the
            // basin from the south, and — the part that is not dressing — the deck registered as a
            // StandablePlatform so the on-foot sim stands on the concrete instead of in the dredged -6 m
            // harbour under it. Same rectangle as before (x∈[-4,4], y∈[-3,3]): the shoreline dip, the
            // dock zone and the arrival park are all authored around it, so it is re-dressed, not
            // re-sited. Null-tolerant — an unimported kit warns and leaves the quay unbuilt rather than
            // half-built.
            NineMileCreekWharf.Place(terrain);

            // --- BUILDINGS (services + a couple of flavour houses), on the WEST land ---------
            var shipwrightShed = MakeBuilding("ShipwrightShed",   LoadSpriteAny(ArtShipwright), ShipwrightShedPos, waterSprite, new Color(0.50f, 0.42f, 0.34f));
            // The buyer's truck stands on the SPIT among the parked trucks, which is where a buyer stands
            // on a working wharf. See FishBuyerPos for why this is no longer "four metres off the planks"
            // and what the test measures instead.
            var fishStall      = MakeBuilding("FishBuyerStall",   LoadSpriteAny(ArtFishStall),  FishBuyerPos, waterSprite, new Color(0.42f, 0.50f, 0.52f));

            // The two FLAVOUR houses are no longer loose sprites — they come from the baked village
            // building kit (NineMileCreekFlavour), which is what §7.2 means by replacing the region's
            // outdated art from the rigs rather than repainting it. The WORKING buildings above and below
            // keep theirs on purpose: their kit (wharfBuildingRig's sheds) has never been baked.
            NineMileCreekFlavour.Place(terrain, SpringHighWater);

            // --- PHASE B DRESSING: the gear, the services and the tideline -------------------
            // ⭐ BUILDER-WIRED, NOT SCENE-WIRED, and that is the whole point of it living here. Phase B's
            // dressing is a set of PURE TABLES derived off the wharf's own geometry — the quay's gear on
            // the berth line, the apron's stations off the authored unloading point, the poles on Wharf
            // Road's published route — so this call reproduces every one of them and the owner's next
            // rebuild keeps the lot. A prop dragged into the scene instead would survive until exactly
            // the next time someone ran this menu item.
            //
            // ⚠️ It does NOT draw the quay face. The committed ISO wharf pack was baked at the rig's
            // default 1.8 m tide and Nine Mile Creek's is 4.4 m, so every structural preset in it stands
            // 2.4 m too short and the pack has no furniture-free course to stack. NineMileCreekQuayFace
            // measures that and states the re-bake that closes it; Place() logs the finding on every
            // build so it cannot go quiet.
            NineMileCreekDressing.Place(terrain);

            // --- THE PHOTOGRAPH PASS: the lots along Wharf Road, and the fleet at the berths -----------
            // ⭐ THE LOTS. The owner's satellite view (2026-08-10) shows a cluster of buildings behind the
            // basin, not one shed: bait sheds, a boatyard, a restaurant, and a shed per boat-owner. Harris
            // & Sons is DRAWN from the baked shipyard kit at the smallYard tier he ruled; everything else
            // is a NAMED RESERVED LOT, because no rig bakes a bait shed or a restaurant and a kit building
            // standing in for one would look finished while being wrong. NineMileCreekLots reports the
            // gap on every build so the art-director's ask cannot go quiet.
            var creekOwners = NineMileCreekMooredFleet.LoadOwners();
            NineMileCreekLots.Place(terrain, creekOwners);

            // ⭐ THE FLEET. One MooredBoat per owner at their berth, carrying their Def. It places and
            // does NOT draw — the mesh path is runtime-owned (see NineMileCreekMooredFleet's class note),
            // so a builder that skinned the hulls here would bake the sprite fallback into the committed
            // scene and the fleet would never be mesh. The berth the player docks in is derived and left
            // clear.
            NineMileCreekMooredFleet.Place();

            // ⭐ THE REVIEW ANCHORAGE — the owner's "every boat model must be VISIBLE in game" ruling
            // (2026-08-14). Twenty-three fleet-pack hulls lying at anchor in the −6 m water east of the
            // basin, so he can go and look at them. NOT the working fleet above and not content: no
            // owner, no skipper, no paint, no BoatHullDef, so nothing here is pilotable or purchasable.
            // It places and does NOT draw, for the same reason the fleet above does not.
            FleetReviewMoorage.Place();

            // ⭐ THE DUALLY, PARKED AT THE PARK — closing the reachability gap #560 left ("the
            // dev-picker path only", and there is no picker). Places and does NOT draw, per the
            // moorage law above; the site is still the owner's PROPOSAL and she derives from its one
            // constant, so the walk verdict moves her with the park. See NineMileCreekTruckPark.
            NineMileCreekTruckPark.Place();

            // --- (NO SHORELINE FENCE — the coast is the terrain now) -------------------------
            // The region used to trace a hand-made EdgeCollider2D at x = -4 that dipped around the wharf,
            // because a rectangular quay standing on a flat -6 m dredged floor gave a hull nothing to
            // ground on: without the fence you could sail straight through the town (owner playtest gap
            // #2). A mainland is not built that way. The coastline, the beach, the ledges, the spit and
            // the quay decks are all AUTHORED TERRAIN, and the hull is stopped by depth against it — the
            // same shallows drag that already stops her on the tidal bar, over the same
            // BoatCrossing.DepthAt read. St Peters, a painted region with a real coast, carries no fence
            // either; a second hand-traced coastline beside the authored one is exactly the duplicate
            // this recreation exists to remove.

            // --- ECONOMY (reuse the cove's components, referenced by id) --------------------
            // Fish Buyer stall: Market → FishBuyer → WharfSellPoint (+ dev 'B' to sell). The hold/wallet
            // providers (player's boat + wallet) live in the origin scene → left unwired (TODO).
            var market = fishStall.AddComponent<Market>();
            var buyer  = fishStall.AddComponent<FishBuyer>();
            var sell   = fishStall.AddComponent<WharfSellPoint>();
            fishStall.AddComponent<DevSellInput>();          // RequireComponent(WharfSellPoint) — present
            SetRef(market, "_config", config);
            // ⭐ AND SAY WHICH MARKET IT IS. This was missing, and it was silent: Market defaults to
            // MarketId.Cove, so the creek's buyer has been quoting the HOME COVE's demand and price level.
            // Everything downstream of it read as if it worked — a sale paid out, a glut depressed a price
            // — while the one thing the outlet exists to be (the better price you cross for, §7.2/§7.5)
            // was not true. No new economy code: the channel, the levels and the arithmetic all shipped in
            // #356; this is the one line of PLACEMENT that connects the creek to them.
            SetEnum(market, "_marketId", CreekMarket);
            SetRef(buyer, "_market", market);
            SetRef(sell, "_buyer", buyer);

            // VS-22 travel: the player's hold + wallet live in the PERSISTENT core (a different scene), so
            // they can't be serialize-referenced here. Wire the wharf to scene-local PROXIES that forward
            // to the live persistent hold (PersistentHoldProxy → the dory's ShipHold) and wallet
            // (PersistentWalletProxy → GameServices.Wallet). The RegionTravelCoordinator binds the hold
            // proxy to the real hold on arrival; the wallet proxy always forwards to the live wallet — so
            // you sell your catch + buy the Punt here against the same hold + coin you sailed in with.
            var providersGo = new GameObject("PersistentProviders");
            providersGo.AddComponent<PersistentHoldProxy>();
            providersGo.AddComponent<PersistentWalletProxy>();
            SetRef(sell, "_holdProvider", providersGo);
            SetRef(sell, "_walletProvider", providersGo);

            // Shipwright shed: buy the Punt by id (+ dev 'P' to buy), paid from the persistent wallet proxy.
            var shipwright = shipwrightShed.AddComponent<Shipwright>();
            shipwrightShed.AddComponent<DevBuyInput>();      // RequireComponent(Shipwright) — present
            SetRef(shipwright, "_offer", puntOffer);
            SetRef(shipwright, "_walletProvider", providersGo);

            // The shed also sells POTS (pots are bought, not conjured): one PotShop per pot kind on the
            // SAME stall, so the existing buy screen lists them beside the Punt (BuyCatalog enumerates
            // every vendor component on the stall — no new UI). Counted, repeatable stock: a purchase
            // increments SaveData.PotStock; the T-set spends from it (PotLocker).
            var lobsterPotShop = shipwrightShed.AddComponent<PotShop>();
            SetRef(lobsterPotShop, "_offer", lobsterPotOffer);
            SetRef(lobsterPotShop, "_walletProvider", providersGo);
            var crabPotShop = shipwrightShed.AddComponent<PotShop>();
            SetRef(crabPotShop, "_offer", crabPotOffer);
            SetRef(crabPotShop, "_walletProvider", providersGo);

            // --- ST PETERS OPENING VENDORS (places + data wiring; interaction drivers are gameplay/ui) ---
            // The opening's earn-your-way loop ends at Nine Mile Creek: sell clams (the Fish Buyer above —
            // baseline Shellfish demand handles the clam, no override needed), buy the COD LICENCE + the
            // ROD, save up, then buy the DAMAGED DORY and pay to repair her. world-content places these
            // components + wires their DATA (the licence/gear/damaged-dory offers, by stable id) and the
            // wallet provider (the persistent proxy). The buy/repair SCREENS + the dig/walk/gear gates are
            // ui-ux / gameplay-systems — NOT wired here (no dev-input is attached so nothing collides with
            // the Punt's 'P'); each is the named seam those lanes attach their driver to.

            // Harbourmaster's office: sells the cod licence (LicenseVendor → license.cod). Reuse a flavour
            // house sprite (no new art — art-pipeline's lane); it sits north on the WEST land.
            var harbourOffice = MakeBuilding("HarbourmasterOffice", LoadSpriteAny(ArtHouseRed), HarbourmasterPos, waterSprite, new Color(0.46f, 0.40f, 0.52f));
            var licenseVendor = harbourOffice.AddComponent<LicenseVendor>();
            SetRef(licenseVendor, "_license", codLicense);
            SetRef(licenseVendor, "_walletProvider", providersGo);

            // General store / chandlery: sells the rod (GearShop → gear.rod). South on the WEST land.
            var store = MakeBuilding("GeneralStore", LoadSpriteAny(ArtHouseTeal), ChandleryPos, waterSprite, new Color(0.40f, 0.50f, 0.40f));
            var gearShop = store.AddComponent<GearShop>();
            SetRef(gearShop, "_offer", rodOffer);
            SetRef(gearShop, "_walletProvider", providersGo);

            // The DAMAGED DORY at the shipwright (the opening's prize): a second Shipwright stall wired with
            // the damaged-dory offer (buy → owned-but-unusable; pay TryRepair → usable). Its own GO so it
            // doesn't fight the Punt shipwright's offer; west land, beside the shed. Buy/repair screens are
            // ui-ux's, so no dev-input is added here (P already buys the Punt next door).
            var doryYard = MakeBuilding("ShipwrightDoryYard", LoadSpriteAny(ArtShipwright), DoryYardPos, waterSprite, new Color(0.46f, 0.40f, 0.32f));
            var doryShipwright = doryYard.AddComponent<Shipwright>();
            SetRef(doryShipwright, "_offer", damagedDoryOffer);
            SetRef(doryShipwright, "_walletProvider", providersGo);

            // --- HECTOR'S BARREL: THE USED OUTBOARD, NOW ACTUALLY FOR SALE ------------------------------
            // #364 stood this stall up EMPTY and wrote the seam down so nobody had to guess it: "the
            // outboard rides the EXISTING shipwright-offer path — add the vendor component here, point it
            // at the offer asset economy-sim authors, and wire its _walletProvider to the
            // PersistentWalletProxy every other till here uses." This is that, taken literally. D8 settled
            // the outboard as a hull VARIANT (boat.dory_outboard), so the vendor is a Shipwright over a
            // ShipwrightOffer rather than a GearShop — the same component the dory next door uses, at the
            // same wallet, publishing the same BoatPurchased.
            //
            // THE MAN, NOT A BUILDING. Canon amended in design/nine-mile-creek-wharf.md: "There is no
            // shipwright in this region. Not on the wharf, not up the hill" — the dory is "sold by someone
            // who is not a shipwright". So the till hangs on HECTOR's barrel, not on a shed: he stands
            // 2.5 m off the dory yard (NineMileCreekPeople.OutboardStallMetres), well inside StallGate's
            // 4 m reach of this spot, and NineMileCreekPeople says why in his own words — "the man who
            // sells you the hull is the man who sells you what pushes her". (The two Shipwright BUILDINGS
            // this scene still draws are pre-existing debt against that same amendment, and the doc leaves
            // re-siting the yard open for world-content. Not moved here.)
            //
            // No dev-input is attached, and it no longer needs one: BuyPointInstaller sweeps every loaded
            // scene after each load and adds a DevBuyInput (P, on-foot + in reach) to any vendor stall
            // lacking one — the driver the creek's other flagged tills already run on.
            var outboardStall = new GameObject("UsedOutboardSeller");
            // At Hector's own spot rather than an offset off the yard's corner: NineMileCreekPeople
            // already derives where he stands (out from the yard toward the water), and the man and his
            // till coming apart is the exact failure NineMileCreekDoryTests measures.
            outboardStall.transform.position = HectorsBarrelPos;
            var outboardSeller = outboardStall.AddComponent<Shipwright>();
            SetRef(outboardSeller, "_offer", doryOutboardOffer);
            SetRef(outboardSeller, "_walletProvider", providersGo);

            // --- REGION SCENE-LOAD PATH -----------------------------------------------------
            var loaderGo = new GameObject("RegionSceneLoader");
            var loader = loaderGo.AddComponent<RegionSceneLoader>();
            SetRefArray(loader, "_regions", new Object[] { nineMileCreek, cove });
            SetString(loader, "_currentSceneName", SceneName);

            // Return passage out to the EAST (toward the cove, which lies east of Nine Mile Creek): sail east into
            // this wide, forgiving band to head back to Coddle Cove — so you arrive home at the cove dock
            // FROM THE WEST (heading east). The matching Cove→Nine Mile Creek passage lives on the cove's WEST edge
            // (GreyboxBuilder.ToNineMileCreekPassagePos).
            var passageGo = new GameObject("PassageToCoddleCove");
            passageGo.transform.position = ToCovePassagePos;
            var trigger = passageGo.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = PassageBandSize;
            var passage = passageGo.AddComponent<RegionPassage>();
            SetRef(passage, "_target", cove);
            SetRef(passage, "_loader", loader);

            // ⭐ THE CROSSING, THIS SIDE OF THE SEAM. The tidal bar to St Peters is ONE bar spanning two
            // regions: you walk 305 m here, cross this band mid-flats, and walk 305 m more over there.
            // A-1 authored the mainland's half and its walk-out band; this is the trigger that stands in
            // it. Note it is deliberately a band you WALK across on bared sand as often as sail over — the
            // load is hidden on flat open flats with nothing happening, which is why the seam sits at the
            // bar's midpoint rather than at the landing (plan §3.2).
            //
            // ⚠ The MATCHING half — a passage on St Peters' side pointing here with BarArrivalKey — is
            // StPetersBuilder's and is NOT wired here; a region builder may not reach into another
            // region's scene. Until it lands, walking in from the island falls back to the wharf, which
            // is precisely the fallback #456's seam was built to give.
            var stPetersRegion = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/StPeters.asset");
            if (stPetersRegion != null)
            {
                var barGo = new GameObject("PassageToStPeters");
                barGo.transform.position = ToStPetersPassagePos;
                var barTrigger = barGo.AddComponent<BoxCollider2D>();
                barTrigger.isTrigger = true;
                barTrigger.size = PassageBandSize;
                var barPassage = barGo.AddComponent<RegionPassage>();
                SetRef(barPassage, "_target", stPetersRegion);
                SetRef(barPassage, "_loader", loader);
                SetRefArray(loader, "_regions", new Object[] { nineMileCreek, cove, stPetersRegion });
            }
            else
            {
                Debug.LogWarning("[NineMileCreekBuilder] No StPeters RegionDef at " + DataRegions +
                                 "/StPeters.asset — the crossing to the island has no passage on this " +
                                 "side, so the bar dead-ends at the region edge. Re-run after building " +
                                 "St Peters.");
            }

            // ⭐ THE SEA DOOR — out east into THE WEST WATER (world-map-plan §6 step 1). The bar above is
            // the crossing you WAIT for; this is the one you don't. Leave the basin, round the breakwater
            // head, hold east on the beacon's own latitude and the bay's first open-water scene takes
            // over — which is the trip the owner buys the dory to make.
            //
            // ⚠ THIS HALF IS THE MAINLAND'S TO WIRE, and the return half is not: WestWaterBuilder authors
            // the passage that brings a boat back here, because a region builder may not reach into
            // another region's scene. Guarded on the west water's def existing, exactly like the crossing
            // above — until it is built the wharf simply has no sea door, which is what it has today.
            var westWaterRegion = AssetDatabase.LoadAssetAtPath<RegionDef>(
                                      DataRegions + "/" + WestWaterPlan.RegionAssetName + ".asset");
            if (westWaterRegion != null)
            {
                var seaDoorGo = new GameObject("PassageToWestWater");
                seaDoorGo.transform.position = ToWestWaterPassagePos;
                var seaTrigger = seaDoorGo.AddComponent<BoxCollider2D>();
                seaTrigger.isTrigger = true;
                seaTrigger.size = NineMileCreekMainland.WestWaterPassageBandSize;
                var seaDoor = seaDoorGo.AddComponent<RegionPassage>();
                // NAMED: the west water has two doors of equal standing, so it must be told which one
                // this boat came in by (#456), or she is parked at the far end of the run she just began.
                seaDoor.Configure(westWaterRegion, loader, WestWaterPlan.FromNineMileCreekArrivalKey);
                // Nulls filtered: St Peters may be absent on a fresh checkout, and a null in the loader's
                // region list is a hole the router would have to trip over rather than an absent door.
                SetRefArray(loader, "_regions",
                            new Object[] { nineMileCreek, cove, stPetersRegion, westWaterRegion }
                                .Where(r => r != null).ToArray());
            }
            else
            {
                Debug.LogWarning("[NineMileCreekBuilder] No West Water RegionDef at " + DataRegions + "/" +
                                 WestWaterPlan.RegionAssetName + ".asset — the wharf has no sea door, so " +
                                 "the only way to the island is the tidal bar. Re-run after building the " +
                                 "west water.");
            }

            // VS-22 arrival anchor: where the persistent rig binds on arrival. The boat parks in the basin
            // off the north wall's face; you step ashore onto its deck. The App RegionTravelCoordinator
            // reads this to reposition the rig and re-point the dock.
            //
            // DISEMBARK GEOMETRY (the cove's proven pattern; do NOT regress #52): ControlSwitcher.InDockZone()
            // is a pure DISTANCE test — Vector2.Distance(boat, dockZone) <= _zoneRadius (3.5 m default on the
            // persistent switcher). It needs NO trigger collider on the dock zone; it only needs the BOAT to
            // PARK within 3.5 m of the dock zone on arrival. The three positions are the plan's, and an
            // EditMode test asserts the arrival↔dock distance stays inside DockZoneRadius without a scene
            // (NineMileCreekDockTests).
            var gwArrival = new GameObject("NineMileCreekArrival");
            gwArrival.transform.position = ArrivalPos;         // in the basin, off the north wall's south face
            var gwDock = new GameObject("NineMileCreekDockZone");
            gwDock.transform.position = DockZonePos;           // against the wall by the unloading apron
            var gwDisembark = new GameObject("NineMileCreekDisembark");
            gwDisembark.transform.position = DisembarkPos;     // up onto the quay deck
            var gwAnchor = new GameObject("NineMileCreekRegionAnchor").AddComponent<RegionAnchor>();
            gwAnchor.Configure("region.nine_mile_creek", gwArrival.transform, gwDock.transform, gwDisembark.transform);

            // ⭐ THE SECOND DOOR (#456). This is the first region you can enter two ways, and this is the
            // half of it that lives here: the bar landing, named, so a fisher who walked the crossing
            // arrives ON THE BAR instead of being teleported 400 m north onto the wharf deck.
            //
            // The BOAT point is deliberately left unset. You walked; your boat is not with you, and an
            // unset point on a named arrival means "leave this one alone" — so she stays at the berth
            // rather than being dragged across the region after you.
            var gwBarLanding = new GameObject("NineMileCreekBarLanding");
            gwBarLanding.transform.position = WalkArrivalPos;
            gwAnchor.ConfigureArrivals(
                new NamedArrival(BarArrivalKey, arrivalPoint: null, disembarkPoint: gwBarLanding.transform));

            // The camera's bounds clamp reads this on arrival — the same extent the sea reads.
            gwAnchor.ConfigureExtent(NineMileCreekSeaCenter, NineMileCreekSeaSize);

            // --- THE DERELICT DORY (§7.2's exit condition, and the template DoD rung) --------------------
            // She lies at the wharf, hauled out on the quay's landward end — canon's own words (the
            // nine-mile-creek-wharf doc's second owner rider) and the only place the on-foot camera can
            // actually promise you see her from where you land. Dressing, not a boat: buying her is the
            // DamagedDoryOffer wired above.
            NineMileCreekDory.Place();

            // --- THE TWO PEOPLE (anchored, unscheduled — §7.1's rule) ------------------------------------
            var creekPeople = NineMileCreekPeople.Place(waterSprite);

            // --- DEV BOOTSTRAP (owner iteration: press Play IN NINE MILE CREEK and walk/fish immediately) ------
            // Nine Mile Creek is a region scene: the real player arrives with the persistent core from St Peters,
            // so playing this scene directly used to give "no character loads" (the owner's report). The
            // fix: bake a minimal, self-contained DEV CORE — services + follow camera + a walkable,
            // rod-fishing player on the wharf — as an INACTIVE root, plus an active DevRegionBootstrap
            // that activates it ONLY when the scene is played directly in the editor and destroys it
            // (never awakened — no service stomp, no duplicate player) when the real core travels in.
            Transform devPlayer = BuildDevBootstrap(config, cam, DisembarkPos);

            // --- AND THE THING THAT MAKES THOSE TWO SPEAK (panel + proximity interact driver) -----------
            // ⚠️ AFTER the dev core and OUTSIDE it, and both halves of that matter. After, because the
            // stand-in it reviews with does not exist until the line above runs; outside, because the dev
            // core is destroyed on arrival and used to take the driver down with it — which is exactly
            // how Wendell and Hector ended up mute for everyone who arrived by sea. See PlaceDialogueDriver.
            PlaceDialogueDriver(creekPeople, devPlayer);

            // --- THE WHARF'S TWO SHOPS (B2) -----------------------------------------------------------
            // ⭐ THE RESTAURANT AND THE FISH MARKET, from the baked shop kit, both ENTERABLE — the owner's
            // 2026-08-11 canon (no façade-only buildings). The restaurant FILLS the lot the photograph
            // pass reserved and NineMileCreekLots has been reporting as an art gap on every build since;
            // the market is B2's proposal. Sites, and the arithmetic that chose them, live in
            // NineMileCreekShops and NineMileCreekMainland.
            //
            // ⚠️ PLACED HERE, AFTER THE DEV CORE, AND THE ORDER IS THE POINT — a BuildingInterior takes an
            // OCCUPANT, and the only one this scene can name is the dev player above. Nothing else in the
            // region has a transform to give it: the real player arrives with the persistent core from
            // another scene, and Unity does not serialize a reference across scenes at all.
            //
            // The dev player is now the REVIEW occupant only, and handing it over is no longer what makes
            // these shops enterable on a live arrival. Travel was fixed after this slice flagged it: the
            // dev core is still destroyed by DevRegionBootstrap, _occupant still goes fake-null, and
            // BuildingInterior now falls through to Core's GameServices.PlayerTransform — published by
            // RegionTravelCoordinator, resolved lazily, so a room needs no arrival event at all. Play
            // NineMileCreek.unity directly and the shops open for the dev player (the serialized
            // reference wins while it is alive); sail in from St Peters and they open for the fisher who
            // actually arrived. Driven end to end by InteriorTravelPlayTests.
            NineMileCreekShops.Place(terrain, devPlayer);

            // --- THE HINTERLAND: fields, hedgerows, scattered trees, marsh -----------------------------
            // ⭐ FIELDS, NOT FOREST — the mainland doc's first photograph, as a build step. The land
            // behind this wharf is farmed, so NineMileCreekFields lays a grass FIELD over the strips,
            // hedgerows on a 96 m boundary lattice and along the two GRAVEL roads, trees standing mostly
            // IN those hedges, and salt marsh on the two ponds' shoulders. Every position derives from
            // the plan and from the road pass's own published carriageway widths — widen a road and its
            // hedge steps back — and nothing at all stands on the wharf's made ground.
            //
            // ⚠ THIS REPLACES THE GREYBOX TREE TABLE, and the retirement is the point. Eleven
            // hand-placed TreeNN.png decor sprites stood here — a shelter belt west of the through-road
            // and a few at the pond margins — drawn from the old imported decor pack rather than the
            // baked Acadian kit, and sited when this region was a 120 m island. Two tree pipelines at
            // two art standards on one coast is the same defect the road kit warns about for its own
            // v1/v3 split ("two road families at different vertical scales in one village is a bug only
            // the eye would ever catch"); NineMileCreekFlavour made exactly this move for the houses one
            // pass earlier. The shelter belt's ground is inside the new hedgerow lattice anyway.
            NineMileCreekFieldPlanter.Plant(terrain);

            // --- THE SHORE: the coast stands up, and the tide gets something to move --------------------
            // ⭐ THE THIRD PASS OF THE TERRAIN ARC, and the one that makes the other two READ. #520 gave
            // this region the displaced sea and #527 gave it painted ground, and between them they draw a
            // coast that is correct and completely still: the water moves, the paint does not, and there
            // is nothing on the beach for the eye to measure the tide against. This pass hangs the things
            // that STAND — weed and marsh that rise and drown twice a day (ShorePlantTideView), boulders
            // the falling water uncovers, and the cliff faces the coast plan has been declaring since the
            // region was authored without anything ever drawing them.
            //
            // ⚠ AFTER the fields, deliberately. The two passes ABUT at the grass floor (4.2 m) rather than
            // overlapping — NineMileCreekShore.ZoneAt stops the upland exactly where
            // NineMileCreekFields.IsGrassGround starts the meadow — so the order cannot double-plant a
            // band either way round. It runs second only so its log line is the last thing about the
            // ground the owner reads.
            //
            // ⚠ EnsureBaked FIRST: the Cliff Face kit's pixels are gitignored by design (the rig is the
            // committed source), so a fresh checkout has no faces and the coast would stand up untextured
            // without a word. St Peters' builder makes the same call for the same reason.
            HiddenHarbours.Tools.RigBaking.CliffBakeMenu.EnsureBaked();
            int cliffChunks = NineMileCreekCliffWalls.Build(terrain);
            var shore = NineMileCreekShorePainter.Paint(terrain);
            shore.CliffChunks = cliffChunks;

            // --- SAVE & REGISTER ------------------------------------------------------------
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[NineMileCreekBuilder] Built NineMileCreek.unity — ⭐ THE MAINLAND. The region is " +
                $"{NineMileCreekSeaSize.x:0} × {NineMileCreekSeaSize.y:0} m now, not a 120 m island: water " +
                "EAST, fields WEST, an open coast run with a coast plan (beach · dune · ledge · gully · " +
                "cliff · deep shore), the barachois and the marsh pool carved behind it, and the harbour " +
                "shoal, the spit, both quay walls and the crib breakwater filled on top " +
                "(MainlandTidalTerrain, pushed from the authored plan in NineMileCreekMainland). " +
                $"TIDE CHANGED: mean {TideMean}, amplitude {TideAmplitude} m, phase {TidePhaseHours} h — " +
                "St Peters' verbatim, because the tidal bar SPANS the seam between the two regions and " +
                "two tides would leave the crossing dry on one side and flooded on the other. " +
                $"DEPTH LADDER: IsDeepHarbour false, {-NineMileCreekBasinElevation:0.0} m — the " +
                "lobster-boat berth, not the dredged harbour it was standing in for. THE CROSSING now has " +
                "a passage on this side, mid-bar, and the region authors a SECOND ARRIVAL " +
                $"('{BarArrivalKey}') so a fisher who WALKS in over the flats lands on the bar instead of " +
                "being teleported onto the wharf 400 m north (#456). THE QUAY IS GROUND: two walls " +
                "registered as standable floor with their decks measured, every berth given a bollard and " +
                "every bollard a real ShoreCleat — the drawn ISO quay is Phase B's. The town moved INLAND " +
                "to the through-road; the working sites are on the spit. Vendors, offers and the market " +
                "id are unchanged and still wired by stable id. ⭐ THE ROADS ARE DRAWN: the four published " +
                "routes are laid in v3 road tiles on their own centre-lines — dirt on the bar road, " +
                "gravel on Wharf Road and the through-road, foot tread down the gully — plus the concrete " +
                "apron at the winch and gravel under the buyers' trucks, and one shell walk up to each of " +
                "the five public town lots (the region's only paving canon does not ask for). ⭐ AND THE " +
                "FIELDS ARE DRESSED: a grass FIELD over the strips west of the coast, hedgerows on a " +
                $"{NineMileCreekFields.FieldStripMetres:0} m boundary lattice and along the two gravel " +
                "roads, trees standing mostly IN those hedges, and salt marsh on both ponds' shoulders — " +
                "FIELDS, NOT FOREST, which is the mainland doc's first photograph and the one thing this " +
                "region's hinterland must never stop being. The greybox TreeNN decor table is retired " +
                "with it: one tree pipeline on this coast, the baked Acadian kit. ⭐ AND THE SHORE IS " +
                $"DRESSED: {shore.ShorePlants:N0} tide-riding plants ({shore.ZoneSummary()}) across " +
                $"{shore.TreatmentSummary()}, {shore.Rocks} boulders on the foreshore, and " +
                $"{cliffChunks} chunks of standing cliff on the two rock sectors the coast plan declares " +
                "— drawn where the PLAN says rock is, never invented. The four treatments are this " +
                "coast's own: the barachois and the basin get the quiet mix, the crib breakwater's outer " +
                "face and the two tall cliff sectors get the exposed one, and the marsh pool — the only " +
                "standing FRESH water either region has — finally gets the cattail the kit has carried " +
                "unplaced since it baked.");
            EditorUtility.DisplayDialog("Hidden Harbours",
                "Nine Mile Creek rebuilt as the MAINLAND.\n\n" +
                $"• {NineMileCreekSeaSize.x:0} × {NineMileCreekSeaSize.y:0} m — water east, fields west\n" +
                "• The tidal bar to St Peters comes ashore to the SOUTH; its passage is mid-bar\n" +
                $"• Tide is now ±{TideAmplitude} m, phase {TidePhaseHours} h (St Peters', because it is one bar)\n" +
                "• The wharf dries out under its fleet at spring low — that is the ruled gate\n" +
                "• The roads are laid: red dirt on the bar road, gravel on Wharf Road and the\n" +
                "  through-road, a foot tread down the gully, concrete at the winch\n" +
                "• The fields are dressed: meadow on the strips, hedgerows on the boundaries and\n" +
                "  the gravel roads, scattered trees, salt marsh at both ponds\n" +
                $"• The shore is dressed: {shore.ShorePlants:N0} plants that RIDE THE TIDE, " +
                $"{shore.Rocks} boulders,\n" +
                $"  and {cliffChunks} chunks of standing cliff on the ledge and the bluff\n\n" +
                "STILL TO DO, and it is yours:\n" +
                "1. Hidden Harbours ▸ Terrain Paint Tool → bake the seabed at 2 px/m, then save\n" +
                "2. Press Play and walk it: the crossing, the bar road, Wharf Road, the wharf front\n" +
                "3. Walk the roads specifically — the junction at (−16, 92) where the dirt meets the\n" +
                "   gravel, the neck between the two ponds, and the shell walks in the town\n" +
                "4. Stand on the through-road and look EAST. You should see a patchwork: hedgerows\n" +
                "   running down to the water, fields between them, the odd tree in a hedge.\n" +
                "   ⚠ If it reads as WOODS, that is the one thing it must not be — say so.\n\n" +
                "The ground is a single greybox plane until you paint it.",
                "Fair winds");
        }

        // ---- dev bootstrap (press Play in Nine Mile Creek → a playable, fishing-capable character) ----------

        /// <summary>
        /// Bake the INACTIVE dev core + the active <see cref="DevRegionBootstrap"/> that arbitrates it
        /// (see the call site). The core is a deliberately minimal mirror of the persistent rig — the
        /// services (GameClock/EnvironmentService/PlayerWallet/GameRoot + HUD), a pixel-perfect follow
        /// camera, and the on-foot player with the ROD RIG mounted directly on them (FishingController +
        /// DevFishingInput; hold = the player's ClamBucket, angler = self) — no boat, no travel rig, no
        /// persistence: it exists to FEEL the on-foot fishing loop in this region, nothing more.
        /// Null-safe on art/data (the greybox rule): missing sheets/defs leave pieces inert, never break
        /// the build.
        /// </summary>
        /// <returns>The dev player's transform — the REVIEW stand-in this scene hands to the region
        /// content that wants a player to watch (the shops' rooms, the dialogue driver). It is the only
        /// one this scene can name, and on the travel path it is not the one that matters: see
        /// <see cref="PlaceDialogueDriver"/> and <c>GameServices.PlayerTransform</c>.</returns>
        static Transform BuildDevBootstrap(GameConfig config, Camera sceneReviewCamera, Vector3 devSpawn)
        {
            var devCore = new GameObject("DevCore");

            // Services root (mirrors PersistentCoreBuilder's GameRoot block; GameRoot wires GameServices
            // on activation). Nine Mile Creek's own authored tide (RegionDef NineMileCreek: mean 0, amp 0.8,
            // phase 2 h) so the dev session's harbour swings on this region's curve.
            var root = new GameObject("GameRoot");
            root.transform.SetParent(devCore.transform, false);
            var clock  = root.AddComponent<GameClock>();
            var env    = root.AddComponent<EnvironmentService>();
            var wallet = root.AddComponent<PlayerWallet>();
            var gameRoot = root.AddComponent<GameRoot>();
            SetRef(clock, "_config", config);
            SetRef(env, "_config", config);
            SetTideProfile(env, TideMean, TideAmplitude, TidePhaseHours);
            SetRef(gameRoot, "_clock", clock);
            SetRef(gameRoot, "_environment", env);
            SetRef(gameRoot, "_wallet", wallet);
            var hud = root.AddComponent<HudController>();
            SetRef(hud, "_config", config);

            // Follow camera (the review camera is silenced when this core seeds; MainCamera tag so
            // Camera.main — the fishing pointer's mapping — resolves to the live one).
            var devCamGo = new GameObject("DevCamera");
            devCamGo.transform.SetParent(devCore.transform, false);
            devCamGo.tag = "MainCamera";
            var devCam = devCamGo.AddComponent<Camera>();
            devCam.orthographic = true;
            devCam.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(CameraFollow.OnFootWorldHeightMeters);
            devCam.clearFlags = CameraClearFlags.SolidColor;
            devCam.backgroundColor = new Color(0.05f, 0.10f, 0.15f);
            devCamGo.transform.position = new Vector3(devSpawn.x, devSpawn.y, -10f);
            devCamGo.AddComponent<AudioListener>();
            ArtCameraSetup.ConfigurePixelPerfect(devCamGo);
            var devPpc = devCamGo.GetComponent<PixelPerfectCamera>();
            if (devPpc != null)
            {
                CameraFollow.ReferenceResolutionForWorldHeight(CameraFollow.OnFootWorldHeightMeters,
                                                               out int refW, out int refH);
                devPpc.refResolutionX = refW;
                devPpc.refResolutionY = refH;
                EditorUtility.SetDirty(devPpc);
            }

            // The on-foot player at the dev spawn (the wharf planks), with the rod rig mounted on THEM:
            // the FishingController's angler defaults to its own transform here, the hold is the player's
            // hand-held ClamBucket, and DevFishingInput is live on foot (the dock-first mode gate).
            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(devCore.transform, false);
            playerGo.transform.position = devSpawn;
            var playerSr = playerGo.AddComponent<SpriteRenderer>();
            playerSr.sortingOrder = 10;
            var prb = playerGo.AddComponent<Rigidbody2D>();
            prb.gravityScale = 0f; prb.freezeRotation = true;
            prb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var foot = playerGo.AddComponent<CircleCollider2D>();
            foot.radius = 0.35f; foot.offset = new Vector2(0f, -0.7f);
            var walk = playerGo.AddComponent<PlayerWalkController>();
            var fisherFrames = LoadSheetFrames(ArtFisher);
            SetRefArray(walk, "_frames", fisherFrames.Cast<Object>().ToArray());
            if (fisherFrames.Length > 0 && fisherFrames[0] != null) playerSr.sprite = fisherFrames[0];
            var isoVisual = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(IsoFisherVisual);
            var isoSkin = playerGo.AddComponent<IsoCharacterSprite>();
            SetRef(isoSkin, "_visual", isoVisual);
            playerGo.AddComponent<ClamBucket>();   // the hand-held IHold the rod rig lands into

            var fishing = playerGo.AddComponent<FishingController>();
            playerGo.AddComponent<DevFishingInput>();
            SetRef(fishing, "_holdProvider", playerGo);   // the bucket above (IHold on the same GO)
            SetRef(fishing, "_config", config);
            SetString(fishing, "_regionId", "region.nine_mile_creek");
            var rodFish = new[]
            {
                AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/AtlanticCod.asset"),
                AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/Haddock.asset"),
                AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/Mackerel.asset"),
                AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/Pollock.asset"),
            }.Where(f => f != null).Cast<Object>().ToArray();
            if (rodFish.Length > 0) SetRefArray(fishing, "_regionFish", rodFish);
            else Debug.LogWarning("[NineMileCreekBuilder] No rod species assets found under " + DataFish +
                                  " — the dev bootstrap will cast into an empty pool (NoBite).");

            devCamGo.AddComponent<FightStrainCamera>();   // the fight's camera tell (no HUD)
            var cameraFollow = devCamGo.AddComponent<CameraFollow>();
            cameraFollow.Target = playerGo.transform;
            SetRef(cameraFollow, "_onFootTarget", playerGo.transform);

            // ⭐ THE CONTROL SWITCHER — the interact VERB'S PUMP, and this dev core never had one.
            // Found 2026-08-18 by the owner standing at the placed Dually (#567) with a dead E and no
            // prompt: this bootstrap "mirrors PersistentCoreBuilder's player + gauge wiring" (its own
            // words, above) and the mirror predates the verb — so direct-playing this scene had NO
            // component calling InteractVerb.PublishCandidate/TryPerform, and every registered
            // interactable in the region (the truck's door, the buyer, the fuel pump) was silently
            // unreachable while St Peters — whose core carries the switcher — worked. A hand-mirrored
            // second implementation drifting from its source is exactly the two-definitions failure
            // this file's own geography block warns about; the real fix is this bootstrap CALLING
            // PersistentCoreBuilder instead of mirroring it, chipped as a follow-up. Walk-only wiring
            // (no dev boat here): the verb needs only the walk controller and Mode's default IS
            // OnFoot, which the shipped-assets PlayMode repro proves sufficient.
            var devSwitcherGo = new GameObject("ControlSwitcher");
            devSwitcherGo.transform.SetParent(devCore.transform, false);
            var devSwitcher = devSwitcherGo.AddComponent<ControlSwitcher>();
            SetRef(devSwitcher, "_playerWalk", walk);

            // The dev toast channel (cast/bite/no-water feedback on screen). No rod gauge: the fight has no
            // UI at all now (owner's ruling 2026-07-23) — it is read off the rod, the line, the sound and
            // the camera.
            var toastGo = new GameObject("DevToast");
            toastGo.transform.SetParent(devCore.transform, false);
            toastGo.AddComponent<DevToast>();

            // Baked INACTIVE — the bootstrap below is the only thing that may ever activate it.
            devCore.SetActive(false);

            var bootstrapGo = new GameObject("DevRegionBootstrap");
            var bootstrap = bootstrapGo.AddComponent<DevRegionBootstrap>();
            bootstrap.Configure(devCore, sceneReviewCamera);

            return playerGo.transform;
        }

        /// <summary>
        /// <b>THE THING THAT MAKES THE CREEK'S TWO PEOPLE SPEAK</b> — the dialogue panel and the
        /// proximity INTERACT driver that offers "E: …" when you walk up to Wendell or Hector, wired to
        /// the cast <see cref="NineMileCreekPeople.Place"/> just stood up.
        ///
        /// <para><b>⭐ SCENE ROOTS, AND THAT IS THE WHOLE FIX.</b> These two used to be built INSIDE the
        /// dev core, because a <see cref="WorldInteractor"/> needs a player to measure from and the dev
        /// stand-in is the only player a region scene can name at build time. But the dev core is
        /// DESTROYED the moment a real core travels in (<see cref="DevRegionBootstrap"/>), and it took
        /// the driver and the panel with it — so Wendell and Hector were mute, promptless and
        /// unspeakable-to for every player who actually sailed here, while working perfectly for the
        /// owner pressing Play. Out here they survive the dev core, and they are toggled with the region
        /// like every other root, so they are live exactly while you are in this region.</para>
        ///
        /// <para><b>The stand-in is still handed in, and is now only a REVIEW affordance.</b>
        /// <see cref="WorldInteractor"/> prefers its serialized player while that player is alive — which
        /// is what keeps direct-play review working, since nothing publishes the Core relay in a scene
        /// with no persistent core — and falls back to <c>GameServices.PlayerTransform</c> once the dev
        /// core is gone. So the reference below is what makes the creek talk to the owner in the editor,
        /// and its DEATH is what makes it talk to the fisher who arrived by sea.</para>
        ///
        /// <para>Nothing is placed for a cast of nobody: an unimported NpcDef means
        /// <see cref="NineMileCreekPeople.Place"/> returns fewer people (or none), and a driver with an
        /// empty list would be a prompt-less canvas built every load for nothing.</para>
        /// </summary>
        /// <returns>The interactor, or null when there was nobody to talk to.</returns>
        public static WorldInteractor PlaceDialogueDriver(IReadOnlyList<Interactable> people,
                                                          Transform reviewPlayer)
        {
            if (people == null || people.Count == 0) return null;

            var dialogueGo = new GameObject("DialogueUI");          // the St Peters name, for one convention
            var presenter = dialogueGo.AddComponent<DialoguePresenter>();

            // ⚠ The two lines that used to be here set "_panelSprite" and "_nameplateSprite" — fields
            // #557 DELETED when the screen-space panel and the portrait went away (the character on
            // screen is the portrait now). SetRef no-ops silently on a property that does not exist,
            // so they had been wiring nothing for some time while still reading as if they were.
            //
            // This is the real thing: the baked bubble kit, wired through one lookup that tolerates
            // absence. Nothing baked → every piece stays null → the presenter draws its tinted-rect
            // greybox exactly as before.
            DialogueBubbleArt.Dress(presenter);

            var interactorGo = new GameObject("WorldInteractor");
            var interactor = interactorGo.AddComponent<WorldInteractor>();
            SetRef(interactor, "_player", reviewPlayer);
            SetRef(interactor, "_presenter", presenter);
            SetRefArray(interactor, "_interactables", people.Cast<Object>().ToArray());
            return interactor;
        }

        static void SetTideProfile(Component env, float mean, float amp, float phase)
        {
            var so = new SerializedObject(env);
            var tp = so.FindProperty("_activeTideProfile");
            if (tp == null) return;
            tp.FindPropertyRelative("MeanLevel").floatValue = mean;
            tp.FindPropertyRelative("Amplitude").floatValue = amp;
            tp.FindPropertyRelative("PhaseHours").floatValue = phase;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Sprite[] LoadSheetFrames(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                            .OrderBy(s => SpriteIndex(s.name)).ToArray();

        // ---- converged water model — shared config (single source of truth with the EditMode test) ----

        /// <summary>
        /// Author Nine Mile Creek's seabed — the whole authored plan, pushed in one call. The builder and
        /// every EditMode test go through HERE, so a test can never assert a coast the scene does not
        /// have (the <see cref="StPetersBuilder.ConfigureTidalTerrain"/> convention).
        ///
        /// <para>It is a one-line forward to <see cref="NineMileCreekMainland.ConfigureTerrain"/> and that
        /// is deliberate: A-1 proved the plan with its own fixtures before any of this existed, and a
        /// builder-shaped copy of it here would be the second coastline this region is not allowed to
        /// have. Kept as a named method on the builder because the name is the seam six test files and
        /// three dressing layers already reach for.</para>
        /// </summary>
        public static void ConfigureNineMileCreekTerrain(MainlandTidalTerrain terrain) =>
            NineMileCreekMainland.ConfigureTerrain(terrain);

        /// <summary>
        /// <b>THE PAINTED GROUND (ADR 0028)</b> — one full-region quad carrying the TerrainSplat shader,
        /// fed this region's painted height map and its five splat maps, replacing the greybox tiled
        /// rectangle. The mainland's mirror of <c>StPetersBuilder</c>'s splat block.
        ///
        /// <para><b>⭐ BUILDER-WIRED, NOT SCENE-WIRED.</b> Everything here is pushed from its owner — the
        /// band ladder and the sector from <see cref="NineMileCreekShoreMap"/>, the bar from
        /// <see cref="NineMileCreekMainland"/>, the paths from <see cref="TerrainSplatAssets"/> — so the
        /// owner's one Build click reproduces the whole wiring and a hand-dragged reference cannot be the
        /// only thing holding it together. <c>NineMileCreekSplatGroundTests</c> pins it.</para>
        ///
        /// <para><b>⚠ THE SECTOR IS NEUTRALISED ON PURPOSE.</b> The shader carries one island-shaped
        /// weather/sheltered split and cannot read a coast run, so the whole region is pushed onto the
        /// SHELTERED ladder and this coast's rock arrives as PAINT instead. See
        /// <see cref="NineMileCreekShoreMap"/> for the argument.</para>
        ///
        /// <para>Returns false — and leaves the caller to lay the greybox tiles — only when the region has
        /// no usable extent or the TerrainSplat material is missing. A region that has simply never been
        /// painted still gets the quad: the height bands alone draw a legible coast, which is the shader's
        /// PR-1 fallback and is strictly better than a flat green rectangle.</para>
        /// </summary>
        public static bool BuildSplatGround(RegionDef region)
        {
            if (region == null || !region.HasUsableExtent)
            {
                Debug.LogWarning("[NineMileCreekBuilder] no usable region extent for the splat ground — " +
                                 "falling back to the tiled greybox. Fix the RegionDef's " +
                                 "WorldSizeMeters / SeabedPixelsPerMetre.");
                return false;
            }

            var splatMat = AssetDatabase.LoadAssetAtPath<Material>(ArtTerrainSplatMat);
            if (splatMat == null)
            {
                Debug.LogWarning("[NineMileCreekBuilder] " + ArtTerrainSplatMat + " not found — falling " +
                                 "back to the tiled greybox ground. Re-run after the material imports.");
                return false;
            }

            // ⚠⚠ CREATED INACTIVE, CONFIGURED, AND ONLY THEN ACTIVATED — and that order is load-bearing.
            // TerrainSplatSurface is [ExecuteAlways], so AddComponent fires OnEnable IMMEDIATELY, and
            // OnEnable builds the ground quad's mesh from whatever extent the component is carrying at
            // that instant — which, on a fresh component, is its serialized default of 160 × 120 m.
            // Configure() afterwards updates the fields but EnsureBuilt() early-returns once the mesh
            // exists, so the quad stays 160 × 120 in the middle of a 760 × 560 region: a small rectangle
            // of painted ground with black around it, which is exactly what the first capture of this pass
            // showed.
            //
            // St Peters does the same thing and gets away with it ONLY because the builder saves the
            // scene: on the next load OnEnable runs again with the correct serialized extent and rebuilds
            // the mesh. That makes the wiring depend on a save/reload round-trip nobody wrote down.
            // Configuring before the first enable removes the dependency instead of relying on it.
            var splatGo = new GameObject("TerrainSplat");
            splatGo.SetActive(false);
            var splat = splatGo.AddComponent<HiddenHarbours.Art.TerrainSplatSurface>();
            splat.Configure(NineMileCreekSeaCenter, NineMileCreekSeaSize, splatMat,
                            HiddenHarbours.Art.TerrainSplatSurface.DefaultSortingOrder);

            // The owner's painted seabed if it exists, else a fresh bake of the analytic coast — NEVER
            // overwrite an existing paint (ADR 0019: refresh, don't clobber).
            var heightMap = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(
                DataTerrain + "/" + TerrainPaintTool.NineMileCreekSeabedName + ".asset");
            if (heightMap == null)
                heightMap = TerrainPaintTool.BakeNineMileCreekSeabed(
                    region.WorldCenter, region.WorldSizeMeters, region.SeabedTexels);
            if (heightMap != null && heightMap.HeightTexture != null)
                splat.ConfigureHeightMap(heightMap.HeightTexture,
                    heightMap.MinElevation, heightMap.MaxElevation);
            else
                Debug.LogWarning("[NineMileCreekBuilder] no painted height map for the splat ground — the " +
                                 "quad clips itself empty (below the paint floor everywhere). Run " +
                                 "'Hidden Harbours ▸ Art ▸ Regenerate Nine Mile Creek Starter Splat' " +
                                 "after re-baking the seabed.");

            ConfigureSplatSurface(splat);

            // The terrain material kit: pack the detail arrays (derived, GUID-stable) and wire them plus
            // any painted splat maps. Both no-op safely when the kit or the paint is absent — the shader
            // falls back to flat band colours / bands-only.
            HiddenHarbours.Art.Editor.TerrainTexArrayBuilder.Build();
            splat.ConfigureDetail(
                AssetDatabase.LoadAssetAtPath<Texture2DArray>(
                    HiddenHarbours.Art.Editor.TerrainTexArrayBuilder.Array256Path),
                AssetDatabase.LoadAssetAtPath<Texture2DArray>(
                    HiddenHarbours.Art.Editor.TerrainTexArrayBuilder.Array512Path));

            // Paths from TerrainSplatAssets with THIS region's stem, never literals here — the stem
            // overloads (#522) are what keep a second painted region off the island's maps.
            var splatMaps = new Texture2D[TerrainSplatBrush.TextureCount];
            for (int i = 0; i < splatMaps.Length; i++)
                splatMaps[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    TerrainSplatAssets.PathOf(i, NineMileCreekStarterSplat.SplatBaseName));
            splat.ConfigureSplat(splatMaps[0], splatMaps[1], splatMaps[2], splatMaps[3], splatMaps[4]);

            if (splatMaps[0] == null)
                Debug.LogWarning("[NineMileCreekBuilder] the splat ground is wired but UNPAINTED (no " +
                                 TerrainSplatAssets.PathOf(0, NineMileCreekStarterSplat.SplatBaseName) +
                                 ") — the coast draws from the height bands alone. Run 'Hidden Harbours ▸ " +
                                 "Art ▸ Regenerate Nine Mile Creek Starter Splat'.");

            // Everything is on the component; NOW let OnEnable build the quad — at the region's real
            // extent, with every uniform already in place. See the SetActive(false) note above.
            splatGo.SetActive(true);
            return true;
        }

        /// <summary>
        /// Push every DESIGN NUMBER the splat shader needs onto <paramref name="splat"/> — the band
        /// ladder, the neutralised sector, the crossing's cobble spine.
        ///
        /// <para><b>⭐ SPLIT OUT FROM <see cref="BuildSplatGround"/> so a test can hold it.</b> The rest of
        /// that method is asset I/O — minting a height map, packing texture arrays, loading five PNGs —
        /// which a test cannot run without writing to the project. This half is pure number-pushing, which
        /// is exactly the half that drifts silently: a band floor left at the component's serialized
        /// default draws a coast metres out of position and nothing errors.
        /// <c>NineMileCreekSplatGroundTests</c> asserts every value below arrives.</para>
        /// </summary>
        public static void ConfigureSplatSurface(HiddenHarbours.Art.TerrainSplatSurface splat)
        {
            if (splat == null) return;

            splat.ConfigureBands(
                NineMileCreekShoreMap.PaintFloorElevation, NineMileCreekShoreMap.RippleFloorElevation,
                NineMileCreekShoreMap.SandFloorElevation, NineMileCreekShoreMap.MarramFloorElevation,
                NineMileCreekShoreMap.GrassFloorElevation, NineMileCreekShoreMap.ShingleFloorElevation,
                NineMileCreekShoreMap.BandWiggleMetres, NineMileCreekShoreMap.BandWiggleScale,
                NineMileCreekShoreMap.BandDetailMetres, NineMileCreekShoreMap.BandDetailScale);

            splat.ConfigureWeatherSector(NineMileCreekShoreMap.SectorAnchor,
                NineMileCreekShoreMap.SectorAspect, NineMileCreekShoreMap.SectorFacing,
                NineMileCreekShoreMap.SectorFeather);

            // The crossing's cobble spine — the walking line the player reads to decide whether the bar is
            // on. Its geometry is this region's half of the bar; its spine numbers are the ISLAND'S,
            // because it is one bar and NineMileCreekMainland §6 keeps the two halves mirror images
            // deliberately.
            splat.ConfigureSandbar(NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo,
                NineMileCreekMainland.BarHalfWidth,
                StPetersShoreMap.BarSpineHalfWidth, StPetersShoreMap.BarSpineFloorElevation);
        }

        /// <summary>
        /// Everything the region's <see cref="RegionDef"/> asserts about itself, applied to an EXISTING
        /// asset as well as a new one (see the call site for why that matters).
        ///
        /// <para>⚠ <b>The depth ladder is the ruled one and it changed here.</b> Nine Mile Creek shipped
        /// as <c>IsDeepHarbour = true, HarbourDepthMeters = 6</c> — it was standing in for Port Greywick.
        /// The ladder is three HARBOURS (St Peters' dock ~0.6 m · Nine Mile Creek ~1.6 m · Port Greywick
        /// 6 m dredged), so this is <b>false / 1.6</b>: the lobster-boat berth, which is the owner's
        /// stated ceiling for the starter world. The fields are flavour today — nothing gates on them —
        /// but leaving them saying "deep dredged harbour" would be a lie in the data, and the day
        /// something DOES gate on them it would be a silent one.</para>
        /// </summary>
        public static void ApplyMainlandRegionFacts(RegionDef r)
        {
            if (r == null) return;
            r.Id = "region.nine_mile_creek";
            r.DisplayName = "Nine Mile Creek";
            r.SceneName = SceneName;

            r.IsDeepHarbour = false;
            r.HarbourDepthMeters = -NineMileCreekBasinElevation;   // the shoal IS the gate: 1.6 m

            r.TideMeanLevel = TideMean;
            r.TideAmplitude = TideAmplitude;
            r.TidePhaseHours = TidePhaseHours;

            r.WorldCenter = NineMileCreekSeaCenter;
            r.WorldSizeMeters = NineMileCreekSeaSize;
            r.SeabedPixelsPerMetre = NineMileCreekMainland.SeabedPixelsPerMetre;

            r.Description = "A working wharf on a big-tide coast: a squared-U quay on a made spit at the " +
                            "creek's mouth, a barachois behind it, and the tidal bar out to St Peters " +
                            "coming ashore to the south. The fleet dries out under itself at spring low.";
            EditorUtility.SetDirty(r);
        }

        // ⭐ The Sea's WaterSurface config, the weather palette, and the four SerializedObject setters they
        // used are GONE from this file — they now live once, in WaterSceneTemplate, called by both land
        // builders (see ConfigureLandRegionWater at the Sea above). They were a hand-rolled copy of St
        // Peters' block, and the cost of that copy was the defect this PR fixes: when the displaced surface
        // was added to the original in ADR 0023 phase 2, the copy did not get one, and Nine Mile Creek has
        // been drawing the pre-displacement sea ever since. Deleting the copy is the fix that stays fixed.

        // ---- helpers (self-contained; the cove builder's are private) -----------------------

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

        /// <summary>
        /// Persist an ENUM field by NAME, not by ordinal. <c>SerializedProperty.enumValueIndex</c> is an
        /// index into <c>enumNames</c> rather than the enum's value, so writing <c>(int)someEnum</c> into
        /// it is only right while the enum happens to be contiguous from zero — a silent wrong-value the
        /// day somebody assigns an explicit number. Looking the name up is exact whatever the values are.
        /// </summary>
        static void SetEnum<TEnum>(Component c, string field, TEnum value) where TEnum : System.Enum
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null || p.propertyType != SerializedPropertyType.Enum)
            {
                Debug.LogWarning($"[NineMileCreekBuilder] no enum field '{field}' on {c.GetType().Name}.");
                return;
            }
            int i = System.Array.IndexOf(p.enumNames, value.ToString());
            if (i < 0)
            {
                Debug.LogWarning($"[NineMileCreekBuilder] '{value}' is not a member of '{field}' on " +
                                 $"{c.GetType().Name} — left at its default.");
                return;
            }
            p.enumValueIndex = i;
            so.ApplyModifiedPropertiesWithoutUndo();
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
            if (p == null) { Debug.LogWarning($"[NineMileCreekBuilder] no array field '{field}'."); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Imported art is sliced (spriteMode Multiple, one sub-sprite), so LoadAssetAtPath<Sprite>
        // returns null — fall back to the first sub-sprite. Null if the art isn't imported.
        static Sprite LoadSpriteAny(string path)
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

        /// <summary>
        /// ⭐ <b>THE GREYBOX GROUND IS ONE SCALED QUAD, NEVER A TILING OF THE REGION.</b> A flat
        /// <paramref name="color"/> rectangle covering <paramref name="size"/>, standing in for the
        /// painted ground on the one checkout <see cref="BuildSplatGround"/> cannot serve.
        ///
        /// <para><b>What this replaced.</b> It was a <c>SpriteDrawMode.Tiled</c> draw of
        /// <c>Tilesets/Grass.png</c> — 32 px at 32 px/unit, so a <b>1 × 1 m</b> tile — sized to the region
        /// rect. Over 760 × 560 m that is <b>425,600 tiles = 1,702,400 vertices</b>, and Unity's tiling
        /// generator refuses past a 16-bit index buffer:
        /// <code>
        ///   Cannot generate 9 slice most likely because the size is too big.
        /// </code>
        /// So the renderer drew NOTHING. The fallback for a checkout that cannot paint its ground has
        /// never once put ground on the screen at this region's size.</para>
        ///
        /// <para><b>Why it broke without anyone changing it.</b> The helper was right when it was written:
        /// it drew a 24 × 40 m town strip beside the quay — 960 tiles, comfortably inside the ceiling. It
        /// became impossible the day the ground was re-pointed at the WHOLE region ("ONE ground plane over
        /// the WHOLE region", at the call site above), which multiplied the tile count by 443 without
        /// touching this line. Same failure the sea plane hit, one rect at a time.</para>
        ///
        /// <para><b>Why the answer is a flat quad and not a coarser tiling.</b> The three ways to keep the
        /// grass texture all cost more than the path is worth: a pre-tiled chunk sprite is a new art asset
        /// for a broken-checkout stand-in; a grid of sub-ceiling renderers is real code on a path that
        /// fires when the project is already unbuildable; and scaling the tile up (8 m/tile would fit)
        /// picks a magic number and draws 32 texels across 8 m. This path already fell back to a flat
        /// coloured square whenever Grass.png was not imported — which, since <c>*.png</c> is LFS and
        /// <c>*.mat</c> is not, is the LIKELIER of the two broken checkouts anyway. Making that the only
        /// behaviour is a simplification of what shipped, not a new look: greybox that admits it is
        /// greybox, correct at any region size, with no ceiling to stay under.</para>
        ///
        /// <para><b>⚠ The scale is DERIVED from the sprite, never assumed.</b> The old fallback branch
        /// hard-coded <c>size * 2f</c> — right only because Square.png happens to be 16 px at 32 px/unit,
        /// i.e. 0.5 m. Reading <c>sprite.bounds.size</c> is what keeps this correct if that asset is ever
        /// re-cut, and it is the same lesson <c>WaterSceneTemplate.ConfigureSeaPlane</c> learned from the
        /// four sea planes that scaled a 0.5 m square as though it were 1 m and covered a QUARTER of their
        /// regions.</para>
        ///
        /// <para>Returns the object so a test can measure it; <c>NineMileCreekGreyboxGroundTests</c> holds
        /// the rule.</para>
        /// </summary>
        public static GameObject MakeGreyboxGround(string name, Sprite square, Vector2 center, Vector2 size,
                                                   int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;

            // ⚠️ An explicit `== null`, never `??`: `??` bypasses UnityEngine.Object's equality overload,
            // so a destroyed-or-missing sprite would sail past it and throw at draw time instead.
            if (square == null)
            {
                Debug.LogWarning("[NineMileCreekBuilder] no square sprite for the greybox ground — the " +
                                 "region draws no ground at all. This is the fallback's own fallback; the " +
                                 "thing to fix is whatever stopped " + ArtTerrainSplatMat + " loading.");
                return go;
            }

            sr.sprite = square;
            sr.color = color;
            sr.drawMode = SpriteDrawMode.Simple;   // ⚠ NOT Tiled — see the summary above

            Vector3 atUnitScale = square.bounds.size;   // metres this sprite draws before any scaling
            go.transform.localScale = new Vector3(
                atUnitScale.x > 1e-4f ? size.x / atUnitScale.x : 1f,
                atUnitScale.y > 1e-4f ? size.y / atUnitScale.y : 1f,
                1f);
            return go;
        }

        // (ShorelinePoints / MakeShoreline are RETIRED. They traced a hand-made land/water fence at x = -4
        // that dipped around an 8 × 6 m quay — the only thing stopping a boat sailing through the town
        // when the harbour floor was a flat -6 m plane. The mainland's coast, beach, ledges, spit and quay
        // decks are authored terrain, so depth stops the hull, and a second hand-traced coastline beside
        // the authored one is the duplicate this recreation exists to remove. See the note at the call
        // site in Build().)

        static GameObject MakeBuilding(string name, Sprite sprite, Vector2 pos, Sprite fallback, Color fallbackColor)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 2;   // pre-Play default only; the YSortSprite below OWNS the order
            // A building is something you walk AROUND, so it layers by world Y like the rest of the world —
            // the same treatment VillageBuildingCatalog gives the kit's houses. Without it the creek's
            // buildings held a fixed 2, which is the decor band's FLOOR, so once the band was re-based to
            // fit the region (ADR 0032) every tuft of grass would have drawn over them.
            go.AddComponent<YSortSprite>();
            if (sprite != null) { sr.sprite = sprite; go.transform.localScale = Vector3.one; }
            else { sr.sprite = fallback; sr.color = fallbackColor; go.transform.localScale = new Vector3(5f, 5f, 1f); }
            // Solid building so the boat / on-foot player can't pass through Nine Mile Creek's geometry. Non-trigger
            // BoxCollider2D sized to the rendered footprint (sprite bounds in local space, or the fallback
            // square's metric size). Buildings sit on the quay land beyond the shoreline; this makes each
            // read solid in its own right (owner playtest: "boat sails through the buildings").
            var box = go.AddComponent<BoxCollider2D>();
            if (sprite != null) { box.size = sprite.bounds.size; box.offset = sprite.bounds.center; }
            else { box.size = Vector2.one; }   // 1 unit × the (5,5,1) localScale → a 5 m × 5 m footprint
            return go;
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

        static void RegisterScene(string path)
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(s => s.path == path)) return;
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void EnsureFolders()
        {
            foreach (var f in new[] { DataConfig, DataShip, DataRegions, ArtSprites, Scenes })
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
