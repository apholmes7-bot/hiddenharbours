using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>SHE WALKS AT THE QUAY EDGE AND IS HELD.</b> The owner's 2026-08-19 walk stepped off Nine Mile
    /// Creek's wharf into the basin, because the quay had no player collision of any kind. The geometry
    /// of the fix is pinned in EditMode (<c>NineMileCreekWallsTests</c>, <c>QuayEdgePlanTests</c>); this
    /// is the half that no amount of geometry can stand in for — a real dynamic body, in a real physics
    /// loop, in the real region, pushed at each wall until it stops her or does not.
    ///
    /// <para><b>The edges are the REGION's, not this fixture's.</b> A PlayMode assembly may not reference
    /// <c>HiddenHarbours.App.Editor</c> (it has no <c>includePlatforms</c>, so the reference would follow
    /// a player build to a machine where that assembly does not exist), which is where Nine Mile Creek's
    /// numbers live. So nothing is restated here: the scene's own quay <see cref="StandablePlatform"/>s
    /// give the decks and their measured heights, the scene's own <see cref="ITidalTerrain"/> gives the
    /// ground, and <see cref="QuayEdgePlan"/> — the SAME call the builder makes — turns those into the
    /// edges she is pushed at. Re-site the wharf and this fixture re-aims itself.</para>
    ///
    /// <para><b>⚠ IT BUILDS ITS OWN BOUNDARY RATHER THAN LOOKING FOR THE BUILDER'S.</b> Committed scenes
    /// in this repo are builder OUTPUT and are banked by hand, so the banked Nine Mile Creek predates this
    /// slice and carries no walls. Hunting for one and skipping when it is absent would make this fixture
    /// green on the very build it exists to catch. It configures a <see cref="PropertyBoundary"/> from the
    /// plan instead, which is exactly what the builder does with exactly the same inputs.</para>
    ///
    /// <para><b>⚠ Every claim has a sabotage arm.</b> A wharf is full of other colliders; "she stopped"
    /// is only evidence if she would NOT have stopped without the wall. Each acceptance below is run
    /// twice — once with the boundary and once without — and the second run must let her through.</para>
    /// </summary>
    public class NineMileCreekWallPlayTests
    {
        const string SceneName = "NineMileCreek";

        /// <summary>Quay surface ids, from <c>NineMileCreekWharf</c>. Matched as a PREFIX so the float and
        /// the brow — whose decks ride the tide and are not a fall — stay out of it.</summary>
        const string QuayIdPrefix = "wharf.nine_mile_creek";
        const string FloatId = "wharf.nine_mile_creek.float";
        const string GangwayId = "wharf.nine_mile_creek.gangway";

        /// <summary>Physics steps she is pushed for. At the walk speed below that is over three metres of
        /// travel — an order more than the wall is thick, so a wall that merely slows her fails.</summary>
        const int PushSteps = 80;

        /// <summary>How hard she is pushed (m/s). Her sprint, near enough: a wall that holds at a walk and
        /// not at a run is not a wall.</summary>
        const float PushSpeed = 5.5f;

        /// <summary>How far past the wall's line her foot may end up (m) and still count as held. Pressed
        /// against the inner face she settles about 0.52 m inboard (half a thickness plus her radius); a
        /// quarter-metre allows for penetration slop and nothing else.</summary>
        const float HeldMarginMetres = 0.25f;

        readonly List<Object> _spawned = new List<Object>();

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            // ⚠ PUT THE LOG GUARD BACK TOO — it is a STATIC, so leaving it on hands every test that
            // follows a suite in which no error can fail anything.
            LogAssert.ignoreFailingMessages = false;

            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();

            // ⚠️ PUT THE WORLD BACK. A PlayMode run shares one player loop, and leaving Nine Mile Creek
            // resident hands every test that follows a wharf, a tide and a seabed it never asked for —
            // measured on this suite before (NineMileCreekWharfPaintPlayTests' own note: two reds landed
            // in PierDisembarkPlayTests and read as a tide regression in someone else's lane).
            var clean = SceneManager.CreateScene("QuayWallCleanup");
            SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == SceneName)
                    yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        // =============================================================================================
        //  the fixture
        // =============================================================================================

        IEnumerator LoadTheCreek()
        {
            // The region logs a 9-slice error from a decor sprite that has nothing to do with walls.
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            for (int i = 0; i < 4; i++) yield return null;   // let the self-installing components register
        }

        /// <summary>The quay's fixed decks, read off the scene's own platforms. The float and the brow are
        /// excluded by id: their decks ride the tide a foot over the water, so stepping off one is a wade,
        /// which the wading model already owns.</summary>
        static List<QuayDeck> QuayDecks()
        {
            return Object.FindObjectsByType<StandablePlatform>()
                         .Where(p => p.Id != null && p.Id.StartsWith(QuayIdPrefix)
                                     && p.Id != FloatId && p.Id != GangwayId
                                     && p.Footprint.width > 1f && p.Footprint.height > 1f)
                         .OrderBy(p => p.Id)
                         .Select(p => new QuayDeck(p.Footprint, p.DeckElevation))
                         .ToList();
        }

        /// <summary>The gates the SCENE can vouch for. Only the brow: it is the one thing you reach by
        /// walking, and it publishes its own hinge and width.</summary>
        static List<EdgeGate> SceneGates()
        {
            var gates = new List<EdgeGate>();
            foreach (var brow in Object.FindObjectsByType<GangwayPlatform>())
                gates.Add(new EdgeGate(brow.ShoreEnd,
                                       QuayEdgePlan.GateClearFor(brow.HalfWidthMetres * 2f),
                                       "the brow to the float"));
            return gates;
        }

        /// <summary>Build the walls the builder would build, from the scene's own geometry.</summary>
        PropertyBoundary Wall(List<QuayDeck> decks, out List<Vector2[]> runs)
        {
            ITidalTerrain terrain = GameServices.TidalTerrain;
            Assert.IsNotNull(terrain, $"'{SceneName}' registered no ITidalTerrain, so which edges are a " +
                                      "fall cannot be asked and this fixture is measuring nothing.");

            runs = QuayEdgePlan.GuardedRuns(decks, terrain.ElevationAt, SceneGates());
            var go = new GameObject("QuayWalls_UnderTest");
            _spawned.Add(go);
            var boundary = go.AddComponent<PropertyBoundary>();
            boundary.Configure(runs, PropertyBoundary.DefaultThicknessMetres, "the quay edge, under test");
            return boundary;
        }

        /// <summary>A body the size and shape of the one both region builders give the player: a dynamic
        /// rigidbody with no gravity and a foot circle under it.
        ///
        /// <para><b>No <c>PlayerWalkController</c>, deliberately.</b> Headless has no focused view, so the
        /// Input System wipes every device each frame and a held key is unreachable — the controller would
        /// write zero velocity every physics step and she would stand rooted while every wall "held".
        /// The <c>PlayerSprintPlayTests</c> precedent: drive the body the controller drives.</para></summary>
        Rigidbody2D Walker(Vector2 footCentre)
        {
            var go = new GameObject("Walker");
            _spawned.Add(go);
            go.transform.position = new Vector3(footCentre.x, footCentre.y + 0.7f, 0f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var foot = go.AddComponent<CircleCollider2D>();
            foot.radius = QuayEdgePlan.WalkerFootRadiusMetres;
            foot.offset = new Vector2(0f, -0.7f);
            return rb;
        }

        static Vector2 FootOf(Rigidbody2D rb) => (Vector2)rb.transform.position + new Vector2(0f, -0.7f);

        /// <summary>Push her, holding the velocity every step so a collision cannot merely absorb it.</summary>
        IEnumerator Push(Rigidbody2D rb, Vector2 direction)
        {
            for (int i = 0; i < PushSteps; i++)
            {
                rb.linearVelocity = direction.normalized * PushSpeed;
                yield return new WaitForFixedUpdate();
            }
        }

        // =============================================================================================
        //  where she is pushed: the plan's own runs, sampled clear of their corners
        // =============================================================================================

        struct Probe
        {
            public Vector2 On;         // a point on the wall line
            public Vector2 Outward;    // the way the water is
        }

        /// <summary>Sample each run away from its corners, and work out which way is OUT by the same test
        /// the plan used: the side with no deck on it.</summary>
        static List<Probe> Probes(List<Vector2[]> runs, List<QuayDeck> decks)
        {
            var probes = new List<Probe>();
            foreach (Vector2[] run in runs)
            {
                for (int i = 0; i + 1 < run.Length; i++)
                {
                    Vector2 a = run[i], b = run[i + 1];
                    if (Vector2.Distance(a, b) < 1f) continue;      // a stub has no middle to stand at
                    Vector2 along = (b - a).normalized;
                    Vector2 n = new Vector2(along.y, -along.x);
                    Vector2 mid = (a + b) * 0.5f;

                    bool outIsN = !decks.Any(d => StandablePlatform.Contains(d.Footprint, mid + n * 1.5f));
                    bool outIsMinusN = !decks.Any(d => StandablePlatform.Contains(d.Footprint, mid - n * 1.5f));
                    if (outIsN == outIsMinusN) continue;            // ambiguous: skip rather than guess

                    Vector2 outward = outIsN ? n : -n;
                    foreach (float t in new[] { 0.25f, 0.5f, 0.75f })
                        probes.Add(new Probe { On = Vector2.Lerp(a, b, t), Outward = outward });
                }
            }
            return probes;
        }

        /// <summary>The spot with the most wall around it — the furthest any probe is from the nearest
        /// OTHER stretch of wall. Where a gate can be cut and still be a gate.</summary>
        static Probe Roomiest(List<Probe> spots, List<Vector2[]> runs)
        {
            Probe best = spots[0];
            float most = -1f;
            foreach (Probe p in spots)
            {
                float nearestOther = float.PositiveInfinity;
                foreach (Vector2[] run in runs)
                    for (int i = 0; i + 1 < run.Length; i++)
                    {
                        float d = GroundPolygon.DistanceToSegment(p.On, run[i], run[i + 1]);
                        if (d > 0.01f) nearestOther = Mathf.Min(nearestOther, d);   // skip its own segment
                    }
                if (nearestOther > most) { most = nearestOther; best = p; }
            }
            return best;
        }

        /// <summary>How far past the wall line her foot ended up, in metres. Negative is inboard.</summary>
        static float PastTheLine(Rigidbody2D rb, Probe p) => Vector2.Dot(FootOf(rb) - p.On, p.Outward);

        /// <summary>Where she starts: clear of the wall's inner face by her own radius and a hand's
        /// width, so the run begins with a real approach rather than already touching.</summary>
        static float StartInset => PropertyBoundary.DefaultThicknessMetres * 0.5f
                                 + QuayEdgePlan.WalkerFootRadiusMetres + 0.15f;

        // =============================================================================================
        //  the acceptance
        // =============================================================================================

        [UnityTest]
        public IEnumerator SheIsHeldAtEveryGuardedEdgeOfTheQuay()
        {
            yield return LoadTheCreek();

            List<QuayDeck> decks = QuayDecks();
            Assert.GreaterOrEqual(decks.Count, 2,
                $"'{SceneName}' has {decks.Count} quay platform(s) under '{QuayIdPrefix}'. The wharf is " +
                "two walls; with fewer than two this fixture would push her at half a quay and call it " +
                "covered. Re-bank the scene from the builder.");

            PropertyBoundary boundary = Wall(decks, out List<Vector2[]> runs);
            Assert.Greater(boundary.SegmentCount, 0, "the plan built no wall in the real region.");

            List<Probe> probes = Probes(runs, decks);
            Assert.GreaterOrEqual(probes.Count, 6,
                $"only {probes.Count} places to push at on a quay whose water faces run to about 180 m — " +
                "the plan or the sampler has collapsed and a green here would mean nothing.");

            var worst = new List<string>();
            foreach (Probe p in probes)
            {
                Rigidbody2D rb = Walker(p.On - p.Outward * StartInset);
                yield return Push(rb, p.Outward);

                float past = PastTheLine(rb, p);
                worst.Add($"({p.On.x:0.#}, {p.On.y:0.#}) -> {past:+0.00;-0.00}");
                Assert.Less(past, HeldMarginMetres,
                    $"SHE WALKED OFF THE QUAY at ({p.On.x:0.#}, {p.On.y:0.#}): pushed outward along " +
                    $"({p.Outward.x:0.#}, {p.Outward.y:0.#}) at {PushSpeed:0.#} m/s for {PushSteps} " +
                    $"physics steps she ended {past:0.00} m past the wall line. This is the owner's " +
                    "2026-08-19 defect, in a test. Readings so far: " + string.Join(" | ", worst));

                Object.Destroy(rb.gameObject);
                _spawned.Remove(rb.gameObject);
                yield return null;
            }

            Debug.Log($"[NineMileCreekWallPlayTests] held at all {probes.Count} edges " +
                      $"({QuayEdgePlan.TotalLength(runs):0.#} m of wall): " + string.Join(" | ", worst));
        }

        /// <summary>
        /// ⚠️ THE SABOTAGE ARM, and it is what makes the acceptance mean anything. A working wharf is full
        /// of colliders — buildings, armour, moored hulls — so "she stopped" is evidence only if she would
        /// NOT have stopped with the wall taken away. Take it away and she must go over the edge at most
        /// of them.
        /// </summary>
        [UnityTest]
        public IEnumerator Sabotage_WithTheWallGone_SheWalksStraightOff()
        {
            yield return LoadTheCreek();

            List<QuayDeck> decks = QuayDecks();
            Assume.That(decks.Count, Is.GreaterThanOrEqualTo(2));

            PropertyBoundary boundary = Wall(decks, out List<Vector2[]> runs);
            List<Probe> probes = Probes(runs, decks);
            Assume.That(probes.Count, Is.GreaterThanOrEqualTo(6));

            Object.Destroy(boundary.gameObject);       // the wall goes away; everything else stays
            _spawned.Remove(boundary.gameObject);
            yield return null;

            int through = 0;
            var readings = new List<string>();
            foreach (Probe p in probes)
            {
                Rigidbody2D rb = Walker(p.On - p.Outward * StartInset);
                yield return Push(rb, p.Outward);

                float past = PastTheLine(rb, p);
                readings.Add($"({p.On.x:0.#}, {p.On.y:0.#}) -> {past:+0.00;-0.00}");
                if (past > HeldMarginMetres) through++;

                Object.Destroy(rb.gameObject);
                _spawned.Remove(rb.gameObject);
                yield return null;
            }

            Assert.Greater(through * 2, probes.Count,
                $"SABOTAGE NOT DETECTED: with the wall DELETED she still stopped at " +
                $"{probes.Count - through} of {probes.Count} edges, so most of what the acceptance " +
                "measures is some other collider and not this slice's wall at all. Readings: " +
                string.Join(" | ", readings));

            Debug.Log($"[NineMileCreekWallPlayTests] sabotage arm: {through}/{probes.Count} edges walked " +
                      "off with the wall gone — " + string.Join(" | ", readings));
        }

        // =============================================================================================
        //  …and the ways through it still work
        // =============================================================================================

        /// <summary>How wide a gangway this fixture assumes when the scene has no real one to measure.
        /// The shipped brow is <c>NineMileCreekQuayFace.BakedRigGangwayWidthMetres</c> = 0.95 m, which
        /// lives in an EDITOR assembly this one may not reference — so a metre is declared here and
        /// <c>NineMileCreekWallsTests.TheBrow_HasAGateWideEnoughToWalkThrough</c> is what holds the
        /// shipped number to the table. It is within a hand's width either way, so a gate cut for it is
        /// the same gate.</summary>
        const float AssumedBrowWidthMetres = 1f;

        /// <summary>
        /// <b>A gate on this quay's real wall lets her through it.</b> Preferring the SHIPPED brow when the
        /// scene carries one, and falling back to a gate of the brow's size cut in the middle of the
        /// apron's own east face when it does not.
        ///
        /// <para><b>⚠ The fallback is not a lesser test of the gate — only of the brow's SITING.</b> The
        /// wall it opens is the real one on the real face, and the opening is the same width the plan
        /// gives the brow. What it cannot say is that the hole is where the gangway is; that is
        /// <c>NineMileCreekWallsTests.TheBrow_HasAGateWideEnoughToWalkThrough</c>'s claim, made against
        /// the region's own numbers. The float and its brow shipped in #594, AFTER the last scene bank,
        /// so today the fallback is what runs — and a fixture that merely skipped here would leave the
        /// gate untested in physics at all.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AGateInTheQuaysWall_LetsHerThrough()
        {
            yield return LoadTheCreek();

            List<QuayDeck> decks = QuayDecks();
            Assert.GreaterOrEqual(decks.Count, 2, "the quay is not in this scene to cut a gate in.");

            ITidalTerrain terrain = GameServices.TidalTerrain;
            Assert.IsNotNull(terrain, $"'{SceneName}' registered no ITidalTerrain.");

            Vector2 hinge, outward;
            float clear;
            string what;

            var brow = Object.FindObjectsByType<GangwayPlatform>().FirstOrDefault();
            if (brow != null)
            {
                hinge = brow.ShoreEnd;
                outward = (brow.FloatEnd - brow.ShoreEnd).normalized;
                clear = QuayEdgePlan.GateClearFor(brow.HalfWidthMetres * 2f);
                what = $"the SHIPPED brow at ({hinge.x:0.#}, {hinge.y:0.#}), {brow.RunMetres:0.#} m to the float";
            }
            else
            {
                // No brow in this bank. Cut the same-sized gate in the middle of the longest guarded run
                // there is — a real stretch of the real wall — and prove she gets through THAT.
                List<Vector2[]> ungated = QuayEdgePlan.GuardedRuns(decks, terrain.ElevationAt);
                List<Probe> spots = Probes(ungated, decks);
                Assert.IsNotEmpty(spots, "no guarded edge to cut a gate in.");

                // The roomiest spot on the wall, so what is left either side of the opening is still a
                // wall — a gate cut through most of a short run would prove far less.
                Probe at = Roomiest(spots, ungated);
                hinge = at.On;
                outward = at.Outward;
                clear = QuayEdgePlan.GateClearFor(AssumedBrowWidthMetres);
                what = $"a {clear:0.00} m gate cut in the real wall at ({hinge.x:0.#}, {hinge.y:0.#}) — " +
                       $"'{SceneName}' as banked carries no GangwayPlatform (the brow shipped in #594, " +
                       "after the last bank), so this proves the GATE and not the brow's siting";
            }

            var gates = new List<EdgeGate> { new EdgeGate(hinge, clear, "the brow to the float") };
            var runs = QuayEdgePlan.GuardedRuns(decks, terrain.ElevationAt, gates);
            var go = new GameObject("QuayWalls_Gated");
            _spawned.Add(go);
            go.AddComponent<PropertyBoundary>()
              .Configure(runs, PropertyBoundary.DefaultThicknessMetres, "the quay edge, gated, under test");

            Rigidbody2D rb = Walker(hinge - outward * StartInset);
            yield return Push(rb, outward);

            float past = Vector2.Dot(FootOf(rb) - hinge, outward);
            Assert.Greater(past, 1f,
                $"the gate did not let her through: she got {past:0.00} m past {what}. A wall across the " +
                "gangway strands the float and every small craft on it. She is " +
                $"{QuayEdgePlan.WalkerFootRadiusMetres * 2f:0.00} m across and the opening is " +
                $"{clear:0.00} m — if that is genuinely too tight, widen GateClearFor rather than " +
                "deleting the wall.");

            Debug.Log($"[NineMileCreekWallPlayTests] through {what}: {past:0.00} m past the hinge.");
        }

        [UnityTest]
        public IEnumerator TheLadder_IsStillInReachFromInsideTheWall()
        {
            yield return LoadTheCreek();

            var ladder = Object.FindObjectsByType<MonoBehaviour>()
                               .OfType<IBoardingLadder>()
                               .FirstOrDefault();
            Assert.IsNotNull(ladder,
                $"'{SceneName}' has no IBoardingLadder, so there is no boarding point to prove passable.");

            List<QuayDeck> decks = QuayDecks();
            Assume.That(decks.Count, Is.GreaterThanOrEqualTo(2));
            Wall(decks, out _);

            // ⚠ THE LADDER GETS NO GATE, ON PURPOSE — a hole at the lip is the 4.6 m fall this slice
            // exists to close. It needs none: the climb is a scripted move that starts from wherever she
            // is STANDING, so what has to be true is that she can stand somewhere the wall allows and
            // still be within reach. She is walked at the wall, held, and the reach is measured from
            // wherever the physics actually leaves her — not from a point picked to pass.
            Vector2 at = ladder.WorldPosition;
            Vector2 outward = new Vector2(Mathf.Sin(ladder.FaceHeadingDegrees * Mathf.Deg2Rad),
                                          Mathf.Cos(ladder.FaceHeadingDegrees * Mathf.Deg2Rad));

            Rigidbody2D rb = Walker(at - outward * StartInset);
            yield return Push(rb, outward);

            float reach = Vector2.Distance(FootOf(rb), at);
            float allowed = GameServices.LadderBoarding.LadderReachMetres;

            Assert.Less(Vector2.Dot(FootOf(rb) - at, outward), HeldMarginMetres,
                "the lip at the ladder let her through — the ladder must be walled like the rest of the " +
                "face; boarding does not need a hole and a hole there is a fall.");
            Assert.Less(reach, allowed,
                $"pressed against the wall at the ladder she is {reach:0.00} m from it, outside the " +
                $"{allowed:0.#} m the boarding move reaches. The wall HAS closed the boarding point and " +
                "the ladder needs a gate after all — size it with QuayEdgePlan.GateClearFor.");

            Debug.Log($"[NineMileCreekWallPlayTests] held at the ladder and still {reach:0.00} m from it, " +
                      $"inside the {allowed:0.#} m boarding reach.");
        }
    }
}
