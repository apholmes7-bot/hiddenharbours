using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;         // StPetersCamperLot, StPetersGinnyPlot
using HiddenHarbours.Core;               // WashStationDef
using HiddenHarbours.Economy;            // HomeDef
using HiddenHarbours.Player;             // StowageDef, GinnyFreezer
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE CAMPER ROOM: does it fit inside its shell, and can the player use it?</b>
    ///
    /// <para><c>camperInteriorRig.js</c> places the Clipper's fit-out against the shell's published
    /// loft. Its own <c>validate()</c> checks the same rules, so this suite does NOT ask it: every
    /// geometric check below reads the raw numbers out of the rig and re-measures them here in C#,
    /// so a rig that grades its own homework wrongly still goes red.</para>
    ///
    /// <para>No bake is involved: the room is not in <see cref="RigCatalog"/> yet (the bake is the
    /// next slice), so nothing here renders a sheet.</para>
    /// </summary>
    public class CamperInteriorRigTests
    {
        const string ShellRig = DwellingRigFleet.CamperRigPath;
        const string RoomRig = "docs/art/rigs/camper-iso-kit/camperInteriorRig.js";
        const string RoomSidecar = "docs/art/rigs/camper-iso-kit/camperInteriorRig.clipper.interior.json";
        const string ShellSidecar = "docs/art/rigs/gameplay/dwellings/camperIsoRig.clipper.gameplay.json";
        const string Global = "CamperInterior";
        const string V = "{variant:'clipper'}";

        // the player as the game builds him (PersistentCoreBuilder: CircleCollider2D radius 0.35 m)
        const float FootRadius = 0.35f;
        const float ReachClear = 0.20f;
        const float ReachSpacing = 0.25f;
        const float InteractReach = 0.25f;
        const float Eps = 1e-3f;

        static readonly string[] NorthFixtures = { "head_room", "galley_counter", "pantry" };
        static readonly string[] KitchenAndBathAnchors =
            { "galley_sink", "stove", "prep", "fridge", "pantry", "head_basin", "head_toilet", "head_shower" };

        static string Full(string repoRelative) =>
            Path.Combine(RigCatalog.RepoRoot, repoRelative.Replace('/', Path.DirectorySeparatorChar));

        static IRigScriptHost Host()
        {
            IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Full(ShellRig)));
            host.Execute(File.ReadAllText(Full(RoomRig)));
            return host;
        }

        // ---------------------------------------------------------------------------------------------
        //  raw numbers out of the rig
        // ---------------------------------------------------------------------------------------------

        struct Rect
        {
            public string Id, Kind, Wall;
            public float X0, Y0, X1, Y1, H, Bottom;
        }

        struct Anchor
        {
            public string Id, Action, Level;
            public Vector2 Reach;
            public Vector3 At;
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        static List<Vector2> Poly(IRigScriptHost host, string expr) =>
            host.EvaluateString($"({expr}).map(p=>p[0]+','+p[1]).join(';')").Split(';')
                .Select(p => p.Split(',')).Select(p => new Vector2(F(p[0]), F(p[1]))).ToList();

        static List<Rect> Rects(IRigScriptHost host, string expr) =>
            host.EvaluateString($"({expr}).map(o=>[o.id,o.kind,o.wall||'',o.footprint[0][0],o.footprint[0][1]," +
                                "o.footprint[2][0],o.footprint[2][1],o.height_above_sole_m,o.bottom_above_sole_m||0].join(',')).join(';')")
                .Split(';').Select(r => r.Split(',')).Select(r => new Rect
                {
                    Id = r[0], Kind = r[1], Wall = r[2], X0 = F(r[3]), Y0 = F(r[4]), X1 = F(r[5]), Y1 = F(r[6]),
                    H = F(r[7]), Bottom = F(r[8]),
                }).ToList();

        static List<Anchor> Anchors(IRigScriptHost host) =>
            host.EvaluateString($"{Global}.interactables({V}).map(a=>[a.id,a.action,a.level,a.reach_point[0],a.reach_point[1]," +
                                "a.at[0],a.at[1],a.at[2]].join(',')).join(';')")
                .Split(';').Select(r => r.Split(',')).Select(r => new Anchor
                {
                    Id = r[0], Action = r[1], Level = r[2], Reach = new Vector2(F(r[3]), F(r[4])),
                    At = new Vector3(F(r[5]), F(r[6]), F(r[7])),
                }).ToList();

        static List<Vector2> Sole(IRigScriptHost h) => Poly(h, $"{Global}.walkable({V}).saloon.polygon");
        static List<Vector2> HeadSole(IRigScriptHost h) => Poly(h, $"{Global}.walkable({V}).head.polygon");
        static List<Rect> SaloonFixtures(IRigScriptHost h) => Rects(h, $"{Global}.walkable({V}).saloon.obstructions");
        static List<Rect> HeadFixtures(IRigScriptHost h) => Rects(h, $"{Global}.walkable({V}).head.obstructions");

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

        static float EdgeDistance(Vector2 p, List<Vector2> poly)
        {
            float m = float.MaxValue;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vector2 a = poly[j], ab = poly[i] - a;
                float t = ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                m = Mathf.Min(m, Vector2.Distance(p, a + t * ab));
            }
            return m;
        }

        static float Distance(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(0f, Mathf.Max(r.X0 - p.x, p.x - r.X1));
            float dy = Mathf.Max(0f, Mathf.Max(r.Y0 - p.y, p.y - r.Y1));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static float OverlapArea(Rect a, Rect b) =>
            Mathf.Max(0f, Mathf.Min(a.X1, b.X1) - Mathf.Max(a.X0, b.X0)) *
            Mathf.Max(0f, Mathf.Min(a.Y1, b.Y1) - Mathf.Max(a.Y0, b.Y0));

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
        public void TheRoom_RefusesToLoadWithoutItsShell()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            Assert.Throws(Is.InstanceOf<Exception>(), () => host.Execute(File.ReadAllText(Full(RoomRig))),
                "camperInteriorRig.js must throw when CamperIso is absent: a room that loads without its " +
                "shell has re-derived the loft, and that is how a room drifts off its body");
        }

        [Test]
        public void TheCatalog_LoadsTheRoomByKey_ShellFirst_OnTheShellsCell()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigGeometry room = RigCatalog.Install(host, RigCatalog.Get("camperInterior"));
            Assert.IsTrue(host.EvaluateBool("typeof CamperIso === 'object' && CamperIso !== null"),
                "the catalog did not install the shell before the room");
            CollectionAssert.AreEqual(new[] { "camper" }, RigCatalog.Get("camperInterior").Prerequisites);
            Assert.AreEqual(RigCatalog.Get("camper").DeclaredConvention, RigCatalog.Get("camperInterior").DeclaredConvention,
                "the room is drawn by the shell's camera, so it turns the shell's way");

            using IRigScriptHost shellHost = RigScriptHostFactory.Create();
            RigGeometry shell = RigCatalog.Install(shellHost, RigCatalog.Get("camper"));
            Assert.AreEqual(shell.Width, room.Width);
            Assert.AreEqual(shell.Height, room.Height);
            Assert.AreEqual(shell.PivotX, room.PivotX, 1e-9);
            Assert.AreEqual(shell.PivotY, room.PivotY, 1e-9);
            Assert.AreEqual(8, room.NativeDirs);
        }

        [Test]
        public void TheRoom_SharesTheShellsCellPivotAndFacings()
        {
            using IRigScriptHost host = Host();
            Assert.That(host.EvaluateBool($"typeof {Global} === 'object' && {Global} !== null"), Is.True);
            foreach (string k in new[] { "W", "H", "PX", "DIRS", "defaultElev", "pivot.x", "pivot.y" })
                Assert.AreEqual(host.EvaluateNumber("CamperIso." + k), host.EvaluateNumber($"{Global}.{k}"),
                    $"{k} differs from the shell: a baked room would not composite under the exterior 1:1");
            Assert.AreEqual(host.EvaluateString("CamperIso.order.join(' ')"), host.EvaluateString($"{Global}.order.join(' ')"));
            Assert.AreEqual("clipper", host.EvaluateString($"{Global}.list().join(' ')"));
            Assert.AreEqual(0, host.EvaluateNumber($"{Global}.validate({V}).length"),
                "the rig's own fit check fails: " + host.EvaluateString($"{Global}.validate({V}).join(' | ')"));
            Assert.That(File.ReadAllText(Full(RoomRig)), Does.Not.Contain("rollA"),
                "HullMeshFleetTests treats any top-level rig containing that token as a hull");
        }

        [Test]
        public void TheCommittedSidecar_IsWhatTheRigGenerates()
        {
            using IRigScriptHost host = Host();
            host.Execute("var __sc = " + File.ReadAllText(Full(RoomSidecar)) + ";");
            string committed = host.EvaluateString(
                "(function(){ const o=Object.assign({},__sc); delete o.derivedFromRigSha256; delete o.shellRigSha256; return JSON.stringify(o); })()");
            string generated = host.EvaluateString($"JSON.stringify({Global}.sidecar({V}))");
            Assert.AreEqual(generated, committed,
                $"{RoomSidecar} has drifted from camperInteriorRig.js. Re-run node tools/rig-recipes/camper-interior.mjs");
        }

        [Test]
        public void TheSidecarStamps_AreTheRigBytes()
        {
            using IRigScriptHost host = Host();
            host.Execute("var __sc = " + File.ReadAllText(Full(RoomSidecar)) + ";");
            Assert.AreEqual(Lf256(RoomRig), host.EvaluateString("__sc.derivedFromRigSha256"),
                "the room rig moved without its sidecar being regenerated");
            Assert.AreEqual(Lf256(ShellRig), host.EvaluateString("__sc.shellRigSha256"),
                "the shell rig moved under the room: re-measure the room against it and regenerate");
        }

        [Test]
        public void TheWalkableSaloon_IsTheShellsOwnSole()
        {
            using IRigScriptHost host = Host();
            host.Execute("var __shell = " + File.ReadAllText(Full(ShellSidecar)) + ";");
            Assert.AreEqual(host.EvaluateString("JSON.stringify(__shell.SOLE[0].polygon)"),
                host.EvaluateString($"JSON.stringify({Global}.walkable({V}).saloon.polygon)"),
                "the room's floor must be the shell's SOLE, not a second polygon beside it");
        }

        // =============================================================================================
        //  2. EVERY ANCHOR THE OWNER ASKED FOR, AND WHAT IT HOLDS
        // =============================================================================================

        [Test]
        public void EveryRequiredAnchor_IsPresentWithItsVerb()
        {
            using IRigScriptHost host = Host();
            var byId = Anchors(host).ToDictionary(a => a.Id);
            var want = new Dictionary<string, string>
            {
                ["door"] = "step_out", ["bunk"] = "sleep_save", ["stove"] = "cook", ["galley_sink"] = "wash_dishes",
                ["prep"] = "prep", ["freezer"] = "freeze_catch", ["fridge"] = "food_storage", ["pantry"] = "food_storage",
                ["gear_locker"] = "storage", ["rod_rack"] = "storage",
                ["head_basin"] = "wash", ["head_toilet"] = "toilet", ["head_shower"] = "bathe",
            };
            foreach (var kv in want)
            {
                Assert.IsTrue(byId.ContainsKey(kv.Key), $"no '{kv.Key}' interactable");
                Assert.AreEqual(kv.Value, byId[kv.Key].Action, $"'{kv.Key}' has the wrong verb");
            }
        }

        [Test]
        public void TheStores_NameTheCampersOwnDefs_AndTheFreezerIsBiggerThanGinnys()
        {
            using IRigScriptHost host = Host();
            string I(string id, string field) =>
                host.EvaluateString($"String({Global}.interactables({V}).find(a=>a.id==='{id}').{field})");

            // The camper's stores are its OWN, gated by its deed, never the shared ones. Their Defs land
            // with the storage PR; until then the ids are strings, and the capacity check below tightens
            // by itself the day a StowageDef carries one of them.
            var home = AssetDatabase.LoadAssetAtPath<HomeDef>("Assets/_Project/Data/Homes/GinnyLotCamper.asset");
            Assert.IsNotNull(home, "the camper's HomeDef moved");
            var bound = new (string anchor, string field, string id)[]
            {
                ("fridge", "kitchen_storage", "kitchen_storage.camper_fridge"),
                ("pantry", "kitchen_storage", "kitchen_storage.camper_pantry"),
                ("freezer", "freezer", "freezer.camper_freezer"),
                ("gear_locker", "stowage", "stowage.camper_gear_locker"),
                ("rod_rack", "stowage", "stowage.camper_rod_rack"),
            };
            foreach (var b in bound)
            {
                Assert.AreEqual(b.id, I(b.anchor, b.field), $"'{b.anchor}' must bind the camper's own store");
                Assert.AreEqual(home.Id, I(b.anchor, "deed_gated"), $"'{b.anchor}' must be gated by the camper's deed");
            }

            var wash = AssetDatabase.FindAssets("t:WashStationDef")
                .Select(g => AssetDatabase.LoadAssetAtPath<WashStationDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null).Select(d => d.Id).ToHashSet();
            foreach (string id in new[] { "head_basin", "head_toilet", "head_shower" })
                Assert.IsTrue(wash.Contains(I(id, "washstation")), $"'{id}' names {I(id, "washstation")}, which no WashStationDef carries");

            Assert.AreEqual(4, int.Parse(I("rod_rack", "capacity")), "the owner sized the camper's rod rack for 4");
            Assert.AreEqual(10, int.Parse(I("gear_locker", "capacity")), "the owner sized the camper's gear locker for about 10");
            var stowage = AssetDatabase.FindAssets("t:StowageDef")
                .Select(g => AssetDatabase.LoadAssetAtPath<StowageDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null).ToList();
            foreach (string anchor in new[] { "rod_rack", "gear_locker" })
            {
                StowageDef def = stowage.FirstOrDefault(d => d.Id == I(anchor, "stowage"));
                if (def != null)
                    Assert.AreEqual(def.Capacity, int.Parse(I(anchor, "capacity")), $"the rig and {def.Id} disagree on capacity");
            }

            var go = new GameObject("ginny-freezer-probe");
            try
            {
                int ginny = go.AddComponent<GinnyFreezer>().CapacityUnits;
                int camper = int.Parse(I("freezer", "capacity_units"));
                Assert.Greater(camper, ginny, "the owner ruled the camper's freezer BIGGER than Ginny's");
                Assert.AreEqual(40, camper, "seat lean: 40 units against her 20");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }

            // a chest freezer that size wants a chest freezer's floor: at least 0.5 x 1.0 m
            Rect fz = SaloonFixtures(host).Single(r => r.Id == "freezer");
            Assert.GreaterOrEqual(Mathf.Min(fz.X1 - fz.X0, fz.Y1 - fz.Y0), 0.5f, "freezer too shallow for a chest");
            Assert.GreaterOrEqual(Mathf.Max(fz.X1 - fz.X0, fz.Y1 - fz.Y0), 1.0f, "freezer too short for 40 units");
        }

        // =============================================================================================
        //  3. EVERYTHING STANDS ON THE FLOOR, APART, AND REACHABLE
        // =============================================================================================

        [Test]
        public void TheFixtures_StandOnTheSole_AndNoTwoOverlap()
        {
            using IRigScriptHost host = Host();
            var sole = Sole(host);
            var fx = SaloonFixtures(host);
            foreach (Rect r in fx)
                foreach (Vector2 c in Corners(r))
                    Assert.IsTrue(OnOrInside(c, sole), $"{r.Id} corner {c} is off the sole");

            for (int i = 0; i < fx.Count; i++)
            for (int j = i + 1; j < fx.Count; j++)
            {
                Rect a = fx[i], b = fx[j];
                bool stacked = (a.Kind == "overhead" && a.Bottom >= b.H - Eps) || (b.Kind == "overhead" && b.Bottom >= a.H - Eps);
                if (stacked) continue;
                Assert.Less(OverlapArea(a, b), 1e-6f, $"{a.Id} and {b.Id} occupy the same floor");
            }

            var hp = HeadSole(host);
            Rect room = fx.Single(r => r.Id == "head_room");
            foreach (Vector2 c in hp)
            {
                Assert.IsTrue(OnOrInside(c, sole), $"head floor corner {c} is off the sole");
                Assert.IsTrue(c.x >= room.X0 - Eps && c.x <= room.X1 + Eps && c.y >= room.Y0 - Eps && c.y <= room.Y1 + Eps,
                    $"head floor corner {c} is outside head_room");
            }
            foreach (Rect r in HeadFixtures(host))
                foreach (Vector2 c in Corners(r))
                    Assert.IsTrue(OnOrInside(c, hp), $"head fixture {r.Id} corner {c} is off the head floor");
        }

        [Test]
        public void EveryReachPoint_StandsOnItsFloor_ClearOfEveryFixture()
        {
            using IRigScriptHost host = Host();
            var sole = Sole(host);
            var hp = HeadSole(host);
            var fx = SaloonFixtures(host).Where(r => r.Kind != "overhead").ToList();
            var hfx = HeadFixtures(host);
            var saloon = new List<Anchor>();

            foreach (Anchor a in Anchors(host))
            {
                if (a.Level == "saloon")
                {
                    Assert.IsTrue(Inside(a.Reach, sole), $"{a.Id} reach {a.Reach} is off the sole");
                    Assert.GreaterOrEqual(EdgeDistance(a.Reach, sole), ReachClear - Eps, $"{a.Id} reach is against the shell");
                    foreach (Rect r in fx)
                        Assert.GreaterOrEqual(Distance(a.Reach, r), ReachClear - Eps, $"{a.Id} reach is within {ReachClear} m of {r.Id}");
                    saloon.Add(a);
                }
                else
                {
                    Assert.AreEqual("head", a.Level, $"{a.Id} names a level this room does not have");
                    Assert.IsTrue(Inside(a.Reach, hp), $"{a.Id} reach {a.Reach} is off the head floor");
                    Assert.GreaterOrEqual(EdgeDistance(a.Reach, hp), ReachClear - Eps, $"{a.Id} reach is against a head wall");
                    foreach (Rect r in hfx)
                        Assert.GreaterOrEqual(Distance(a.Reach, r), ReachClear - Eps, $"{a.Id} reach is within {ReachClear} m of the {r.Id}");
                }
            }

            for (int i = 0; i < saloon.Count; i++)
            for (int j = i + 1; j < saloon.Count; j++)
                Assert.GreaterOrEqual(Vector2.Distance(saloon[i].Reach, saloon[j].Reach), ReachSpacing - Eps,
                    $"{saloon[i].Id} and {saloon[j].Id} stand on the same spot: one press would pick either");
        }

        [Test]
        public void TheFoot_WalksFromTheDoor_ToWithinReachOfEverySaloonAnchor()
        {
            using IRigScriptHost host = Host();
            var sole = Sole(host);
            var fx = SaloonFixtures(host).Where(r => r.Kind != "overhead").ToList();
            Vector2 landing = new Vector2(
                (float)host.EvaluateNumber($"{Global}.sidecar({V}).THRESHOLD.landing_centre[0]"),
                (float)host.EvaluateNumber($"{Global}.sidecar({V}).THRESHOLD.landing_centre[1]"));

            const float step = 0.02f;
            float minX = sole.Min(p => p.x), maxX = sole.Max(p => p.x), minY = sole.Min(p => p.y), maxY = sole.Max(p => p.y);
            int nx = Mathf.CeilToInt((maxX - minX) / step) + 1, ny = Mathf.CeilToInt((maxY - minY) / step) + 1;
            Vector2 At(int i, int j) => new Vector2(minX + i * step, minY + j * step);
            bool Free(Vector2 p) => Inside(p, sole) && EdgeDistance(p, sole) >= FootRadius && fx.All(r => Distance(p, r) >= FootRadius);

            var free = new bool[nx, ny];
            for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++) free[i, j] = Free(At(i, j));

            int si = Mathf.RoundToInt((landing.x - minX) / step), sj = Mathf.RoundToInt((landing.y - minY) / step);
            Assert.IsTrue(free[si, sj], $"the foot cannot stand on the door landing {landing}");
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

            foreach (Anchor an in Anchors(host).Where(a => a.Level == "saloon"))
            {
                float best = reached.Min(p => Vector2.Distance(p, an.Reach));
                Assert.LessOrEqual(best, InteractReach + step,
                    $"a 0.35 m foot walking in from the door gets no nearer than {best:0.00} m to '{an.Id}'");
            }

            Vector2 headSide = new Vector2(
                (float)host.EvaluateNumber($"{Global}.sidecar({V}).INNER_DOORS[0].saloon_side[0]"),
                (float)host.EvaluateNumber($"{Global}.sidecar({V}).INNER_DOORS[0].saloon_side[1]"));
            Assert.LessOrEqual(reached.Min(p => Vector2.Distance(p, headSide)), InteractReach + step,
                "the foot cannot get to the head door");
        }

        [Test]
        public void TheDoor_LetsThePlayerIn_AndItsLandingHoldsHisFoot()
        {
            using IRigScriptHost host = Host();
            float clear = (float)host.EvaluateNumber("CamperIso.VARIANTS.clipper.door.y1 - CamperIso.VARIANTS.clipper.door.y0");
            Assert.GreaterOrEqual(clear, 2f * FootRadius, $"the door's {clear:0.00} m is narrower than the {2 * FootRadius:0.00} m foot");

            var sole = Sole(host);
            Vector2 landing = new Vector2(
                (float)host.EvaluateNumber($"{Global}.sidecar({V}).THRESHOLD.landing_centre[0]"),
                (float)host.EvaluateNumber($"{Global}.sidecar({V}).THRESHOLD.landing_centre[1]"));
            Assert.IsTrue(Inside(landing, sole));
            Assert.GreaterOrEqual(EdgeDistance(landing, sole), FootRadius, "the landing circle crosses the shell");
            foreach (Rect r in SaloonFixtures(host).Where(r => r.Kind != "overhead"))
                Assert.GreaterOrEqual(Distance(landing, r), FootRadius, $"the landing circle hits {r.Id}");

            float y0 = (float)host.EvaluateNumber("CamperIso.VARIANTS.clipper.door.y0");
            float y1 = (float)host.EvaluateNumber("CamperIso.VARIANTS.clipper.door.y1");
            Assert.That(landing.y - FootRadius, Is.GreaterThanOrEqualTo(y0 - Eps).And.LessThanOrEqualTo(y1));
            Assert.That(landing.y + FootRadius, Is.LessThanOrEqualTo(y1 + Eps), "the foot overhangs the door's clear span");
            Assert.AreEqual(landing.x, (float)host.EvaluateNumber($"{Global}.interactables({V}).find(a=>a.id==='door').reach_point[0]"), Eps);
        }

        // =============================================================================================
        //  4. KITCHEN AND BATH ON THE NORTH WALL (owner ruling), AS THE LOT ACTUALLY PARKS IT
        // =============================================================================================

        [Test]
        public void TheKitchenAndBath_StandOnTheNorthWall_AsTheLotParksTheCamper()
        {
            // the lot: curb (+x, the door) toward the approach, hitch (+y) the way the camper is drawn
            Vector2 curb = (StPetersCamperLot.Approach - StPetersCamperLot.LotPos).normalized;
            Vector2 nose = StPetersCamperLot.NoseDirection;
            Assert.AreEqual(0f, Vector2.Dot(curb, nose), 1e-4f, "the camper's long axis is not square to its approach");
            Assert.Greater(curb.x * nose.y - curb.y * nose.x, 0f,
                "+x then +y must turn counter-clockwise (the rig frame is right-handed, +z up)");
            Vector2 rigMinusX = -curb;
            Assert.Greater(Vector2.Dot(rigMinusX, Vector2.up), 0.99f,
                $"rig -x points {rigMinusX} on this lot, not north: the north-wall rule now means a different side");

            using IRigScriptHost host = Host();
            var sole = Sole(host);
            var fx = SaloonFixtures(host).ToDictionary(r => r.Id);
            foreach (string id in NorthFixtures)
            {
                Rect r = fx[id];
                Assert.AreEqual("north", r.Wall, $"{id} is tagged for the wrong wall");
                Assert.Less(r.X1, 0f, $"{id} crosses the centreline");
                float wall = -EdgeOfSoleAt(sole, (r.Y0 + r.Y1) / 2f);
                Assert.LessOrEqual(r.X0, wall + 0.06f, $"{id} does not stand against the north liner");
            }
            var an = Anchors(host).ToDictionary(a => a.Id);
            foreach (string id in KitchenAndBathAnchors)
                Assert.Less(an[id].At.x, 0f, $"'{id}' is not on the north side");
        }

        /// <summary>The sole's half-width at a station, from the polygon itself.</summary>
        static float EdgeOfSoleAt(List<Vector2> sole, float y)
        {
            float best = 0f;
            for (int i = 0, j = sole.Count - 1; i < sole.Count; j = i++)
            {
                Vector2 a = sole[j], b = sole[i];
                if ((a.y > y) == (b.y > y) || a.x <= 0f || b.x <= 0f) continue;
                best = Mathf.Max(best, a.x + (y - a.y) * (b.x - a.x) / (b.y - a.y));
            }
            return best;
        }

        // =============================================================================================
        //  5. THE SHED'S GROUND ON THE LOT
        // =============================================================================================

        [Test]
        public void TheShedReservation_ClearsTheCamperTheShedsAndTheClearing()
        {
            const float walkingGap = 2.0f;   // the plot's walking gap (StPetersCamperLot.LotOffset notes)
            Vector2 lot = StPetersCamperLot.LotPos, shed = StPetersCamperLot.ShedPos;
            float r = StPetersCamperLot.ShedFootprintRadiusMetres;
            float camperR = StPetersCamperLot.FootprintRadiusMetres;
            Assert.Greater(camperR, 0f, "the camper sidecar did not read: nothing below would mean anything");

            float gap = Vector2.Distance(shed, lot) - camperR - r;
            Assert.GreaterOrEqual(gap, walkingGap, $"the shed is {gap:0.00} m from the camper");
            foreach (var s in StPetersGinnyPlot.Sheds)
            {
                float g = Vector2.Distance(shed, s.Position) - s.FootprintRadiusMetres - r;
                Assert.GreaterOrEqual(g, walkingGap, $"the lot shed is {g:0.00} m from {s.Key}");
            }
            float reach = Vector2.Distance(shed, StPetersGinnyPlot.CottagePos) + r;
            Assert.LessOrEqual(reach, StPetersGinnyPlot.ClearingRadius, $"the shed reaches {reach:0.00} m, outside her clearing");
            Assert.LessOrEqual(reach, StPetersGinnyPlot.RequiredClearingRadius() + Eps, "RequiredClearingRadius ignores the lot shed");
            Assert.Greater(Vector2.Dot(StPetersCamperLot.ShedDoorDirection, Vector2.down), 0.99f, "the shed door should face south, with the camper's");

            // the sidecar says the same thing in the rig frame: world = LotPos + (y, -x)
            using IRigScriptHost host = Host();
            float cx = (float)host.EvaluateNumber($"{Global}.lot({V}).shed_reservation.centre_rig_m[0]");
            float cy = (float)host.EvaluateNumber($"{Global}.lot({V}).shed_reservation.centre_rig_m[1]");
            Vector2 nose = StPetersCamperLot.NoseDirection, curb = (StPetersCamperLot.Approach - lot).normalized;
            Vector2 world = lot + curb * cx + nose * cy;
            Assert.AreEqual(shed.x, world.x, 1e-3f, "sidecar and StPetersCamperLot disagree on the shed");
            Assert.AreEqual(shed.y, world.y, 1e-3f, "sidecar and StPetersCamperLot disagree on the shed");
            Assert.AreEqual(r, (float)host.EvaluateNumber($"{Global}.lot({V}).shed_reservation.reserve_radius_m"), 1e-3f);
            Assert.AreEqual(StPetersCamperLot.ShedSize.x, (float)host.EvaluateNumber($"{Global}.lot({V}).shed_reservation.footprint_m.along_camper"), 1e-4f);
            Assert.AreEqual(StPetersCamperLot.ShedSize.y, (float)host.EvaluateNumber($"{Global}.lot({V}).shed_reservation.footprint_m.across_camper"), 1e-4f);
        }
    }
}
