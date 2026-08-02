using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Content validation for the M1 slice's last rung: the used outboard on the dory</b> (§7.7 /
    /// D8). Like <see cref="PilotableFleetContentTests"/> this reads the REAL committed assets off
    /// disk, because the failure it catches is a STALE ASSET rather than a broken algorithm — a
    /// builder that was never re-run, a fitting that was re-baked and lost its second wearer, a
    /// mount left at the default it was born with.
    ///
    /// <para><b>The mount is the one to read carefully.</b>
    /// <c>BoatVisualDef.MotorMountLocalMeters</c> initialises to the SKIFFS' <c>(0, −3.53, 0.72)</c>,
    /// so twelve of fourteen visuals carry that triple whether or not they hang an engine there. It
    /// is therefore not evidence of anything, and the dory carried it for a year while having no
    /// engine at all. Hers is now measured off her own rig, and this test is what stops it drifting
    /// back.</para>
    /// </summary>
    public class DoryOutboardContentTests
    {
        const string DataBoats = "Assets/_Project/Data/Boats";

        /// <summary>doryIsoRig's own transom MOTOR BOARD: <c>MOUNT = (0, TR.y − 2·BOARD.t,
        /// TR.zTop + BOARD.rise)</c> at TR.y = −L/2 = −2.25, BOARD.t = 0.045, TR.zTop = T[0][3] +
        /// T[0][2] = 0.70, BOARD.rise = 0.10 → (0, −2.34, 0.80); her <c>motorMount()</c> projects
        /// <c>MOUNT.y − 0.01</c>, which is the point this field states.</summary>
        static readonly Vector3 DoryMotorBoard = new Vector3(0f, -2.35f, 0.80f);

        /// <summary>The field's INITIALISER — the skiffs' clamp. Not an authored fact about anyone
        /// else, which is the trap this file names.</summary>
        static readonly Vector3 SkiffDefaultMount = new Vector3(0f, -3.53f, 0.72f);

        /// <summary>The loan: how far the punt's outboard moves to hang on the dory's transom.
        /// y = doryMotorRig YA (−2.38) − puntIsoRig YA (−2.66); z hangs it by the part that must be
        /// in the water — the punt's prop is 0.475 m under her clamp, the dory's own kicker puts
        /// hers at z 0.140, so the borrowed swivel sits at 0.615.</summary>
        static readonly Vector3 BorrowedFitment = new Vector3(0f, 0.28f, 0.055f);

        const string BorrowedFittingId = "hullprop.punt_motor_basic";

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(a, $"{path} is missing. Run the cove builder (it authors the hull assets) " +
                                "and Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Build Boat Visual Defs.");
            return a;
        }

        static BoatVisualDef DoryVisual() =>
            Load<BoatVisualDef>($"{DataBoats}/Visuals/DoryIso.asset");

        // ---- the hull variant (D8) ----------------------------------------------------------

        [Test]
        public void TheOutboardDory_IsTheRowedDory_WithExactlyOneFieldFlipped()
        {
            var rowed = Load<BoatHullDef>($"{DataBoats}/Dory.asset");
            var powered = Load<BoatHullDef>($"{DataBoats}/DoryOutboard.asset");

            Assert.AreEqual("boat.dory_outboard", powered.Id, "ids are append-only and stable (§5).");
            Assert.AreEqual(PropulsionType.Oars, rowed.Propulsion);
            Assert.AreEqual(PropulsionType.Engine, powered.Propulsion,
                "the whole of D8's M1 answer: a second hull asset whose Propulsion is Engine, which " +
                "the purchase swaps the active hull to.");

            // D8's stated cost is "two assets to keep in sync". The builder pays it by COPYING the
            // dory rather than restating her, so these can never drift — and this is the assertion
            // that finds out if someone hand-edits one of them.
            Assert.AreSame(rowed.Visual, powered.Visual,
                "both dories wear ONE visual (visual.dory_iso). The engine is drawn from the HULL's " +
                "propulsion, not by giving her a second skin to keep in step.");
            Assert.AreEqual(rowed.LengthMeters, powered.LengthMeters, 1e-4f);
            Assert.AreEqual(rowed.MassKg, powered.MassKg, 1e-4f);
            Assert.AreEqual(rowed.HoldUnits, powered.HoldUnits, "an outboard is not a bigger hold.");
            Assert.AreEqual(rowed.ForwardDrag, powered.ForwardDrag, 1e-4f);
            Assert.AreEqual(rowed.EnginePower, powered.EnginePower, 1e-4f);
            Assert.AreEqual(rowed.CameraWorldHeightMeters, powered.CameraWorldHeightMeters, 1e-4f);
            Assert.AreSame(rowed.DeckContainer, powered.DeckContainer);
        }

        // ---- the visual: her engine, her clamp, her loan -------------------------------------

        [Test]
        public void TheDorysVisual_WearsAMotorFitting_AndItIsTheBorrowedOne()
        {
            var v = DoryVisual();

            Assert.IsTrue(v.HasMotorMesh(),
                "visual.dory_iso must carry a usable motor FITTING — without it the outboard rung " +
                "cannot be seen at all. Run Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Bake ALL hull fittings.");
            Assert.AreEqual(BorrowedFittingId, v.MotorMesh.Id,
                "she is wearing the PUNT's starter engine on loan, because her own kicker " +
                "(docs/art/rigs/doryMotorRig.js) is written but has never been baked.\n\n" +
                "IF THAT BAKE HAS NOW HAPPENED, this test is the checklist: take DoryIso off the " +
                "punt-basic entry in HullPropFleet, add the dory's own kicker there, and zero " +
                "MotorMeshFitmentOffsetMeters in BoatVisualLibraryBuilder — a borrowed part's shift " +
                "applied to a purpose-baked one hangs it a foot forward of her transom.");
            Assert.AreEqual(OutboardMotorLayer.MotorVariant.Basic, v.MotorVariant,
                "identity follows the engine actually drawn.");
            Assert.AreEqual(OutboardMotorLayer.MotorFit.Single, v.MotorFit,
                "one engine, on the centreline. A dory does not hang a twin.");
        }

        [Test]
        public void TheDorysMotorMount_IsHerOwnTransomBoard_NotTheSkiffDefaultSheInherited()
        {
            var v = DoryVisual();

            Assert.AreNotEqual(SkiffDefaultMount, v.MotorMountLocalMeters,
                "this is the field's INITIALISER, 1.18 m astern of the dory's transom on a 4.5 m " +
                "boat. Carrying it means nobody ever measured hers.");
            Assert.AreEqual(DoryMotorBoard.x, v.MotorMountLocalMeters.x, 1e-4f);
            Assert.AreEqual(DoryMotorBoard.y, v.MotorMountLocalMeters.y, 1e-4f,
                "doryIsoRig MOUNT.y = TR.y − 2·BOARD.t = −2.34, projected at −0.01.");
            Assert.AreEqual(DoryMotorBoard.z, v.MotorMountLocalMeters.z, 1e-4f,
                "doryIsoRig MOUNT.z = TR.zTop + BOARD.rise = 0.80 — her board stands proud of the " +
                "sheer, which is why her clamp is 0.24 m above the punt's.");
        }

        [Test]
        public void TheBorrowedEngine_CarriesTheShiftThatPutsItOnHerTransom()
        {
            var v = DoryVisual();

            Assert.AreEqual(BorrowedFitment.y, v.MotorMeshFitmentOffsetMeters.y, 1e-4f,
                "+0.28 m forward: the dory's kicker swivel (−2.38) against the punt's (−2.66).");
            Assert.AreEqual(BorrowedFitment.z, v.MotorMeshFitmentOffsetMeters.z, 1e-4f,
                "+0.055 m up: hung by the LEG, so the prop lands at z 0.140 where her own kicker " +
                "puts it, rather than by the clamp — which would leave the prop clear of the water.");
            Assert.AreEqual(0f, v.MotorMeshFitmentOffsetMeters.x, 1e-4f, "on the centreline.");
        }

        [Test]
        public void EveryOtherVisual_WearsItsOwnEngine_WithNoShiftAtAll()
        {
            // A non-zero fitment is a LOAN, and exactly one boat in the game has one. This is the
            // guard against the field turning into a nudge knob for engines that sit wrong.
            foreach (string guid in AssetDatabase.FindAssets("t:BoatVisualDef",
                                                            new[] { $"{DataBoats}/Visuals" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var v = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(path);
                if (v == null || v.Id == "visual.dory_iso") continue;
                Assert.AreEqual(0f, v.MotorMeshFitmentOffsetMeters.sqrMagnitude, 1e-6f,
                    $"{path} shifts its motor fitting. Only a BORROWED fitting is shifted, and only " +
                    "the dory borrows one — a fitting is baked in its own hull's rig space, so a " +
                    "hull wearing her own engine needs no shift and a non-zero one here is hiding " +
                    "a bad bake.");
            }
        }

        [Test]
        public void TheSpritePath_IsUntouched_SheIsStillARowingBoatOnHerSheets()
        {
            var v = DoryVisual();

            Assert.IsFalse(v.HasMotor(),
                "no motor SHEETS on the dory, and there must not be: her oar sheets already own the " +
                "sorting band an engine's lower layer needs. The mesh fitting can share a hull with " +
                "the oars precisely because it is posed by rotation rather than indexed by facing.");
            Assert.IsFalse(v.HasConflictingOverlays(),
                "…which is why the oars-vs-motor exclusion is still satisfied.");
            Assert.IsTrue(v.HasOarSheets(), "her sprite oars are exactly as they were.");
            Assert.IsTrue(v.HasOarMeshes(), "…and so are her mesh ones.");
            Assert.IsTrue(v.HasFullCompass());
        }

        [Test]
        public void HerSteerColumnsAndAuthority_AreTheEnginesOwn_NotAPlaceholder()
        {
            var v = DoryVisual();

            Assert.AreEqual(OutboardMotorMath.SteerColumns, v.MotorColumnCount,
                "the mesh layer runs its swivel accumulator in COLUMN units off this number — the " +
                "tuned cadence, shared with the sprite path. Left at 1 (a hull with no engine) the " +
                "outboard is pinned dead ahead at every helm.");
            Assert.AreEqual(32f, v.MotorMaxSteerDegrees, 1e-4f,
                "both the punt's engine and the dory's own kicker bake ±32°, so the loan does not " +
                "change how far she swings.");
        }

    }
}
