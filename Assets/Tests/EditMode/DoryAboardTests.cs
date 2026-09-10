using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐⭐ <b>THE DORY, ABOARD — the shipped DATA behind the owner's 2026-09-09 ruling.</b>
    /// <i>"accept the deck feel. she can walk while standing in the dory in its narrow deck, sits at
    /// the helm with e and rows only from that position."</i>
    ///
    /// <para>Two of those three sentences are settled by numbers in <c>.asset</c> files rather than by
    /// anything that runs, and numbers in an asset are guarded by nothing unless a case reads them:
    /// <list type="bullet">
    ///   <item><b>the deck feel goes ON</b> — <c>Data/Config/GameConfig.asset</c> now carries
    ///   <c>FeelOnDeckEnabled: 1</c>. <c>GameConfigAssetCoverageTests</c> checks that every declared key
    ///   is PRESENT in that YAML and no stale one survives; it never compares a VALUE, so the dial the
    ///   owner turned could be turned back by a stray Unity save and nothing would say so;</item>
    ///   <item><b>her narrow deck</b> — the walk clamp is a 22-point HULL-LOCAL polygon on
    ///   <c>Data/Boats/Decks/DoryIso.asset</c>, and this PR did NOT change it. It is restated here as
    ///   the measurement it is, because the next lane's washboards will add an area beside it;</item>
    ///   <item><b>the seat she sits at</b> — her helm station has to be somewhere she can actually
    ///   STAND, or "press E at the helm" is a press she can never make.</item>
    /// </list></para>
    ///
    /// <para><b>What is NOT here.</b> The camera's behaviour with the dial on is
    /// <c>CameraFeelTests</c>'s (which measures the CODE default, still off — see the note at its deck
    /// case); the switcher's reading of the station is <c>HelmStationTests</c>'s; the sit and the oar
    /// gate are <c>DoryAboardPlayTests</c>'s, because both need a live boat. This file is the flat
    /// data: three assets, read as shipped.</para>
    /// </summary>
    public class DoryAboardTests
    {
        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";
        private const string DoryDeckPath = "Assets/_Project/Data/Boats/Decks/DoryIso.asset";

        /// <summary>The tuned world-axis offset both scenes still carry — #789's dory tiller. Restated
        /// as the NEGATIVE control: her seat must not be it.</summary>
        private static readonly Vector2 ShippedHelmOffset = new Vector2(0f, -1.3f);

        /// <summary>The dory artwork's bake, as <c>DoryIsoHullMesh.asset</c> carries it.</summary>
        private const float BakeElevationDeg = 40f;

        // ---- the shipped clamp, MEASURED off DoryIso.asset and restated (a def read back against
        // ---- itself would agree with any shape at all) -----------------------------------------------
        private const int FloorOutlinePoints = 22;
        private const float FloorHalfBeam = 0.225f;      // amidships-ish, y = −0.72 … −1.08
        private const float FloorHalfLength = 1.8f;
        private static readonly Vector3 FloorHeightPlane = new Vector3(0f, 0.020075757f, 0.10363636f);

        /// <summary>Her AFTER THWART in hull metres — <c>STATIONS[id=helm]</c>, evaluated from her rig.</summary>
        private static readonly Vector3 DoryThwart = new Vector3(0f, -0.72f, 0.3056f);

        private const float Tol = 1e-4f;

        /// <summary>
        /// ⭐ <b>"accept the deck feel."</b> The dial the owner turned is ON in the asset the game
        /// actually loads.
        ///
        /// <para>⚠ <b>Two switches in series, so both are read.</b> <c>CameraFollow</c> asks
        /// <c>FeelEnabled</c> first and only then <c>FeelOnDeckEnabled</c>; a master switch left off
        /// would make the deck flag dead data, and a case that read only the deck flag could not tell
        /// the difference. Neither is a bar the code supplies to itself: both come off the YAML.</para>
        /// </summary>
        [Test]
        public void TheShippedConfig_TurnsTheDeckFeelOn()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.IsNotNull(config, $"the shipped config {ConfigAssetPath} must exist — every scene wires it");

            Assert.IsFalse(JuiceSettings.Default.FeelOnDeckEnabled,
                "harness: the CODE default must stay OFF. The owner's flip lives in the ASSET, and if " +
                "the default ever flips too this case would pass on a config nobody ships — it would " +
                "stop being a guard on his tuning and become a mirror of the constructor.");

            Assert.IsTrue(config.Juice.FeelOnDeckEnabled,
                "the deck feel has been turned back OFF in Data/Config/GameConfig.asset. The owner " +
                "accepted it on 2026-09-09 (\"accept the deck feel\") — and a Unity save can rewrite " +
                "this file silently, which is exactly why the flip is asserted and not merely made.");

            Assert.IsTrue(config.Juice.FeelEnabled,
                "…and the MASTER feel switch is off, which makes the line above dead data: CameraFollow " +
                "gates on FeelEnabled first, so the deck would be as quiet as it was before the flip");
        }

        /// <summary>
        /// ⭐⭐ <b>"sits at the helm with e" — so the helm has to be somewhere she can STAND.</b>
        /// Her station is clamped by the production walk clamp and comes back UNMOVED.
        ///
        /// <para><b>Through the clamp, not around it.</b> <see cref="BoatDeckDef.ClampToWalkable"/> is
        /// the method the walk calls every tick; asking it is the only way to know the seat survives
        /// the same test her feet get. Re-implementing the point-in-polygon here would prove that two
        /// transcriptions agree, which is not the question.</para>
        ///
        /// <para><b>The negative control matters.</b> Her floor is 0.45 m wide — most of the hull frame
        /// is OUTSIDE it, so "the seat is inside" is only worth asserting alongside a point that is not.
        /// The tuned tiller she used to be steered from is that point: 2.02 m abaft amidships, 0.22 m
        /// astern of her transom end, over open water.</para>
        /// </summary>
        [Test]
        public void HerHelmStation_IsAPlaceSheCanStand()
        {
            BoatDeckDef dory = Load(DoryDeckPath);
            Assert.IsTrue(dory.HasHelmStation,
                "premise: she publishes a helm station (STATIONS[id=helm]). Without it there is no seat " +
                "to stand at and this case is about nothing.");

            var seat = new Vector2(dory.HelmStationLocalMeters.x, dory.HelmStationLocalMeters.y);
            int hint = -1;
            Vector2 clamped = dory.ClampToWalkable(seat, ref hint, out float soleHeight);

            Assert.AreEqual(seat.x, clamped.x, Tol,
                $"the walk clamp drags her helm {seat} inboard to {clamped} — she cannot press E at a " +
                "seat she is never allowed to stand at");
            Assert.AreEqual(seat.y, clamped.y, Tol,
                $"the walk clamp drags her helm {seat} along the keel to {clamped}");
            Assert.AreEqual(0, hint,
                "…and it is her FLOOR she is standing on, not some other area that happens to contain it");

            // The negative control: the clamp really does reject a point off her floor.
            Vector2 tiller = DeckAreaMath.WorldToDeck(ShippedHelmOffset, 0f, 0f, BakeElevationDeg);
            int tillerHint = -1;
            Vector2 tillerClamped = dory.ClampToWalkable(tiller, ref tillerHint, out _);
            Assert.Greater(Vector2.Distance(tiller, tillerClamped), 0.1f,
                $"harness: the tuned tiller {tiller} is supposed to be OFF her floor and to be dragged " +
                $"onto it (it landed at {tillerClamped}). If the clamp accepts everything, the four " +
                "assertions above are vacuous.");

            // A THWART, not the bottom boards: she sits on it, so it stands clear of the sole under it.
            float seatAboveSole = dory.HelmStationLocalMeters.z - soleHeight;
            Assert.Greater(seatAboveSole, 0.15f,
                $"her helm sits {seatAboveSole:0.000} m above the sole beneath it — that is a mark on the " +
                "bottom boards, not a thwart to sit on. (The rig publishes the seat TOP.)");
            Assert.Less(seatAboveSole, 0.45f,
                $"her helm sits {seatAboveSole:0.000} m above her sole — higher than any thwart in a 4.5 m " +
                "open boat, so the station's z is probably in the wrong datum");

            Assert.AreEqual(DoryThwart.z, dory.HelmStationLocalMeters.z, Tol,
                "…and it is the height her rig published, not one that drifted at import");
        }

        /// <summary>
        /// ⭐ <b>"her narrow deck" — the walk clamp as this PR found it, and did not touch.</b> One
        /// polygon, 0.45 m across and 3.6 m long, tapering to a hand's width at the stem.
        ///
        /// <para><b>Why restate a shape nobody changed.</b> Two reasons. #806: the arrival deck was once
        /// a WORLD-AXIS SQUARE 6.95× her real deck, and the lesson banked from it is that a standable
        /// test and a walk clamp must be ONE shape — so the coarse box (<c>WalkCenter</c> /
        /// <c>WalkHalfExtents</c>, which the boarding path and <c>TryDeckBox</c> read) is asserted to
        /// AGREE with the polygon it summarises, not merely to exist. And the next lane adds the
        /// washboards as a second area beside this one; when it does, this case is the record of what
        /// the deck was before, so a re-import that quietly reshapes the floor is not mistaken for the
        /// lane's own work.</para>
        ///
        /// <para><b>It is a LANE, not a box.</b> Probed at the polygon rather than at the bounds: a
        /// point 0.20 m abeam is aboard amidships and over the water near the stem. A rectangle cannot
        /// tell those apart, which is the whole of what "she walks a narrow deck" means.</para>
        /// </summary>
        [Test]
        public void HerWalkableDeck_IsTheNarrowLaneTheOwnerRuled()
        {
            BoatDeckDef dory = Load(DoryDeckPath);

            Assert.IsTrue(dory.HasWalkableDeck(), "she must have a floor to walk at all");
            Assert.IsFalse(dory.HasWashboards(),
                "⚠ she has grown a WASHBOARD area. That is the next PR's work (the owner ruled them a " +
                "single-width walkable lane round the hull with three exits) — but it changes what " +
                "ClampToWalkable accepts, so it does not arrive by accident in this one.");

            Assert.AreEqual(1, dory.Areas.Length,
                "an open rowed boat has ONE walkable area — her bottom boards");
            DeckArea floor = dory.Areas[0];
            Assert.AreEqual("floor", floor.Id, "the sidecar's own area id");
            Assert.AreEqual(DeckAreaKind.Deck, floor.Kind, "a DECK area: walked freely, not climbed onto");
            Assert.AreEqual(FloorOutlinePoints, floor.Outline.Length,
                "the imported outline is 22 points — 11 stations a side. A different count is a re-import, " +
                "and every number below was measured against this one.");

            // The measured extents, hull-local: 0.45 m across, 3.6 m along.
            Assert.AreEqual(-FloorHalfBeam, floor.Bounds.x, Tol, "port edge");
            Assert.AreEqual(-FloorHalfLength, floor.Bounds.y, Tol, "the transom end of the floor");
            Assert.AreEqual(FloorHalfBeam, floor.Bounds.z, Tol, "starboard edge");
            Assert.AreEqual(FloorHalfLength, floor.Bounds.w, Tol, "the stem end of the floor");

            // #806: the coarse box and the polygon are ONE shape.
            Assert.AreEqual(0f, dory.WalkCenter.x, Tol, "her floor is centred on the keel");
            Assert.AreEqual(0f, dory.WalkCenter.y, Tol, "…and on amidships");
            Assert.AreEqual(FloorHalfBeam, dory.WalkHalfExtents.x, Tol,
                "the coarse box is WIDER than the polygon it summarises — TryDeckBox callers would place " +
                "her outside the floor her own feet are clamped to (#806)");
            Assert.AreEqual(FloorHalfLength, dory.WalkHalfExtents.y, Tol,
                "the coarse box is LONGER than the polygon it summarises (#806)");

            Assert.Greater(FloorHalfLength / FloorHalfBeam, 4f,
                "the owner ruled a NARROW deck; a floor less than four times longer than it is wide is " +
                "not a lane you walk, it is a raft");

            // She TAPERS. Probed at the polygon, which is the only thing that can show it.
            Assert.IsTrue(DeckAreaMath.Contains(floor.Outline, floor.Bounds, new Vector2(0.2f, 0f)),
                "0.20 m abeam amidships is aboard — that is where her floor is at its widest");
            Assert.IsFalse(DeckAreaMath.Contains(floor.Outline, floor.Bounds, new Vector2(0.2f, 1.7f)),
                "0.20 m abeam at the STEM is over the water: her floor closes to 0.14 m across up there. " +
                "If this reads inside, the clamp has become the bounding box and she can walk out over " +
                "her own bow — the shape of #806 all over again.");

            // The height read the walk lifts her by: a plane, rising toward the bow.
            Assert.AreEqual(FloorHeightPlane.x, floor.HeightPlane.x, Tol,
                "her sole has no athwartships tilt — she is symmetric about the keel");
            Assert.AreEqual(FloorHeightPlane.y, floor.HeightPlane.y, Tol,
                "the fitted rise toward the bow (m per m of hull)");
            Assert.AreEqual(FloorHeightPlane.z, floor.HeightPlane.z, Tol,
                "…and the sole's height above the keel at amidships");
            Assert.Less(floor.HeightResidualMax, 0.1f,
                "the importer's own error bar on that plane. Above about a tenth of a metre her sole is " +
                "not a plane at all and the walk is lifting her to a height she is not standing at.");
        }

        private static BoatDeckDef Load(string path)
        {
            var def = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatDeckDef>(path);
            Assert.IsNotNull(def, $"the authored deck {path} must exist");
            return def;
        }
    }
}
