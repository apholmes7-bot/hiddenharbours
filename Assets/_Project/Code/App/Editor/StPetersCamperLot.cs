#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;                 // YSortSprite
using HiddenHarbours.Art.Editor;          // InteriorCatalog
using HiddenHarbours.Core;                // ITidalTerrain, HomeDeeds
using HiddenHarbours.Economy;             // HomeDef, HomeDoor

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE CAMPER LOT</b> — a second-hand travel trailer standing on the back of Aunt Ginny's land,
    /// and the first place in this game the player can call his own. The owner directed (2026-08-16) that
    /// a camper gets its own lot near her plot, to be either the starter home or something you buy.
    ///
    /// <para><b>⚠️⚠️ IT IS A SHELL. YOU CAN BUY IT AND YOU CANNOT GO IN.</b> The camper kit's README says
    /// so in its own words — <i>"No interior. This is the shell."</i> — and <c>camperInteriorRig.js</c>,
    /// which it names, is not in the drop. So this PR ships "built but not enterable" as a FIRST-CLASS
    /// STATE rather than as a missing feature: the door is present, it is interactable, and when you try
    /// it while holding the deed it tells you the truth (<see cref="HomeDoor.InteriorStood"/>). The
    /// seamless-interiors ruling (2026-07-30) still stands for the day the interior rig lands; wiring it
    /// is that PR's job and <see cref="InteriorKey"/> is the tripwire that will notice it arrive.</para>
    ///
    /// <para><b>The site: (84, 42) — due north of her cottage, 14 m out, and every part of that is
    /// measured.</b> The approach side was tried first and does not work: the player walks up from the
    /// west-south-west, and her dooryard, her freezer and the lean-to already stand on that ground, so a
    /// camper that clears all three lands 72–74 m from the village hearth — <i>inside</i>
    /// <see cref="StPetersWoods.DooryardRadius"/>, the very band her plot was sited past to be her land
    /// rather than the village's weedy edge. North is the corridor that is actually free: between the
    /// woodshed and the lean-to, on the back of the plot, where you would in fact park a trailer.
    /// <b>84.4 m from the hearth even at its drawbar</b>, so it is on her land by the same test she
    /// is.</para>
    ///
    /// <para><b>⚠️ THE CLIPPER RESERVES 5.21 m — THE SAME GROUND AS HER COTTAGE (5.21 m).</b> That is the
    /// finding that decided the layout: her plot was sized for a cottage and three sheds, and this adds a
    /// second dwelling as big as the first. Only one slot inside the old 18 m clearing takes it, threaded
    /// between two sheds with 2.1 m either side, so the clearing GROWS —
    /// <see cref="StPetersGinnyPlot.ClearingRadius"/> 18 → 20 m, which is 239 m² more ground the woods
    /// are held off. Everything downstream (the tree planter, the erratics, the grass) follows that one
    /// number, and <c>StPetersVillageTests</c> re-derives what is needed rather than trusting it.</para>
    ///
    /// <para><b>Greybox-tolerant by construction.</b> Both variants are in
    /// <c>DwellingRigFleet.NotBaked</c> and the sheets are a separate lane's PR. Nothing here waits on
    /// it: <see cref="TryLoadCamperSprite"/> is the ONE lookup, it falls through to the same tinted
    /// marker the sheds and the freezer already use, and the day the bake lands the camper draws itself
    /// with no line changing here.</para>
    ///
    /// <para><b>Everything derives from two points and one asset.</b> The site is
    /// <see cref="StPetersGinnyPlot.CottagePos"/> plus <see cref="LotOffset"/>; the size, the spacing,
    /// the clearing and the doorstep all come from the chosen variant's own gameplay sidecar
    /// (<see cref="CamperSidecar"/>); the price, the acquisition mode and the variant come from one
    /// <see cref="HomeDef"/> asset the owner edits without touching code.</para>
    /// </summary>
    public static class StPetersCamperLot
    {
        /// <summary>The GameObject the lot hangs off, under the plot root, so a rebuild finds and
        /// replaces it.</summary>
        public const string RootName = "CamperLot";

        /// <summary>
        /// The one asset that carries the owner's two open decisions — starter-home vs purchasable, and
        /// Bantam vs Clipper. Loaded, never created: an absent Def is a loud warning and the shipped
        /// defaults below, not a silently authored file (the LoadOrCreate trap — an initialiser that runs
        /// only when the asset is missing reports success and then never updates the real one).
        /// </summary>
        public const string HomeDefPath = "Assets/_Project/Data/Homes/GinnyLotCamper.asset";

        /// <summary>The stable home id the deed is recorded under when the Def is missing. The Def's own
        /// <see cref="HomeDef.Id"/> is authoritative whenever it loads.</summary>
        public const string FallbackHomeId = "home.ginny_lot_camper";

        // =====================================================================================
        //  THE SITE
        // =====================================================================================

        /// <summary>
        /// Where the lot sits relative to <see cref="StPetersGinnyPlot.CottagePos"/>: <b>due north,
        /// 14 m out</b>. See the class remarks for why north and not the approach side, and why 14 —
        /// the shortest distance at which the camper clears her cottage, the woodshed and the lean-to by
        /// the plot's own tightest existing walking gap (2.09 m, the woodshed's).
        ///
        /// <para>Deliberately a round offset on the same axis as
        /// <see cref="StPetersGinnyPlot.GardenOffset"/> (0, −7): the garden is seven metres south of her
        /// door and the camper is fourteen north of it, which is a layout a person can hold in their
        /// head.</para>
        /// </summary>
        public static readonly Vector2 LotOffset = new Vector2(0f, 14f);

        /// <summary>Where the camper stands. ⚠ This is its rig ORIGIN, which the sidecar defines as the
        /// ground centre of the BODY — the drawbar hangs past it. See
        /// <see cref="CamperSidecar.Dimensions.NoseMetres"/>.</summary>
        public static Vector2 LotPos => StPetersGinnyPlot.CottagePos + LotOffset;

        /// <summary>
        /// Where you walk up from: her cottage. The island's rule is that a door turns toward the ground
        /// people arrive on, and on this plot everything arrives past her house — you come up from the
        /// village, pass her door, and the camper is on the back of the land.
        /// </summary>
        public static Vector2 Approach => StPetersGinnyPlot.CottagePos;

        /// <summary>
        /// <b>The mark you stand on to try the door</b>, out from the camper toward
        /// <see cref="Approach"/>. Derived from the camper's own step rather than from the house
        /// convention: <see cref="StPetersInhabitants.DoorstepMetres"/> is 7.5 m, which is right for a
        /// cottage you can see the whole of and absurd for a trailer whose fold-down step reaches 1.44 m
        /// — it would put you standing in the woods.
        /// </summary>
        public static Vector2 Doorstep
        {
            get
            {
                CamperSidecar.Dimensions d = Dimensions;
                Vector2 v = Approach - LotPos;
                if (v.sqrMagnitude < 1e-6f) return LotPos;
                return LotPos + v.normalized * DoorstepMetres(d);
            }
        }

        /// <summary>How far out the doorstep stands: the outermost tread plus one stride, so you are off
        /// the step and not on it. Falls back to one stride alone on an unreadable sidecar.</summary>
        public static float DoorstepMetres(in CamperSidecar.Dimensions d) =>
            (d.IsValid ? d.DoorReachMetres : 0f) + StrideMetres;

        /// <summary>One walking stride (m) — the gap between the bottom tread and where you stand.</summary>
        public const float StrideMetres = 1f;

        /// <summary>
        /// <b>Which way the drawbar points</b>, as a unit vector. DERIVED, not authored: the camper's
        /// long axis runs perpendicular to <see cref="Approach"/> (so the curb side, which is where the
        /// door and the step are, faces the ground you walk up from), and of the two perpendiculars it
        /// takes the one whose nose has more room — measured against everything else standing on the
        /// plot. Today that is east, because the woodshed leaves more room than the lean-to; if a shed
        /// moves, the parking re-derives instead of poking a drawbar through a wall.
        /// </summary>
        public static Vector2 NoseDirection
        {
            get
            {
                Vector2 toApproach = Approach - LotPos;
                if (toApproach.sqrMagnitude < 1e-6f) return Vector2.right;
                Vector2 a = new Vector2(-toApproach.y, toApproach.x).normalized;   // one perpendicular
                Vector2 b = -a;                                                    // and the other

                float nose = Dimensions.IsValid ? Dimensions.NoseMetres : 0f;
                return NoseRoom(a, nose) >= NoseRoom(b, nose) ? a : b;
            }
        }

        /// <summary>How far the nearest thing on the plot is from where the drawbar tip would end up if
        /// the camper were parked pointing <paramref name="dir"/>. Bigger is better.</summary>
        static float NoseRoom(Vector2 dir, float noseMetres)
        {
            Vector2 tip = LotPos + dir * noseMetres;
            float worst = Vector2.Distance(tip, StPetersGinnyPlot.CottagePos);
            foreach (var shed in StPetersGinnyPlot.Sheds)
            {
                float gap = Vector2.Distance(tip, shed.Position) - shed.FootprintRadiusMetres;
                if (gap < worst) worst = gap;
            }
            return worst;
        }

        // =====================================================================================
        //  WHAT IS PARKED THERE
        // =====================================================================================

        /// <summary>
        /// The home Def, or null when the asset is missing. Loaded fresh each call rather than cached:
        /// this is editor code that runs once per build, and a cache would go stale the moment the owner
        /// edits the asset — which is the ONE thing this asset exists to let him do.
        /// </summary>
        public static HomeDef Home => AssetDatabase.LoadAssetAtPath<HomeDef>(HomeDefPath);

        /// <summary>Which camper: the Def's <see cref="HomeDef.Variant"/>, or the Clipper when the Def
        /// is missing or names a variant the kit does not loft.</summary>
        public static string Variant
        {
            get
            {
                HomeDef home = Home;
                string v = home != null ? home.Variant : null;
                if (string.IsNullOrEmpty(v)) return CamperSidecar.Clipper;
                return CamperSidecar.Read(v).IsValid ? v : CamperSidecar.Clipper;
            }
        }

        /// <summary>The chosen camper's measured shape, off art's own sidecar.</summary>
        public static CamperSidecar.Dimensions Dimensions => CamperSidecar.Read(Variant);

        /// <summary>The stable home id the deed is recorded under.</summary>
        public static string HomeId
        {
            get
            {
                HomeDef home = Home;
                return home != null && !string.IsNullOrEmpty(home.Id) ? home.Id : FallbackHomeId;
            }
        }

        /// <summary>
        /// The ground the camper reserves, from <see cref="LotPos"/> — the same any-facing circle the
        /// sheds and the village houses reserve, computed from the sidecar's furthest corner. Zero when
        /// the sidecar cannot be read, which a test reports rather than passing vacuously.
        /// </summary>
        public static float FootprintRadiusMetres => Dimensions.FootprintRadiusMetres;

        /// <summary>How far the lot's furthest ground reaches from
        /// <see cref="StPetersGinnyPlot.CottagePos"/> — what the plot's clearing has to contain.</summary>
        public static float ClearingReachMetres =>
            Vector2.Distance(LotPos, StPetersGinnyPlot.CottagePos) + FootprintRadiusMetres;

        /// <summary>
        /// The key a baked camper INTERIOR would be filed under, if one existed.
        ///
        /// <para><b>⚠️ It is a tripwire, not a feature.</b> Nothing has ever been baked under it — the
        /// interior rig is not in the repo. <see cref="Place"/> asks the interior contract for it anyway,
        /// so the day someone bakes a camper room the build LOG says so out loud instead of the room
        /// sitting there unnoticed. Standing it is a follow-up PR's job (a camper room is not a house
        /// room: no gable, no porch, and its sole is a tumblehome polygon, so
        /// <c>StPetersInteriors.Stand</c> does not apply unchanged).</para>
        /// </summary>
        public static string InteriorKey => "camper." + Variant;

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Stand the camper on its lot under <paramref name="parent"/> (the plot root). Returns true if
        /// it went up.
        ///
        /// <para><paramref name="greybox"/> is the fallback sprite — the same one the sheds and the
        /// freezer are drawn with. <paramref name="occupant"/> is accepted for symmetry with the
        /// cottage's interior wiring and is unused while the camper is a shell; it is named here so the
        /// interior PR has an argument to fill rather than a signature to change.</para>
        /// </summary>
        public static bool Place(Transform parent, ITidalTerrain terrain, Sprite greybox,
                                 Transform occupant = null)
        {
            CamperSidecar.Dimensions d = Dimensions;
            if (!d.IsValid)
            {
                Debug.LogWarning(
                    $"[StPetersCamperLot] No readable gameplay sidecar for the '{Variant}' camper " +
                    $"({CamperSidecar.PathFor(Variant) ?? "no such variant"}), so there is nothing to " +
                    "park and no footprint to reserve. Skipping the lot rather than placing a marker of " +
                    "a guessed size.");
                return false;
            }

            HomeDef home = Home;
            if (home == null)
                Debug.LogWarning(
                    $"[StPetersCamperLot] No HomeDef at {HomeDefPath}, so the camper is placed on the " +
                    $"shipped defaults (purchasable, '{CamperSidecar.Clipper}'). The owner's " +
                    "starter-home and Bantam/Clipper decisions BOTH live on that asset — restore it " +
                    "rather than re-deciding them in code.");

            // ⚠️ Checked against the AUTHORED TERRAIN, not against the constants — the #345 lesson. A
            // camper standing in the sea is an authoring bug to fix, not a home to quietly drop.
            AssertDry(terrain, LotPos, "Camper");

            var root = new GameObject(RootName);
            root.transform.SetParent(parent, worldPositionStays: false);
            root.transform.position = new Vector3(LotPos.x, LotPos.y, 0f);

            var go = new GameObject($"Camper ({d.Label})");
            go.transform.SetParent(root.transform, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;

            var sr = go.AddComponent<SpriteRenderer>();
            Sprite baked = TryLoadCamperSprite(Variant, DoorFacing);
            if (baked != null)
            {
                // The bake lane's sheets have landed. Draw them at honest metric size — the rig bakes the
                // camera's foreshortening in at 32 px = 1 m, so scaling to the footprint would break the
                // pixel grid (the building kit's first guardrail).
                sr.sprite = baked;
                go.transform.localScale = Vector3.one;
                Debug.Log($"[StPetersCamperLot] Baked camper art found ('{baked.name}') — drawing the " +
                          $"{d.Label} at facing {DoorFacing}. ⚠ That facing index is DECLARED, not read " +
                          "from a contract (the kit ships none): confirm on screen that the door faces " +
                          "the cottage, and correct StPetersCamperLot.DoorFacing if it does not.");
            }
            else
            {
                // The tinted-marker convention the sheds and the freezer already use. Polished aluminium:
                // lighter and cooler than the sheds' weathered board, warmer than the freezer's white, so
                // three greybox things on one plot still read as three different objects.
                sr.sprite = greybox;
                sr.color = new Color(0.80f, 0.82f, 0.86f);
                go.transform.localScale = new Vector3(d.HalfSpanMetres * 2f, d.OverallLengthMetres, 1f);

                // ⚠ The marker is nudged onto the ground the camper ACTUALLY covers. The rig origin is
                // the body centre and the drawbar hangs past it, so a rectangle centred on the position
                // would sit half a tow hitch short at the nose and half a hitch long at the tail.
                Vector2 centre = NoseDirection * ((d.NoseMetres - d.TailMetres) * 0.5f);
                go.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
                go.transform.localRotation =
                    Quaternion.FromToRotation(Vector3.up, new Vector3(NoseDirection.x, NoseDirection.y, 0f));
            }

            sr.sortingOrder = 4;   // pre-Play default only; the YSortSprite below OWNS the order
            // A thing you walk up to and around layers by world Y like everything else (ADR 0032 re-based
            // the band; a fixed order would be buried by the meadow around it).
            go.AddComponent<YSortSprite>();

            // ---- the door --------------------------------------------------------------------------
            // The tripwire: nothing has ever been baked under this key, and if that changes the build
            // says so rather than the room going unnoticed.
            bool interiorBaked = InteriorCatalog.FindRoom(InteriorKey).IsValid;
            if (interiorBaked)
                Debug.LogWarning(
                    $"[StPetersCamperLot] A room IS baked under '{InteriorKey}' — the camper interior " +
                    "rig has landed since this lot was written. The door is still refusing entry, " +
                    "because standing a camper room is not the same job as standing a house room (no " +
                    "gable, no porch, a tumblehome sole). Wire it in its own PR and set the door's " +
                    "interiorStood.");

            var door = go.AddComponent<HomeDoor>();
            door.Configure(home, interiorStood: false);

            // A starter home is yours from the first morning: the deed is granted here, not at a door.
            // ⚠ This does NOT move the opening spawn — see HomeDef.Mode.StarterHome.
            if (home != null && home.Acquisition == HomeDef.Mode.StarterHome)
            {
                bool granted = HomeDeeds.Grant(home.Id);
                Debug.Log($"[StPetersCamperLot] '{home.Id}' is a STARTER HOME — deed " +
                          $"{(granted ? "granted" : "already held")}. The player wakes up owning it; " +
                          "where he wakes up is unchanged.");
            }

            Debug.Log(
                $"[StPetersCamperLot] The {d.Label} stands at ({LotPos.x:0.#},{LotPos.y:0.#}) — " +
                $"{d.OverallLengthMetres:0.00} m overall on a {d.HalfSpanMetres * 2f:0.00} m span, " +
                $"reserving {FootprintRadiusMetres:0.00} m of ground at any heading, drawbar pointing " +
                $"({NoseDirection.x:0.#},{NoseDirection.y:0.#}). " +
                $"{Vector2.Distance(LotPos, StPetersGinnyPlot.CottagePos):0.0} m from Ginny's door, " +
                $"reaching {ClearingReachMetres:0.00} m into her {StPetersGinnyPlot.ClearingRadius:0.0} m " +
                $"clearing. {(home != null ? home.Acquisition.ToString() : "Purchasable (no Def)")}" +
                $"{(home != null && home.Acquisition == HomeDef.Mode.Purchasable ? $", ₲{home.Price}" : "")}. " +
                $"Interior: NONE — the kit ships a shell, so the door refuses entry and says why. " +
                $"Art: {(baked != null ? "baked sheet" : "greybox marker (sheets not baked yet)")}.");

            return true;
        }

        /// <summary>
        /// <b>⭐ THE ONE LOOKUP.</b> The camper's baked sprite for a facing, or null if the bake lane's
        /// PR has not landed — in which case the caller draws the tinted marker.
        ///
        /// <para><b>Why it searches rather than naming a path.</b> Both variants sit in
        /// <c>DwellingRigFleet.NotBaked</c> and the sheets are a separate lane's work; where that lane
        /// files them and what it calls the contract are its decisions, not this file's, and hard-coding
        /// either would couple two PRs that must stay independent. So this asks the asset database for a
        /// camper texture under the art tree and matches the variant and facing out of the sprite names,
        /// which is the naming every kit in this repo already bakes to (<c>stem_d0 … stem_d7</c>).</para>
        ///
        /// <para><b>It draws the right art or none — never the wrong art.</b> If the requested facing is
        /// not on the sheet it returns null rather than falling back to whatever sprite happens to be
        /// first: a camper drawn at an arbitrary heading looks finished and is wrong, which is worse to
        /// find than a grey box that is obviously unfinished.</para>
        ///
        /// <para>⚠ Sheets import spriteMode MULTIPLE, so <c>LoadAssetAtPath&lt;Sprite&gt;</c> returns null
        /// for them — the sub-sprites come out of <see cref="AssetDatabase.LoadAllAssetsAtPath"/>, matched
        /// BY NAME because Unity does not promise that array's order.</para>
        /// </summary>
        public static Sprite TryLoadCamperSprite(string variant, int facing)
        {
            if (string.IsNullOrEmpty(variant) || facing < 0) return null;

            string facingSuffix = "_d" + facing;
            foreach (string guid in AssetDatabase.FindAssets("camper t:Texture2D",
                                                             new[] { "Assets/_Project/Art" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is Sprite s &&
                        s.name.IndexOf(variant, System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                        s.name.EndsWith(facingSuffix, System.StringComparison.Ordinal))
                        return s;
            }
            return null;
        }

        /// <summary>
        /// Which of the rig's eight facings shows the camper parked the way this lot parks it.
        ///
        /// <para><b>⚠ DECLARED, NOT DERIVED — the one number here a contract should own.</b> The building
        /// kit reads its facing from the bake's own door anchors (<c>BuildingFacing.FacingToward</c>); the
        /// camper kit ships no contract, so there is nothing for this file to read. The reasoning: the
        /// rig's order is <c>N NE E SE S SW W NW</c> (measured in the repo's V8 by
        /// <c>CamperIsoKitProbeTests</c>), the lot parks the camper broadside to the approach so the curb
        /// side faces the cottage, and <see cref="NoseDirection"/> comes out east — index 2.</para>
        ///
        /// <para><b>✔ CROSS-CHECKED against the bake lane's measured anchors, on two independent
        /// features.</b> Its <c>Camper.json</c> gives, for <c>camper_clipper_rest</c> facing 2, a hitch at
        /// <c>(+158.1, −10.8)</c> px from the pivot — level and to the right, i.e. pointing EAST — and the
        /// fold-down step at <c>(+2.6, +23.7)</c>, down-screen toward the camera, i.e. SOUTH. This lot
        /// parks the drawbar east and wants the step facing Ginny's cottage, which is due south of it.
        /// Both agree. <b>That check was made against an OPEN branch</b> (the bake PR), so if its facing
        /// convention moves before it merges, re-check — but this is no longer a guess.</para>
        ///
        /// <para>Belt and braces either way: this repo has been wrong about a facing order before (the
        /// iso art baked counter-clockwise; the azimuth probe's taper heuristic was backwards on eighteen
        /// hulls), so <see cref="TryLoadCamperSprite"/> draws NOTHING rather than the wrong cell if this
        /// is ever wrong — the correction is one integer.</para>
        /// </summary>
        public const int DoorFacing = 2;

        /// <summary>Ground check shared with the plot — see <c>StPetersGinnyPlot.AssertDry</c>'s note.
        /// Silent when there is no terrain to ask, which is what the pure EditMode fixtures do.</summary>
        static void AssertDry(ITidalTerrain terrain, Vector2 p, string what)
        {
            if (terrain == null) return;

            float ground = terrain.ElevationAt(p);
            float springHigh = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;
            if (ground <= springHigh)
                Debug.LogError(
                    $"[StPetersCamperLot] {what} is sited at {p} where the ground is {ground:0.00} m — " +
                    $"at or below spring high water ({springHigh:0.00} m). You cannot live below the " +
                    "tide. Move the lot; do not lower the tide.");
        }
    }
}
#endif
