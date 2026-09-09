using System;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐⭐ <b>THE RIDER STAYS ON THE DECK POINT SHE IS STANDING ON.</b> The guard for the owner's
    /// 2026-09-09 playtest: <i>"in large waves the sprite does not stay anchored to the correct spot on
    /// the deck."</i>
    ///
    /// <para><b>⭐ The calm control is the headline, and it is why this fixture leads with it.</b> The
    /// complaint named large waves, so the obvious fixture would have driven a storm — and would have
    /// measured the wrong thing. At ANCHOR, with no storm at all and her own def amplitudes, the rider
    /// was already off her deck point at every one of five swept points: amidships 1.5 px, 3 m forward
    /// 1.5 px, rail abeam 3.0 px, foredeck 5.5 px, aft transom 6.1 px, against a 1 px bar. The sea the
    /// wind law can actually make barely adds to it — <c>GameConfig</c> opens the storm band at 0.4 and
    /// #797 caps the sea at ~0.55, so <c>StormBlend01</c> is <b>0.109</b>, 10.9% in. He noticed it in the
    /// biggest sea because that is where 6 px becomes 9.</para>
    ///
    /// <para><b>⭐⭐ And the SWEEP is not decoration.</b> The old pose was
    /// <c>(RollDegrees, LiftMeters)</c> — a lean about the feet and a VERTICAL lift, with no lateral
    /// term. So the foredeck's 5.5 px was <b>5.5 px of pure LATERAL swing</b>, which no choice of
    /// amplitude can remove, while the transom's 6.1 px was <b>6.1 px vertical</b>, which amplitude alone
    /// would. Same rock, opposite failure, because the lever arm points differently at each end of the
    /// boat. A fixture that stood her amidships would have read 1.5 px and someone would have called it
    /// noise — and a fix that only matched the amplitudes would have left the foredeck 5× over the bar
    /// and passed a one-point test.</para>
    ///
    /// <para><b>Every failure prints the three rival predictions</b> beside the observed Δ — (a) the
    /// amplitude/lever mismatch, (b) a one-frame lag, (c) an unreported term — so a red names its channel
    /// instead of starting an argument.</para>
    ///
    /// <para><b>⚠ What this fixture does NOT cover, stated so nothing implies otherwise.</b> It drives the
    /// pose maths and the presenter seam. It does not drive <c>DeckRiderVisual</c>'s own
    /// <c>LateUpdate</c>, the live wave field, or a running scene — those need PlayMode, and
    /// <see cref="TheRiderReadsAfterTheHullHasWritten"/> pins the one ordering fact that would otherwise
    /// be a comment.</para>
    /// </summary>
    public class RiderHoldsTheDeckTests
    {
        private const float Elev = 40f;          // CapeIslanderIsoHullMesh / DoryIsoHullMesh ElevationDeg
        private const float Ppu = 32f;           // the sheets' pixels per metre
        private const float BarPixels = 1f;      // the acceptance bar
        private static float BarMetres => BarPixels / Ppu;

        // BoatVisualDef's shipped rock, identical on CapeIslanderIso and DoryIso (MotorRock*).
        private const float HullRollDeg = 3.4f, HullPitchDeg = 1.9f, HullHeavePx = 1.3f;

        // DeckRiderVisual's serialized defaults — the private amplitudes this PR retires on the mesh path.
        private const float OldRiderRollDeg = 5f, OldRiderHeavePx = 1.6f, OldRiderPitchLiftM = 0.02f;

        /// <summary>GameConfig.asset StormRock: band opens at 0.4, exponent 1.6; #797 caps the sea at
        /// 0.55. The largest storm the wind law can actually produce, computed rather than typed.</summary>
        private static float CapBlend01 => Mathf.Pow((0.55f - 0.4f) / (1f - 0.4f), 1.6f);

        /// <summary>The mesh extras' caps (GameConfig), reached only in proportion to the blend.</summary>
        private static float CapExtraRollDeg => 8f * CapBlend01;
        private static float CapExtraPitchDeg => 10f * CapBlend01;

        private static readonly (string name, Vector3 p)[] DeckPoints =
        {
            ("amidships",     new Vector3(0f, 0f, 0.72f)),
            ("3 m fwd, sole", new Vector3(0f, 3f, 0.72f)),
            ("foredeck",      new Vector3(0f, 5f, 2.90f)),
            ("rail abeam",    new Vector3(1.5f, 0f, 0.72f)),
            ("aft, transom",  new Vector3(0f, -6f, 0.72f)),
        };

        private static readonly float[] Headings = { 0f, 90f, 180f, 270f };

        // ---- the instrument --------------------------------------------------------------------------

        /// <summary>Where the HULL draws this deck point: her applied attitude through the rig
        /// projection, plus the heave folded into the same channel.</summary>
        private static Vector2 HullFoot(Vector3 p, float headingDeg, float rollDeg, float pitchDeg,
                                        float heaveMeters)
            => MountedRockPoseMath.Project(p, -headingDeg * Mathf.Deg2Rad, rollDeg * Mathf.Deg2Rad,
                                           pitchDeg * Mathf.Deg2Rad, Elev * Mathf.Deg2Rad)
             + new Vector2(0f, heaveMeters);

        /// <summary>Where the deck WALK stands her: the level deck point, no attitude at all.</summary>
        private static Vector2 LevelFoot(Vector3 p, float headingDeg)
            => MountedRockPoseMath.Project(p, -headingDeg * Mathf.Deg2Rad, 0f, 0f, Elev * Mathf.Deg2Rad);

        /// <summary>The hull's pose at a phase, as <c>MeshHullDriver</c> composes it.</summary>
        private static void HullPose(float phase, float amplitudeScale, float extraRoll, float extraPitch,
                                     out float roll, out float pitch, out float heaveMeters)
        {
            HullMeshMath.RockPose(phase, HullRollDeg * amplitudeScale, HullPitchDeg * amplitudeScale,
                                  HullHeavePx * amplitudeScale, out roll, out pitch, out float heavePx);
            float c = Mathf.Cos(phase * Mathf.Deg2Rad);
            roll += extraRoll * c;
            pitch += extraPitch * c;
            heaveMeters = heavePx / Ppu;
        }

        // ---- the cases -------------------------------------------------------------------------------

        /// <summary>⭐ The instrument is the SHIPPED transform, not a model of it: at roll = pitch = 0 the
        /// rig projection IS <see cref="DeckAreaMath.DeckToWorld"/>, the transform the deck walk places
        /// her by. If this ever drifts, every number below is measuring two different boats.</summary>
        [Test]
        public void TheRigProjectionAtRest_IsTheTransformTheDeckWalkPlacesHerBy()
        {
            foreach (float heading in Headings)
            foreach ((string name, Vector3 p) in DeckPoints)
            {
                Vector2 viaRig = LevelFoot(p, heading);
                Vector2 viaDeck = DeckAreaMath.DeckToWorld(new Vector2(p.x, p.y), p.z, heading, Elev);
                Assert.AreEqual(viaDeck.x, viaRig.x, 1e-5f, $"{name} at {heading:F0}°: x");
                Assert.AreEqual(viaDeck.y, viaRig.y, 1e-5f, $"{name} at {heading:F0}°: y");
            }
        }

        /// <summary>⭐⭐ THE CALM CONTROL. At anchor, no storm, her own amplitudes — the case that was
        /// already 1.5–6.1 px wrong and the reason this is not a storm bug.</summary>
        [Test]
        public void TheRiderHoldsHerDeckPoint_AtAnchor()
            => AssertHoldsThroughAPeriod("the calm control (at anchor, no storm)", 1f, 0f, 0f);

        /// <summary>⭐ …and at the largest sea the wind law can make.</summary>
        [Test]
        public void TheRiderHoldsHerDeckPoint_AtTheReachableCap()
            => AssertHoldsThroughAPeriod($"the cap (sea 0.55 → blend {CapBlend01:F3})",
                                         1f, CapExtraRollDeg, CapExtraPitchDeg);

        /// <summary>⭐ …and on the stiffest hull the response scale allows, where the def amplitudes
        /// double.</summary>
        [Test]
        public void TheRiderHoldsHerDeckPoint_AtMaxHullResponse()
            => AssertHoldsThroughAPeriod("max hull response (MaxHullResponseScale 2)",
                                         2f, CapExtraRollDeg, CapExtraPitchDeg);

        /// <summary>
        /// ⭐⭐ <b>THE DORY, MEASURED — not asserted by construction.</b> The first draft of this PR said
        /// "the dory is unchanged by construction, because she is a sprite hull". <b>She is not.</b>
        /// <c>BoatHullVariant</c> is <c>Sprite = 0, Mesh = 1</c> and every shipped visual reads
        /// <c>Variant: 1</c>, <c>DoryIso</c> included — the whole fleet is mesh. So the mirror DOES apply
        /// to her, the gate does not protect her, and "her oar rock is unchanged" had to stop being an
        /// argument and become a number. It is this one: her rock is tracked to the same bar as every
        /// other hull's, because her amplitudes ARE the same amplitudes (MotorRock 3.4° / 1.9° / 1.3 px).
        /// </summary>
        [Test]
        public void TheDoryIsAMeshHullToo_SoHerRockIsTrackedAndNotAssumed()
        {
            var dory = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                "Assets/_Project/Data/Boats/Visuals/DoryIso.asset");
            Assert.IsNotNull(dory, "the starter dory's visual def is missing");
            Assert.AreEqual(BoatHullVariant.Mesh, dory.Variant,
                "the dory has become a SPRITE hull. That is a real change and it is fine — but the mirror " +
                "is gated on SupportsContinuousRock, so she would fall through to the cosmetic lean and " +
                "this suite would stop measuring her. Re-point it before believing the green.");
            Assert.AreEqual(HullRollDeg, dory.MotorRockRollDegrees, 1e-4f,
                "the dory's rock amplitudes have moved; the constants at the top of this fixture are stale");

            AssertHoldsThroughAPeriod("the dory at anchor", 1f, 0f, 0f);
        }

        /// <summary>⭐ THE EVIDENCE, kept in the run. Measures what the OLD private amplitudes would still
        /// do — not a regression guard, the number that justified the change. If this ever falls under the
        /// bar, the hull's amplitudes have moved to meet the rider's and this whole PR is measuring
        /// something else.</summary>
        [Test]
        public void ThePrivateAmplitudesThisReplaced_MissedByThisMuch()
        {
            var worst = new StringBuilder();
            float worstPixels = 0f;

            foreach (float heading in Headings)
            foreach ((string name, Vector3 p) in DeckPoints)
            {
                float here = 0f;
                for (float phase = 0f; phase < 360f; phase += 1f)
                {
                    HullPose(phase, 1f, 0f, 0f, out float roll, out float pitch, out float heave);
                    DeckRidePose old = DeckRideMath.Ride(phase, OldRiderRollDeg, OldRiderHeavePx,
                                                         OldRiderPitchLiftM, Ppu, 1f, 1f);
                    Vector2 oldFoot = LevelFoot(p, heading) + new Vector2(0f, old.LiftMeters);
                    here = Mathf.Max(here, (HullFoot(p, heading, roll, pitch, heave) - oldFoot).magnitude);
                }
                if (here > worstPixels) { worstPixels = here; worst.Clear(); worst.Append($"{name} at {heading:F0}°"); }
            }

            Debug.Log($"[Rider] the private amplitudes (5°/1.6 px) against the hull's (3.4°/1.3 px), at " +
                      $"ANCHOR: worst {worstPixels * Ppu:F1} px at {worst} — the bar is {BarPixels:F0} px.");
            Assert.Greater(worstPixels, BarMetres * 3f,
                "the old private amplitudes now agree with the hull to within 3 px. Either the def's rock " +
                "or DeckRiderVisual's serialized defaults have moved, and this suite is no longer " +
                "measuring the defect it was written for.");
        }

        /// <summary>⭐ The strength knob still stands her square at 0 — the A/B's off side, and the one
        /// thing the three retired feel fields could do that a single multiplier must keep doing.</summary>
        [Test]
        public void AStrengthOfZero_StandsHerSquare()
        {
            HullPose(70f, 1f, 0f, 0f, out float roll, out float pitch, out float heave);
            DeckRidePose off = MountedRockPoseMath.MirrorHull(DeckPoints[3].p, 270f, roll, pitch, heave,
                                                              Elev, 0f);
            Assert.AreEqual(0f, off.RollDegrees, 1e-6f, "a strength of 0 must not lean her");
            Assert.AreEqual(0f, off.LiftMeters, 1e-6f, "…nor lift her");
            Assert.AreEqual(0f, off.SwayMeters, 1e-6f, "…nor sway her");
        }

        /// <summary>⚠ A pose built the two-argument way carries no sway, so every existing construction —
        /// the sprite path's cosmetic lean included — is unchanged by the field existing.</summary>
        [Test]
        public void ATwoArgumentPose_CarriesNoSway()
        {
            var pose = new DeckRidePose(4f, 0.05f);
            Assert.AreEqual(0f, pose.SwayMeters, 0f, "the two-argument ctor must leave sway at exactly 0");
            Assert.AreEqual(new Vector2(0f, 0.05f), pose.OffsetMeters);
        }

        /// <summary>
        /// ⭐ <b>THE ORDER, PINNED — not left as a comment.</b> The rider reads what the hull APPLIED, so
        /// it must run after the hull wrote it. <c>BoatWaveMotion</c> (−120) → <c>MeshHullDriver</c>
        /// (−110) → <c>DeckRiderVisual</c> (100). A one-frame lag was rival (b) in this measurement, and
        /// the way it would arrive is somebody re-tuning one of these three attributes.
        /// </summary>
        [Test]
        public void TheRiderReadsAfterTheHullHasWritten()
        {
            int wave = ExecutionOrderOf(typeof(BoatWaveMotion));
            int driver = ExecutionOrderOf(typeof(MeshHullDriver));
            int rider = ExecutionOrderOf(typeof(DeckRiderVisual));

            Assert.Less(wave, driver,
                $"BoatWaveMotion ({wave}) must run before MeshHullDriver ({driver}) — the driver poses " +
                "the hull from what the wave motion published");
            Assert.Less(driver, rider,
                $"MeshHullDriver ({driver}) must run before DeckRiderVisual ({rider}) — the rider mirrors " +
                "what the driver APPLIED, and reading it first is a one-frame lag that grows with wave speed");
        }

        private static int ExecutionOrderOf(Type t)
        {
            object[] a = t.GetCustomAttributes(typeof(DefaultExecutionOrder), inherit: true);
            Assert.IsNotEmpty(a, $"{t.Name} carries no [DefaultExecutionOrder]; the order below is then " +
                                 "whatever the project settings happen to say, which is not a guarantee");
            return ((DefaultExecutionOrder)a[0]).order;
        }

        // ---- the sweep -------------------------------------------------------------------------------

        private static void AssertHoldsThroughAPeriod(string label, float amplitudeScale,
                                                      float extraRoll, float extraPitch)
        {
            foreach (float heading in Headings)
            foreach ((string name, Vector3 p) in DeckPoints)
            {
                for (float phase = 0f; phase < 360f; phase += 1f)
                {
                    HullPose(phase, amplitudeScale, extraRoll, extraPitch,
                             out float roll, out float pitch, out float heave);

                    Vector2 hull = HullFoot(p, heading, roll, pitch, heave);
                    Vector2 level = LevelFoot(p, heading);
                    DeckRidePose pose = MountedRockPoseMath.MirrorHull(p, heading, roll, pitch, heave,
                                                                        Elev, 1f);
                    Vector2 rider = level + pose.OffsetMeters;

                    float delta = (hull - rider).magnitude;
                    if (delta <= BarMetres) continue;

                    // ⭐ The three rivals, printed beside the observed Δ so a red names its channel.
                    DeckRidePose old = DeckRideMath.Ride(phase, OldRiderRollDeg, OldRiderHeavePx,
                                                         OldRiderPitchLiftM, Ppu, 1f, 1f);
                    float rivalA = (hull - (level + new Vector2(0f, old.LiftMeters))).magnitude;
                    DeckRidePose lagged = DeckRideMath.Ride(phase - 6f, OldRiderRollDeg, OldRiderHeavePx,
                                                            OldRiderPitchLiftM, Ppu, 1f, 1f);
                    float rivalB = Mathf.Abs(lagged.LiftMeters - old.LiftMeters);

                    Assert.Fail(
                        $"{label}: {name} at {heading:F0}°, phase {phase:F0}° — the rider is " +
                        $"{delta * Ppu:F2} px off the deck point the hull draws (bar {BarPixels:F0} px).\n" +
                        $"  lateral {Mathf.Abs(hull.x - rider.x) * Ppu:F2} px, " +
                        $"vertical {Mathf.Abs(hull.y - rider.y) * Ppu:F2} px\n" +
                        $"  rival (a) the old private amplitudes would be off by {rivalA * Ppu:F2} px here\n" +
                        $"  rival (b) a one-frame lag would be {rivalB * Ppu:F2} px\n" +
                        $"  rival (c) an unreported term is 0 on the mesh path by construction — " +
                        "MeshHullDriver folds the ride INTO the heave it reports\n" +
                        $"  If (a) is close to the observed Δ the mirror is not being consulted at all; if " +
                        "(b) is, the execution order moved; if neither, the presenter is reporting " +
                        "something other than what it applied.");
                }
            }
        }
    }
}
