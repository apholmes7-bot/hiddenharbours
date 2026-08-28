using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Boats;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// 🔴 <b>THE MARKS PUSH BACK — the owner's ask of 2026-08-27, measured.</b> His words after watching
    /// a skipper drive through the buoyed entrance: <i>"buoys should also have collision with some type
    /// of rubberbanding effect depended on the mass of the vessel."</i>
    ///
    /// <para><b>⭐ What is actually under test is a PROPERTY, not a mechanism.</b> There is no per-hull
    /// case in the shipped code and no response curve: a mark carries a stated displacement and the
    /// solver exchanges momentum, so a struck hull's deflection falls out as
    /// <c>m_buoy / (m_buoy + m_hull)</c>. These tests therefore assert the ORDERING that follows from
    /// that — the light hull is shouldered further than the heavy one, every time — rather than any
    /// particular number, which would pin the tuning rather than the law.</para>
    ///
    /// <para><b>⚠️⭐⭐ THE HULL WEARS THE SHIPPED CAPSULE.</b> Every player boat carries
    /// <c>PersistentCoreBuilder</c>'s fixed 1.7 × 4.0 m capsule; <c>BoatController.SetHull</c> re-derives
    /// MASS from displacement and never touches the collider. A fixture that sized a hull's collider to
    /// her real dimensions would measure a boat that does not exist — the same trap that gave the
    /// arrival a 177 m turning circle. The mass comes from the committed defs; the capsule is the
    /// shipped one, deliberately.</para>
    ///
    /// <para>The mooring's own arithmetic — she yields, rebounds, settles, and never leaves her watch
    /// circle — is EditMode's (<c>NavMarkPlacementTests</c> §8), because it is pure. What needs Play is
    /// the part that needs a solver: two bodies actually meeting.</para>
    /// </summary>
    public class NavBuoyCollisionPlayTests
    {
        // The kit's harbour rung (s12, 1.2 m) — the size both regions' entrances are buoyed with.
        private const float MarkMassKg = 288f;      // NavBuoyKit.MooredMassFor(1.2)
        private const float MarkRadiusM = 0.6f;     // NavBuoyKit.CollisionRadiusFor(1.2)
        private const float WatchRadiusM = 3f;      // NavBuoyKit.WatchRadiusFor(1.2)
        private const float SpringPerS2 = 4f;
        private const float DampingRatio = 0.5f;

        /// <summary>PersistentCoreBuilder's fixed hull capsule. Every boat afloat wears this one.</summary>
        private static readonly Vector2 ShippedHullCapsule = new Vector2(1.7f, 4.0f);

        private const float ApproachSpeed = 3f;     // 6 knots — harbour speed for a working boat
        private const float StrikeOffsetM = 0.9f;   // off her centreline, so the blow is a glancing one

        private GameObject _mark;
        private GameObject _hull;

        [TearDown]
        public void TearDown()
        {
            if (_mark != null) Object.DestroyImmediate(_mark);
            if (_hull != null) Object.DestroyImmediate(_hull);
        }

        // =============================================================================================
        //  the fixture
        // =============================================================================================

        private NavBuoyMooring MoorAMark(Vector2 at)
        {
            _mark = new GameObject("NavMark_UnderTest");
            _mark.transform.position = at;                 // BEFORE the component, so Awake's fallback
                                                           // anchor is this and not the origin
            var mooring = _mark.AddComponent<NavBuoyMooring>();   // brings its own body + collider
            mooring.MoorAt(at);
            mooring.Configure(MarkMassKg, MarkRadiusM, WatchRadiusM, SpringPerS2, DampingRatio);
            return mooring;
        }

        /// <summary>A hull as she actually ships: the fixed capsule, and her def's mass on
        /// BoatController's own <c>MassKg / 100</c> scale.</summary>
        private Rigidbody2D LaunchAHull(float massKg, Vector2 from, Vector2 velocity)
        {
            _hull = new GameObject($"Hull_{massKg:F0}kg");
            _hull.transform.position = from;

            var capsule = _hull.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = ShippedHullCapsule;
            capsule.offset = Vector2.zero;

            var rb = _hull.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearDamping = 0f;                        // no drag: the only thing acting is the mark
            rb.angularDamping = 0f;
            rb.mass = Mathf.Max(1f, massKg / 100f);       // BoatController.SetHull's own scale
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.linearVelocity = velocity;
            return rb;
        }

        /// <summary>Drive a hull of this mass past a moored mark and report how far off her line she
        /// ends up, how much way she kept, and what the mark did.</summary>
        private IEnumerator RunTheStrike(float hullMassKg, System.Action<Strike> report)
        {
            NavBuoyMooring mooring = MoorAMark(Vector2.zero);
            Rigidbody2D hull = LaunchAHull(hullMassKg,
                                           new Vector2(-StrikeOffsetM, -14f),
                                           new Vector2(0f, ApproachSpeed));

            var strike = new Strike();
            for (int step = 0; step < 750; step++)          // 15 s at the default fixed step
            {
                yield return new WaitForFixedUpdate();

                strike.FarthestMarkOffset =
                    Mathf.Max(strike.FarthestMarkOffset, mooring.OffsetFromAnchorMetres);

                // Her deflection is measured once she is past the mark and clear of it, so a reading
                // is never taken mid-contact while the solver is still resolving penetration.
                if (hull.position.y > 8f)
                {
                    strike.LateralOffset = Mathf.Abs(hull.position.x + StrikeOffsetM);
                    strike.WayKept = hull.linearVelocity.y / ApproachSpeed;
                    if (!strike.Passed) strike.Passed = true;
                }
            }

            strike.FinalMarkOffset = mooring.OffsetFromAnchorMetres;
            report(strike);
        }

        private class Strike
        {
            /// <summary>How far off her original line the hull ended up, in metres.</summary>
            public float LateralOffset;
            /// <summary>What fraction of her approach speed she still carried.</summary>
            public float WayKept;
            /// <summary>The mark's greatest excursion from her anchor, in metres.</summary>
            public float FarthestMarkOffset;
            /// <summary>Where the mark had settled by the end, in metres from her anchor.</summary>
            public float FinalMarkOffset;
            /// <summary>Did the hull actually get past the mark at all?</summary>
            public bool Passed;
        }

        // =============================================================================================
        //  1. she gives, and she comes home
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The mark is never an obstacle and never lost.</b> Struck, she must MOVE — a buoy that a
        /// boat bounces off without shifting is a rock — she must stay inside her watch circle while it
        /// happens, and she must be back on her anchor afterwards. All three, or she is not moored.
        /// </summary>
        [UnityTest]
        public IEnumerator AStruckMarkYields_StaysOnHerChain_AndComesHome()
        {
            Strike strike = null;
            yield return RunTheStrike(400f, s => strike = s);   // the dory: Dory.asset MassKg 400

            Assert.That(strike.Passed, Is.True,
                "the hull never got past the mark at all — she was stopped or turned back. A buoy is " +
                "channel furniture, not a wall.");

            Assert.That(strike.FarthestMarkOffset, Is.GreaterThan(0.05f),
                $"the mark moved {strike.FarthestMarkOffset:F3} m when a boat hit her. She is behaving " +
                "as static geometry; check her Rigidbody2D is dynamic and her displacement is not " +
                "enormous.");

            Assert.That(strike.FarthestMarkOffset, Is.LessThanOrEqualTo(WatchRadiusM + 0.05f),
                $"she was dragged {strike.FarthestMarkOffset:F2} m from her anchor against a " +
                $"{WatchRadiusM:F2} m watch circle. A mark that can be towed away marks nothing.");

            Assert.That(strike.FinalMarkOffset, Is.LessThan(0.15f),
                $"fifteen seconds after the knock she is still {strike.FinalMarkOffset:F2} m off her " +
                "anchor. She has to settle back onto the edge she marks.");
        }

        // =============================================================================================
        //  2. the mass response — one law, and the ladder that falls out of it
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>THE OWNER'S ASK, MEASURED: "depended on the mass of the vessel".</b> The same mark, the
        /// same speed, the same glancing blow — and the punt is shouldered off her line while the cape
        /// islander barely notices. Asserted as an ORDERING between two real hulls rather than as a
        /// number, because the number is tuning and the ordering is the law.
        ///
        /// <para>⚠ The masses are the committed defs', not typed here: the whole claim is about the
        /// fleet's own ladder, and a mirror of it would go quietly stale the day a hull is re-weighed.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheLightHullIsShoulderedOffHerLine_AndTheHeavyOneIsNot()
        {
#if UNITY_EDITOR
            var dory = AssetDatabase.LoadAssetAtPath<BoatHullDef>(
                "Assets/_Project/Data/Boats/Dory.asset");
            var cape = AssetDatabase.LoadAssetAtPath<BoatHullDef>(
                "Assets/_Project/Data/Boats/CapeIslander.asset");
            Assert.That(dory, Is.Not.Null, "the starting dory's def must exist");
            Assert.That(cape, Is.Not.Null, "the arrival hull's def must exist");
            Assert.That(cape.MassKg, Is.GreaterThan(dory.MassKg),
                "this test's whole premise is that one of these hulls is much heavier than the other");

            Strike light = null;
            yield return RunTheStrike(dory.MassKg, s => light = s);
            TearDown();

            Strike heavy = null;
            yield return RunTheStrike(cape.MassKg, s => heavy = s);

            Assert.That(light.Passed && heavy.Passed, Is.True, "both hulls must get past the mark");

            Assert.That(light.LateralOffset, Is.GreaterThan(heavy.LateralOffset),
                $"the {dory.MassKg:F0} kg hull came off {light.LateralOffset:F3} m and the " +
                $"{cape.MassKg:F0} kg hull {heavy.LateralOffset:F3} m. The response is supposed to " +
                "scale with the vessel's mass, and it has stopped doing so — which is what a scripted " +
                "shove on top of the collision would cause.");

            Assert.That(light.LateralOffset, Is.GreaterThan(0.05f),
                $"the light hull was only moved {light.LateralOffset:F3} m by a mark most of her own " +
                "displacement. That is not a shoulder; the owner asked to feel this.");

            Assert.That(heavy.WayKept, Is.GreaterThan(0.8f),
                $"the {cape.MassKg:F0} kg hull lost {(1f - heavy.WayKept) * 100f:F0}% of her way to a " +
                "buoy. She should barely notice one — a mark that stops a working boat reads as " +
                "hitting the wharf.");

            Debug.Log($"[navbuoy] {dory.MassKg:F0} kg came off {light.LateralOffset:F3} m; " +
                      $"{cape.MassKg:F0} kg came off {heavy.LateralOffset:F3} m and kept " +
                      $"{heavy.WayKept * 100f:F0}% of her way.");
#else
            yield return null;
            Assert.Ignore("Needs the AssetDatabase: the claim is about the COMMITTED hull masses.");
#endif
        }

        // =============================================================================================
        //  3. the negative control — the fixture can see a mark that does not collide
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Proving the microphone is live.</b> The tests above measure a deflection, and a
        /// deflection of nothing passes just as happily when the two bodies never meet at all. So: run
        /// the identical strike with the mark's collider turned into a trigger — the decor tier, in
        /// effect — and require the hull to sail straight through undeflected. If THIS test fails, the
        /// ones above are measuring something other than a collision.
        /// </summary>
        [UnityTest]
        public IEnumerator AndAMarkThatDoesNotCollideDeflectsNothing()
        {
            NavBuoyMooring mooring = MoorAMark(Vector2.zero);
            _mark.GetComponent<CircleCollider2D>().isTrigger = true;    // decor, as she used to be

            Rigidbody2D hull = LaunchAHull(400f, new Vector2(-StrikeOffsetM, -14f),
                                           new Vector2(0f, ApproachSpeed));

            for (int step = 0; step < 400; step++) yield return new WaitForFixedUpdate();

            Assert.That(hull.position.y, Is.GreaterThan(8f), "she should have run straight past");
            Assert.That(Mathf.Abs(hull.position.x + StrikeOffsetM), Is.LessThan(0.01f),
                "a hull was deflected by a mark she cannot touch. Something other than the collision " +
                "is moving her, and every deflection measured above is suspect.");
            Assert.That(mooring.OffsetFromAnchorMetres, Is.LessThan(0.01f),
                "the mark moved without being touched.");
        }
    }
}
