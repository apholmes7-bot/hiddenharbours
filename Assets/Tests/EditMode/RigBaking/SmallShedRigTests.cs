using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;               // StowageAccepts
using HiddenHarbours.Player;             // StowageDef
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE CAMPER LOT'S SMALL SHED: does it fit its ground, and can the player use it?</b>
    ///
    /// <para><c>smallShedRig.js</c> is the shed outside and in: walls, roof, door, window, an upright
    /// rack for 8 rods and a 4-bay, 3-shelf gear store. Its own <c>validate()</c> checks the same
    /// rules, so apart from asking it once, every geometric check below reads the raw numbers out of
    /// the rig and re-measures them here, so a rig that grades its own homework wrongly still fails.</para>
    ///
    /// <para>Where it stands on the lot, and whether that ground is dry, is checked against
    /// <c>StPetersCamperLot</c> in <c>StPetersLotShedTests</c> (this assembly does not see the St
    /// Peters editor code). No bake is involved: the shed is not in <see cref="RigCatalog"/> yet.</para>
    /// </summary>
    public class SmallShedRigTests
    {
        const string Rig = "docs/art/rigs/small-shed-kit/smallShedRig.js";
        const string Sidecar = "docs/art/rigs/small-shed-kit/smallShedRig.lot_shed.gameplay.json";
        const string Global = "SmallShed";
        const string V = "{variant:'lot_shed'}";

        // the player as the game builds him (PersistentCoreBuilder: CircleCollider2D radius 0.35 m)
        const float FootRadius = 0.35f;
        const float ReachClear = 0.20f;
        const float ReachSpacing = 0.25f;
        const float InteractReach = 0.25f;
        const float Eps = 1e-3f;

        // the owner's ask (2026-11-17): the shed holds more rods and more gear; the seat sized it
        const int RodsWanted = 8;
        const int GearWanted = 12;

        static string Full(string repoRelative) =>
            Path.Combine(RigCatalog.RepoRoot, repoRelative.Replace('/', Path.DirectorySeparatorChar));

        static IRigScriptHost Host()
        {
            IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Full(Rig)));
            return host;
        }

        // ---------------------------------------------------------------------------------------------
        //  raw numbers out of the rig
        // ---------------------------------------------------------------------------------------------

        struct Rect
        {
            public string Id, Kind, Wall;
            public float X0, Y0, X1, Y1, H;
            public Rect(float x0, float y0, float x1, float y1) { Id = Kind = Wall = ""; X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; H = 0; }
        }

        struct Anchor
        {
            public string Id, Action, Level;
            public Vector2 Reach;
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        static float N(IRigScriptHost h, string expr) => (float)h.EvaluateNumber(expr);
        static Vector2 P2(IRigScriptHost h, string expr) => new Vector2(N(h, $"({expr})[0]"), N(h, $"({expr})[1]"));

        static List<Vector2> Poly(IRigScriptHost host, string expr) =>
            host.EvaluateString($"({expr}).map(p=>p[0]+','+p[1]).join(';')").Split(';')
                .Select(p => p.Split(',')).Select(p => new Vector2(F(p[0]), F(p[1]))).ToList();

        static List<Rect> Fixtures(IRigScriptHost host) =>
            host.EvaluateString($"{Global}.walkable({V}).floor.obstructions.map(o=>[o.id,o.kind,o.wall,o.footprint[0][0],o.footprint[0][1]," +
                                "o.footprint[2][0],o.footprint[2][1],o.height_above_floor_m].join(',')).join(';')")
                .Split(';').Select(r => r.Split(',')).Select(r => new Rect(F(r[3]), F(r[4]), F(r[5]), F(r[6]))
                {
                    Id = r[0], Kind = r[1], Wall = r[2], H = F(r[7]),
                }).ToList();

        static List<Anchor> Anchors(IRigScriptHost host) =>
            host.EvaluateString($"{Global}.interactables({V}).map(a=>[a.id,a.action,a.level,a.reach_point[0],a.reach_point[1]].join(',')).join(';')")
                .Split(';').Select(r => r.Split(',')).Select(r => new Anchor
                {
                    Id = r[0], Action = r[1], Level = r[2], Reach = new Vector2(F(r[3]), F(r[4])),
                }).ToList();

        static List<Vector2> Floor(IRigScriptHost h) => Poly(h, $"{Global}.walkable({V}).floor.polygon");

        static List<Vector3> Points3(IRigScriptHost h, string expr) =>
            h.EvaluateString($"({expr}).map(p=>p[0]+','+p[1]+','+p[2]).join(';')").Split(';')
                .Select(p => p.Split(',')).Select(p => new Vector3(F(p[0]), F(p[1]), F(p[2]))).ToList();

        // ---------------------------------------------------------------------------------------------
        //  plane geometry, independent of the rig's own helpers
        // ---------------------------------------------------------------------------------------------

        static bool Inside(Vector2 p, List<Vector2> poly)
        {
            bool c = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) c = !c;
            }
            return c;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + t * ab);
        }

        static float EdgeDistance(Vector2 p, List<Vector2> poly)
        {
            float m = float.MaxValue;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) m = Mathf.Min(m, SegmentDistance(p, poly[j], poly[i]));
            return m;
        }

        static float Distance(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(0f, Mathf.Max(r.X0 - p.x, p.x - r.X1));
            float dy = Mathf.Max(0f, Mathf.Max(r.Y0 - p.y, p.y - r.Y1));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static IEnumerable<Vector2> Corners(Rect r)
        {
            yield return new Vector2(r.X0, r.Y0);
            yield return new Vector2(r.X1, r.Y0);
            yield return new Vector2(r.X1, r.Y1);
            yield return new Vector2(r.X0, r.Y1);
        }

        static bool OnOrInside(Vector2 p, List<Vector2> poly) => Inside(p, poly) || EdgeDistance(p, poly) < 0.02f;

        static string Lf256(string repoRelative)
        {
            byte[] raw = File.ReadAllBytes(Full(repoRelative));
            var lf = new List<byte>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == (byte)'\r' && i + 1 < raw.Length && raw[i + 1] == (byte)'\n') continue;
                lf.Add(raw[i]);
            }
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(lf.ToArray()).Select(b => b.ToString("x2")));
        }

        // =============================================================================================
        //  1. THE RIG AND ITS SIDECAR
        // =============================================================================================

        [Test]
        public void TheRig_LoadsOnItsOwn_AndPassesItsOwnFit()
        {
            using IRigScriptHost host = Host();
            Assert.That(host.EvaluateBool($"typeof {Global} === 'object' && {Global} !== null"), Is.True,
                "smallShedRig.js ran but did not install globalThis.SmallShed");
            Assert.AreEqual("lot_shed", host.EvaluateString($"{Global}.list().join(' ')"));
            Assert.AreEqual(32, N(host, $"{Global}.PX"), "the fleet's scale is 32 px = 1 m");
            Assert.AreEqual(8, N(host, $"{Global}.DIRS"));
            Assert.AreEqual(0, N(host, $"{Global}.validate({V}).length"),
                "the rig's own fit check fails: " + host.EvaluateString($"{Global}.validate({V}).join(' | ')"));
            Assert.That(File.ReadAllText(Full(Rig)), Does.Not.Contain("rollA"),
                "HullMeshFleetTests treats any rig containing that token as a hull");
        }

        [Test]
        public void TheCommittedSidecar_IsWhatTheRigGenerates()
        {
            using IRigScriptHost host = Host();
            host.Execute("var __sc = " + File.ReadAllText(Full(Sidecar)) + ";");
            string committed = host.EvaluateString(
                "(function(){ const o=Object.assign({},__sc); delete o.derivedFromRigSha256; return JSON.stringify(o); })()");
            string generated = host.EvaluateString($"JSON.stringify({Global}.sidecar({V}))");
            Assert.AreEqual(generated, committed,
                $"{Sidecar} has drifted from smallShedRig.js. Re-run node tools/rig-recipes/small-shed.mjs");
        }

        [Test]
        public void TheSidecarStamp_IsTheRigBytes()
        {
            using IRigScriptHost host = Host();
            host.Execute("var __sc = " + File.ReadAllText(Full(Sidecar)) + ";");
            Assert.AreEqual(Lf256(Rig), host.EvaluateString("__sc.derivedFromRigSha256"),
                "the shed rig moved without its sidecar being regenerated");
        }

        // =============================================================================================
        //  2. THE GROUND IT TAKES
        // =============================================================================================

        [Test]
        public void ThePlan_EavesIncluded_FitsTheReservedGround()
        {
            using IRigScriptHost host = Host();
            float len = N(host, $"{Global}.BODY.len"), dep = N(host, $"{Global}.BODY.dep");
            float rake = N(host, $"{Global}.BODY.rake"), eave = N(host, $"{Global}.BODY.eave");
            float along = N(host, $"{Global}.RESERVE.along"), across = N(host, $"{Global}.RESERVE.across");
            float radius = N(host, $"{Global}.RESERVE.radius");

            // the lot's numbers (StPetersLotShedTests holds these against StPetersCamperLot itself)
            Assert.AreEqual(2.4f, along, Eps, "the reserved footprint along the camper moved");
            Assert.AreEqual(1.8f, across, Eps, "the reserved footprint across the camper moved");
            Assert.AreEqual(new Vector2(along, across).magnitude * 0.5f, radius, Eps, "the reserve circle is not the footprint's half-diagonal");

            float planX = len + 2f * rake, planY = dep + 2f * eave;
            Assert.LessOrEqual(planX, along + Eps, $"the shed's plan is {planX:0.00} m along, the lot reserves {along:0.00}");
            Assert.LessOrEqual(planY, across + Eps, $"the shed's plan is {planY:0.00} m across, the lot reserves {across:0.00}");
            Assert.LessOrEqual(new Vector2(planX, planY).magnitude * 0.5f, radius + Eps, "the eave corners leave the reserve circle");
            Assert.Greater(len, 2f * FootRadius + 1f, "walls that short leave no room to stand");

            Assert.AreEqual(planX, N(host, $"{Global}.planExtent().x1 - {Global}.planExtent().x0"), Eps, "planExtent disagrees with BODY");
            Assert.AreEqual(planY, N(host, $"{Global}.planExtent().y1 - {Global}.planExtent().y0"), Eps, "planExtent disagrees with BODY");
        }

        // =============================================================================================
        //  3. EVERY ANCHOR, AND WHAT THE STORES HOLD
        // =============================================================================================

        [Test]
        public void EveryRequiredAnchor_IsPresentWithItsVerb()
        {
            using IRigScriptHost host = Host();
            var byId = Anchors(host).ToDictionary(a => a.Id);
            var want = new Dictionary<string, (string verb, string level)>
            {
                ["door"] = ("step_out", "floor"), ["door_outside"] = ("enter", "ground"),
                ["rod_rack"] = ("storage", "floor"), ["gear_shelf"] = ("storage", "floor"),
            };
            foreach (var kv in want)
            {
                Assert.IsTrue(byId.ContainsKey(kv.Key), $"no '{kv.Key}' interactable");
                Assert.AreEqual(kv.Value.verb, byId[kv.Key].Action, $"'{kv.Key}' has the wrong verb");
                Assert.AreEqual(kv.Value.level, byId[kv.Key].Level, $"'{kv.Key}' is on the wrong level");
            }
            host.Execute("var __sc = " + File.ReadAllText(Full(Sidecar)) + ";");
            Assert.AreEqual(FootRadius, N(host, "__sc.STAND.radius_m"), Eps, "the stand spot is not sized for his foot");
        }

        [Test]
        public void TheStores_HoldEightRodsAndTwelveGear_AsData()
        {
            using IRigScriptHost host = Host();
            string I(string id, string field) =>
                host.EvaluateString($"String({Global}.interactables({V}).find(a=>a.id==='{id}').{field})");

            Assert.AreEqual(RodsWanted, int.Parse(I("rod_rack", "capacity")), "the shed's rack holds 8 rods");
            Assert.AreEqual(GearWanted, int.Parse(I("gear_shelf", "capacity")), "the shed's shelving holds 12 pieces of gear");
            Assert.AreEqual(RodsWanted, (int)N(host, $"{Global}.slots({V}).rods.length"), "one placed slot per rod");
            Assert.AreEqual(GearWanted, (int)N(host, $"{Global}.slots({V}).gear.length"), "one placed slot per piece of gear");
            Assert.AreEqual("stowage.shed_rod_rack", I("rod_rack", "stowage"));
            Assert.AreEqual("stowage.shed_gear_locker", I("gear_shelf", "stowage"));

            // what each takes uses the game's own flags, and the rod rack takes what the existing rod rack takes
            var rodAccepts = (StowageAccepts)Enum.Parse(typeof(StowageAccepts), I("rod_rack", "accepts"));
            var gearAccepts = (StowageAccepts)Enum.Parse(typeof(StowageAccepts), I("gear_shelf", "accepts"));
            var rack = AssetDatabase.LoadAssetAtPath<StowageDef>("Assets/_Project/Data/Stowage/RodRack.asset");
            Assert.IsNotNull(rack, "the rod rack StowageDef moved");
            Assert.AreEqual(rack.Accepts, rodAccepts, "the shed's rod rack must take what the rod rack Def takes");
            Assert.AreEqual(StowageAccepts.General, gearAccepts, "the gear shelf takes general gear, not rods");

            // the stowage Defs (PR 1307): once they are on main, they must agree with the rig
            var defs = AssetDatabase.FindAssets("t:StowageDef")
                .Select(g => AssetDatabase.LoadAssetAtPath<StowageDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null).ToList();
            foreach (var (id, cap, acc) in new[] { ("stowage.shed_rod_rack", RodsWanted, rodAccepts), ("stowage.shed_gear_locker", GearWanted, gearAccepts) })
            {
                StowageDef d = defs.FirstOrDefault(x => x.Id == id);
                if (d == null) continue;   // not authored yet: the rig carries the number until it is
                Assert.AreEqual(cap, d.Capacity, $"{id}: the Def and the shed rig disagree on capacity");
                Assert.AreEqual(acc, d.Accepts, $"{id}: the Def and the shed rig disagree on what it takes");
            }
        }

        [Test]
        public void EverySlot_SitsOnItsFixture_AndNoTwoShareASpot()
        {
            using IRigScriptHost host = Host();
            var fx = Fixtures(host).ToDictionary(r => r.Id);
            float floorZ = N(host, $"{Global}.BODY.floorZ");
            var rods = Points3(host, $"{Global}.slots({V}).rods.map(s=>s.foot)");
            var gear = Points3(host, $"{Global}.slots({V}).gear.map(s=>s.centre)");
            Rect rr = fx["rod_rack"], gs = fx["gear_shelf"];
            foreach (Vector3 p in rods)
                Assert.AreEqual(0f, Distance(p, rr), Eps, $"rod slot {p} is off the rack");
            foreach (Vector3 p in gear)
            {
                Assert.AreEqual(0f, Distance(p, gs), Eps, $"gear slot {p} is off the shelving");
                Assert.That(p.z, Is.GreaterThan(floorZ).And.LessThanOrEqualTo(floorZ + gs.H), $"gear slot {p} is not on a shelf");
            }
            for (int i = 0; i < rods.Count; i++) for (int j = i + 1; j < rods.Count; j++)
                Assert.Greater(Vector3.Distance(rods[i], rods[j]), 0.05f, "two rods stand in one cup");
            for (int i = 0; i < gear.Count; i++) for (int j = i + 1; j < gear.Count; j++)
                Assert.Greater(Vector3.Distance(gear[i], gear[j]), 0.2f, "two gear slots overlap");
        }

        [Test]
        public void TheRods_StandUprightUnderTheRoof()
        {
            using IRigScriptHost host = Host();
            float plate = N(host, $"{Global}.BODY.plateZ"), dep = N(host, $"{Global}.BODY.dep"), pitch = N(host, $"{Global}.BODY.pitch");
            float rafter = N(host, $"{Global}.BODY.rafter"), floorZ = N(host, $"{Global}.BODY.floorZ");
            float longest = N(host, $"{Global}.STORE.rod_rack.minRod");
            Assert.GreaterOrEqual(longest, 2.10f, "the rack is sized for a 7 ft rod at least");
            foreach (Vector3 tip in Points3(host, $"{Global}.slots({V}).rods.map(s=>s.tip)"))
            {
                float under = plate + (dep * 0.5f - Mathf.Abs(tip.y)) * pitch - rafter;
                Assert.LessOrEqual(tip.z, under + Eps, $"a rod tip at {tip} is in the rafters");
                Assert.GreaterOrEqual(under - floorZ, longest - Eps, $"a {longest:0.00} m rod at y {tip.y:0.00} does not stand under the roof");
            }
        }

        // =============================================================================================
        //  4. EVERYTHING STANDS ON THE FLOOR, APART, AND REACHABLE FROM THE DOOR
        // =============================================================================================

        [Test]
        public void TheFixtures_StandOnTheFloor_AndNoTwoTouch()
        {
            using IRigScriptHost host = Host();
            var floor = Floor(host);
            var fx = Fixtures(host);
            Assert.AreEqual(2, fx.Count, "a rod rack and a gear shelf, nothing else on the floor");
            foreach (Rect r in fx)
                foreach (Vector2 c in Corners(r))
                    Assert.IsTrue(OnOrInside(c, floor), $"{r.Id} corner {c} is off the floor");
            for (int i = 0; i < fx.Count; i++)
            for (int j = i + 1; j < fx.Count; j++)
            {
                float gx = Mathf.Max(fx[i].X0 - fx[j].X1, fx[j].X0 - fx[i].X1), gy = Mathf.Max(fx[i].Y0 - fx[j].Y1, fx[j].Y0 - fx[i].Y1);
                Assert.Greater(Mathf.Max(gx, gy), 0f, $"{fx[i].Id} and {fx[j].Id} share floor");
            }
        }

        [Test]
        public void EveryFloorReachPoint_StandsClearOfWallsAndFixtures()
        {
            using IRigScriptHost host = Host();
            var floor = Floor(host);
            var fx = Fixtures(host);
            var pts = Anchors(host).Where(a => a.Level == "floor").ToList();
            foreach (Anchor a in pts)
            {
                Assert.IsTrue(Inside(a.Reach, floor), $"{a.Id} reach {a.Reach} is off the floor");
                Assert.GreaterOrEqual(EdgeDistance(a.Reach, floor), ReachClear - Eps, $"{a.Id} reach is against a wall");
                foreach (Rect r in fx)
                    Assert.GreaterOrEqual(Distance(a.Reach, r), ReachClear - Eps, $"{a.Id} reach is within {ReachClear} m of {r.Id}");
            }
            for (int i = 0; i < pts.Count; i++)
            for (int j = i + 1; j < pts.Count; j++)
                Assert.GreaterOrEqual(Vector2.Distance(pts[i].Reach, pts[j].Reach), ReachSpacing - Eps,
                    $"{pts[i].Id} and {pts[j].Id} stand on the same spot: one press would pick either");
        }

        [Test]
        public void TheFoot_WalksInFromTheDoor_ToEveryStoreAndTheStandSpot()
        {
            using IRigScriptHost host = Host();
            host.Execute("var __sc = " + File.ReadAllText(Full(Sidecar)) + ";");
            var floor = Floor(host);
            var fx = Fixtures(host);
            Vector2 landing = P2(host, "__sc.THRESHOLD.inside_landing.centre");
            Vector2 stand = P2(host, "__sc.STAND.centre");

            const float step = 0.02f;
            float minX = floor.Min(p => p.x), maxX = floor.Max(p => p.x), minY = floor.Min(p => p.y), maxY = floor.Max(p => p.y);
            int nx = Mathf.CeilToInt((maxX - minX) / step) + 1, ny = Mathf.CeilToInt((maxY - minY) / step) + 1;
            Vector2 At(int i, int j) => new Vector2(minX + i * step, minY + j * step);
            bool Free(Vector2 p) => Inside(p, floor) && EdgeDistance(p, floor) >= FootRadius - 1e-4f && fx.All(r => Distance(p, r) >= FootRadius - 1e-4f);

            var free = new bool[nx, ny];
            for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++) free[i, j] = Free(At(i, j));

            int si = Mathf.RoundToInt((landing.x - minX) / step), sj = Mathf.RoundToInt((landing.y - minY) / step);
            Assert.IsTrue(free[si, sj], $"the foot cannot stand on the inside landing {landing}");
            var seen = new bool[nx, ny];
            var q = new Queue<(int, int)>();
            q.Enqueue((si, sj)); seen[si, sj] = true;
            var reached = new List<Vector2>();
            while (q.Count > 0)
            {
                var (i, j) = q.Dequeue();
                reached.Add(At(i, j));
                foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int a = i + di, b = j + dj;
                    if (a < 0 || b < 0 || a >= nx || b >= ny || seen[a, b] || !free[a, b]) continue;
                    seen[a, b] = true; q.Enqueue((a, b));
                }
            }

            foreach (Anchor an in Anchors(host).Where(a => a.Level == "floor"))
            {
                float best = reached.Min(p => Vector2.Distance(p, an.Reach));
                Assert.LessOrEqual(best, InteractReach + step,
                    $"a 0.35 m foot walking in from the door gets no nearer than {best:0.00} m to '{an.Id}'");
            }
            Assert.LessOrEqual(reached.Min(p => Vector2.Distance(p, stand)), step, "the stand spot cannot be walked to from the door");
            Assert.IsTrue(Free(stand), "the foot does not fit on the stand spot: no room to stand");
        }

        [Test]
        public void TheDoor_LetsHimIn_AndBothLandingsHoldHisFoot()
        {
            using IRigScriptHost host = Host();
            host.Execute("var __sc = " + File.ReadAllText(Full(Sidecar)) + ";");
            float u0 = N(host, "__sc.THRESHOLD.door_clear_m.u0"), u1 = N(host, "__sc.THRESHOLD.door_clear_m.u1");
            Assert.GreaterOrEqual(u1 - u0, 2f * FootRadius, $"the door's {u1 - u0:0.00} m is narrower than his {2 * FootRadius:0.00} m foot");
            float floorZ = N(host, $"{Global}.BODY.floorZ"), plate = N(host, $"{Global}.BODY.plateZ");
            float doorH = N(host, "__sc.THRESHOLD.door_clear_m.height_above_floor");
            Assert.GreaterOrEqual(doorH, 1.80f, "he walks through the door upright");
            Assert.Less(floorZ + doorH, plate, "the door head runs into the wall plate");

            var floor = Floor(host);
            Vector2 inside = P2(host, "__sc.THRESHOLD.inside_landing.centre");
            Assert.IsTrue(Inside(inside, floor));
            Assert.GreaterOrEqual(EdgeDistance(inside, floor), FootRadius - Eps, "the inside landing circle crosses a wall");
            foreach (Rect r in Fixtures(host))
                Assert.GreaterOrEqual(Distance(inside, r), FootRadius, $"the inside landing circle hits {r.Id}");
            Assert.That(inside.x - FootRadius, Is.GreaterThanOrEqualTo(u0 - Eps), "the foot overhangs the door's west jamb");
            Assert.That(inside.x + FootRadius, Is.LessThanOrEqualTo(u1 + Eps), "the foot overhangs the door's east jamb");

            // outside: on the ground, off the walls, off the step, and clear of the leaf at rest
            Vector2 outside = P2(host, "__sc.THRESHOLD.outside_landing.centre");
            float xo = N(host, $"{Global}.BODY.len") * 0.5f, yo = N(host, $"{Global}.BODY.dep") * 0.5f;
            Assert.GreaterOrEqual(Distance(outside, new Rect(-xo, -yo, xo, yo)), FootRadius, "the outside landing is under the walls");
            Assert.Less(outside.y, -yo, "the outside landing is not on the door side");
            Rect stepR = new Rect(N(host, $"{Global}.BODY.step.x0"), N(host, $"{Global}.BODY.step.y0"), N(host, $"{Global}.BODY.step.x1"), N(host, $"{Global}.BODY.step.y1"));
            Assert.GreaterOrEqual(Distance(outside, stepR), FootRadius - Eps, "the outside landing circle sits on the step");
            foreach (string pose in new[] { "shut_plan", "open_plan" })
            {
                var leaf = Poly(host, $"__sc.THRESHOLD.leaf.{pose}");
                Assert.IsFalse(Inside(outside, leaf), $"the outside landing is inside the {pose} leaf");
                Assert.GreaterOrEqual(EdgeDistance(outside, leaf), FootRadius, $"the leaf ({pose}) stands on the outside landing");
            }
            Assert.AreEqual(inside.x, N(host, $"{Global}.interactables({V}).find(a=>a.id==='door').reach_point[0]"), Eps);
            Assert.AreEqual(outside.y, N(host, $"{Global}.interactables({V}).find(a=>a.id==='door_outside').reach_point[1]"), Eps);
        }

        // =============================================================================================
        //  5. THE CUTAWAY
        // =============================================================================================

        [Test]
        public void TheCutaway_AtTheLotFacing_DropsTheRoofAndTheDoorWall_AndKeepsTheBack()
        {
            using IRigScriptHost host = Host();
            int dir = (int)N(host, $"{Global}.lot({V}).rig_dir");
            string kept = $"{Global}.faces({{variant:'lot_shed',door:'open'}}).filter({Global}.cutaway({dir}).keep)";
            Assert.Greater(N(host, $"{Global}.faces({V}).filter(f=>f.lid).length"), 0, "the closed shed has no roof");
            Assert.AreEqual(0, N(host, $"{kept}.filter(f=>f.lid).length"), "the roof survives the cut");
            Assert.AreEqual(0, N(host, $"{kept}.filter(f=>f.seg==='wall_south_high').length"), "the door wall is near at the lot facing and must be sectioned");
            Assert.Greater(N(host, $"{kept}.filter(f=>f.seg==='wall_north_high').length"), 0, "the back wall must stand");
            Assert.Greater(N(host, $"{kept}.filter(f=>f.seg==='wall_south_low').length"), 0, "the door wall keeps its sill");
            Assert.Greater(N(host, $"{kept}.filter(f=>f.seg.indexOf('fx:gear_shelf')===0).length"), 0, "the shelving must show in the room");
        }
    }
}
