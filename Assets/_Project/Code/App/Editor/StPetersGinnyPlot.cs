#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Art;                 // YSortSprite, PreconfiguredLight, ChimneySmoke
using HiddenHarbours.Art.Editor;          // VillageBuildingCatalog, VillageBuildingKit
using HiddenHarbours.Core;                // ITidalTerrain
using HiddenHarbours.Player;              // GinnyFreezer — the cold chain that moved out here with her

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>AUNT GINNY'S PLOT</b> — her cottage, her dooryard, her garden, her freezer and three derelict
    /// sheds, standing together on a clearing in the island's eastern woods. The owner ruled on
    /// 2026-08-16 that Ginny moves out of the village onto <b>land that is hers</b>, with outbuildings
    /// the player can one day repair.
    ///
    /// <para><b>⭐ WHY THIS IS ITS OWN FILE AND NOT FOUR MORE CONSTANTS ON THE BUILDER.</b> The cottage
    /// used to be <c>StPetersBuilder.CottagePos</c>, and that one name was doing SIX jobs: where Ginny
    /// lived, the point <see cref="StPetersBuilder.VillageGreen"/> is the midpoint of, the centre of the
    /// woods planter's 44 m no-plant clearing, the origin
    /// <see cref="StPetersVillage.RequiredClearingRadius"/> measures building reach from, the anchor the
    /// shops measure back to, and the centre of the grass/decor keepouts. Five of those six mean <i>"the
    /// middle of the village"</i> and have nothing to do with the aunt. Moving the constant would have
    /// dragged the green, every villager's door facing and the village's own tree clearing east into the
    /// woods — planting a forest through the schoolhouse. So the old name stayed put and was renamed for
    /// what it always meant (<see cref="StPetersBuilder.VillageHearthPos"/>, value unmoved at (0,14), so
    /// the rename is provably behaviour-neutral), and Ginny's home became this file.</para>
    ///
    /// <para><b>The site: (84, 28), and every number in that choice is measured.</b> Elliptical distance
    /// from the island centre is exactly 50.0 m against a 120 m radius, which puts it deep in
    /// <see cref="StPetersWoods.InlandFraction"/>'s sheltered core — the densest, most hardwood ground
    /// the woods generator makes, i.e. genuinely IN the trees rather than at their edge. The plateau is
    /// flat 6 m there (<c>TidalTerrain.IslandProfile</c> returns <c>_islandElevation</c> anywhere inside
    /// the ellipse), so it is dry at every tide against a 2.2 m spring high. It is <b>85.2 m from the
    /// village hearth</b>, which is the number that actually decides the site: <see
    /// cref="StPetersWoods.DooryardRadius"/> is 74.8 m, the repo's own line for how far human
    /// disturbance from the village reads on the ground. Her land begins past it. Anything nearer would
    /// have been the village's weedy edge wearing a fence.</para>
    ///
    /// <para><b>⚠ THE COMMUTE IS A REAL COST, NOT A DETAIL.</b> 85.2 m at her authored 1 m/s is 85 real
    /// seconds, and at <c>GameConfig.DefaultSecondsPerDay</c> = 1800 a game hour is 75 real seconds — so
    /// her walk to the village is <b>1.13 GAME HOURS each way</b>. That is why <see
    /// cref="StPetersRoutines"/> gives her three blocks and not four: a fourth would have put her at
    /// 2.3 h/day on the road. It is also why she leaves at 4.5 — see the routine's own note.</para>
    ///
    /// <para><b>⭐ THE SHEDS ARE REAL BUILDINGS AS OF 2026-08-19 — the stated art gap is discharged.</b>
    /// This file used to say: "nothing in the repo draws a shed at all, derelict or sound … the upstream
    /// ask is a shed/outbuilding rig with a WEAR axis." That ask arrived as the BUILDING LIFECYCLE PASS,
    /// which gives every iso building seven construction phases and four states of dereliction — so each
    /// shed is now a baked 8-facing <c>wharfBuilding</c> build in the decay state this file's own prose
    /// already described (see <see cref="Sheds"/>), placed through the same
    /// <c>VillageBuildingCatalog</c> path as the cottage. The data rows did not move; only the sprite
    /// lookup changed, which is exactly what they were shaped for.</para>
    ///
    /// <para><b>⚠ THE FOOTPRINTS GREW, AND THAT IS MEASURED, NOT INCIDENTAL.</b> The rows were
    /// hand-sized as 2.6–3.4 m markers; the wharf rig cannot draw a building smaller than
    /// <b>3.60 × 4.50 m</b> at any point on its size axis (swept 0.0 → 1.0 in the standalone V8 harness),
    /// and its smallest preset, <c>netShed</c>, is 3.88 × 5.10 m. So
    /// <see cref="Shed.FootprintRadiusMetres"/> now comes from the BAKE CONTRACT when the build is
    /// baked, and falls back to the authored size only when it is not — the same
    /// derive-don't-declare rule the cottage has always followed. <c>StPetersVillageTests</c> re-derives
    /// the clearing from those numbers and fails with the figure to use, which is what makes growing
    /// them safe rather than hopeful.</para>
    ///
    /// <para>The greybox path is KEPT, not deleted: a working tree with no baked sheets still stands the
    /// plot up in tinted markers rather than dropping three buildings on the floor, which is what the
    /// pure EditMode fixtures and a fresh clone both need.</para>
    ///
    /// <para><b>Repair is NOT here.</b> The owner's "sheds the player can eventually repair" is
    /// phase-gated gameplay (CLAUDE.md rule 8) and is logged as its own backlog item —
    /// <c>backlog/building-lifecycle-gameplay.md</c>, M3. This file places derelict buildings; it grants
    /// no mechanic for mending them, and adds no def, interaction or save field toward one. What it does
    /// give that arc is the art it will need: the ladder from <c>ruin</c> back to <c>finished</c> is
    /// already baked-able for every one of these buildings.</para>
    ///
    /// <para><b>There is no land-ownership system and this file does not invent one.</b> A sweep for
    /// parcel/plot/ownership data found only <c>BoatOwnerDef</c>. "Hers" is therefore a declared
    /// boundary — <see cref="ClearingRadius"/> around <see cref="CottagePos"/>, which the world reads to
    /// know where her land is and nothing else reads to grant a right.</para>
    /// </summary>
    public static class StPetersGinnyPlot
    {
        /// <summary>The GameObject the whole plot hangs off, so a rebuild can find and replace it.</summary>
        public const string RootName = "GinnyPlot";

        // =====================================================================================
        //  THE SITE
        // =====================================================================================

        /// <summary>
        /// Ginny's cottage: the centre of its footprint, and the origin every other number on the plot is
        /// measured from. See the class remarks for why this point and not another.
        /// </summary>
        public static readonly Vector2 CottagePos = new Vector2(84f, 28f);

        /// <summary>
        /// Which of the kit's builds her cottage IS.
        ///
        /// <para><b>⚠ It reuses <c>sageCottage</c> deliberately.</b> The kit's builds are dialled from the
        /// house rig and BAKED — a bespoke <c>ginnyCottage</c> would need an exterior bake, an interior
        /// bake, a slice and a contract entry, which is an art-lane job and not this PR's. <c>sageCottage</c>
        /// is the kit's one-room dwelling ("one room, a fisherman's"), it is 6.60 × 8.05 m, it already has
        /// a baked room in <c>Interiors.json</c>, and its <c>porch: 'front'</c> puts the door on the gable
        /// its room registers a doorway to — the one axis that decides whether a building can be entered
        /// at all (the porch-'none' trap). Cost of the reuse, stated plainly: the island now has two
        /// cottages of the same build, one in the village and one 100 m east in the woods. <b>The upstream
        /// ask is a dialled <c>ginnyCottage</c> variant</b> — a body colour and a roof are one axis change
        /// each once someone runs the bake.</para>
        /// </summary>
        public const string CottageKey = "sageCottage";

        /// <summary>
        /// Where her door looks: back down the island toward the village green, which is the direction you
        /// arrive from. The village turns every door toward <see cref="StPetersBuilder.VillageGreen"/> for
        /// this reason and the plot keeps the rule — a door is turned toward the ground people walk up
        /// from, never given a hard-coded facing index, so a re-bake with a different facing count
        /// re-derives the nearest cell instead of pointing her door at a tree.
        /// </summary>
        public static Vector2 Approach => StPetersBuilder.VillageGreen;

        /// <summary>
        /// The spot in front of her door — <see cref="StPetersInhabitants.DoorstepMetres"/> out toward
        /// <see cref="Approach"/>. This is her STEP: the mark she stands on at night, and the one the
        /// station table hangs <c>station.st_peters.cottage_step</c> off. It follows the cottage, which is
        /// the whole point of deriving it rather than authoring it.
        /// </summary>
        public static Vector2 Dooryard
        {
            get
            {
                Vector2 d = Approach - CottagePos;
                return d.sqrMagnitude < 1e-6f
                    ? CottagePos
                    : CottagePos + d.normalized * StPetersInhabitants.DoorstepMetres;
            }
        }

        /// <summary>
        /// Her garden, relative to the cottage: on the SOUTH side, which is the sunny one and the side
        /// away from both sheds and the door. It used to be the west side, back when the cottage stood in
        /// the village and west was its quiet flank; out here west is the FRONT (the door faces the
        /// approach), so the garden moved round rather than being left standing on her own doorstep.
        /// </summary>
        public static readonly Vector2 GardenOffset = new Vector2(0f, -7f);

        /// <summary>Where she gardens: <see cref="CottagePos"/> + <see cref="GardenOffset"/>.</summary>
        public static Vector2 GardenPos => CottagePos + GardenOffset;

        /// <summary>
        /// AUNT GINNY'S FREEZER (M1 §7.3 — "free, but only at home"). <b>It moved with her</b>, on the
        /// owner's explicit 2026-08-16 ruling: it is her freezer at her house, so it goes where she goes.
        ///
        /// <para><b>⚠ THIS IS A LIVE COLD-CHAIN CHANGE, not set dressing.</b> Freezing a catch used to be
        /// three metres from the start spawn; it is now an 85 m walk inland from the village and further
        /// from the slip. Nothing in the economy was retuned to match — the owner chose the friction with
        /// the trek measured in front of him. Sited on the APPROACH side of the cottage so you meet it
        /// walking up rather than having to go round the house.</para>
        /// </summary>
        public static readonly Vector2 FreezerPos = new Vector2(78f, 24f);

        // =====================================================================================
        //  THE SHEDS
        // =====================================================================================

        /// <summary>One derelict outbuilding: what it was, where it stands, which baked build draws it,
        /// and why it is there. That day the rows were shaped for arrived on 2026-08-19 — see the class
        /// remarks.</summary>
        public readonly struct Shed
        {
            /// <summary>Scene-object name. Unchanged since the greybox rows, and deliberately: it is what
            /// <c>root.transform.Find(...)</c> looks for in the tests and what the owner sees in the
            /// hierarchy.</summary>
            public readonly string Key;

            /// <summary>
            /// The <c>VillageBuildingKit.LifecycleSet</c> build that draws it — a wharf-rig preset in a
            /// decay state. Empty would mean "greybox only"; nothing declares that today.
            /// </summary>
            public readonly string BuildKey;

            public readonly Vector2 Position;

            /// <summary>
            /// The GREYBOX marker's size, and the footprint fallback for a tree with no baked sheets.
            ///
            /// <para>⚠ This is NOT the drawn building's footprint any more — see
            /// <see cref="FootprintRadiusMetres"/>, which prefers the contract. Kept because a plot that
            /// cannot draw itself still has to reserve ground plausibly, and because it is the number
            /// the owner originally sized these sheds by.</para>
            /// </summary>
            public readonly Vector2 SizeMetres;

            public readonly string Reason;

            public Shed(string key, string buildKey, Vector2 position, Vector2 sizeMetres, string reason)
            {
                Key = key;
                BuildKey = buildKey;
                Position = position;
                SizeMetres = sizeMetres;
                Reason = reason;
            }

            /// <summary>The baked build that draws this shed, or an invalid placement when the kit has
            /// not been baked in this working tree.</summary>
            public VillageBuildingCatalog.Placement Placement =>
                string.IsNullOrEmpty(BuildKey) ? default : VillageBuildingCatalog.Find(BuildKey);

            /// <summary>The ground this shed reserves at any facing — the same half-diagonal circle
            /// <see cref="StPetersVillage.FootprintRadiusMetres(VillageBuildingKit.Entry)"/> reserves a
            /// house with, and for the same reason: a quarter-turned building is deeper than a face-on
            /// one, so the reservation may not be a rectangle.
            ///
            /// <para><b>⭐ FROM THE CONTRACT WHEN THERE IS ONE.</b> The rig's own <c>Wd</c>/<c>Ln</c>,
            /// measured at bake time against the rendered silhouette — not the hand-sized marker, which
            /// is 1.3 m narrower than the smallest building the rig can draw. Falling back to the marker
            /// keeps an unbaked tree honest rather than reserving nothing.</para></summary>
            public float FootprintRadiusMetres
            {
                get
                {
                    var placement = Placement;
                    if (placement.IsValid) return StPetersVillage.FootprintRadiusMetres(placement);
                    return 0.5f * Mathf.Sqrt(SizeMetres.x * SizeMetres.x + SizeMetres.y * SizeMetres.y);
                }
            }
        }

        /// <summary>
        /// The three, in the order you meet them walking up from the village. Spacing is derived, not
        /// eyeballed: every one clears the cottage's own 5.21 m footprint radius plus its own plus a
        /// walking gap, which <c>StPetersGinnyPlotTests</c> re-derives from the contract rather than
        /// trusting these numbers.
        /// </summary>
        /// <para><b>⚠ ALL THREE MOVED ON 2026-08-19, AND THE MEASUREMENT IS WHY.</b> They were sited as
        /// 2.6–3.4 m greybox markers (1.70–2.14 m of half-diagonal); the buildings that now draw them
        /// reserve <b>3.20–3.45 m</b>, so every one of them grew by about 80 % where it stands. Measured
        /// consequence, from the first test run after the bake: the woodshed came within
        /// <b>1.98 m</b> of the camper against a 2.00 m walking gap, and the lean-to within
        /// <b>1.33 m</b>. The camper is a home the owner parks and its lot was threaded through the old
        /// gaps, so the SHEDS moved rather than the camper:
        /// woodshed (91,34) → (93,34) · net store (93,23) → (95,22) · lean-to (77,35) → (74,33).
        /// Each keeps its compass character and its sentence; each now clears the camper by 3.4–14.4 m
        /// and the cottage by 1.2–4.1 m, and the plot's furthest reach is unchanged at 19.21 m (the
        /// camper still dominates) against a 20 m clearing.</para>
        ///
        /// <para><b>⭐ THE DECAY STATE OF EACH ONE WAS READ OFF THESE SENTENCES, not chosen by eye.</b>
        /// "The one still standing squarest" is <c>neglected</c>; "the furthest gone" is <c>ruin</c>;
        /// "its back broken" is <c>collapsing</c>, which is precisely what that rung of the ladder draws
        /// — a roof slope stove in with the rafters showing. The prose came first and the art was made to
        /// match it, rather than the other way round.</para>
        public static IReadOnlyList<Shed> Sheds => new[]
        {
            new Shed("woodshed", "ginnyWoodshed", new Vector2(93f, 34f), new Vector2(3.0f, 2.4f),
                     "behind the cottage on the north side — the one still standing squarest, because a " +
                     "woodshed is the one you keep the roof on"),
            new Shed("netStore", "ginnyNetStore", new Vector2(95f, 22f), new Vector2(3.4f, 2.6f),
                     "east, the furthest out — a net store from when this land was worked, and the " +
                     "furthest gone"),
            new Shed("leanTo", "ginnyLeanTo", new Vector2(74f, 33f), new Vector2(2.6f, 2.2f),
                     "north-west, first thing you pass walking up — a lean-to with its back broken"),
        };

        // =====================================================================================
        //  THE BOUNDARY (pure — no assets, no scene, so a test can assert the plot without either)
        // =====================================================================================

        /// <summary>
        /// <b>The declared edge of Ginny's land</b>, in metres from <see cref="CottagePos"/> — and the
        /// clearing the woods hold off. There is no ownership system behind it (see the class remarks):
        /// this is how the world knows where her plot is, and the only thing that reads it is placement.
        ///
        /// <para>⚠ Do not re-tune by eye. <see cref="RequiredClearingRadius"/> derives what the buildings
        /// on the plot actually need and <c>StPetersVillageTests</c> fails with the number to use if a
        /// shed moves or the cottage is re-baked bigger. Kept a <c>const</c> for the same reason
        /// <see cref="StPetersWoods.VillageClearingRadius"/> is: <see cref="StPetersWoods.IsPlantable"/>
        /// calls it tens of thousands of times per build and must not read a contract to answer.</para>
        ///
        /// <para><b>⚠ 18 → 20 m on 2026-08-17, and the reason is a measurement.</b> The camper lot
        /// (<see cref="StPetersCamperLot"/>) put a SECOND DWELLING on her land, and the Clipper reserves
        /// 5.21 m of ground — the same as her cottage's 5.21 m. Her plot was sized for one house and
        /// three sheds; there is exactly one slot inside the old 18 m that takes a camper, threaded
        /// between the woodshed and the lean-to with 2.1 m either side, and parking a home you own in a
        /// gap is not a lot. Twenty metres holds the whole thing at the plot's own tightest existing
        /// walking gap. <b>Cost, stated:</b> 239 m² more ground the woods are held off, and the erratics
        /// and the meadow follow the same number — it is a slightly larger clearing in the trees, not a
        /// different kind of place.</para>
        /// </summary>
        public const float ClearingRadius = 20f;

        /// <summary>
        /// <b>⭐ THE SEAM THE FOREST LANE ADOPTED — and this is what became of it.</b> Is this ground on
        /// GINNY'S land?
        ///
        /// <para>It used to be <c>Vector2.Distance(p, CottagePos) &lt; ClearingRadius</c>, asked from
        /// exactly one line in <see cref="StPetersWoods.IsPlantable"/> so the dense-forest pass could take
        /// the call over by name. It did: placement now asks
        /// <see cref="StPetersWoodlandZones.IsCleared"/>, which covers her plot, the cut lane out to her
        /// dooryard and the corridors through the island's three woodland lots — <b>one clearing mechanism,
        /// still one call site in placement.</b></para>
        ///
        /// <para><b>⚠ THIS ANSWERS THE NARROW QUESTION ON PURPOSE.</b> It asks the table about HER row
        /// alone, not whether the ground is cleared at all — those are different questions and the general
        /// one would put the village green, both painted paths and every lot's lane "on Ginny's land". A
        /// caller who means <i>"may a tree stand here"</i> wants <c>IsCleared</c>; a caller who means
        /// <i>"is this hers"</i> — a later pass siting the camper on her plot, say — wants this.</para>
        ///
        /// <para>The GEOMETRY is still declared here, in <see cref="CottagePos"/> and
        /// <see cref="ClearingRadius"/>: the zone row reads those two constants rather than restating them,
        /// so growing or moving her land is still a one-line change in this file and the woods follow it.</para>
        ///
        /// <para><b>⚠ THE CAMPER LOT IS INSIDE THIS SAME DISC, on purpose — there is no second clearing
        /// mechanism and there must not be.</b> When <see cref="StPetersCamperLot"/> needed cleared
        /// ground the answer was to widen <see cref="ClearingRadius"/> (18 → 20 m, 2026-08-17), not to
        /// declare a second circle or a second zone row: two mechanisms means one of them gets forgotten.
        /// The zone row reads the widened constant, so the woods follow it with no table change. Anything
        /// else that ever stands on her land goes the same way — into
        /// <see cref="RequiredClearingRadius"/>, and this predicate grows to hold it.</para>
        /// </summary>
        public static bool IsInsideClearing(Vector2 p) =>
            StPetersWoodlandZones.IsInside(StPetersWoodlandZones.GinnyPlotZone, p);

        /// <summary>
        /// The clearing the plot ACTUALLY needs, derived from what stands on it: the furthest any
        /// footprint reaches from <see cref="CottagePos"/>. Returns the cottage's own reach alone when the
        /// kit is unbaked, so a test can say so rather than pass vacuously.
        ///
        /// <para>The camper lot is in here too, and it is the term that DOMINATES (2026-08-17) — see
        /// <see cref="ClearingRadius"/>. Its reach comes off the chosen variant's own gameplay sidecar,
        /// so the owner's Bantam/Clipper decision re-derives this number instead of leaving the clearing
        /// measured against the camper he did not pick.</para>
        /// </summary>
        public static float RequiredClearingRadius()
        {
            var placement = VillageBuildingCatalog.Find(CottageKey);
            float worst = placement.IsValid ? StPetersVillage.FootprintRadiusMetres(placement) : 0f;

            foreach (var shed in Sheds)
            {
                float reach = Vector2.Distance(shed.Position, CottagePos) + shed.FootprintRadiusMetres;
                if (reach > worst) worst = reach;
            }

            float camper = StPetersCamperLot.ClearingReachMetres;
            return camper > worst ? camper : worst;
        }

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Stand the plot up: the cottage through the kit's ONE build path (so a cottage the owner drags
        /// in and this one are the same object), then the sheds as greybox markers. Returns how many
        /// buildings were placed.
        ///
        /// <para><paramref name="occupant"/> is the player transform the interior kit needs to make the
        /// door work — the same argument <see cref="StPetersVillage.Place"/> takes, passed straight
        /// through to <see cref="StPetersInteriors.Stand"/>. Null-safe: a null occupant places a solid
        /// shell, which is what the EditMode fixtures want.</para>
        /// </summary>
        public static int Place(ITidalTerrain terrain, Sprite greybox, Transform occupant = null)
        {
            var root = new GameObject(RootName);
            int placed = 0;

            // ---- the cottage ------------------------------------------------------------------------
            var placement = VillageBuildingCatalog.Find(CottageKey);
            if (!placement.IsValid)
            {
                Debug.LogWarning(
                    $"[StPetersGinnyPlot] '{CottageKey}' is not in the building contract, so Ginny has no " +
                    "cottage to move into. Re-bake the kit (Hidden Harbours ▸ Art ▸ Bake Village " +
                    "Buildings) rather than re-siting her round the gap.");
            }
            else
            {
                int facing = StPetersVillage.FacingToward(placement.Entry, CottagePos, Approach);
                Sprite sprite = VillageBuildingCatalog.LoadFacing(placement, facing);
                if (sprite == null)
                {
                    Debug.LogWarning(
                        $"[StPetersGinnyPlot] {CottageKey} facing {facing} has no sprite — its sheet is " +
                        $"missing or unsliced ({placement.SheetPath}). Skipping it rather than placing a " +
                        "blank.");
                }
                else
                {
                    // ⚠️ Checked against the AUTHORED TERRAIN, not against the constants — the #345 lesson
                    // that cost the village three wrong sites at once. Loud, because a house standing in
                    // the sea is an authoring bug to fix, not a building to quietly drop.
                    AssertDry(terrain, CottagePos, CottageKey);

                    var go = new GameObject(CottageKey);
                    go.transform.SetParent(root.transform, worldPositionStays: false);

                    // The pivot IS the ground centre, so the site position is the position. No offset.
                    go.transform.position = new Vector3(CottagePos.x, CottagePos.y, 0f);

                    // Configure owns the renderer, the scale, the pinned rotation, the YSortSprite and the
                    // sorting order — nothing here re-derives any of them.
                    SpriteRenderer shell = VillageBuildingCatalog.Configure(go, placement, sprite);

                    // THE INSIDE. The owner ruled her cottage enterable, and interiors are seamless and
                    // true-to-footprint — so this is the same call the village's pilot cottage makes, with
                    // the same occupant, and the #512 runtime re-bind applies to it unchanged.
                    //
                    // ⭐ AND A STOREY ABOVE IT, which the village's cottage does NOT get. The plan is asked
                    // for by SITE key, not by build key, and this is the reason why: the two cottages are
                    // the same build (see CottageKey), so a plan hung off 'sageCottage' would have put the
                    // player's bed — and a save point — inside a stranger's house on the green as well.
                    // Ginny's is the one with a lodger in it.
                    bool enterable = StPetersInteriors.Stand(go, shell, CottageKey, facing, occupant,
                                                             StPetersInteriors.GinnyCottagePlanKey);
                    if (!enterable)
                        Debug.LogWarning(
                            $"[StPetersGinnyPlot] {CottageKey} has no baked room, so Ginny's door does not " +
                            "open. The owner ruled this cottage enterable — re-bake the interior kit.");

                    // The cosy night read. ⚠ The greybox standee this replaces carried THREE night
                    // pieces; only two survive the move to a kit building. CottageDayNight is a SPRITE
                    // SWAP between Cottage.png and CottageNight.png, and a kit building draws from baked
                    // per-facing sheets that have no lit-window twin — so the lit PANES are lost and the
                    // upstream ask is a night sheet (or a lit-window axis) on the building kit. The GLOW
                    // and the SMOKE carry over unchanged, which is what actually makes the cottage read
                    // warm from across a clearing.
                    var glowGo = new GameObject("WindowGlow");
                    glowGo.transform.SetParent(go.transform, worldPositionStays: false);
                    glowGo.AddComponent<PreconfiguredLight>();   // defaults to LightPresets.Kind.WindowGlow

                    var chimneyGo = new GameObject("ChimneySmoke");
                    chimneyGo.transform.SetParent(go.transform, worldPositionStays: false);
                    chimneyGo.transform.localPosition = new Vector3(0.6f, 1.6f, 0f);   // roofline, flue side
                    chimneyGo.AddComponent<ChimneySmoke>();

                    placed++;
                }
            }

            // ---- the freezer ------------------------------------------------------------------------
            // AUNT GINNY'S FREEZER, moved out here with her on the owner's ruling. Same greybox marker
            // and the same proximity-interactable pattern it had in the village (StallReach resolves the
            // player itself — no builder wiring); only the coordinate changed. See FreezerPos for the
            // cold-chain consequence, which is real and was chosen deliberately.
            AssertDry(terrain, FreezerPos, "GinnyFreezer");

            var freezerGo = new GameObject("GinnyFreezer");
            freezerGo.transform.SetParent(root.transform, worldPositionStays: false);
            freezerGo.transform.position = new Vector3(FreezerPos.x, FreezerPos.y, 0f);
            var freezerSr = freezerGo.AddComponent<SpriteRenderer>();
            freezerSr.sprite = greybox;
            freezerSr.color = new Color(0.85f, 0.92f, 0.95f);   // chest-freezer white, reads icy
            freezerSr.sortingOrder = 3;   // pre-Play default only; the YSortSprite below OWNS the order
            freezerGo.transform.localScale = new Vector3(1.2f, 1.0f, 1f);
            freezerGo.AddComponent<GinnyFreezer>();
            // A thing you walk up to layers by world Y like everything else you can stand in front of
            // (ADR 0032 re-based the band; a fixed order would be buried by the meadow around it).
            freezerGo.AddComponent<YSortSprite>();

            // ---- the sheds --------------------------------------------------------------------------
            // ⭐ REAL BUILDINGS SINCE 2026-08-19 (the lifecycle pass). Each is a baked wharf-rig build in
            // its own decay state, stood up through the SAME VillageBuildingCatalog path as the cottage
            // above — so a derelict and a sound building are the same kind of object, sorted the same
            // way, pivoted the same way, and a future repair mechanic swaps a sprite rather than
            // swapping a class.
            //
            // ⚠ NOT enterable, and that is the kit's own limit rather than an omission: the pass draws
            // no interior at any phase (a frame is see-through by geometry, not by a cutaway), and a
            // ruin has no room to bake. StPetersInteriors is deliberately not called here.
            int shedsDrawn = 0;
            foreach (var shed in Sheds)
            {
                AssertDry(terrain, shed.Position, shed.Key);

                var shedGo = new GameObject(shed.Key);
                shedGo.transform.SetParent(root.transform, worldPositionStays: false);
                shedGo.transform.position = new Vector3(shed.Position.x, shed.Position.y, 0f);

                // An outbuilding faces the house it serves, and the facing is DERIVED from the bake's own
                // door anchors rather than given as an index — same rule as every village door, so a
                // re-bake at a different facing count re-derives instead of pointing a doorway at a tree.
                // Named for the shed, not just `placement`: the cottage above holds one of those in an
                // enclosing scope, and C# refuses the shadow rather than picking the wrong one.
                var shedPlacement = shed.Placement;
                Sprite art = shedPlacement.IsValid
                    ? VillageBuildingCatalog.LoadFacing(
                          shedPlacement,
                          StPetersVillage.FacingToward(shedPlacement.Entry, shed.Position, CottagePos))
                    : null;

                if (art != null)
                {
                    // The pivot IS the ground centre, so the site position is the position. No offset.
                    VillageBuildingCatalog.Configure(shedGo, shedPlacement, art);
                    shedsDrawn++;
                }
                else
                {
                    // The greybox fallback — an unbaked tree, or a sheet that is there but unsliced.
                    // Weathered board, and DARKER than the cottage: the one thing a flat marker can say
                    // about a derelict building is that nobody has painted it this century.
                    var sr = shedGo.AddComponent<SpriteRenderer>();
                    sr.sprite = greybox;
                    sr.color = new Color(0.42f, 0.38f, 0.32f);
                    sr.sortingOrder = 2;   // pre-Play default only; the YSortSprite below OWNS the order
                    shedGo.transform.localScale = new Vector3(shed.SizeMetres.x, shed.SizeMetres.y, 1f);

                    // A thing you walk around layers by world Y like the rest of the world (ADR 0032).
                    shedGo.AddComponent<YSortSprite>();

                    Debug.LogWarning(
                        $"[StPetersGinnyPlot] '{shed.Key}' has no baked art for build '{shed.BuildKey}' " +
                        "— standing a greybox marker instead. Re-bake (Hidden Harbours ▸ Art ▸ Bake " +
                        "Village Buildings) and re-slice.");
                }

                placed++;
            }

            // ---- the camper lot ---------------------------------------------------------------------
            // The player's own address, on the back of her land (2026-08-17). It is placed FROM here
            // because it stands on her ground and its site derives from her cottage — but everything
            // about it (which camper, what it costs, whether it is bought at all, how much room it
            // takes) belongs to StPetersCamperLot and its HomeDef asset, not to this file.
            if (StPetersCamperLot.Place(root.transform, terrain, greybox, occupant)) placed++;

            float need = RequiredClearingRadius();
            Debug.Log(
                $"[StPetersGinnyPlot] Ginny's plot stands at ({CottagePos.x:0.#},{CottagePos.y:0.#}) — " +
                $"{placed} building(s): the {CottageKey} cottage, {Sheds.Count} derelict shed(s) " +
                $"({shedsDrawn} of them in baked art, the rest greybox) and the " +
                $"camper on its lot at ({StPetersCamperLot.LotPos.x:0.#},{StPetersCamperLot.LotPos.y:0.#})" +
                $", her freezer at ({FreezerPos.x:0.#},{FreezerPos.y:0.#}), her garden on the south side. " +
                $"Furthest footprint reaches {need:0.0} m against a {ClearingRadius:0.0} m declared " +
                $"clearing, so the woods are held off her dooryard. " +
                $"{Vector2.Distance(CottagePos, StPetersBuilder.VillageHearthPos):0.0} m from the village " +
                $"hearth (past the {StPetersWoods.DooryardRadius:0.0} m dooryard band, so this is her " +
                "land and not the village's edge).");

            return placed;
        }

        /// <summary>Ground check shared by the cottage and the sheds — see the #345 note at the call
        /// site. Silent when there is no terrain to ask, which is what the pure EditMode fixtures do.</summary>
        static void AssertDry(ITidalTerrain terrain, Vector2 p, string what)
        {
            if (terrain == null) return;

            float ground = terrain.ElevationAt(p);
            float springHigh = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;
            if (ground <= springHigh)
                Debug.LogError(
                    $"[StPetersGinnyPlot] {what} is sited at {p} where the ground is {ground:0.00} m — at " +
                    $"or below spring high water ({springHigh:0.00} m). Ginny's land does not flood. Move " +
                    "the site; do not lower the tide.");
        }
    }
}
#endif
