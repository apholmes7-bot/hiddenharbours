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
    /// <para><b>The edges are the REGION's, not this fixture's.</b> Nothing is restated here: the scene's
    /// own quay walls give the lines she is pushed at, the scene's own quay platforms give the deck height,
    /// and the scene's own <see cref="ITidalTerrain"/> says which side of each line is the drop, by
    /// <see cref="QuayEdgePlan.IsFallEdge"/> — the rule the plan walls a quay by. Re-site the wharf and
    /// this fixture re-aims itself.</para>
    ///
    /// <para><b>⚠ SINCE THE G3 ACTIVATION THE QUAY IS GROUND, AND THE WALLS ARE THE SCENE'S.</b> N1 step
    /// 05 (<c>05-wharf-support-g3</c>) carries the quay in the creek's G3 heightfield, leaves its two
    /// platforms as quarter-metre caps along the faces, and re-lines the scene's own quay boundary on the
    /// G3 quay line: the crane face, the step and the north face, cut at the brow. Before it, this fixture
    /// handed the platforms' decks to <see cref="QuayEdgePlan"/> and built its own wall; there are no
    /// decks to hand it now, and the wall the player meets is the scene's. So the fixture reads that wall
    /// off the quay's own root, and FAILS, never skips, when it is not there — hunting for a wall and
    /// skipping when it is absent would make it green on the very build it exists to catch.</para>
    ///
    /// <para><b>⚠ A wall must stand on a face, and every face must have its wall.</b> Each wall segment is
    /// asked the fall rule both ways: no fall on either side is a wall where no face is, a fall on both is
    /// a pier, and either fails by name. And every step of a cap's face that IS a fall must lie under a
    /// wall, or inside the brow's gate — the one hole the plan allows. A cap's quarter-metre ends are
    /// stubs, as they are to the probes: the rule's two-metre reach there reads the quay's own skirt (at
    /// the east end, a metre over two of slope), not a face.</para>
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

        /// <summary>How close to a ladder's foot the quay's wall line must run (m) for the ladder to be the
        /// quay's: the ladders hang off the face, so their feet stand on its line.</summary>
        const float LadderOnTheWallMetres = 1f;

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

        /// <summary>The quay's platforms, read off the scene: since step 05, two quarter-metre caps along
        /// the faces at the deck's height. The float and the brow are excluded by id: their decks ride the
        /// tide a foot over the water, so stepping off one is a wade, which the wading model already
        /// owns.</summary>
        static List<StandablePlatform> QuayCaps()
        {
            return Object.FindObjectsByType<StandablePlatform>()
                         .Where(p => p.Id != null && p.Id.StartsWith(QuayIdPrefix)
                                     && p.Id != FloatId && p.Id != GangwayId)
                         .OrderBy(p => p.Id)
                         .ToList();
        }

        static List<StandablePlatform> RequireCaps()
        {
            List<StandablePlatform> caps = QuayCaps();
            Assert.GreaterOrEqual(caps.Count, 2,
                $"'{SceneName}' has {caps.Count} quay platform(s) under '{QuayIdPrefix}'. The wharf is two " +
                "faces; with fewer than two this fixture would push her at half a quay and call it covered.");
            return caps;
        }

        static List<QuayDeck> Decks(List<StandablePlatform> caps) =>
            caps.Select(p => new QuayDeck(p.Footprint, p.DeckElevation)).ToList();

        /// <summary>The quay's own walls: the boundary on the quay's root, the same root its platforms hang
        /// on. Exactly one, with segments, or this fixture is measuring nothing.</summary>
        static PropertyBoundary QuayWalls(List<StandablePlatform> caps)
        {
            Transform root = caps[0].transform.parent;
            List<PropertyBoundary> walls = Object.FindObjectsByType<PropertyBoundary>()
                                                 .Where(b => b.transform.parent == root)
                                                 .ToList();
            Assert.AreEqual(1, walls.Count,
                $"'{SceneName}' has {walls.Count} boundaries on the quay's root " +
                $"'{(root != null ? root.name : "(none)")}'. Since step 05 the quay's walls are the scene's own, " +
                "re-lined on the G3 quay line; without them this fixture would push her at nothing.");
            Assert.Greater(walls[0].SegmentCount, 0, "the quay's boundary carries no wall.");
            return walls[0];
        }

        static System.Func<Vector2, float> Ground()
        {
            ITidalTerrain terrain = GameServices.TidalTerrain;
            Assert.IsNotNull(terrain, $"'{SceneName}' registered no ITidalTerrain, so which side of a wall is " +
                                      "a fall cannot be asked and this fixture is measuring nothing.");
            return terrain.ElevationAt;
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
        //  where she is pushed: the scene's own walls, sampled clear of their corners
        // =============================================================================================

        struct Probe
        {
            public Vector2 On;         // a point on the wall line
            public Vector2 Outward;    // the way the water is
        }

        /// <summary>Sample each wall segment away from its corners, and find which way is OUT by the rule
        /// the plan walls a quay by: the side whose ground falls a metre below the deck. A segment that
        /// falls neither way, or both, stands off its face and is named in <paramref name="offFace"/>.</summary>
        static List<Probe> Probes(List<Vector2[]> runs, List<QuayDeck> decks, System.Func<Vector2, float> ground,
                                  List<string> offFace)
        {
            float deck = decks.Max(d => d.Elevation);
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

                    bool fallsN = QuayEdgePlan.IsFallEdge(decks, deck, mid, n, ground);
                    bool fallsMinusN = QuayEdgePlan.IsFallEdge(decks, deck, mid, -n, ground);
                    if (fallsN == fallsMinusN)
                    {
                        offFace.Add($"({a.x:0.#}, {a.y:0.#})-({b.x:0.#}, {b.y:0.#}) falls " +
                                    (fallsN ? "both ways" : "neither way"));
                        continue;
                    }

                    Vector2 outward = fallsN ? n : -n;
                    foreach (float t in new[] { 0.25f, 0.5f, 0.75f })
                        probes.Add(new Probe { On = Vector2.Lerp(a, b, t), Outward = outward });
                }
            }
            return probes;
        }

        /// <summary>Every <see cref="QuayEdgePlan.WalkStepMetres"/> of a cap's face that is a fall with no
        /// wall over it — a place she could step off. The brow is excused, as wide a gate as the plan would
        /// ever cut for it; a cap's quarter-metre ends are stubs and are not walked.</summary>
        static List<string> UnwalledFalls(List<QuayDeck> decks, System.Func<Vector2, float> ground,
                                          List<Vector2[]> runs, float halfThickness, GangwayPlatform brow)
        {
            var open = new List<string>();
            foreach (QuayDeck cap in decks)
            {
                Rect r = cap.Footprint;
                var edges = new (Vector2 a, Vector2 b, Vector2 outward)[]
                {
                    (new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), Vector2.down),
                    (new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), Vector2.up),
                    (new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), Vector2.left),
                    (new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), Vector2.right),
                };
                foreach (var (a, b, outward) in edges)
                {
                    float length = Vector2.Distance(a, b);
                    if (length < 1f) continue;                      // an end, not a face
                    int steps = Mathf.RoundToInt(length / QuayEdgePlan.WalkStepMetres);
                    for (int s = 0; s <= steps; s++)
                    {
                        Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                        if (!QuayEdgePlan.IsFallEdge(decks, cap.Elevation, p, outward, ground)) continue;
                        if (DistanceToWall(p, runs) <= halfThickness + 0.01f) continue;
                        if (brow != null && InTheBrowsGate(p, brow)) continue;
                        open.Add($"({p.x:0.0}, {p.y:0.0})");
                    }
                }
            }
            return open;
        }

        static float DistanceToWall(Vector2 p, List<Vector2[]> runs)
        {
            float nearest = float.PositiveInfinity;
            foreach (Vector2[] run in runs)
                for (int i = 0; i + 1 < run.Length; i++)
                    nearest = Mathf.Min(nearest, GroundPolygon.DistanceToSegment(p, run[i], run[i + 1]));
            return nearest;
        }

        /// <summary>Is <paramref name="p"/> inside the widest gate the plan would cut for the brow: within
        /// half of <see cref="QuayEdgePlan.GateClearFor"/> its width of the brow's centreline?</summary>
        static bool InTheBrowsGate(Vector2 p, GangwayPlatform brow)
        {
            Vector2 axis = (brow.FloatEnd - brow.ShoreEnd).normalized;
            Vector2 rel = p - brow.ShoreEnd;
            float across = Mathf.Abs(rel.x * axis.y - rel.y * axis.x);
            return across <= QuayEdgePlan.GateClearFor(brow.HalfWidthMetres * 2f) * 0.5f;
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

            List<StandablePlatform> caps = RequireCaps();
            List<QuayDeck> decks = Decks(caps);
            System.Func<Vector2, float> ground = Ground();
            PropertyBoundary walls = QuayWalls(caps);
            List<Vector2[]> runs = walls.WorldRuns();

            var offFace = new List<string>();
            List<Probe> probes = Probes(runs, decks, ground, offFace);
            Assert.IsEmpty(offFace, "a quay wall stands off its face: " + string.Join(" | ", offFace));
            Assert.GreaterOrEqual(probes.Count, 6,
                $"only {probes.Count} places to push at on a quay whose walls run to about 170 m — the walls " +
                "or the sampler have collapsed and a green here would mean nothing.");

            List<string> open = UnwalledFalls(decks, ground, runs, walls.ThicknessMetres * 0.5f,
                                              Object.FindObjectsByType<GangwayPlatform>().FirstOrDefault());
            Assert.IsEmpty(open,
                $"{open.Count} point(s) of the quay's faces drop to the basin with no wall over them and no brow " +
                "— the walk off the quay is open there: " + string.Join(" ", open.Take(12)));

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

            List<StandablePlatform> caps = QuayCaps();
            Assume.That(caps.Count, Is.GreaterThanOrEqualTo(2));

            List<Probe> probes = Probes(QuayWalls(caps).WorldRuns(), Decks(caps), Ground(), new List<string>());
            Assume.That(probes.Count, Is.GreaterThanOrEqualTo(6));

            // EVERY wall in the scene goes: the quay's own and every yard's fence. The question this arm
            // asks is "is it a PropertyBoundary holding her, or something else?", so all of them go;
            // everything that is not a wall stays.
            foreach (PropertyBoundary wall in
                     Object.FindObjectsByType<PropertyBoundary>(FindObjectsInactive.Include))
                Object.Destroy(wall.gameObject);
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
                "measures is some other collider and not the quay's wall at all. Readings: " +
                string.Join(" | ", readings));

            Debug.Log($"[NineMileCreekWallPlayTests] sabotage arm: {through}/{probes.Count} edges walked " +
                      "off with the wall gone — " + string.Join(" | ", readings));
        }

        // =============================================================================================
        //  …and the ways through it still work
        // =============================================================================================

        /// <summary>
        /// <b>The brow's gate in the quay's real wall lets her through it.</b> Since step 05 the scene
        /// carries both the brow and the wall cut for it, so this is the SHIPPED opening at the shipped
        /// brow — no longer a gate the fixture cuts. Its width is held by the scene's own numbers in
        /// EditMode (<c>NineMileCreekWallsTests</c>, <c>NineMileCreekSceneQuayTests</c>); here she is pushed
        /// from inside the wall along the brow and must come out more than a metre past its hinge.
        /// </summary>
        [UnityTest]
        public IEnumerator AGateInTheQuaysWall_LetsHerThrough()
        {
            yield return LoadTheCreek();

            List<StandablePlatform> caps = QuayCaps();
            Assert.GreaterOrEqual(caps.Count, 2, "the quay is not in this scene to cut a gate in.");
            PropertyBoundary walls = QuayWalls(caps);

            var brow = Object.FindObjectsByType<GangwayPlatform>().FirstOrDefault();
            Assert.IsNotNull(brow, $"'{SceneName}' carries no GangwayPlatform: the quay's one gate is the " +
                                   "brow's, and with no brow there is no way through the wall to the float.");

            Vector2 hinge = brow.ShoreEnd;
            Vector2 outward = (brow.FloatEnd - brow.ShoreEnd).normalized;
            string what = $"the SHIPPED brow at ({hinge.x:0.#}, {hinge.y:0.#}), {brow.RunMetres:0.#} m to the float";

            Rigidbody2D rb = Walker(hinge - outward * StartInset);
            yield return Push(rb, outward);

            float past = Vector2.Dot(FootOf(rb) - hinge, outward);
            Assert.Greater(past, 1f,
                $"the gate did not let her through: she got {past:0.00} m past {what}, through " +
                $"'{walls.name}'. A wall across the gangway strands the float and every small craft on it. " +
                $"She is {QuayEdgePlan.WalkerFootRadiusMetres * 2f:0.00} m across — if the opening is " +
                "genuinely too tight, widen it rather than deleting the wall.");

            Debug.Log($"[NineMileCreekWallPlayTests] through {what}: {past:0.00} m past the hinge.");
        }

        [UnityTest]
        public IEnumerator TheLadder_IsStillInReachFromInsideTheWall()
        {
            yield return LoadTheCreek();

            List<StandablePlatform> caps = QuayCaps();
            Assume.That(caps.Count, Is.GreaterThanOrEqualTo(2));
            List<Vector2[]> runs = QuayWalls(caps).WorldRuns();

            List<IBoardingLadder> ladders = Object.FindObjectsByType<MonoBehaviour>()
                                                  .OfType<IBoardingLadder>()
                                                  .Where(l => DistanceToWall(l.WorldPosition, runs) <= LadderOnTheWallMetres)
                                                  .OrderBy(l => l.WorldPosition.x)
                                                  .ToList();
            Assert.IsNotEmpty(ladders,
                $"'{SceneName}' has no IBoardingLadder on the quay's wall, so there is no boarding point to " +
                "prove passable.");

            float allowed = GameServices.LadderBoarding.LadderReachMetres;
            var readings = new List<string>();
            foreach (IBoardingLadder ladder in ladders)
            {
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
                Assert.Less(Vector2.Dot(FootOf(rb) - at, outward), HeldMarginMetres,
                    $"the lip at the ladder at ({at.x:0.#}, {at.y:0.#}) let her through — the ladder must be " +
                    "walled like the rest of the face; boarding does not need a hole and a hole there is a fall.");
                Assert.Less(reach, allowed,
                    $"pressed against the wall at the ladder at ({at.x:0.#}, {at.y:0.#}) she is {reach:0.00} m " +
                    $"from it, outside the {allowed:0.#} m the boarding move reaches. The wall HAS closed the " +
                    "boarding point and the ladder needs a gate after all — size it with QuayEdgePlan.GateClearFor.");
                readings.Add($"({at.x:0.#}, {at.y:0.#}) {reach:0.00} m");

                Object.Destroy(rb.gameObject);
                _spawned.Remove(rb.gameObject);
                yield return null;
            }

            Debug.Log($"[NineMileCreekWallPlayTests] held at {ladders.Count} ladder(s) and still within the " +
                      $"{allowed:0.#} m boarding reach: " + string.Join(" | ", readings));
        }
    }
}
